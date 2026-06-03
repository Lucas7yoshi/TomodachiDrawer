# TomodachiDrawer.Firmware.ESP32S3

ESP-IDF port of the RP2040 firmware in [../TomodachiDrawer.Firmware/](../TomodachiDrawer.Firmware/). Same TDLD opcode loop, same Pokken HID identity, same Switch behaviour — just running on an ESP32-S3 instead.

## Hardware

Any ESP32-S3 board that exposes the native USB-OTG pins (GPIO 19/20) to a USB connector. Tested against the ESP32-S3-DevKitC-1. Presets for several boards live in `idf.py menuconfig` → "TomodachiDrawer Configuration" → "Target board":

| Board | LED GPIO |
|------|----------|
| ESP32-S3-DevKitC-1 (older rev) | 38 |
| ESP32-S3-DevKitC-1 (later rev) | 48 |
| Waveshare ESP32-S3-Zero | 21 |
| ESP32-S3-DevKitM-1 | 48 |
| Adafruit QT Py ESP32-S3 | 39 |
| Lolin S3 Mini | 47 |
| M5Stack AtomS3 / AtomS3 Lite | 35 |
| Custom | set `CONFIG_TDD_LED_GPIO` manually |

For the DevKitC-1 in particular, two USB-C ports:
- **Left (`UART`)**: USB-Serial-JTAG, used for flashing and serial logs during development.
- **Right (`USB`)**: native USB-OTG. **This is the port that goes into the Switch.**

Runtime use of the BOOT button (GPIO 0):
- Press during the rainbow "done" LED → replay the loaded drawing
- Press during playback → abort, release all inputs, drop into the rainbow state

Hardware functions of the buttons (BOOT-hold-during-reset = ROM download mode, RST = chip reset) are unaffected.

## Building

1. Install ESP-IDF v5.1 or newer (the Espressif Windows installer works fine).
2. Open this folder in VSCode with the Espressif IDF extension.
3. Set target to `esp32s3`.
4. Plug into the **left** USB-C port, select the COM port.
5. 🔥 build/flash/monitor.

## Layout

| File | Purpose |
|------|---------|
| `CMakeLists.txt` | IDF project root |
| `main/TomodachiDrawer.Firmware.c` | Entry point and opcode loop |
| `main/usb_descriptors.[ch]` | HID/device descriptors (same bytes as RP2040) |
| `main/Kconfig.projbuild` | Board presets / LED GPIO menuconfig |
| `main/idf_component.yml` | Managed component deps (`led_strip`, `esp_tinyusb`) |
| `partitions.csv` | Factory app + custom `tdld` data partition |
| `sdkconfig.defaults` | Committed build-time defaults |
