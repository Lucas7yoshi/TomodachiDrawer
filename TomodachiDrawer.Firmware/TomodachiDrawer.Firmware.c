#include "pico/stdlib.h"
#include "bsp/board.h"
#include "tusb.h"
#include "hardware/pio.h"
#include "ws2812.pio.h"

// Waveshare RP2040 Zero onboard NeoPixel
#define NEOPIXEL_PIN 16
#define NEOPIXEL_PIO pio0
#define NEOPIXEL_SM  0

#define NEOPIXEL_BRIGHT 127 // Its surprisingly bright lol
#define RAINBOW_DIVISOR 4 // because again, its really bright

// We store the instructions in the top half of our flash.
// Reality is we could probably do 1.75MB since this program is teeny tiny but 1MB seems to be enough.
#define FLASH_TARGET_OFFSET (1 * 1024 * 1024)

typedef uint16_t gamepad_button_t;

#define BTN_Y       ((gamepad_button_t)0x0001)
#define BTN_B       ((gamepad_button_t)0x0002)
#define BTN_A       ((gamepad_button_t)0x0004)
#define BTN_X       ((gamepad_button_t)0x0008)
#define BTN_L       ((gamepad_button_t)0x0010)
#define BTN_R       ((gamepad_button_t)0x0020)
#define BTN_ZL      ((gamepad_button_t)0x0040)
#define BTN_ZR      ((gamepad_button_t)0x0080)
#define BTN_MINUS   ((gamepad_button_t)0x0101)
#define BTN_PLUS    ((gamepad_button_t)0x0102)
#define BTN_LCLICK  ((gamepad_button_t)0x0104)
#define BTN_RCLICK  ((gamepad_button_t)0x0108)
#define BTN_HOME    ((gamepad_button_t)0x0110)
#define BTN_CAPTURE ((gamepad_button_t)0x0120)

#define DPAD_UP        0
#define DPAD_UPRIGHT   1
#define DPAD_RIGHT     2
#define DPAD_DOWNRIGHT 3
#define DPAD_DOWN      4
#define DPAD_DOWNLEFT  5
#define DPAD_LEFT      6
#define DPAD_UPLEFT    7
#define DPAD_NEUTRAL   8

#define STICK_LX     3
#define STICK_LY     4
#define STICK_RX     5
#define STICK_RY     6
#define STICK_CENTER 128

// Report layout: [Btn1, Btn2, DPad, LX, LY, RX, RY, Padding]
static uint8_t current_report[8] = {0x00, 0x00, 0x08, 128, 128, 128, 128, 0x00};

const uint8_t *flash_contents = (const uint8_t *)(XIP_BASE + FLASH_TARGET_OFFSET);

// mapping C#'s Button enum (A=0, B=1, X=2, Y=3, ...) to report bits
const gamepad_button_t button_map[] = {
    BTN_A, BTN_B, BTN_X, BTN_Y, BTN_L, BTN_R,
    BTN_ZL, BTN_ZR, BTN_MINUS, BTN_PLUS, BTN_LCLICK,
    BTN_RCLICK, BTN_HOME, BTN_CAPTURE
};

// C#'s Stick enum: LX=0, LY=1, RX=2, RY=3
const uint8_t stick_axis_map[] = {
    STICK_LX, STICK_LY, STICK_RX, STICK_RY
};


// opcodes are from FileControllerSink.cs, same for version.
#define TDLD_VERSION 0x03

// All opcodes are stored in the upper nibble (4 bits)
#define OPCODE_INVALID          0x0 // "invalid" to avoid boring 0x00 files in hex editor originally, repurposed for EOF.
#define OPCODE_PRESS_BUTTON     0x1 // Press a button down. lower nibble is input, through button_map
#define OPCODE_RELEASE_BUTTON   0x2 // Release a button. lower nibble is input.
#define OPCODE_PRESS_DPAD       0x3 // Press the DPAD (or more correctly, set its state). Lower nibble is direction.
#define OPCODE_RELEASE_DPAD     0x4 // "Releases" (sets to DPAD_NEUTRAL)
#define OPCODE_RELEASE_ALL      0x5 // Reset everything. Unused by my implementation
#define OPCODE_DELAY            0x6 // 2 bytes. Lower nibble is upper 4 bits, second byte is another 8 bits for 12 bits. Max 4095ms delay. Can be repeated
#define OPCODE_SET_STICK        0x7 // Set stick. Unused ATM. lower nibble is axis, second byte is value
#define OPCODE_TAP_BUTTON       0x8 // 1 byte "tap" shortcut, Press -> 25ms delay -> Release -> 25ms delay. Used a LOT
#define OPCODE_TAP_DPAD         0x9 // Same as above but for DPad

#define OPCODE_REPEAT_LAST_1    0xE // Repeat the last 1 byte record the number of times in the bottom nibble (0-15)
#define OPCODE_REPEAT_LAST_2    0xF // Repeat the last 1 byte record but with 12 bits (0-4096)

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

static inline void hid_set_dpad(uint8_t direction) {
    current_report[2] = direction;
}

static inline void hid_set_stick(uint8_t axis, uint8_t value) {
    current_report[axis] = value;
}

// neopixel stuff.
static void neopixel_init(void) {
    uint offset = pio_add_program(NEOPIXEL_PIO, &ws2812_program);
    ws2812_program_init(NEOPIXEL_PIO, NEOPIXEL_SM, offset, NEOPIXEL_PIN, 800000, false);
}

