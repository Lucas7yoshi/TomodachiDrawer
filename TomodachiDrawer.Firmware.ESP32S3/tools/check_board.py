#!/usr/bin/env python3
"""Probe an ESP32 board's bootloader and report whether it can run this
firmware. Requires the chip family to have native USB-OTG (S2/S3/P4) and at
least 4MB of flash.

    python tools/check_board.py --port COM3

Cannot detect whether the USB-OTG pins are routed to a physical connector -
that's a board-layout property the chip doesn't know about - so the verdict
covers chip and flash only.
"""

import argparse
import os
import re
import subprocess
import sys

# Chip families that have a USB-OTG peripheral and can therefore run this
# project's firmware after a port. Anything else fundamentally won't work,
# Bluetooth-only firmware notwithstanding.
SUPPORTED_CHIPS = {
    "ESP32-S2": "supported via port (not yet implemented; only S3 ships today)",
    "ESP32-S3": "fully supported - this is the target chip for this firmware",
    "ESP32-P4": "supported via port (not yet implemented)",
}

INCOMPATIBLE_CHIPS = {
    "ESP32": (
        "Original ESP32 has no USB peripheral at all. Cannot enumerate as a "
        "USB HID device. Switch to an ESP32-S3 board (e.g. ESP32-S3-DevKitC-1 "
        "or Waveshare ESP32-S3-Zero ~$5)."
    ),
    "ESP32-C3": (
        "ESP32-C3 only has USB-Serial-JTAG, not USB-OTG. The USB-Serial-JTAG "
        "is hardwired to enumerate as a serial+JTAG composite device and "
        "cannot be configured for HID. Switch to an ESP32-S3."
    ),
    "ESP32-C6": (
        "ESP32-C6 only has USB-Serial-JTAG, same limitation as the C3. "
        "Switch to an ESP32-S3."
    ),
    "ESP32-H2": (
        "ESP32-H2 only has USB-Serial-JTAG, same limitation as the C3. "
        "Switch to an ESP32-S3."
    ),
}

MIN_FLASH_MB = 4


def find_idf_python():
    """Same logic as flash_drawing.py - we want the IDF venv Python because
    it has esptool installed."""
    venv_root = os.environ.get("IDF_PYTHON_ENV_PATH")
    if venv_root:
        candidate = os.path.join(venv_root, "Scripts", "python.exe")
        if os.path.isfile(candidate):
            return candidate
    for candidate in [
        r"C:\Espressif\tools\python\v6.0.1\venv\Scripts\python.exe",
        r"C:\Espressif\tools\python\v5.4\venv\Scripts\python.exe",
        r"C:\Espressif\python_env\idf6.0_py3.11_env\Scripts\python.exe",
        r"C:\Espressif\python_env\idf5.4_py3.11_env\Scripts\python.exe",
    ]:
        if os.path.isfile(candidate):
            return candidate
    return None


def run_esptool(idf_python, port, *args):
    """Run esptool as a subprocess and return its stdout. Raises on failure."""
    cmd = [idf_python, "-m", "esptool", "--port", port] + list(args)
    proc = subprocess.run(cmd, capture_output=True, text=True)
    if proc.returncode != 0:
        raise RuntimeError(
            f"esptool {' '.join(args)} failed (rc={proc.returncode}):\n"
            f"--- stdout ---\n{proc.stdout}\n--- stderr ---\n{proc.stderr}"
        )
    return proc.stdout


def parse_chip_info(output):
    """Pull chip family + revision out of an esptool stdout dump."""
    chip_match = re.search(r"Chip is (\S+)(?:\s+\([^)]+\))?(?:\s+\(revision (\S+)\))?", output)
    if not chip_match:
        chip_match = re.search(r"Detecting chip type\.+\s*(\S+)", output)
        if not chip_match:
            return None, None
        return chip_match.group(1), None
    return chip_match.group(1), chip_match.group(2)


def parse_flash_size(output):
    """Pull detected flash size out of esptool's stdout."""
    m = re.search(r"Detected flash size:\s*(\d+)MB", output)
    if m:
        return int(m.group(1))
    return None


def parse_mac(output):
    m = re.search(r"MAC:\s*([0-9a-fA-F:]+)", output)
    return m.group(1) if m else None


def main():
    p = argparse.ArgumentParser(description=__doc__,
                                formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--port", required=True, help="Serial port (e.g. COM3)")
    args = p.parse_args()

    idf_python = find_idf_python()
    if not idf_python:
        print("ERROR: could not locate the ESP-IDF venv Python (which has "
              "esptool installed). Run from an ESP-IDF terminal or install "
              "esptool yourself with: pip install esptool", file=sys.stderr)
        return 2

    print(f"Probing board on {args.port} ...")
    try:
        # `flash-id` triggers chip detection AND flash detection in one call,
        # printing all the info we need to a single stdout buffer.
        out = run_esptool(idf_python, args.port, "flash-id")
    except RuntimeError as e:
        print(f"\nERROR: esptool failed to talk to the chip.", file=sys.stderr)
        print("Common causes:", file=sys.stderr)
        print("  - Wrong COM port (try the other USB-C connector if you have"
              " two)", file=sys.stderr)
        print("  - Another process holding the port (close the ESP-IDF "
              "monitor)", file=sys.stderr)
        print("  - Board not in bootloader mode (try holding BOOT then "
              "pressing RST, then release BOOT)", file=sys.stderr)
        print(f"\nesptool output:\n{e}", file=sys.stderr)
        return 1

    chip, revision = parse_chip_info(out)
    flash_mb = parse_flash_size(out)
    mac = parse_mac(out)

    print()
    print("=== Board Compatibility Check ===")
    print(f"Port:      {args.port}")
    print(f"Chip:      {chip or '(could not detect)'}"
          f"{f' rev {revision}' if revision else ''}")
    print(f"Flash:     {f'{flash_mb} MB' if flash_mb else '(could not detect)'}")
    print(f"MAC:       {mac or '(could not detect)'}")
    print()

    if not chip:
        print("VERDICT: UNKNOWN")
        print("Could not parse chip family from esptool output. Run with the "
              "ESP-IDF terminal active and paste the full output for help.")
        return 1

    issues = []
    if chip in INCOMPATIBLE_CHIPS:
        issues.append(f"Chip family {chip}: {INCOMPATIBLE_CHIPS[chip]}")
    elif chip not in SUPPORTED_CHIPS:
        issues.append(
            f"Chip family {chip} is not on the known-supported list "
            f"({', '.join(SUPPORTED_CHIPS)}). It may or may not work; "
            f"verify it has a USB-OTG peripheral."
        )

    if flash_mb is not None and flash_mb < MIN_FLASH_MB:
        issues.append(
            f"Flash is {flash_mb} MB; need >= {MIN_FLASH_MB} MB for the "
            f"partition layout (2 MB app + 1 MB tdld + bootloader/nvs/phy)."
        )

    if issues:
        print("VERDICT: INCOMPATIBLE")
        print()
        for issue in issues:
            print(f"  - {issue}")
        print()
        return 1

    print("VERDICT: COMPATIBLE")
    print()
    print(f"  {chip}: {SUPPORTED_CHIPS[chip]}")
    print()
    print("NOTE: This check confirms the chip family supports USB HID and the "
          "flash is large enough. It CANNOT verify that the board's USB-OTG "
          "D+/D- pins are routed to a physical USB connector. If your board "
          "has a second USB port (or its only USB port is wired to the chip's "
          "USB pins, not just a USB-UART bridge), you should be good.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
