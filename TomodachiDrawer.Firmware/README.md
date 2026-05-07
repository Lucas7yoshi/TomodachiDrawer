# TomodachiDrawer.Firmware
This folder houses the Pico SDK .c code that runs on the RP2040 or RP2350 board.

To be candid, this went through a few versions before I got it working, some which were like 80% ai written.

The best part? The ai written versions were overly complicated and had horrifying timing problems.

The end version is pretty much all my code, except for the rainbow logic which i asked ai to write.

This is simple by design, it is loaded all into ram for speed to avoid the need for wrapping functions in stuff.

## Building

### Prerequisites
You need the **ARM Embedded Toolchain** and **CMake**. The Pico SDK is fetched automatically by `pico_sdk_import.cmake`.

**Windows:** Install the [Raspberry Pi Pico VS Code Extension](https://marketplace.visualstudio.com/items?itemName=raspberry-pi.raspberry-pi-pico), which handles toolchain + SDK installation automatically.

**Linux:**
```bash
# Ubuntu/Debian:
sudo apt install cmake gcc-arm-none-eabi libnewlib-arm-none-eabi build-essential
```

### Build the firmwares

```bash
cd TomodachiDrawer.Firmware
./build_all.sh          # builds both RP2040 and RP2350
./build_all.sh --clean  # clear cache and build from scratch
```

Outputs:
- `build-rp2040/TomodachiDrawer.Firmware.uf2` for RP2040 Zero
- `build-rp2350/TomodachiDrawer.Firmware.uf2` for RP2350 Zero

### Flashing to the board manually

1. Hold BOOTSEL on the board and plug it into USB
2. The board should mount as an USB mass storage drive named "RPI-RP2" or "RP2350" depending on the board
3. Drag-and-drop the firmware `.uf2` file for your board to that drive
4. If everything goes well, the board should reboot itself and starting running the firmware
