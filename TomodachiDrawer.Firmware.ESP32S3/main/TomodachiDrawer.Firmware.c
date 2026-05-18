#include <stdbool.h>
#include <stdint.h>

#include "driver/gpio.h"
#include "driver/rmt_types.h"
#include "esp_log.h"
#include "esp_partition.h"
#include "esp_timer.h"
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "led_strip.h"
#include "sdkconfig.h"
#include "tinyusb.h"

#include "usb_descriptors.h"

// LED + button GPIOs come from Kconfig so different boards just need a
// menuconfig change.
#define NEOPIXEL_PIN CONFIG_TDD_LED_GPIO
#define BOOT_BUTTON_GPIO CONFIG_TDD_BOOT_BUTTON_GPIO

#define NEOPIXEL_BRIGHT 127  // its surprisingly bright
#define RAINBOW_DIVISOR 4    // because again, its really bright

// must match FileControllerSink.cs and the RP2040 firmware
#define TDLD_VERSION 0x03
#define TDLD_PARTITION_SUBTYPE 0x40  // matches partitions.csv

typedef uint16_t gamepad_button_t;

#define BTN_Y ((gamepad_button_t)0x0001)
#define BTN_B ((gamepad_button_t)0x0002)
#define BTN_A ((gamepad_button_t)0x0004)
#define BTN_X ((gamepad_button_t)0x0008)
#define BTN_L ((gamepad_button_t)0x0010)
#define BTN_R ((gamepad_button_t)0x0020)
#define BTN_ZL ((gamepad_button_t)0x0040)
#define BTN_ZR ((gamepad_button_t)0x0080)
#define BTN_MINUS ((gamepad_button_t)0x0101)
#define BTN_PLUS ((gamepad_button_t)0x0102)
#define BTN_LCLICK ((gamepad_button_t)0x0104)
#define BTN_RCLICK ((gamepad_button_t)0x0108)
#define BTN_HOME ((gamepad_button_t)0x0110)
#define BTN_CAPTURE ((gamepad_button_t)0x0120)

#define DPAD_UP 0
#define DPAD_UPRIGHT 1
#define DPAD_RIGHT 2
#define DPAD_DOWNRIGHT 3
#define DPAD_DOWN 4
#define DPAD_DOWNLEFT 5
#define DPAD_LEFT 6
#define DPAD_UPLEFT 7
#define DPAD_NEUTRAL 8

#define STICK_LX 3
#define STICK_LY 4
#define STICK_RX 5
#define STICK_RY 6
#define STICK_CENTER 128

// The HID descriptor declares 7 bytes per report (16 button bits + 4 hat + 4
// pad + 4 stick axes). Switch tolerates an extra trailing byte; Windows does
// not, so we send exactly 7.
#define HID_REPORT_WIRE_LEN 7

// Report layout: [Btn1, Btn2, DPad, LX, LY, RX, RY, Padding]
static uint8_t current_report[8] = {0x00, 0x00, 0x08, 128, 128, 128, 128, 0x00};

// mapping C#'s Button enum (A=0, B=1, X=2, Y=3, ...) to report bits
static const gamepad_button_t button_map[] = {
    BTN_A,      BTN_B,      BTN_X,    BTN_Y,    BTN_L,
    BTN_R,      BTN_ZL,     BTN_ZR,   BTN_MINUS, BTN_PLUS,
    BTN_LCLICK, BTN_RCLICK, BTN_HOME, BTN_CAPTURE};

// C#'s Stick enum: LX=0, LY=1, RX=2, RY=3
static const uint8_t stick_axis_map[] = {STICK_LX, STICK_LY, STICK_RX, STICK_RY};

// opcodes (must match FileControllerSink.cs Opcode constants)
#define OPCODE_INVALID 0x0
#define OPCODE_PRESS_BUTTON 0x1
#define OPCODE_RELEASE_BUTTON 0x2
#define OPCODE_PRESS_DPAD 0x3
#define OPCODE_RELEASE_DPAD 0x4
#define OPCODE_RELEASE_ALL 0x5
#define OPCODE_DELAY 0x6
#define OPCODE_SET_STICK 0x7
#define OPCODE_TAP_BUTTON 0x8
#define OPCODE_TAP_DPAD 0x9
#define OPCODE_REPEAT_LAST_1 0xE
#define OPCODE_REPEAT_LAST_2 0xF

