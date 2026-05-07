using System.Buffers.Binary;

namespace TomodachiDrawer.UI.Avalonia;

internal static class UF2Flasher
{
    public enum BoardType
    {
        Unknown,
        RP2040,
        RP2350,
    }

    private const uint FamilyIdRP2040 = 0xE48BFF56u;
    private const uint FamilyIdRP2350 = 0xE48BFF5Au; // ARM core

    // This is repeated code so i would like to make it shared in .Core later.
    public static byte[] BuildTDLDUF2(byte[] tdldData, BoardType boardType)
    {
        const int MaxTDLDSize = 1 * 1024 * 1024;
        if (tdldData.Length > MaxTDLDSize)
            throw new ArgumentException(
                $"TDLD data exceeds maximum size of {MaxTDLDSize} bytes. This will shoot past the end of the board's flash!"
            );
        const uint TargetBase = 0x10100000u; // 1MB into the 2MB flash, so 1MB limit.
        uint familyId = boardType == BoardType.RP2350 ? FamilyIdRP2350 : FamilyIdRP2040;
        const uint PayloadSize = 256u;

        int blockCount = (tdldData.Length + (int)PayloadSize - 1) / (int)PayloadSize;
        byte[] output = new byte[blockCount * 512];

        for (int i = 0; i < blockCount; i++)
        {
            var block = output.AsSpan(i * 512, 512);
            BinaryPrimitives.WriteUInt32LittleEndian(block[0x000..], 0x0A324655);
            BinaryPrimitives.WriteUInt32LittleEndian(block[0x004..], 0x9E5D5157);
            BinaryPrimitives.WriteUInt32LittleEndian(block[0x008..], 0x00002000);
            BinaryPrimitives.WriteUInt32LittleEndian(
                block[0x00C..],
                TargetBase + (uint)(i * PayloadSize)
            );
            BinaryPrimitives.WriteUInt32LittleEndian(block[0x010..], PayloadSize);
            BinaryPrimitives.WriteUInt32LittleEndian(block[0x014..], (uint)i);
            BinaryPrimitives.WriteUInt32LittleEndian(block[0x018..], (uint)blockCount);
            BinaryPrimitives.WriteUInt32LittleEndian(block[0x01C..], familyId);
            BinaryPrimitives.WriteUInt32LittleEndian(block[0x1FC..], 0x0AB16F30);

            int srcOffset = i * (int)PayloadSize;
            int copyLen = Math.Min((int)PayloadSize, tdldData.Length - srcOffset);
            tdldData.AsSpan(srcOffset, copyLen).CopyTo(block[0x020..]);
        }

        return output;
    }

    public static BoardType DetectBoardType(string drivePath)
    {
        var infoFilePath = Path.Combine(drivePath, "INFO_UF2.TXT");

        if (File.Exists(infoFilePath))
        {
            var content = File.ReadAllText(infoFilePath);
            if (content.Contains("RP2350", StringComparison.OrdinalIgnoreCase))
                return BoardType.RP2350;
            else if (content.Contains("RPI-RP2", StringComparison.OrdinalIgnoreCase))
                return BoardType.RP2040;
        }

        return BoardType.Unknown;
    }

    public static string? FindBoardDrive()
    {
        // this should work crossplatform...
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.IsReady && drive.VolumeLabel == "RPI-RP2" || drive.VolumeLabel == "RP2350")
                    return drive.RootDirectory.FullName;
            }
            catch { }
        }

        // Fallback for Linux where volume labels may not surface through DriveInfo
        if (OperatingSystem.IsLinux())
        {
            foreach (var baseDir in new[] { "/media", "/run/media" })
            {
                if (!Directory.Exists(baseDir))
                    continue;
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
                return candidate.EndsWith(Path.DirectorySeparatorChar)
                    ? candidate
                    : candidate + Path.DirectorySeparatorChar;
        }

        return null;
    }
}
