using System.Text.Json.Serialization;
using TomodachiDrawer.Core.Models;

namespace TomodachiDrawer.UI.Avalonia;

internal class AppSettings
{
    public SwitchVersion SelectedSwitchVersion { get; set; } = SwitchVersion.None;

    public int SelectedThemeIndex { get; set; } = 0;

    public bool EnableExperimentalFeatures { get; set; } = false;

    public bool CheckForUpdatesOnStart { get; set; } = true;

    public int FirstStartId { get; set; } = 0;
}

// Source gen serialization to avoid trimming warnings.
[JsonSerializable(typeof(AppSettings))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal partial class AppSettingsContext : JsonSerializerContext { }
