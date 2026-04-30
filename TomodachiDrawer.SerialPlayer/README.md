# TomodachiLifeDrawer.SerialPlayer
This is meant for development purposes and runs on a Raspberry Pi 4B.

This is not practical for end-users and is meant mostly for me during development.
SerialPlayer runs on a Raspberry Pi 4b which reads the tdld file and pipes it over the uart.

Why? Because unplugging and plugging back in to adjust timings and so on is annoying, and bad for the USB C port.

## MicroPython code
in `rp2040_micropython_src/`

Flash your RP2040 with the appropriate MicroPython firmware, then drop in the `code.py` and `boot.py` files onto it.

The RP2040 will then listen for serial over the UART interface at a baudrate of 230400.

Disclaimer: the python code is from the Slop era, primarily because i dont really use python that much.
That said, the code is pretty simple to follow. `boot.py` is perhaps a bit more out there. Like why did it add a comment calling it "professional"?