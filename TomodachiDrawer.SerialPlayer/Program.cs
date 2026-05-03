using System.Text;
using TomodachiDrawer.Core.OutputSinks;

// !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
// SLOP DANGER ZONE
// Since this is just for debugging I deferred to the dark lord, partially.
// This should not be used for production by any sane human beings, for that and other reasons.
// !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

namespace TomodachiDrawer.SerialPlayer
{
    internal class Program
    {
        // can prob just read off of .Core's FileControllerSink but meh this is just
        // a test console app thing anyway.
        private const double TapHoldMs = 25.0;
        private const double TapReleaseMs = 25.0;
        private const double DelayResMs = 1.0;

        private static SwitchController? controller;

        static void Main(string[] args)
        {
            Console.WriteLine("TomodachiDrawer.SerialPlayer");
            Console.WriteLine(
                "If you are running this and your name is not Lucas, then you are most certainly running the wrong thing."
            );

            if (args.Length > 0 && args[0] == "counthelper")
            {
                // write out the enums available
                // Buttons and DPad
                foreach (var button in Enum.GetValues(typeof(Button)))
                {
                    Console.WriteLine($"Button: {button} = {(int)button}");
                }
                foreach (var dpad in Enum.GetValues(typeof(DPad)))
                {
                    Console.WriteLine($"DPad: {dpad} = {(int)dpad}");
                }

                Console.WriteLine("Select input to repeat...");
                var input = Console.ReadLine();
                // find what it matches, we are taking in the NAME
                if (input != null)
                {
                    // See if its a Dpad or a Button
                    DPad? dpadButton = null;
                    Button? faceButton = null;

                    foreach (var button in Enum.GetValues(typeof(Button)))
                    {
                        if (button.ToString() == input)
                        {
                            faceButton = (Button)button;
                            break;
                        }
                    }
                    foreach (var dpad in Enum.GetValues(typeof(DPad)))
                    {
                        if (dpad.ToString() == input)
                        {
                            dpadButton = (DPad)dpad;
                            break;
                        }
                    }
                    var controller = new SwitchController();
                    if (!controller.Connect())
                    {
                        Console.WriteLine("Failed to connect to controller. Exiting.");
                        return;
                    }

                    using (controller)
                    {
                        int count = 0;
                        Console.WriteLine("Press Enter to send one tap. Type a number and Enter to send that many. Type 'q' to quit.");

                        while (true)
                        {
                            Console.Write($"[{count} taps sent] > ");
                            var line = Console.ReadLine()?.Trim();

                            if (line == "q" || line == "quit")
                            {
                                Console.WriteLine($"Done. Total taps sent: {count}");
                                break;
                            }

                            int taps = 1;
                            if (!string.IsNullOrEmpty(line))
                                int.TryParse(line, out taps);

                            for (int i = 0; i < taps; i++)
                            {
                                if (dpadButton.HasValue)
                                    controller.Tap(dpadButton.Value, TapHoldMs, TapReleaseMs);
                                else if (faceButton.HasValue)
                                    controller.Tap(faceButton.Value, TapHoldMs, TapReleaseMs);
                            }

                            count += taps;
                        }
                    }

                    return;
                }
            }

            var inputFilePath = args.Length > 0 ? args[0] : "test.tdld";
            if (!File.Exists(inputFilePath))
            {
                Console.WriteLine($"{Path.GetFileName(inputFilePath)} does not exist. Exiting");
                return;
            }

            // Load into memory first to avoid I/O jitter during playback.
            var bytes = File.ReadAllBytes(inputFilePath);
            using var reader = new BinaryReader(new MemoryStream(bytes));

            var magic = reader.ReadBytes(4);
            if (Encoding.ASCII.GetString(magic) != "TDLD")
            {
                Console.WriteLine("Invalid file format. Exiting.");
                return;
            }

            byte version = reader.ReadByte();
            reader.ReadByte(); // padding

            if (version == 2)
            {
                Play(reader);
            }
            else
            {
                Console.WriteLine($"Unsupported file version {version}. Exiting.");
            }
        }

