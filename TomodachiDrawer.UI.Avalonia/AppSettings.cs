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
        const string settingsFileName = "settings.json";

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
        else
        {
            return settingsFileName;
        }
    }

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
