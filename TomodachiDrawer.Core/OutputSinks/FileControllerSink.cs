using System.Text;

using TomodachiDrawer.Core.Interfaces;

namespace TomodachiDrawer.Core.OutputSinks
{
    public class FileControllerSink : ISwitchOutput
    {
        private const byte OpcodeDelayResolutionMs = 1;

        private const double DefaultHoldDuration = 25.0;
        private const double DefaultReleaseDuration = 25.0;

        // opcodes, packed into high nibble.
        // max of 16 courtesy of that.
        public static class Opcode
        {
            public const byte Invalid = 0x0; // 0x00 is invalid, mostly to make hex look nicer, can be used later if needed.
            public const byte PressButton = 0x1; // low nibble = Button value (0-13)
            public const byte ReleaseButton = 0x2; // low nibble = Button value ^^^^^^
            public const byte PressDPad = 0x3; // low nibble = DPad value (0-7)
            public const byte ReleaseDPad = 0x4; // low nibble = DPad value ^^^^^
            public const byte ReleaseAll = 0x5; // low nibble ignored (0)
            public const byte Delay = 0x6; // 2-byte record: low nibble of first byte = high 4 bits of unit count,

            // second byte = low 8 bits. 12-bit total milliseconds (see opcodedelayresolutionms)
            public const byte SetStick = 0x7; // 2-byte record: low nibble = Stick axis, second byte = value
            public const byte TapButton = 0x8; // low nibble = Button value; implies 25ms hold + 25ms release
            public const byte TapDPad = 0x9; // low nibble = DPad value;   implies 25ms hold + 25ms release

            // compression opcodes, simply repeats the last record N times.
            // For now, this is only done on single byte records, not ones like delay or SetStick but those are compartively
            // rare in practice.
            public const byte RepeatLast1 = 0xE; // Run Length Encoding. 4 bits for repeat count. Max 15 repeats.
            public const byte RepeatLast2 = 0xF; // Run Length Encoding. 12 bits for repeat count. Max 4095 repeats.
        }

        private readonly BinaryWriter _writer;

        // Run Length Encoding tracking for RepeatLast1/RepeatLast2 opcodes.
        private byte? _lastSingleByteRecord;
        private int _pendingRepeats;
        private const int MaxRleCount = 0xFFF; // 4095. Have to flsuh. This should realistically never be hit.

        public FileControllerSink(string filePath)
        {
            _writer = new BinaryWriter(File.Open(filePath, FileMode.Create));

            _writer.Write(Encoding.ASCII.GetBytes("TDLD")); // magic, because why not
            _writer.Write((byte)3); // version
            _writer.Write((byte)0); // padding, keeps header 6 bytes
        }

        public void Press(Button btn) => WriteNibbleRecord(Opcode.PressButton, (byte)btn);

        public void Release(Button btn) => WriteNibbleRecord(Opcode.ReleaseButton, (byte)btn);

        public void Press(DPad dir) => WriteNibbleRecord(Opcode.PressDPad, (byte)dir);

        public void Release(DPad dir) => WriteNibbleRecord(Opcode.ReleaseDPad, (byte)dir);

        public void ReleaseAll() => WriteNibbleRecord(Opcode.ReleaseAll, 0x0);

        public void SetStick(Stick stick, byte value) =>
            Write2ByteRecord((byte)((Opcode.SetStick << 4) | (byte)stick), value);

        public void Delay(double milliseconds)
        {
            int units = (int)Math.Round(milliseconds / OpcodeDelayResolutionMs);
            // max 0xFFF (4095) units per record = ~4s at 1ms resolution; loop for larger delays
            while (units > 0)
            {
                int chunk = Math.Min(units, 0xFFF);
                byte opcodeAndHighNibble = (byte)((Opcode.Delay << 4) | (chunk >> 8));
                byte lower = (byte)(chunk & 0xFF);
                Write2ByteRecord(opcodeAndHighNibble, lower);

                units -= chunk;
            }
        }

        // To avoid 4 records (press+delay+release+delay)
        // 99.99% of the time, we have a dedicated opcode for normal 25ms taps so its 1 byte.

        void ISwitchOutput.Tap(Button btn, double holdDuration, double releaseDuration)
        {
            if (holdDuration == DefaultHoldDuration && releaseDuration == DefaultReleaseDuration)
            {
                WriteNibbleRecord(Opcode.TapButton, (byte)btn);
                return;
            }

            Press(btn);
            Delay(holdDuration);
            Release(btn);
            Delay(releaseDuration);
        }

        void ISwitchOutput.Tap(DPad dir, double holdDuration, double releaseDuration)
        {
            if (holdDuration == DefaultHoldDuration && releaseDuration == DefaultReleaseDuration)
            {
                WriteNibbleRecord(Opcode.TapDPad, (byte)dir);
                return;
            }

            Press(dir);
            Delay(holdDuration);
            Release(dir);
            Delay(releaseDuration);
        }

        private void Write2ByteRecord(byte opcode, byte value)
        {
            // No RLE support for 2 byte for simplicity sake for now
            FlushRle();
            _writer.Write(opcode);
            _writer.Write(value);
        }

        private void WriteNibbleRecord(byte opcode, byte value)
        {
            byte record = (byte)((opcode << 4) | (value & 0xF));
            if (_lastSingleByteRecord == record)
            {
                _pendingRepeats++;
                if (_pendingRepeats > MaxRleCount)
                {
                    _pendingRepeats--; // errrr
                    FlushRle();
                    _writer.Write(record);
                    _lastSingleByteRecord = record;
                    _pendingRepeats = 0;
                }
            }
            else
            {
                FlushRle();
                _writer.Write(record);
                _lastSingleByteRecord = record;
                _pendingRepeats = 0;
            }
        }

        private void FlushRle()
        {
            if (_pendingRepeats > 0)
            {
                // Writing a repeat for one 1 byte record is kinda just silly
                // so just write it as normal instead.
                if (_pendingRepeats == 1)
                {
                    if (_lastSingleByteRecord is byte last)
                    {
                        _writer.Write(last);
                    }
                }
                else if (_pendingRepeats <= 0xF)
                {
                    // 1 byte repeat record
                    byte record = (byte)(Opcode.RepeatLast1 << 4 | (byte)_pendingRepeats);
                    _writer.Write(record);
                    Console.WriteLine(
                        $"RepeatLast1: Repeating opcode 0x{_lastSingleByteRecord:X2} {_pendingRepeats} times"
                    );
                }
                else
                {
                    // 2 byte repeat record
                    byte opcodeAndHighNibble = (byte)(
                        Opcode.RepeatLast2 << 4 | (_pendingRepeats >> 8)
                    );
                    byte lower = (byte)(_pendingRepeats & 0xFF);
                    _writer.Write([opcodeAndHighNibble, lower]);
                    Console.WriteLine(
                        $"RepeatLast2: Repeating opcode 0x{_lastSingleByteRecord:X2} {_pendingRepeats} times"
                    );
                }

                _lastSingleByteRecord = null;
                _pendingRepeats = 0;
            }
        }

        public void Dispose()
        {
            FlushRle();
            // we mark the end of the file for convenience in the flash reading logic on the RP2040 with the invalid opcode.
            _writer.Write((byte)(Opcode.Invalid << 4)); // this is just 0x00 but yknow.
            _writer.Dispose();
        }
    }
}
