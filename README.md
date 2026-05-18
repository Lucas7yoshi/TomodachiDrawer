# TomodachiDrawer

TomodachiDrawer is a collection of firmware and software that generates inputs to control a Nintendo Switch to draw arbitrary images in the Palette House.

## WARNING: Switch 1 is prone to desyncs
See #12 , unfortunately it seems that the Switch 1 is prone to desyncing randomly from inexplicable lag spikes. Current testing suggests this is related to the 3D preview. Switch 2 users are unaffected, and drawings of types that are just lone transparent images do not seem to be prone to these effects. 0.3.1 and 0.3.2 added some mitigations for some other types of delay, but the lag one is still under investigation. If you have any information or clips of the desync occuring, be sure to comment it on that issue!

<img src="Docs/baconator_preview.webp" width="600" alt="Tomodachi Drawer drawing a Baconator">
<img src="Docs/nurture_preview.webp" width="600" alt="Tomodachi Drawer drawing the Porter Robinson album art for Nurture">

The program splits images into layers matched to colours in the game, and generates optimized routes for the pen to follow to draw your image.

It has a crossplatform Avalonia UI desktop app that supports flashing directly to a RP2040-Zero or ESP32-S3 board, which can then be plugged into the USB port of a Switch or Switch 2 where it will begin to draw.

## Hardware Compatibility
This was designed for an RP2040-Zero as it was one of the cheapest options, however any RP2040 based board *should* be compatible, with support for the LED on the standard Raspberry Pi Pico too.

ESP32-S3 boards are also supported. The recommended board is the Waveshare ESP32-S3-Zero - same physical form factor as the RP2040-Zero and onboard WS2812. Other tested S3 boards (Espressif DevKitC-1, Adafruit QT Py ESP32-S3, Lolin S3 Mini, M5Stack AtomS3) have presets in `idf.py menuconfig`, see [TomodachiDrawer.Firmware.ESP32S3/README.md](TomodachiDrawer.Firmware.ESP32S3/README.md). Note that this needs an ESP32-S3 specifically - the original ESP32, ESP32-C3/C6/H2 lack the USB-OTG peripheral required to enumerate as a Switch controller.

One UX wrinkle for the single-port S3 boards (S3-Zero, QT Py S3, AtomS3, etc): once the firmware is running it's pretending to be a Switch controller on that one USB-C port, so esptool can't reach it any more. To re-flash a drawing you have to put the chip back into ROM bootloader mode first - hold BOOT while plugging into the PC (or hold BOOT and tap RST if the board has both buttons). Boards with two USB-C ports (DevKitC-1, DevKitM-1) don't have this problem because their second port is a separate USB-UART bridge that esptool can always reach. If you'll be iterating on drawings a lot, the dual-port boards are friendlier.

## How To Use

Initial setup requires a few steps, made easier by the UI.

### Following the YouTube tutorial is recommended:

