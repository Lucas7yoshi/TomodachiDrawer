using System;
using System.Collections.Generic;
using System.Text;

using System.Buffers.Binary;

namespace TomodachiDrawer.UI.Windows
{
    internal class UF2Flasher
    {
        public static byte[] BuildTDLDUF2(byte[] tdldData)
        {
            const uint TargetBase = 0x10100000u;
            const uint FamilyId = 0xE48BFF56u; // RP2040.
            const uint PayloadSize = 256u;

            int blockCount = (tdldData.Length + (int)PayloadSize - 1) / (int)PayloadSize;
            byte[] output = new byte[blockCount * 512];

            for (int i = 0; i < blockCount; i++)
            {
                var block = output.AsSpan(i * 512, 512);

                BinaryPrimitives.WriteUInt32LittleEndian(block[0x000..], 0x0A324655); // Magic 1
                BinaryPrimitives.WriteUInt32LittleEndian(block[0x004..], 0x9E5D5157); // Magic 2
                BinaryPrimitives.WriteUInt32LittleEndian(block[0x008..], 0x00002000); // Flags
                BinaryPrimitives.WriteUInt32LittleEndian(block[0x00C..], TargetBase + (uint)(i * PayloadSize)); // Target addr
                BinaryPrimitives.WriteUInt32LittleEndian(block[0x010..], PayloadSize);
                BinaryPrimitives.WriteUInt32LittleEndian(block[0x014..], (uint)i);          // Block number
                BinaryPrimitives.WriteUInt32LittleEndian(block[0x018..], (uint)blockCount); // Total blocks
                BinaryPrimitives.WriteUInt32LittleEndian(block[0x01C..], FamilyId);
                BinaryPrimitives.WriteUInt32LittleEndian(block[0x1FC..], 0x0AB16F30); // Final magic

                // Actual payload
                int srcOffset = i * (int)PayloadSize;
                int copyLen = Math.Min((int)PayloadSize, tdldData.Length - srcOffset);
                tdldData.AsSpan(srcOffset, copyLen).CopyTo(block[0x020..]);
            }

            return output;
        }

        public static string? FindRP2040Drive()
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady && drive.VolumeLabel == "RPI-RP2")
                    return drive.RootDirectory.FullName;
            }
            return null;
        }
    }
}