static const char* TAG = "tdd";
static led_strip_handle_t led;

static const uint8_t* tdld_data = NULL;

// set by delay_ms_usb when BOOT is pressed mid-play, unwinds the opcode loop.
static volatile bool abort_requested = false;

// hid helpers. avoids repeated error-prone bit shite.
static inline void hid_press(gamepad_button_t btn) {
  current_report[btn >> 8] |= (btn & 0xFF);
}

static inline void hid_release(gamepad_button_t btn) {
  current_report[btn >> 8] &= ~(btn & 0xFF);
}

static inline void hid_release_all(void) {
  current_report[0] = 0x00;
  current_report[1] = 0x00;
  current_report[2] = DPAD_NEUTRAL;
  current_report[3] = STICK_CENTER;
  current_report[4] = STICK_CENTER;
  current_report[5] = STICK_CENTER;
  current_report[6] = STICK_CENTER;
  current_report[7] = 0x00;
}

static inline void hid_set_dpad(uint8_t direction) { current_report[2] = direction; }
static inline void hid_set_stick(uint8_t axis, uint8_t value) { current_report[axis] = value; }

// neopixel stuff. RMT-driven via the led_strip managed component.
static void neopixel_init(void) {
  led_strip_config_t strip_config = {
      .strip_gpio_num = NEOPIXEL_PIN,
      .max_leds = 1,
      .led_model = LED_MODEL_WS2812,
      .color_component_format = LED_STRIP_COLOR_COMPONENT_FMT_GRB,
  };
  led_strip_rmt_config_t rmt_config = {
      .clk_src = RMT_CLK_SRC_DEFAULT,
      .resolution_hz = 10 * 1000 * 1000,
      .flags = {.with_dma = false},
  };
  ESP_ERROR_CHECK(led_strip_new_rmt_device(&strip_config, &rmt_config, &led));
  ESP_ERROR_CHECK(led_strip_clear(led));
}

static void neopixel_set_rgb(uint8_t r, uint8_t g, uint8_t b) {
  led_strip_set_pixel(led, 0, r, g, b);
  led_strip_refresh(led);
}

// BOOT button - GPIO 0 on essentially every S3 dev board. We reuse it as a
// runtime replay / abort button.
static void boot_button_init(void) {
  gpio_config_t cfg = {
      .pin_bit_mask = 1ULL << BOOT_BUTTON_GPIO,
      .mode = GPIO_MODE_INPUT,
      .pull_up_en = GPIO_PULLUP_ENABLE,
      .pull_down_en = GPIO_PULLDOWN_DISABLE,
      .intr_type = GPIO_INTR_DISABLE,
  };
  ESP_ERROR_CHECK(gpio_config(&cfg));
}

static inline bool boot_button_pressed(void) {
  return gpio_get_level(BOOT_BUTTON_GPIO) == 0;
}

// send the report to the connected device, actually sends the data.
static void send_report_raw(void) {
  while (!tud_hid_ready()) {
    vTaskDelay(1);  // 1ms tick (CONFIG_FREERTOS_HZ=1000)
  }
  tud_hid_report(0, current_report, HID_REPORT_WIRE_LEN);
}

// send report and update neopixel - green while any button is held, dim-white when idle.
static void push_report(void) {
  send_report_raw();
  if (current_report[0] != 0 || current_report[1] != 0) {
    neopixel_set_rgb(0, NEOPIXEL_BRIGHT, 0);
  } else {
    neopixel_set_rgb(10, 10, 10);
  }
}

// Delay while keeping the host's report queue fresh, stopping one bInterval
// before the deadline so the last report has time to be delivered before the
// next opcode mutates the buffer. Yields with vTaskDelay so the USB task and
// rest of FreeRTOS keep running.
static void delay_ms_usb(uint32_t ms) {
  if (ms == 0 || abort_requested) return;
  int64_t end_us = esp_timer_get_time() + (int64_t)ms * 1000;
  int64_t stop_sending_us = (ms > TDD_HID_BINTERVAL_MS)
                                ? end_us - (int64_t)TDD_HID_BINTERVAL_MS * 1000
                                : end_us;
  while (esp_timer_get_time() < end_us) {
    if (boot_button_pressed()) {
      abort_requested = true;
      return;
    }
    if (esp_timer_get_time() < stop_sending_us && tud_hid_ready()) {
      tud_hid_report(0, current_report, HID_REPORT_WIRE_LEN);
    }
    vTaskDelay(1);
  }
}

