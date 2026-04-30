#include "tusb.h"

#define USB_VID   0x0F0D
#define USB_PID   0x0092

uint8_t const desc_hid_report[] = {
    0x05, 0x01, 0x09, 0x05, 0xA1, 0x01, 0x15, 0x00,
    0x25, 0x01, 0x35, 0x00, 0x45, 0x01, 0x75, 0x01,
    0x95, 0x10, 0x05, 0x09, 0x19, 0x01, 0x29, 0x10,
    0x81, 0x02, 0x05, 0x01, 0x25, 0x07, 0x46, 0x3b,
    0x01, 0x75, 0x04, 0x95, 0x01, 0x65, 0x14, 0x09,
    0x39, 0x81, 0x42, 0x65, 0x00, 0x95, 0x01, 0x81,
    0x01, 0x26, 0xff, 0x00, 0x46, 0xff, 0x00, 0x09,
    0x30, 0x09, 0x31, 0x09, 0x32, 0x09, 0x35, 0x75,
    0x08, 0x95, 0x04, 0x81, 0x02, 0xc0
};

#define EPNUM_HID   0x81

uint8_t const desc_device[] = {
    18, TUSB_DESC_DEVICE, 0x00, 0x02,
    0x00, 0x00, 0x00,               // Class/SubClass/Protocol: defined at interface level
    CFG_TUD_ENDPOINT0_SIZE,
    (uint8_t)(USB_VID & 0xff), (uint8_t)(USB_VID >> 8),
    (uint8_t)(USB_PID & 0xff), (uint8_t)(USB_PID >> 8),
    0x00, 0x01, 0x01, 0x02, 0x00, 0x01
};

uint8_t const desc_configuration[] = {
    // 1 interface (HID only)
    TUD_CONFIG_DESCRIPTOR(1, 1, 0, (TUD_CONFIG_DESC_LEN + TUD_HID_DESC_LEN), TUSB_DESC_CONFIG_ATT_REMOTE_WAKEUP, 500),
    TUD_HID_DESCRIPTOR(0, 1, HID_ITF_PROTOCOL_NONE, sizeof(desc_hid_report), EPNUM_HID, 64, 8)
};

char const* string_desc_arr[] = {
    (const char[]) { 0x09, 0x04 }, // 0: English
    "HORI CO., LTD.",              // 1: Manufacturer
    "POKKEN CONTROLLER",           // 2: Product
    "123456",                      // 3: Serial
    "Tomodachi Gamepad",           // 4: HID Interface Name
};

uint8_t const * tud_descriptor_device_cb(void) { return desc_device; }
uint8_t const * tud_descriptor_configuration_cb(uint8_t index) { (void)index; return desc_configuration; }
uint8_t const * tud_hid_descriptor_report_cb(uint8_t instance) { (void)instance; return desc_hid_report; }

uint16_t _desc_str[32];
uint16_t const* tud_descriptor_string_cb(uint8_t index, uint16_t langid) {
    (void) langid;
    uint8_t chr_count = 0;

    if (index == 0) {
        memcpy(&_desc_str[1], string_desc_arr[0], 2);
        chr_count = 1;
    } else {
        if (index >= sizeof(string_desc_arr) / sizeof(string_desc_arr[0])) return NULL;
        const char* str = string_desc_arr[index];
        chr_count = strlen(str);
        if (chr_count > 31) chr_count = 31;
        for (uint8_t i = 0; i < chr_count; i++) {
            _desc_str[1 + i] = str[i];
        }
    }
    _desc_str[0] = (TUSB_DESC_STRING << 8) | (2 * chr_count + 2);
    return _desc_str;
}
