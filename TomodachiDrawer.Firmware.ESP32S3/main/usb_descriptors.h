// USB descriptors for the Pokken-spoofed Switch gamepad. Same bytes as the
// RP2040 firmware - the host can't tell them apart.

#pragma once

#include <stddef.h>
#include <stdint.h>

#include "tusb.h"

extern const tusb_desc_device_t tdd_device_descriptor;
extern const uint8_t tdd_configuration_descriptor[];
extern const char* tdd_string_descriptors[];
extern const size_t tdd_string_descriptor_count;

// Polling interval reported in the HID endpoint descriptor. delay_ms_usb
// stops pushing reports one bInterval before the deadline.
#define TDD_HID_BINTERVAL_MS 8
