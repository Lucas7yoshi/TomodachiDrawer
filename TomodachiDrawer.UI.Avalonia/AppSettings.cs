using System.Text.Json;
using System.Text.Json.Serialization;
using TomodachiDrawer.Core.Models;

namespace TomodachiDrawer.UI.Avalonia;

internal class AppSettings
{
    public SwitchVersion SelectedSwitchVersion { get; set; } = SwitchVersion.None;

    public int SelectedThemeIndex { get; set; } = 0;

    public bool EnableExperimentalFeatures { get; set; } = false;

    public bool CheckForUpdatesOnStart { get; set; } = true;

    public string SelectedColourMatcher { get; set; } = "Arbitrary";

    public int ColourLimit { get; set; } = 16;

    public string SelectedDenoiser { get; set; } = "None";

    public int FirstStartId { get; set; } = 0;

    public string SelectedESP32BoardId { get; set; } = "devkitc_1_r38";

    /// <summary>This is null by default to indicate they havent been asked yet.</summary>
    public bool? EnableTelemetry { get; set; } = null;

    internal static string GetSettingsFilePath()
    {
#if DEBUG
        const string settingsFileName = "settings_debug.json";
#else
        const string settingsFileName = "settings.json";
#endif

        if (OperatingSystem.IsMacOS() && AppContext.BaseDirectory.Contains(".app/Contents/MacOS"))
        {
            var appDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "TomodachiDrawer"
            );
            if (!Directory.Exists(appDataFolder))
            {
                Directory.CreateDirectory(appDataFolder);
            }
            return Path.Combine(appDataFolder, settingsFileName);
        }
        else if (OperatingSystem.IsWindows())
        {
            // Saving in AppData/Roaming for consistency across updates and avoid a telemetry prompt
            // every update.
            var appDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "L7Y Media",
                "TomodachiDrawer"
            );
            if (!Directory.Exists(appDataFolder))
            {
                Directory.CreateDirectory(appDataFolder);
            }
            return Path.Combine(appDataFolder, settingsFileName);
        }
        else
        {
            // Linux/Unix has kinda the same deal but its somewhat desktop dependent... it should
            // fallback if it can't find something tho.
            var appDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "TomodachiDrawer"
            );
            if (!Directory.Exists(appDataFolder))
            {
                Directory.CreateDirectory(appDataFolder);
            }
            return Path.Combine(appDataFolder, settingsFileName);
        }
    }

    // Retry writing.
    // We aren't holding onto the file stream for any real duration at all so the most likely
    // candidates are AV or something else grabbing the file... So we just retry a few times and hope it writes.
    // If it doesn't, a stale version may be on disk but the app will continue working with it's settings
    // and if anything changes, try writing again.
    private static readonly object _writeLock = new();

    internal void Save()
    {
        var json = JsonSerializer.Serialize(this, AppSettingsContext.Default.AppSettings);
        var path = GetSettingsFilePath();

        Task.Run(() =>
        {
            lock (_writeLock)
            {
                for (var attempt = 0; ; attempt++)
                {
                    try
                    {
                        File.WriteAllText(path, json);
                        return;
                    }
                    catch (Exception ex)
                        when ((ex is IOException or UnauthorizedAccessException) && attempt < 4)
                    {
                        Thread.Sleep(50);
                    }
                    catch (Exception ex)
                    {
                        SentrySdk.CaptureException(ex);
                        return;
                    }
                }
            }
        });
    }

    /// <summary>Attempt to load the settings from disk, if it exists.</summary>
    /// <returns>The loaded settings or null if loading failed.</returns>
    internal static AppSettings? TryLoad()
    {
        var path = GetSettingsFilePath();
        if (!File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize(json, AppSettingsContext.Default.AppSettings);
        }
        catch (Exception)
        {
            return null;
        }
    }
}

// Source gen serialization to avoid trimming warnings.
[JsonSerializable(typeof(AppSettings))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal partial class AppSettingsContext : JsonSerializerContext { }
