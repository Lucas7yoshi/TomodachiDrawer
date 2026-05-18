#!/usr/bin/env python3
"""Generate synthetic TDLD blobs (and optionally flash them) for exercising
the firmware without the Avalonia UI. The opcode encoding mirrors
FileControllerSink.cs in TomodachiDrawer.Core.

    python test_tdld.py easy_a                       # write test_easy_a.bin
    python test_tdld.py easy_a --port COM3 --flash   # write + flash
    python test_tdld.py --list                       # list presets

Detach the serial monitor before flashing (it holds the COM port). After
flashing, reset the board.
"""

import argparse
import os
import subprocess
import sys

# Opcodes (high nibble) - must match FileControllerSink.cs and the firmware.
OP_INVALID = 0x0
OP_PRESS_BUTTON = 0x1
OP_RELEASE_BUTTON = 0x2
OP_PRESS_DPAD = 0x3
OP_RELEASE_DPAD = 0x4
OP_RELEASE_ALL = 0x5
OP_DELAY = 0x6
OP_SET_STICK = 0x7
OP_TAP_BUTTON = 0x8
OP_TAP_DPAD = 0x9
OP_REPEAT_1 = 0xE
OP_REPEAT_2 = 0xF

# Buttons (C# enum index - the low nibble of press/release/tap records)
BTN_A, BTN_B, BTN_X, BTN_Y = 0, 1, 2, 3
BTN_L, BTN_R, BTN_ZL, BTN_ZR = 4, 5, 6, 7
BTN_MINUS, BTN_PLUS = 8, 9
BTN_LCLICK, BTN_RCLICK = 10, 11
BTN_HOME, BTN_CAPTURE = 12, 13

# DPad
DPAD_UP, DPAD_UPRIGHT, DPAD_RIGHT, DPAD_DOWNRIGHT = 0, 1, 2, 3
DPAD_DOWN, DPAD_DOWNLEFT, DPAD_LEFT, DPAD_UPLEFT = 4, 5, 6, 7
DPAD_NEUTRAL = 8

# Sticks (axis index for OP_SET_STICK)
STICK_LX, STICK_LY, STICK_RX, STICK_RY = 0, 1, 2, 3


# Encoders.
def header():
    return bytes([ord('T'), ord('D'), ord('L'), ord('D'), 0x03, 0x00])

def press_button(b):
    return bytes([(OP_PRESS_BUTTON << 4) | b])

def release_button(b):
    return bytes([(OP_RELEASE_BUTTON << 4) | b])

def press_dpad(d):
    return bytes([(OP_PRESS_DPAD << 4) | d])

def release_dpad():
    return bytes([(OP_RELEASE_DPAD << 4)])

def release_all():
    return bytes([(OP_RELEASE_ALL << 4)])

def delay(ms):
    # The opcode is 12-bit so max single delay is 4095ms. For longer waits,
    # chain multiple delays (same approach the C# sink uses).
    out = b""
    while ms > 0:
        chunk = min(ms, 0xFFF)
        out += bytes([(OP_DELAY << 4) | (chunk >> 8), chunk & 0xFF])
        ms -= chunk
    return out

def set_stick(axis, value):
    return bytes([(OP_SET_STICK << 4) | axis, value])

def tap_button(b):
    return bytes([(OP_TAP_BUTTON << 4) | b])

def tap_dpad(d):
    return bytes([(OP_TAP_DPAD << 4) | d])

def repeat_1(count):
    assert 0 <= count <= 15, "repeat_1 count must fit in 4 bits"
    return bytes([(OP_REPEAT_1 << 4) | count])

def repeat_2(count):
    assert 0 <= count <= 0xFFF, "repeat_2 count must fit in 12 bits"
    return bytes([(OP_REPEAT_2 << 4) | (count >> 8), count & 0xFF])

def eof():
    return bytes([(OP_INVALID << 4)])


# Test presets.
def test_easy_a():
    """Hold A button for 3 seconds. The firmware's BTN_A sets bit 2 of report
    byte 0, which is HID button 3 in joy.cpl - NOT button 1. Or use
    gamepad-tester.com which polls faster and lays out the buttons clearly."""
    out = header()
    out += delay(500)
    out += press_button(BTN_A)
    out += delay(3000)
    out += release_button(BTN_A)
    out += delay(500)
    out += eof()
    return out


def test_buttons_slow():
    """A, B, X, Y in sequence, each held for 3 seconds. Maps to HID buttons:
        A (BTN_A=bit 2) -> HID button 3
        B (BTN_B=bit 1) -> HID button 2
        X (BTN_X=bit 3) -> HID button 4
        Y (BTN_Y=bit 0) -> HID button 1
    """
    out = header()
    out += delay(500)
    for b in [BTN_A, BTN_B, BTN_X, BTN_Y]:
        out += press_button(b)
        out += delay(3000)
        out += release_button(b)
        out += delay(500)
    out += eof()
    return out


def test_all_buttons():
    """Press each button in turn for 500ms - verifies the full button_map.
    Skips HOME and CAPTURE so we don't trigger Switch menus during testing."""
    out = header()
    out += delay(500)
    buttons = [
        ("A", BTN_A), ("B", BTN_B), ("X", BTN_X), ("Y", BTN_Y),
        ("L", BTN_L), ("R", BTN_R), ("ZL", BTN_ZL), ("ZR", BTN_ZR),
        ("MINUS", BTN_MINUS), ("PLUS", BTN_PLUS),
        ("LCLICK", BTN_LCLICK), ("RCLICK", BTN_RCLICK),
    ]
    for _, b in buttons:
        out += press_button(b)
        out += delay(500)
        out += release_button(b)
        out += delay(200)
    out += eof()
    return out


