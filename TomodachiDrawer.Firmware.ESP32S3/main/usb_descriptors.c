#include "usb_descriptors.h"

// HORI Pokken Tournament Pro Pad. The switch accepts this without pairing.
#define USB_VID 0x0F0D
#define USB_PID 0x0092

#define EPNUM_HID 0x81

// HID report descriptor. Same bytes as the RP2040 - don't touch.
//   bytes 0-1: 16 button bits
//   byte 2:    hat / D-pad (4 bits + 4 bits constant)
//   bytes 3-6: LX, LY, RX, RY analogue sticks
static const uint8_t desc_hid_report[] = {
    0x05, 0x01, 0x09, 0x05, 0xA1, 0x01, 0x15, 0x00,
    0x25, 0x01, 0x35, 0x00, 0x45, 0x01, 0x75, 0x01,
    0x95, 0x10, 0x05, 0x09, 0x19, 0x01, 0x29, 0x10,
    0x81, 0x02, 0x05, 0x01, 0x25, 0x07, 0x46, 0x3b,
    0x01, 0x75, 0x04, 0x95, 0x01, 0x65, 0x14, 0x09,
    0x39, 0x81, 0x42, 0x65, 0x00, 0x95, 0x01, 0x81,
    0x01, 0x26, 0xff, 0x00, 0x46, 0xff, 0x00, 0x09,
    0x30, 0x09, 0x31, 0x09, 0x32, 0x09, 0x35, 0x75,
    0x08, 0x95, 0x04, 0x81, 0x02, 0xc0};

const tusb_desc_device_t tdd_device_descriptor = {
    .bLength = sizeof(tusb_desc_device_t),
    .bDescriptorType = TUSB_DESC_DEVICE,
    .bcdUSB = 0x0200,
    .bDeviceClass = 0x00,
    .bDeviceSubClass = 0x00,
    .bDeviceProtocol = 0x00,
    .bMaxPacketSize0 = CFG_TUD_ENDPOINT0_SIZE,
    .idVendor = USB_VID,
    .idProduct = USB_PID,
    .bcdDevice = 0x0100,
    .iManufacturer = 0x01,
    .iProduct = 0x02,
    .iSerialNumber = 0x00,
    .bNumConfigurations = 0x01,
};

const uint8_t tdd_configuration_descriptor[] = {
    TUD_CONFIG_DESCRIPTOR(1, 1, 0, (TUD_CONFIG_DESC_LEN + TUD_HID_DESC_LEN),
                          TUSB_DESC_CONFIG_ATT_REMOTE_WAKEUP, 500),
    TUD_HID_DESCRIPTOR(0, 1, HID_ITF_PROTOCOL_NONE, sizeof(desc_hid_report),
                       EPNUM_HID, 64, TDD_HID_BINTERVAL_MS),
};

// Index 0 is the language ID encoded as two raw bytes per the USB spec.
const char* tdd_string_descriptors[] = {
    (const char[]){0x09, 0x04},
    "HORI CO., LTD.",
    "POKKEN CONTROLLER",
    "123456",
    "Tomodachi Gamepad",
};
const size_t tdd_string_descriptor_count =
    sizeof(tdd_string_descriptors) / sizeof(tdd_string_descriptors[0]);

const uint8_t* tud_hid_descriptor_report_cb(uint8_t instance) {
  (void)instance;
  return desc_hid_report;
}

uint16_t tud_hid_get_report_cb(uint8_t itf, uint8_t id, hid_report_type_t type,
                               uint8_t* buf, uint16_t len) {
  (void)itf; (void)id; (void)type; (void)buf; (void)len;
  return 0;
}

void tud_hid_set_report_cb(uint8_t itf, uint8_t id, hid_report_type_t type,
                           const uint8_t* buf, uint16_t len) {
  (void)itf; (void)id; (void)type; (void)buf; (void)len;
}
