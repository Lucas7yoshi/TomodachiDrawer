using System.Buffers.Binary;

namespace TomodachiDrawer.UI.Avalonia;

internal static class UF2Flasher
{
    public static byte[] BuildTDLDUF2(byte[] tdldData)
    {
        const uint TargetBase = 0x10100000u;
        const uint FamilyId = 0xE48BFF56u;
        const uint PayloadSize = 256u;

        int blockCount = (tdldData.Length + (int)PayloadSize - 1) / (int)PayloadSize;
        byte[] output = new byte[blockCount * 512];

        for (int i = 0; i < blockCount; i++)
        {
            var block = output.AsSpan(i * 512, 512);
            BinaryPrimitives.WriteUInt32LittleEndian(block[0x000..], 0x0A324655);
            BinaryPrimitives.WriteUInt32LittleEndian(block[0x004..], 0x9E5D5157);
            BinaryPrimitives.WriteUInt32LittleEndian(block[0x008..], 0x00002000);
            BinaryPrimitives.WriteUInt32LittleEndian(block[0x00C..], TargetBase + (uint)(i * PayloadSize));
            BinaryPrimitives.WriteUInt32LittleEndian(block[0x010..], PayloadSize);
            BinaryPrimitives.WriteUInt32LittleEndian(block[0x014..], (uint)i);
            BinaryPrimitives.WriteUInt32LittleEndian(block[0x018..], (uint)blockCount);
            BinaryPrimitives.WriteUInt32LittleEndian(block[0x01C..], FamilyId);
            BinaryPrimitives.WriteUInt32LittleEndian(block[0x1FC..], 0x0AB16F30);

            int srcOffset = i * (int)PayloadSize;
            int copyLen = Math.Min((int)PayloadSize, tdldData.Length - srcOffset);
            tdldData.AsSpan(srcOffset, copyLen).CopyTo(block[0x020..]);
        }

        return output;
    }

    public static string? FindRP2040Drive()
    {
        // Primary: DriveInfo works on all platforms (.NET reads volume labels cross-platform)
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.IsReady && drive.VolumeLabel == "RPI-RP2")
                    return drive.RootDirectory.FullName;
            }
            catch { }
        }

        // Fallback for Linux where volume labels may not surface through DriveInfo
        if (OperatingSystem.IsLinux())
        {
            foreach (var baseDir in new[] { "/media", "/run/media" })
            {
                if (!Directory.Exists(baseDir)) continue;
                foreach (var userDir in Directory.GetDirectories(baseDir))
                {
                    var candidate = Path.Combine(userDir, "RPI-RP2");
                    if (Directory.Exists(candidate))
                        return candidate + Path.DirectorySeparatorChar;
                }
            }
        }
        // Fallback for macOS
        else if (OperatingSystem.IsMacOS())
        {
            var candidate = "/Volumes/RPI-RP2";
            if (Directory.Exists(candidate))
                return candidate + "/";
        }

        return null;
    }
}