// error thing. flashes red.
// todo: make this more human friendly (different colours? patterns?)
static void error_flash(uint32_t interval_ms) {
  while (true) {
    neopixel_set_rgb(NEOPIXEL_BRIGHT, 0, 0);
    delay_ms_usb(interval_ms);
    neopixel_set_rgb(0, 0, 0);
    delay_ms_usb(interval_ms);
  }
}

// new and improved rainbow from the hsv thing. no s,v for simplicity.
static void get_good_rainbow(uint8_t hue, uint8_t* r, uint8_t* g, uint8_t* b) {
  if (hue < 85) {
    *r = 255 - hue * 3;
    *g = hue * 3;
    *b = 0;
  } else if (hue < 170) {
    hue -= 85;
    *r = 0;
    *g = 255 - hue * 3;
    *b = hue * 3;
  } else {
    hue -= 170;
    *r = hue * 3;
    *g = 0;
    *b = 255 - hue * 3;
  }
}

// rainbow until BOOT is pressed. The RP2040 loops forever here; we have an
// unused button so might as well let users replay.
static void rainbow_until_replay(void) {
  // a mid-playback abort leaves BOOT held - drain it first or we'd
  // immediately re-fire.
  while (boot_button_pressed()) {
    neopixel_set_rgb(NEOPIXEL_BRIGHT, NEOPIXEL_BRIGHT, NEOPIXEL_BRIGHT);
    vTaskDelay(pdMS_TO_TICKS(20));
  }
  ESP_LOGI(TAG, "press BOOT to replay, RST to reboot");

  uint8_t hue = 0;
  uint8_t r, g, b;
  while (true) {
    if (boot_button_pressed()) {
      vTaskDelay(pdMS_TO_TICKS(20));  // debounce
      if (boot_button_pressed()) {
        while (boot_button_pressed()) vTaskDelay(pdMS_TO_TICKS(10));
        return;
      }
    }
    get_good_rainbow(hue, &r, &g, &b);
    neopixel_set_rgb(r / RAINBOW_DIVISOR, g / RAINBOW_DIVISOR, b / RAINBOW_DIVISOR);
    vTaskDelay(pdMS_TO_TICKS(10));
    hue++;
  }
}

// single byte opcodes are in their own function because they are eligible for
// repeats so this avoids duplicated code.
static void run_single_byte_opcode(uint8_t record) {
  uint8_t opcode = record >> 4;
  uint8_t nibble = record & 0xF;
  switch (opcode) {
    case OPCODE_PRESS_BUTTON:
      hid_press(button_map[nibble]);
      push_report();
      break;
    case OPCODE_RELEASE_BUTTON:
      hid_release(button_map[nibble]);
      push_report();
      break;
    case OPCODE_PRESS_DPAD:
      hid_set_dpad(nibble);
      push_report();
      break;
    case OPCODE_RELEASE_DPAD:
      hid_set_dpad(DPAD_NEUTRAL);
      push_report();
      break;
    case OPCODE_RELEASE_ALL:
      hid_release_all();
      push_report();
      break;
    // Tap: press -> 25ms -> release -> 25ms.
    case OPCODE_TAP_BUTTON:
      hid_press(button_map[nibble]);
      push_report();
      delay_ms_usb(25);
      hid_release(button_map[nibble]);
      push_report();
      delay_ms_usb(25);
      break;
    case OPCODE_TAP_DPAD:
      hid_set_dpad(nibble);
      push_report();
      delay_ms_usb(25);
      hid_set_dpad(DPAD_NEUTRAL);
      push_report();
      delay_ms_usb(25);
      break;
    default:
      error_flash(5000);  // corruption?
      break;
  }
}

