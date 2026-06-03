using System.Diagnostics;
using System.IO.Ports;
using System.Text;

namespace TomodachiDrawer.UI.Avalonia;

// Detection + flashing for the ESP32-S3 target. Shells out to a bundled
// esptool (or one on PATH) for chip-id probing and write-flash.
internal static class ESP32S3Flasher
{
    // tdld partition offset = factory app size (2MB) + bootloader/nvs/phy
    // headers (0x10000). Must match partitions.csv on the firmware side.
    public const uint TdldPartitionOffset = 0x210000;
    public const int TdldMaxBytes = 1 * 1024 * 1024;  // 1MB partition

    public record DetectedBoard(string Port, string ChipFamily, string? ChipRevision);

    // esptool lives in EspTools/ next to the app binary in shipped releases.
    // Falls back to PATH so devs with ESP-IDF installed don't need to copy it.
    public static string? FindEsptool()
    {
        string exeName = OperatingSystem.IsWindows() ? "esptool.exe" : "esptool";
        var bundled = Path.Combine(AppContext.BaseDirectory, "EspTools", exeName);
        if (File.Exists(bundled))
            return bundled;

        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (pathEnv != null)
        {
            foreach (var dir in pathEnv.Split(Path.PathSeparator))
            {
                try
                {
                    var candidate = Path.Combine(dir, exeName);
                    if (File.Exists(candidate))
                        return candidate;
                }
                catch { }
            }
        }
        return null;
    }

