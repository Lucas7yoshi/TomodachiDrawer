# TomodachiDrawer

TomodachiDrawer is a collection of firmware and software that generates inputs to control a Nintendo Switch to draw arbitrary images in the Palette House.

The program splits images into layers matched to colours in the game, and generates optimized routes for the pen to follow to draw your image.

It has a Windows WinForms UI that supports flashing directly to a RP2040-Zero which can then be plugged into the USB port of a Switch or Switch 2 where it will begin to draw.

## How To Use

Initial setup requires a few steps, made easier by the windows UI.

### Following the YouTube tutorial is recommended:

Note: flashing lights warning for video, apologies.
[YouTube Tutorial](https://youtu.be/GIaiw3gzabo)


### Or briefly, in text:

1. Download the Windows app here: TODO
2. Extract the zip folder
3. Run TomodachiDrawer.UI.Windows.exe
4. Plug in your RP2040-Zero to your PC while holding the boot button, or while connected hold BOOT and press reset while still holding boot.
5. The program should recognize it.
6. Press "Flash Base Firmware", this will install the code that handles sending the inputs.
7. Repeat the steps to hold the boot button, then open your image by pressing the open button or dragging it in. It must be 256x256 or smaller.
8. Select the Colour Matcher that looks best, adjust the TSP solver time limit (explained in the ? button)
9. Select "Export to RP2040" which will write it directly to the RP2040.
10. Unplug the RP2040 and connect it to your switch (Note: Ensure "Wired Pro Controller Communication" is enabled in your settings!)
    - Note: you must have Palette house open, on "pro" mode, the cursor in the top left of where you want it drawn, zoomed out, and your top colour to be set to black.
11. Upon completion, the RGB LED on the Pi will go to a rainbow. If you disconnect it and reconnect it, it will draw it again. Connect to your PC to change the image!

## Roadmap
Things I want to do in roughly the order I want to do them in:
- Avalonia UI for Mac/Linux users.
- Further optimizations
- Use bucket tool to fill the most significant colour
- Use shape tools for non-square areas of arbitrary size
- Experiment with input acceleration and analogue input for faster movement

## Contributing

This project is a recreation of a mess of AI coded nonsense that was unmaintainable by me and too fixated to my setup. Please refrain from using AI irresponsibily if you wish to contribute. As I encountered several times, even just leaning on it to think of a general idea on how to approach a problem can send you down a overly complicated rabbit hole that you really dont need to, so be smart.

This project is split into the TomodachiDrawer.Core which houses all the main pathing logic, the output sinks, and colour palette info, as well as the UI's (which there is just one, the UI.Windows in WinForms)

I intend to recreate the UI in Avalonia for crossplatform support, although I have never used it so it may be a short bit.

The binary format used is .tdld, and is custom made by me for the purposes of controller microcontrollers. Technically speaking, this format is not at all bound to Tomodachi Life as it is just a generic way to represent inputs and delays in a compact form.

Visual Studio 2026 is neccasary as well as the .NET 10 runtime. For the TomodachiDrawer.Firmware, please see the README.md in the folder.

Contributions are encouraged, and if you want to make a new UI for a new platform you are more than welcome to, in fact, it would be greatly appreciated!

The main areas for improvement are optimizations to the routing logic, I strongly discourage letting AI go loose on this as well, as I found my prior ai-slop-proof-of-concept version was actively slower than even the more simpler logic in the first iteration of this!

## License
See [LICENSE](./LICENSE)

## Used libraries
This project depends on the following libraries:

- SkiaSharp	(For image reading/writing)
- Google.OrTools (for the TSP solving)