// "blocking" is interesting but doesnt seem to cause any trouble...
static void neopixel_set_rgb(uint8_t r, uint8_t g, uint8_t b) {
    uint32_t grb = ((uint32_t)g << 24) | ((uint32_t)r << 16) | ((uint32_t)b << 8);
    pio_sm_put_blocking(NEOPIXEL_PIO, NEOPIXEL_SM, grb);
}

// delays precisely but while running tud_task so the usb is active.
// this is the alternative to running a multicore architecture which i tried
// and let me tell you, it was not a source of much fun.
static void delay_ms_usb(uint32_t ms) {
    absolute_time_t end = make_timeout_time_ms(ms);
    while (!time_reached(end)) {
        tud_task();
    }
}

// send the report to the connected device, actually sends the data.
// this is called by push_report and the countdown to send the neutral state.
static void send_report_raw(void) {
    while (!tud_hid_ready()) {
        tud_task();
    }
    uint8_t local_report[8];
    for (int i = 0; i < 8; i++) {
        local_report[i] = current_report[i];
    }
    tud_hid_report(0, local_report, sizeof(local_report));
}

// send report and update neopixel. used during actual playback.
static void push_report(void) {
    send_report_raw();
    // Mirror the Python firmware: green while any button is held, dim-white when idle
    if (current_report[0] != 0 || current_report[1] != 0) {
        neopixel_set_rgb(0, NEOPIXEL_BRIGHT, 0);
    } else {
        neopixel_set_rgb(10, 10, 10);
    }
}

// error thing. flashes red.
// todo: make this more human friendly (flash different colours? or patterns?)
static void error_flash(int interval_ms) {
    while (true) {
        neopixel_set_rgb(NEOPIXEL_BRIGHT, 0, 0);
        delay_ms_usb(interval_ms);
        neopixel_set_rgb(0, 0, 0);
        delay_ms_usb(interval_ms);
    }
}

// new and improved rainbow from like, the hsv thing. but no s,v for simplicity.
void get_good_rainbow(uint8_t hue, uint8_t *r, uint8_t *g, uint8_t *b) {
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

// worlds worst "rainbow
static void done_rainbow(void) {
    uint8_t hue = 0;
    uint8_t r, g, b;
    while (true) {
        get_good_rainbow(hue, &r, &g, &b);
        neopixel_set_rgb(r/RAINBOW_DIVISOR,g/RAINBOW_DIVISOR,b/RAINBOW_DIVISOR);
        sleep_ms(10);
        hue++;
    }
}

// single byte opcodes are in their own function because
// they are eligible for repeats so this avoids duplicated code.
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
        // Tap: press -> 25ms -> release -> 25ms
        // This exists for storage saving, because the alternative is 4 records, which would be 6 bytes (1+2+1+2)
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
            error_flash(5000); // corruption?
            break;
    }
}

// ACTUAL ENTRYPOINT
int main(void) {
    board_init();
    tusb_init();
    neopixel_init();

    // yapp until they hear
    while (!tud_mounted()) {
        tud_task();
    }

    // 3 flash countdown, this is mostly just for there to be some
    // time for the switch to acknowledge things. On that note: we send empty neutral inputs here
    // so the switch expects us when we start playing back.
    // Otherwise it seemed to be off by one.
    for (int i = 0; i < 3; i++) {
        neopixel_set_rgb(NEOPIXEL_BRIGHT, NEOPIXEL_BRIGHT, 0);
        send_report_raw();
        delay_ms_usb(500);
        neopixel_set_rgb(0, 0, 0);
        send_report_raw();
        delay_ms_usb(500);
    }

    const uint8_t *ptr = flash_contents;

    // Validate TDLD magic ("TomoDachi Life Drawer")
    if (ptr[0] != 'T' || ptr[1] != 'D' || ptr[2] != 'L' || ptr[3] != 'D') {
        error_flash(250); // fast blink = bad magic. todo: better error reporting lol
        return 0;
    }
    if (ptr[4] != TDLD_VERSION) {
        error_flash(1000); // slow blink = wrong version
        return 0;
    }
    ptr += 6; // 4-byte magic + 1-byte version + 1-byte padding

    uint8_t last_1byte_record = 0; // for Repeat opcodes.
    bool working = true;
    while (working) {
        tud_task();
        uint8_t record = *ptr++;
        uint8_t opcode = record >> 4;
        uint8_t nibble  = record & 0x0F;

        switch (opcode) {
            case OPCODE_INVALID:
                working = false;
                break;
            case OPCODE_DELAY: { // 2 byte record. 4 bits from record, 8 from second byte
                uint8_t data     = *ptr++;
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
            case OPCODE_REPEAT_LAST_1: { // 1 byte repeat.
                uint8_t count = nibble;
                for (int i = 0; i < count; i++) {
                    run_single_byte_opcode(last_1byte_record);
                }
                break;
            }
            case OPCODE_REPEAT_LAST_2: {
                uint8_t data      = *ptr++;
                uint16_t count    = (nibble << 8) | data;
                for (int i = 0; i < count; i++) {
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

    done_rainbow();
    return 0;
}

// no-op required callbacks >:(
uint16_t tud_hid_get_report_cb(uint8_t itf, uint8_t id, hid_report_type_t type, uint8_t *buf, uint16_t len) { return 0; }
void     tud_hid_set_report_cb(uint8_t itf, uint8_t id, hid_report_type_t type, uint8_t const *buf, uint16_t len) {}