static bool tdld_partition_init(void) {
  const esp_partition_t* part = esp_partition_find_first(
      ESP_PARTITION_TYPE_DATA, TDLD_PARTITION_SUBTYPE, "tdld");
  if (!part) {
    ESP_LOGE(TAG, "tdld partition not found");
    return false;
  }
  const void* mapped = NULL;
  esp_partition_mmap_handle_t handle;
  esp_err_t err = esp_partition_mmap(part, 0, part->size,
                                     ESP_PARTITION_MMAP_DATA, &mapped, &handle);
  if (err != ESP_OK) {
    ESP_LOGE(TAG, "esp_partition_mmap failed: %s", esp_err_to_name(err));
    return false;
  }
  tdld_data = (const uint8_t*)mapped;
  return true;
}

static void tinyusb_init(void) {
  const tinyusb_config_t tusb_cfg = {
      .device_descriptor = &tdd_device_descriptor,
      .string_descriptor = tdd_string_descriptors,
      .string_descriptor_count = (int)tdd_string_descriptor_count,
      .external_phy = false,
      .configuration_descriptor = tdd_configuration_descriptor,
  };
  ESP_ERROR_CHECK(tinyusb_driver_install(&tusb_cfg));
}

// Countdown + validate + opcode loop. The 3-flash countdown is mostly so the
// switch acknowledges us before playback - sending empty neutral inputs here
// means it expects us when we start. Otherwise it was off by one.
static void play_tdld(void) {
  abort_requested = false;

  for (int i = 0; i < 3 && !abort_requested; i++) {
    neopixel_set_rgb(NEOPIXEL_BRIGHT, NEOPIXEL_BRIGHT, 0);
    send_report_raw();
    delay_ms_usb(500);
    neopixel_set_rgb(0, 0, 0);
    send_report_raw();
    delay_ms_usb(500);
  }

  if (!abort_requested) {
    const uint8_t* ptr = tdld_data;
    if (ptr[0] != 'T' || ptr[1] != 'D' || ptr[2] != 'L' || ptr[3] != 'D') {
      error_flash(250);  // fast blink = bad magic
    }
    if (ptr[4] != TDLD_VERSION) {
      error_flash(1000);  // slow blink = wrong version
    }
    ptr += 6;  // 4-byte magic + 1-byte version + 1-byte padding

    uint8_t last_1byte_record = 0;
    bool working = true;
    while (working && !abort_requested) {
      uint8_t record = *ptr++;
      uint8_t opcode = record >> 4;
      uint8_t nibble = record & 0x0F;

      switch (opcode) {
        case OPCODE_INVALID:
          working = false;
          break;
        case OPCODE_DELAY: {
          uint8_t data = *ptr++;
          uint16_t delayMs = (nibble << 8) | data;
          delay_ms_usb(delayMs);
          break;
        }
        case OPCODE_SET_STICK: {
          uint8_t axis_value = *ptr++;
          hid_set_stick(stick_axis_map[nibble], axis_value);
          push_report();
          break;
        }
        case OPCODE_REPEAT_LAST_1: {
          uint8_t count = nibble;
          for (int i = 0; i < count && !abort_requested; i++) {
            run_single_byte_opcode(last_1byte_record);
          }
          break;
        }
        case OPCODE_REPEAT_LAST_2: {
          uint8_t data = *ptr++;
          uint16_t count = (nibble << 8) | data;
          for (int i = 0; i < count && !abort_requested; i++) {
            run_single_byte_opcode(last_1byte_record);
          }
          break;
        }
        default:
          run_single_byte_opcode(record);
          last_1byte_record = record;
          break;
      }
    }
  }

  if (abort_requested) {
    hid_release_all();
    // best-effort neutral so the switch doesn't see a held button.
    if (tud_hid_ready()) {
      tud_hid_report(0, current_report, HID_REPORT_WIRE_LEN);
    }
  }
}

void app_main(void) {
  neopixel_init();
  boot_button_init();
  neopixel_set_rgb(64, 0, 0);  // dim red while waiting for USB

  tdld_partition_init();
  tinyusb_init();

  while (!tud_mounted()) vTaskDelay(pdMS_TO_TICKS(10));

  while (true) {
    play_tdld();
    rainbow_until_replay();
  }
}