        static void Play(BinaryReader reader)
        {
            controller = ConnectController();
            if (controller is null)
                return;

            using (controller)
            {
                byte? lastSingleByteRecord = null;

                while (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    byte raw = reader.ReadByte();
                    byte opcode = (byte)(raw >> 4);
                    byte nibble = (byte)(raw & 0xF);

                    switch (opcode)
                    {
                        case FileControllerSink.Opcode.Invalid:
                            Console.WriteLine("Hit Invalid opcode, end of playback.");
                            return;
                        case FileControllerSink.Opcode.Delay:
                            // 2-byte 12-bit record: nibble = high 4 bits, next byte = low 8 bits
                            int delayUnits = (nibble << 8) | reader.ReadByte();
                            controller.PreciseDelay(delayUnits * DelayResMs);
                            lastSingleByteRecord = null; // reset, we should never have a repeat after a delay.
                            break;

                        case FileControllerSink.Opcode.SetStick:
                            // 2-byte record: nibble = Stick axis, next byte = value
                            byte axisValue = reader.ReadByte();
                            controller.SetStick((Stick)nibble, axisValue);
                            lastSingleByteRecord = null; // see above
                            break;

                        case FileControllerSink.Opcode.RepeatLast1:
                        {
                            if (lastSingleByteRecord is null)
                                throw new Exception("repeatlast1 hit with no prior opcodes..?");

                            int repeats = nibble;
#if DEBUG
                            Console.WriteLine(
                                "RepeatLast1: Repeating opcode 0x{0:X2} {1} times",
                                lastSingleByteRecord.Value,
                                repeats
                            );
#endif
                            for (int i = 0; i < repeats; i++)
                                RunSingleByteOpcode(lastSingleByteRecord.Value);

                            break;
                        }

                        case FileControllerSink.Opcode.RepeatLast2:
                        {
                            if (lastSingleByteRecord is null)
                                throw new Exception("repeatlast2 hit with no prior opcodes..?");

                            // RepeatLast2 has 2 bytes, or well, 4 bits and 8 bits for 12 bits of repeat count.
                            int repeats = (nibble << 8) | reader.ReadByte();
#if DEBUG
                            Console.WriteLine(
                                "RepeatLast2: Repeating opcode 0x{0:X2} {1} times",
                                lastSingleByteRecord.Value,
                                repeats
                            );
#endif

                            for (int i = 0; i < repeats; i++)
                                RunSingleByteOpcode(lastSingleByteRecord.Value);

                            break;
                        }

                        default:
                            RunSingleByteOpcode(raw);
                            lastSingleByteRecord = raw;
                            break;
                    }
                }
            }
        }

        // Moved to seperate method for RLE.
        static void RunSingleByteOpcode(byte raw)
        {
            byte opcode = (byte)(raw >> 4);
            byte nibble = (byte)(raw & 0xF);

            if (controller == null)
                throw new Exception("Conroller is null..?");

            switch (opcode)
            {
                case FileControllerSink.Opcode.PressButton:
                    controller.Press((Button)nibble);
                    break;
                case FileControllerSink.Opcode.ReleaseButton:
                    controller.Release((Button)nibble);
                    break;
                case FileControllerSink.Opcode.PressDPad:
                    controller.Press((DPad)nibble);
                    break;
                case FileControllerSink.Opcode.ReleaseDPad:
                    controller.Release((DPad)nibble);
                    break;
                case FileControllerSink.Opcode.ReleaseAll:
                    controller.ReleaseAll();
                    break;
                case FileControllerSink.Opcode.TapButton:
                    controller.Tap((Button)nibble, TapHoldMs, TapReleaseMs);
                    break;
                case FileControllerSink.Opcode.TapDPad:
                    controller.Tap((DPad)nibble, TapHoldMs, TapReleaseMs);
                    break;
                default:
                    throw new Exception($"Unknown opcode 0x{opcode:X1}");
            }
        }

        static SwitchController? ConnectController()
        {
            var controller = new SwitchController();
            if (!controller.Connect())
            {
                Console.WriteLine("Error during serial connection.");
                controller.Dispose();
                return null;
            }
            return controller;
        }
    }
}
