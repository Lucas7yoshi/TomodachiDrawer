# EspFirmware

Pre-built ESP32-S3 firmware binaries shipped with the UI so the "Flash Base Firmware (ESP32-S3)" button can flash a fresh board without the user needing ESP-IDF installed.

Expected contents:

| File | Flash offset | Source |
|------|--------------|--------|
| `bootloader.bin` | `0x0` | `TomodachiDrawer.Firmware.ESP32S3/build/bootloader/bootloader.bin` |
| `partition-table.bin` | `0x8000` | `TomodachiDrawer.Firmware.ESP32S3/build/partition_table/partition-table.bin` |
| `TomodachiDrawer_FW_ESP32S3.bin` | `0x10000` | `TomodachiDrawer.Firmware.ESP32S3/build/TomodachiDrawer_FW_ESP32S3.bin` |

The binaries themselves are not committed. They get here two ways:

- **CI**: the `build-esp-firmware` job in `.github/workflows/dotnet-build.yml` runs `espressif/esp-idf-ci-action` against the firmware project and downloads the resulting bins into this folder before `dotnet publish`.
- **Local dev**: an MSBuild target in the .csproj copies the bins from `../TomodachiDrawer.Firmware.ESP32S3/build/` if they exist there (i.e. if you've run `idf.py build` on the firmware project).

If this folder is empty when the app starts, the "Flash Base Firmware (ESP32-S3)" button stays disabled.