def test_dpad():
    """Cycle the D-pad through all 8 directions, 500ms each."""
    out = header()
    out += delay(500)
    for d in [DPAD_UP, DPAD_UPRIGHT, DPAD_RIGHT, DPAD_DOWNRIGHT,
              DPAD_DOWN, DPAD_DOWNLEFT, DPAD_LEFT, DPAD_UPLEFT]:
        out += press_dpad(d)
        out += delay(500)
    out += release_dpad()
    out += eof()
    return out


def test_sticks():
    """Move both sticks to extremes - exercises OP_SET_STICK."""
    out = header()
    out += delay(500)
    # LX: right, center, left, center
    out += set_stick(STICK_LX, 255); out += delay(500)
    out += set_stick(STICK_LX, 128); out += delay(300)
    out += set_stick(STICK_LX, 0);   out += delay(500)
    out += set_stick(STICK_LX, 128); out += delay(300)
    # LY: down, center, up, center
    out += set_stick(STICK_LY, 255); out += delay(500)
    out += set_stick(STICK_LY, 128); out += delay(300)
    out += set_stick(STICK_LY, 0);   out += delay(500)
    out += set_stick(STICK_LY, 128); out += delay(300)
    # RX/RY: same as LX/LY
    out += set_stick(STICK_RX, 255); out += delay(500)
    out += set_stick(STICK_RX, 0);   out += delay(500)
    out += set_stick(STICK_RX, 128); out += delay(300)
    out += set_stick(STICK_RY, 0);   out += delay(500)
    out += set_stick(STICK_RY, 255); out += delay(500)
    out += set_stick(STICK_RY, 128); out += delay(300)
    out += eof()
    return out


def test_rle():
    """Stress test the OP_REPEAT_LAST_1 and OP_REPEAT_LAST_2 paths. Without
    these, the file size for a real drawing would balloon several-fold."""
    out = header()
    out += delay(500)

    # 10 A taps via the 4-bit repeat (1 tap + 9 repeats)
    out += tap_button(BTN_A)
    out += repeat_1(9)
    out += delay(1000)

    # 50 D-pad UP taps via the 12-bit repeat
    out += tap_dpad(DPAD_UP)
    out += repeat_2(49)
    out += delay(500)

    out += eof()
    return out


def test_combo():
    """Hold A while moving the left stick - exercises overlapping report
    state, which is the common pattern during real drawing playback."""
    out = header()
    out += delay(500)
    out += press_button(BTN_A)
    out += set_stick(STICK_LX, 200)
    out += delay(500)
    out += set_stick(STICK_LX, 50)
    out += delay(500)
    out += set_stick(STICK_LX, 128)
    out += release_button(BTN_A)
    out += delay(500)
    out += eof()
    return out


TESTS = {
    "easy_a": test_easy_a,
    "buttons_slow": test_buttons_slow,
    "all_buttons": test_all_buttons,
    "dpad": test_dpad,
    "sticks": test_sticks,
    "rle": test_rle,
    "combo": test_combo,
}


def find_parttool():
    """Locate parttool.py via $IDF_PATH (set by the ESP-IDF terminal) or fall
    back to a sane default. Returns the absolute path or None."""
    idf_path = os.environ.get("IDF_PATH")
    candidates = []
    if idf_path:
        candidates.append(os.path.join(idf_path, "components", "partition_table",
                                       "parttool.py"))
    # Fallbacks for the Espressif Windows installer's default layout
    candidates += [
        r"D:\esp\.espressif\v6.0.1\esp-idf\components\partition_table\parttool.py",
        r"C:\Espressif\frameworks\esp-idf-v6.0.1\components\partition_table\parttool.py",
        r"C:\Espressif\frameworks\esp-idf-v5.4\components\partition_table\parttool.py",
    ]
    for p in candidates:
        if os.path.isfile(p):
            return p
    return None


def flash_bin(bin_path, port):
    parttool = find_parttool()
    if not parttool:
        print("ERROR: could not locate parttool.py. Run this from the ESP-IDF "
              "terminal or set IDF_PATH.", file=sys.stderr)
        return 1
    cmd = [sys.executable, parttool, "--port", port, "write_partition",
           "--partition-name", "tdld", "--input", bin_path]
    print("Running:", " ".join(cmd))
    return subprocess.call(cmd)


def main():
    p = argparse.ArgumentParser(description=__doc__,
                                formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("test", nargs="?", choices=list(TESTS.keys()),
                   help="Which preset to generate")
    p.add_argument("--list", action="store_true", help="List all presets")
    p.add_argument("-o", "--output", default=None,
                   help="Output path (default: ./test_<name>.bin)")
    p.add_argument("--port", help="Serial port for flashing (e.g. COM3)")
    p.add_argument("--flash", action="store_true",
                   help="After generating, flash to the ESP32 (requires --port)")
    args = p.parse_args()

    if args.list:
        for name, fn in TESTS.items():
            doc = (fn.__doc__ or "").splitlines()[0]
            print(f"  {name:<14} {doc}")
        return 0

    if not args.test:
        p.error("specify a test name or use --list")

    data = TESTS[args.test]()
    out_path = args.output or f"test_{args.test}.bin"
    with open(out_path, "wb") as f:
        f.write(data)
    print(f"Wrote {len(data)} bytes to {out_path}")

    if args.flash:
        if not args.port:
            p.error("--flash requires --port")
        return flash_bin(out_path, args.port)
    elif args.port:
        # Convenience: --port without --flash just prints what we'd run
        parttool = find_parttool()
        if parttool:
            print("To flash:")
            print(f"  python {parttool} --port {args.port} "
                  f"write_partition --partition-name tdld --input {out_path}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
