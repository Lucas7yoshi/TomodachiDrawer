#!/usr/bin/env python3
"""Flash a drawing onto the ESP32-S3 tdld partition. Accepts either a raw
.tdld or a .uf2 (the UI's RP2040-wrapped export); the UF2 form has its blocks
unwrapped before flashing.

    python tools/flash_drawing.py path/to/drawing.uf2  --port COM3
    python tools/flash_drawing.py path/to/drawing.tdld --port COM3

Detach the serial monitor before running (it holds the COM port). Reset the
board after flashing to start playback.
"""

import argparse
import os
import struct
import subprocess
import sys

# UF2 block format constants - must match UF2Flasher.cs in the Avalonia UI.
UF2_BLOCK_SIZE = 512
UF2_PAYLOAD_OFFSET = 0x20
UF2_PAYLOAD_SIZE_OFFSET = 0x10
UF2_START_MAGIC0 = 0x0A324655
UF2_START_MAGIC1 = 0x9E5D5157
UF2_END_MAGIC = 0x0AB16F30


def extract_tdld_from_uf2(uf2_path):
    """Walk the UF2 blocks and concatenate their payloads back into the raw
    TDLD byte stream that the firmware on the ESP32-S3 expects.

    UF2Flasher.cs zero-pads the tail of the last block, which is harmless to
    the firmware - it reads opcodes byte by byte and stops at the first EOF
    opcode (0x00 in the high nibble), so trailing zeros are simply ignored.
    """
    with open(uf2_path, "rb") as f:
        uf2_bytes = f.read()

    if len(uf2_bytes) == 0 or len(uf2_bytes) % UF2_BLOCK_SIZE != 0:
        raise ValueError(
            f"file size {len(uf2_bytes)} is not a positive multiple of 512 - "
            "doesn't look like a UF2"
        )

    out = bytearray()
    for i in range(len(uf2_bytes) // UF2_BLOCK_SIZE):
        block = uf2_bytes[i * UF2_BLOCK_SIZE:(i + 1) * UF2_BLOCK_SIZE]
        magic0, magic1 = struct.unpack_from("<II", block, 0)
        end_magic, = struct.unpack_from("<I", block, UF2_BLOCK_SIZE - 4)
        if (magic0 != UF2_START_MAGIC0 or magic1 != UF2_START_MAGIC1
                or end_magic != UF2_END_MAGIC):
            raise ValueError(f"block {i} has bad UF2 magic")
        payload_size, = struct.unpack_from("<I", block, UF2_PAYLOAD_SIZE_OFFSET)
        if payload_size > (UF2_BLOCK_SIZE - UF2_PAYLOAD_OFFSET - 4):
            raise ValueError(f"block {i} payload size {payload_size} too large")
        out.extend(block[UF2_PAYLOAD_OFFSET:UF2_PAYLOAD_OFFSET + payload_size])
    return bytes(out)


def find_parttool():
    idf_path = os.environ.get("IDF_PATH")
    candidates = []
    if idf_path:
        candidates.append(os.path.join(idf_path, "components", "partition_table",
                                       "parttool.py"))
    candidates += [
        r"D:\esp\.espressif\v6.0.1\esp-idf\components\partition_table\parttool.py",
        r"C:\Espressif\frameworks\esp-idf-v6.0.1\components\partition_table\parttool.py",
        r"C:\Espressif\frameworks\esp-idf-v5.4\components\partition_table\parttool.py",
    ]
    for p in candidates:
        if os.path.isfile(p):
            return p
    return None


def find_idf_python():
    """Find the Python interpreter from the ESP-IDF venv (which has esptool
    installed). parttool.py internally shells out to `python -m esptool`, so
    if we run it with the system Python that lacks esptool the call fails.
    The IDF venv has everything pre-installed."""
    # IDF_PYTHON_ENV_PATH is set when an ESP-IDF environment is activated.
    venv_root = os.environ.get("IDF_PYTHON_ENV_PATH")
    if venv_root:
        candidate = os.path.join(venv_root, "Scripts", "python.exe")
        if os.path.isfile(candidate):
            return candidate

    # Default Espressif Windows installer locations.
    for candidate in [
        r"C:\Espressif\tools\python\v6.0.1\venv\Scripts\python.exe",
        r"C:\Espressif\tools\python\v5.4\venv\Scripts\python.exe",
        r"C:\Espressif\python_env\idf6.0_py3.11_env\Scripts\python.exe",
        r"C:\Espressif\python_env\idf5.4_py3.11_env\Scripts\python.exe",
    ]:
        if os.path.isfile(candidate):
            return candidate

    return None


def flash_bytes(tdld_bytes, port, source_label):
    if tdld_bytes[:4] != b"TDLD":
        print(f"WARN: {source_label} bytes don't start with 'TDLD' magic "
              f"(first 4 bytes = {tdld_bytes[:4].hex()})", file=sys.stderr)

    # parttool wants a file - drop into the script's folder so we don't pollute
    # whatever directory the user is in.
    tmp_path = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                            "_extracted.tdld")
    with open(tmp_path, "wb") as f:
        f.write(tdld_bytes)

    parttool = find_parttool()
    if not parttool:
        print("ERROR: could not locate parttool.py. Run from an ESP-IDF "
              "terminal or set IDF_PATH.", file=sys.stderr)
        return 2

    idf_python = find_idf_python()
    if not idf_python:
        print("ERROR: could not locate the ESP-IDF venv Python (which has "
              "esptool installed). Either run this script from an ESP-IDF "
              "terminal, set IDF_PYTHON_ENV_PATH, or install esptool in "
              "your current Python:  pip install esptool", file=sys.stderr)
        return 2

    cmd = [idf_python, parttool, "--port", port, "write_partition",
           "--partition-name", "tdld", "--input", tmp_path]
    print("Running:", " ".join(cmd))
    return subprocess.call(cmd)


def main():
    p = argparse.ArgumentParser(
        description=__doc__,
        formatter_class=argparse.RawDescriptionHelpFormatter,
    )
    p.add_argument("input", help="Path to .uf2 or .tdld file from the UI")
    p.add_argument("--port", required=True, help="Serial port (e.g. COM3)")
    p.add_argument("--save-tdld", metavar="PATH",
                   help="When extracting from UF2, also save the recovered "
                        "TDLD bytes to PATH for inspection")
    args = p.parse_args()

    if not os.path.isfile(args.input):
        p.error(f"file not found: {args.input}")

    ext = os.path.splitext(args.input)[1].lower()
    if ext == ".uf2":
        print(f"Extracting TDLD from {args.input} ...")
        tdld_bytes = extract_tdld_from_uf2(args.input)
        print(f"Extracted {len(tdld_bytes)} bytes "
              f"(header + opcode stream + trailing zero padding)")
        if args.save_tdld:
            with open(args.save_tdld, "wb") as f:
                f.write(tdld_bytes)
            print(f"Wrote {args.save_tdld}")
    elif ext in (".tdld", ".bin"):
        with open(args.input, "rb") as f:
            tdld_bytes = f.read()
        print(f"Read {len(tdld_bytes)} bytes from {args.input}")
    else:
        p.error(f"unsupported extension {ext}; expected .uf2 or .tdld")

    return flash_bytes(tdld_bytes, args.port, args.input)


if __name__ == "__main__":
    sys.exit(main())