Note: flashing lights warning for video, apologies.
[YouTube Tutorial](https://youtu.be/GIaiw3gzabo)

An addendum video covering the changes since that was made is available here:
[Addendum Tutorial](https://youtu.be/9rVLea1-nlY)

### Downloads
Downloads are available in the releases, they come in a few forms

[Releases](https://github.com/Lucas7yoshi/TomodachiDrawer/releases)

- TomodachiDrawer.UI.Avalonia.#.#.#.platform.zip
platform can be win64 for windows, osx-arm64 for Mac on ARM cpus, osx64 for Mac on x64 cpus, and linux64 and linuxarm64 for the same on linux.
Download the one that is right for your computer, for mac users with any recent macbook arm64 should work.

For Linux and Mac users, you may need to run chmod +x binaryNameHere or go into your settings to allow it.

### Or briefly, in text:

1. Download the Desktop app here, for your platform: https://github.com/Lucas7yoshi/TomodachiDrawer/releases
3. Extract the zip folder
4. Run TomodachiDrawer.UI.Avalonia.[YourPlatform].exe (or similar for your platform)
5. Plug in your RP2040-Zero (or Raspberry Pi Pico) to your PC while holding the boot button, or while connected hold BOOT and press reset while still holding boot.
6. The program should recognize it.
7. Press "Flash Base Firmware", this will install the code that handles sending the inputs.
8. Repeat the steps to hold the boot button, then open your image by pressing the open button or dragging it in. It must be 256x256 or smaller.
9. Select the Colour Matcher that looks best (arbitrary uses the full range), adjust the TSP solver time limit (explained in the ? button) as needed.
10. Select "Export to RP2040" which will write it directly to the RP2040.
11. Unplug the RP2040 and connect it to your switch (Note: Ensure "Wired Pro Controller Communication" is enabled in your settings!)
    - Note: you must have Palette house open, on "pro" mode, the cursor in the top left of where you want it drawn, zoomed out, and your top colour to be set to black.
12. Upon completion, the RGB LED on the Pi will go to a rainbow. If you disconnect it and reconnect it, it will draw it again. Connect to your PC to change the image!

#### If the program does not recognize your RP2040
The logic may be delicate for Linux and Mac platforms as those are ones I cannot test.
If it does not detect it, you can still drag-and-drop the .uf2 included with the download to your Pi for step 7, and for the image data, select "export .uf2" and save it to a destination, and once done, drag and drop onto the RP2040 drive to flash it manually.

### Using an ESP32-S3 instead of an RP2040

The UI has a separate "ESP32-S3 Output" panel below the RP2040 one. Plug the board in (use the left `UART` port if you have a dual-port DevKitC-1; otherwise just the one port), click Re-scan, and it'll identify the chip + show what firmware is on it. From there:

1. Click Flash Base Firmware to install the firmware (only needed once per board, or after firmware updates).
2. Click Export To ESP32-S3 to write your drawing - same routing as the RP2040 path, just goes to the ESP32 via esptool instead of a UF2 drag-and-drop.
3. Unplug from your PC, plug into the Switch via the board's native USB-OTG port (on the DevKitC-1 that's the right USB-C labeled `USB`, not the left `UART` one; on single-port boards like the S3-Zero it's just the one port).

For single-port S3 boards, you'll need to put the chip back into ROM bootloader mode before each new Export To ESP32-S3 - hold BOOT while plugging in. Dual-port boards skip this step because esptool can reset the chip via the UART port's DTR/RTS signals.

Runtime: the BOOT button replays the loaded drawing once playback finishes, or aborts a drawing mid-playback if you press it during playback (handy for long drawings - the RP2040 path needs a power cycle for the same).

## Contributing

This project is a recreation of a mess of AI coded nonsense that was unmaintainable by me and too fixated to my setup. Please refrain from using AI irresponsibily if you wish to contribute. As I encountered several times, even just leaning on it to think of a general idea on how to approach a problem can send you down a overly complicated rabbit hole that you really dont need to, so be smart.

This project is split into the TomodachiDrawer.Core which houses all the main pathing logic, the output sinks, and colour palette info, as well as the UI's (which there is just one, the UI.Windows in WinForms)

The binary format used is .tdld, and is custom made by me for the purposes of controller microcontrollers. Technically speaking, this format is not at all bound to Tomodachi Life as it is just a generic way to represent inputs and delays in a compact form.

Visual Studio 2026 is neccasary as well as the .NET 10 runtime. For the TomodachiDrawer.Firmware (RP2040, Pico SDK) and TomodachiDrawer.Firmware.ESP32S3 (ESP32-S3, ESP-IDF), please see the README.md in each folder.

Contributions are encouraged, and if you want to make a new UI for a new platform you are more than welcome to, in fact, it would be greatly appreciated!

The main areas for improvement are optimizations to the routing logic, I strongly discourage letting AI go loose on this as well, as I found my prior ai-slop-proof-of-concept version was actively slower than even the more simpler logic in the first iteration of this!

## License
This project is licensed under the GPL-3.0 license, read it in full here: [LICENSE](./LICENSE)

The main motivator for this license is that it requires that derivatives share their work with the class openly if they derive from this.

## Used libraries
This project depends on the following libraries:

- SkiaSharp	(For image reading/writing)
- Google.OrTools (for the TSP solving)
- ImageSharp (For its WuQuantizer)
- System.IO.Ports (for ESP32-S3 serial port enumeration in the UI)

The ESP32-S3 firmware (TomodachiDrawer.Firmware.ESP32S3) additionally uses:

- ESP-IDF (Espressif's framework, currently pinned to v6.0 in CI)
- TinyUSB via esp_tinyusb (USB HID device stack)
- led_strip (WS2812 RGB LED driver, RMT-backed)
- esptool (bundled with the Avalonia release for flashing - GPLv2-or-later, redistributed under GPLv3 compatibility)
