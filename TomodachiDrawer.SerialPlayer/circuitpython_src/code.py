import board
import busio
import usb_hid
import neopixel

pixel = neopixel.NeoPixel(board.NEOPIXEL, 1)
pixel.brightness = 0.2
# 115200, 230400, 460800
uart = busio.UART(board.GP0, board.GP1, baudrate=230400, timeout=0.01)

gp = usb_hid.devices[0]

# Mapping
BUTTONS = {"Y": (0, 0x01), "B": (0, 0x02), "A": (0, 0x04), "X": (0, 0x08),
           "L": (0, 0x10), "R": (0, 0x20), "ZL": (0, 0x40), "ZR": (0, 0x80),
           "MINUS": (1, 0x01), "PLUS": (1, 0x02), "LCLICK": (1, 0x04), "RCLICK": (1, 0x08),
           "HOME": (1, 0x10), "CAPTURE": (1, 0x20)}

DPAD = {"UP": 0, "UPRIGHT": 1, "RIGHT": 2, "DOWNRIGHT": 3, "DOWN": 4, "DOWNLEFT": 5, "LEFT": 6, "UPLEFT": 7}

# 128 is center for all sticks. Report: [Btn1, Btn2, DPad, LX, LY, RX, RY, Padding]
current_report = bytearray([0x00, 0x00, 0x08, 128, 128, 128, 128, 0x00])

while True:
    if uart.in_waiting > 0:
        line = uart.readline()
        if line:
            try:
                cmd_string = line.decode('utf-8').strip().upper()
                parts = cmd_string.split(" ")
                action = parts[0]
                
                if action == "PING":
                    uart.write(b"PONG\n")
                elif action == "RELEASE" and parts[1] == "ALL":
                    current_report = bytearray([0x00, 0x00, 0x08, 128, 128, 128, 128, 0x00])
                elif action == "STICK":
                    axis, val = parts[1], int(parts[2])
                    if axis == "LX": current_report[3] = val
                    elif axis == "LY": current_report[4] = val
                    elif axis == "RX": current_report[5] = val
                    elif axis == "RY": current_report[6] = val
                elif action == "PRESS":
                    target = parts[1]
                    if target in BUTTONS:
                        idx, mask = BUTTONS[target]; current_report[idx] |= mask
                    elif target in DPAD: current_report[2] = DPAD[target]
                elif action == "RELEASE":
                    target = parts[1]
                    if target in BUTTONS:
                        idx, mask = BUTTONS[target]; current_report[idx] &= ~mask
                    elif target in DPAD: current_report[2] = 0x08

                gp.send_report(current_report)
                pixel[0] = (0, 255, 0) if any(current_report[0:2]) else (10, 10, 10)
            except: pass