    // Probe a single COM port. Invasive - esptool resets the chip on connect,
    // so this kicks any running firmware. Call only when about to flash, or
    // once on user-triggered Re-scan.
    public static async Task<DetectedBoard?> ProbePortAsync(
        string port,
        string esptoolPath,
        TimeSpan? timeout = null,
        Action<string>? log = null
    )
    {
        var to = timeout ?? TimeSpan.FromSeconds(10);
        var psi = new ProcessStartInfo
        {
            FileName = esptoolPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("--port");
        psi.ArgumentList.Add(port);
        psi.ArgumentList.Add("chip-id");

        try
        {
            using var proc = Process.Start(psi);
            if (proc == null) return null;
            var stdout = new StringBuilder();
            proc.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
            proc.ErrorDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            var completed = await Task.Run(() => proc.WaitForExit((int)to.TotalMilliseconds));
            if (!completed)
            {
                try { proc.Kill(entireProcessTree: true); } catch { }
                log?.Invoke($"esptool chip-id timed out on {port}");
                return null;
            }
            if (proc.ExitCode != 0)
                return null;  // port busy, no chip, or not an Espressif chip

            string? chip = null;
            string? revision = null;
            foreach (var line in stdout.ToString().Split('\n'))
            {
                var trimmed = line.Trim();
                if (chip == null && trimmed.StartsWith("Detecting chip type"))
                {
                    var idx = trimmed.IndexOf("...");
                    if (idx >= 0)
                        chip = trimmed[(idx + 3)..].Trim();
                }
                if (revision == null && trimmed.StartsWith("Chip is "))
                {
                    var revIdx = trimmed.IndexOf("revision ");
                    if (revIdx >= 0)
                    {
                        var rest = trimmed[(revIdx + "revision ".Length)..];
                        var endParen = rest.IndexOf(')');
                        revision = endParen >= 0 ? rest[..endParen] : rest;
                    }
                }
            }
            return chip == null ? null : new DetectedBoard(port, chip, revision);
        }
        catch (Exception ex)
        {
            log?.Invoke($"esptool probe of {port} failed: {ex.Message}");
            return null;
        }
    }

    // Cheap "is this port still around?" check used by the polling loop to
    // notice disconnects without re-probing.
    public static bool IsPortStillPresent(string port)
    {
        try
        {
            foreach (var p in SerialPort.GetPortNames())
            {
                if (string.Equals(p, port, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch { }
        return false;
    }

    public static string[] EnumeratePorts()
    {
        try { return SerialPort.GetPortNames(); }
        catch { return Array.Empty<string>(); }
    }

    // Base-firmware files we expect under EspFirmware/. The flash offsets
    // match what `idf.py flash` would write: bootloader at 0x0, partition
    // table at 0x8000, app at 0x10000 (= factory partition start in
    // partitions.csv on the firmware side).
    public record FirmwareLayout(string BootloaderPath, string PartitionTablePath, string AppPath)
    {
        public const uint BootloaderOffset = 0x0;
        public const uint PartitionTableOffset = 0x8000;
        public const uint AppOffset = 0x10000;
    }

    // esp_app_desc_t lives 32 bytes into the app partition (after the standard
    // esp_image_header_t + esp_image_segment_header_t).
    public const uint AppDescriptorOffset = FirmwareLayout.AppOffset + 0x20;
    public const uint AppDescriptorMagic = 0xABCD5432;

    public record AppDescriptor(string ProjectName, string Version, string IdfVersion);

    // Read esp_app_desc_t from the app partition. Returns null if no recognized
    // firmware (magic word mismatch - e.g. fresh chip with 0xFF flash, or some
    // other binary entirely).
    public static async Task<AppDescriptor?> ReadAppDescriptorAsync(
        DetectedBoard board,
        string esptoolPath
    )
    {
        string tmpPath = Path.Combine(
            Path.GetTempPath(),
            $"app_desc_{System.Random.Shared.Next(1000000, 9999999)}.bin"
        );
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = esptoolPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("--chip");
            psi.ArgumentList.Add("esp32s3");
            psi.ArgumentList.Add("--port");
            psi.ArgumentList.Add(board.Port);
            psi.ArgumentList.Add("read-flash");
            psi.ArgumentList.Add($"0x{AppDescriptorOffset:X}");
            psi.ArgumentList.Add("256");
            psi.ArgumentList.Add(tmpPath);

            using var proc = Process.Start(psi);
            if (proc == null) return null;
            // discard esptool's chatter for this internal read
            proc.OutputDataReceived += (_, _) => { };
            proc.ErrorDataReceived += (_, _) => { };
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            await proc.WaitForExitAsync();
            if (proc.ExitCode != 0) return null;

            var bytes = await File.ReadAllBytesAsync(tmpPath);
            if (bytes.Length < 144) return null;
            var magic = BitConverter.ToUInt32(bytes, 0);
            if (magic != AppDescriptorMagic) return null;

            // Field offsets in esp_app_desc_t (see esp_app_format.h):
            //   u32 magic_word     0
            //   u32 secure_version 4
            //   u32 reserv1[2]     8
            //   char version[32]   16
            //   char project_name[32] 48
            //   char time[16]      80
            //   char date[16]      96
            //   char idf_ver[32]   112
            return new AppDescriptor(
                ProjectName: ReadCString(bytes, 48, 32),
                Version: ReadCString(bytes, 16, 32),
                IdfVersion: ReadCString(bytes, 112, 32)
            );
        }
        catch { return null; }
        finally
        {
            try { if (File.Exists(tmpPath)) File.Delete(tmpPath); } catch { }
        }
    }

    static string ReadCString(byte[] buf, int offset, int maxLen)
    {
        int end = offset;
        int limit = Math.Min(buf.Length, offset + maxLen);
        while (end < limit && buf[end] != 0) end++;
        return Encoding.UTF8.GetString(buf, offset, end - offset);
    }

    // Supported board variants. Each maps to a sdkconfig.defaults.<Id> file in
    // the firmware project and a TomodachiDrawer_FW_ESP32S3-<Id>.bin produced
    // by the build-esp-firmware CI matrix. The bundled firmware bins only differ
    // per board for the onboard WS2812 LED GPIO - the HID gamepad behavior is
    // identical. To add a board: extend main/Kconfig.projbuild, add a
    // sdkconfig.defaults.<id>, add the id to the workflow matrix, and append
    // an entry here.
    public record BoardInfo(string Id, string DisplayName, int LedGpio)
    {
        // ComboBox renders items via ToString() by default; the auto-record
        // ToString prints all fields which would look ugly in the dropdown.
        public override string ToString() => DisplayName;
    }

    public static readonly IReadOnlyList<BoardInfo> SupportedBoards = new[]
    {
        new BoardInfo("devkitc_1_r38",  "ESP32-S3-DevKitC-1 (older rev, LED on GPIO 38)", 38),
        new BoardInfo("devkitc_1_r48",  "ESP32-S3-DevKitC-1 (later rev, LED on GPIO 48)", 48),
        new BoardInfo("s3_zero",        "Waveshare ESP32-S3-Zero (LED on GPIO 21)",        21),
        new BoardInfo("devkitm_1",      "ESP32-S3-DevKitM-1 (LED on GPIO 48)",             48),
        new BoardInfo("qtpy_s3",        "Adafruit QT Py ESP32-S3 (LED on GPIO 39)",        39),
        new BoardInfo("lolin_s3_mini",  "Lolin S3 Mini (LED on GPIO 47)",                  47),
        new BoardInfo("atom_s3",        "M5Stack AtomS3 / AtomS3 Lite (LED on GPIO 35)",   35),
    };

    public const string DefaultBoardId = "devkitc_1_r38";

    public static FirmwareLayout? FindBundledFirmware(string boardId, out string? missingReason)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "EspFirmware");
        var boot = Path.Combine(dir, "bootloader.bin");
        var ptab = Path.Combine(dir, "partition-table.bin");
        var suffixedAppName = $"TomodachiDrawer_FW_ESP32S3-{boardId}.bin";
        var suffixedApp = Path.Combine(dir, suffixedAppName);

        // Local-dev fallback: `idf.py build` writes the unsuffixed name. If
        // we're running against a local build instead of a CI release, accept
        // it but warn that whichever board is picked must match what was
        // actually compiled.
        var unsuffixedApp = Path.Combine(dir, "TomodachiDrawer_FW_ESP32S3.bin");
        string? appPath = File.Exists(suffixedApp) ? suffixedApp
                       : File.Exists(unsuffixedApp) ? unsuffixedApp
                       : null;

        var missing = new List<string>();
        if (!File.Exists(boot)) missing.Add("bootloader.bin");
        if (!File.Exists(ptab)) missing.Add("partition-table.bin");
        if (appPath == null) missing.Add(suffixedAppName);
        if (missing.Count == 0)
        {
            missingReason = null;
            return new FirmwareLayout(boot, ptab, appPath!);
        }
        missingReason = $"missing in {dir}: {string.Join(", ", missing)}";
        return null;
    }

    public static FirmwareLayout? FindBundledFirmware(string boardId) =>
        FindBundledFirmware(boardId, out _);

    // Flash bootloader + partition table + app in a single esptool call.
    public static async Task<bool> FlashBaseFirmwareAsync(
        DetectedBoard board,
        FirmwareLayout firmware,
        string esptoolPath,
        Action<string>? log = null
    )
    {
        var psi = new ProcessStartInfo
        {
            FileName = esptoolPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("--chip");
        psi.ArgumentList.Add("esp32s3");
        psi.ArgumentList.Add("--port");
        psi.ArgumentList.Add(board.Port);
        psi.ArgumentList.Add("write-flash");
        psi.ArgumentList.Add($"0x{FirmwareLayout.BootloaderOffset:X}");
        psi.ArgumentList.Add(firmware.BootloaderPath);
        psi.ArgumentList.Add($"0x{FirmwareLayout.PartitionTableOffset:X}");
        psi.ArgumentList.Add(firmware.PartitionTablePath);
        psi.ArgumentList.Add($"0x{FirmwareLayout.AppOffset:X}");
        psi.ArgumentList.Add(firmware.AppPath);

        log?.Invoke($"Flashing base firmware to ESP32-S3 on {board.Port}...");
        using var proc = Process.Start(psi);
        if (proc == null)
        {
            log?.Invoke("Failed to start esptool process.");
            return false;
        }
        proc.OutputDataReceived += (_, e) => { if (e.Data != null) log?.Invoke(e.Data); };
        proc.ErrorDataReceived += (_, e) => { if (e.Data != null) log?.Invoke(e.Data); };
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        await proc.WaitForExitAsync();
        if (proc.ExitCode != 0)
        {
            log?.Invoke($"esptool write-flash failed (exit {proc.ExitCode}).");
            return false;
        }
        log?.Invoke("Base firmware flashed. The board has reset. Re-scan to confirm.");
        return true;
    }

    // Write the TDLD bytes to the tdld partition via esptool write-flash.
    public static async Task<bool> WriteTdldImageAsync(
        DetectedBoard board,
        byte[] tdldBytes,
        string esptoolPath,
        Action<string>? log = null
    )
    {
        if (tdldBytes.Length > TdldMaxBytes)
        {
            log?.Invoke(
                $"TDLD image is {tdldBytes.Length} bytes, exceeds {TdldMaxBytes} byte"
                + " partition limit. Use a smaller image or fewer colours."
            );
            return false;
        }

        string tmpPath = Path.Combine(
            Path.GetTempPath(),
            $"esp32s3_tdld_{System.Random.Shared.Next(1000000, 9999999)}.bin"
        );
        try
        {
            await File.WriteAllBytesAsync(tmpPath, tdldBytes);

            var psi = new ProcessStartInfo
            {
                FileName = esptoolPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("--chip");
            psi.ArgumentList.Add("esp32s3");
            psi.ArgumentList.Add("--port");
            psi.ArgumentList.Add(board.Port);
            psi.ArgumentList.Add("write-flash");
            psi.ArgumentList.Add($"0x{TdldPartitionOffset:X}");
            psi.ArgumentList.Add(tmpPath);

            log?.Invoke(
                $"Running: esptool --chip esp32s3 --port {board.Port} "
                + $"write-flash 0x{TdldPartitionOffset:X} <{tdldBytes.Length} bytes>"
            );

            using var proc = Process.Start(psi);
            if (proc == null)
            {
                log?.Invoke("Failed to start esptool process.");
                return false;
            }
            proc.OutputDataReceived += (_, e) => { if (e.Data != null) log?.Invoke(e.Data); };
            proc.ErrorDataReceived += (_, e) => { if (e.Data != null) log?.Invoke(e.Data); };
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            await proc.WaitForExitAsync();
            if (proc.ExitCode != 0)
            {
                log?.Invoke($"esptool write-flash failed (exit {proc.ExitCode}).");
                return false;
            }
            log?.Invoke(
                "Wrote TDLD to ESP32-S3 tdld partition. The board has reset and is"
                + " replaying the drawing - unplug from this PC and connect to the Switch."
            );
            return true;
        }
        finally
        {
            try
            {
                if (File.Exists(tmpPath))
                    File.Delete(tmpPath);
            }
            catch { }
        }
    }
}
