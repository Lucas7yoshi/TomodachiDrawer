# TomodachiDrawer.Firmware
This folder houses the Pico SDK .c code that runs on the RP2040.

To be candid, this went through a few versions before I got it working, some which were like 80% ai written.

The best part? The ai written versions were overly complicated and had horrifying timing problems.

The end version is pretty much all my code, except for the rainbow logic which i asked ai to write.

This is simple by design, it is loaded all into ram for speed to avoid the need for wrapping functions in stuff.

## Building
I built this through the vscode Pico SDK which did a lot of the installations on its own and seemed to generally just work™ so that was nice.