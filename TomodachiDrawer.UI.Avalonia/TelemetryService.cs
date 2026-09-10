using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace TomodachiDrawer.UI.Avalonia
{
    internal record StartupEventDto(string Os, string OsVersion, string Arch, string AppVersion);

    internal record ImageEventDto(
        int ImageWidth,
        int ImageHeight,
        int ImageColourCount,
        string QuantizerMode,
        int? ColourLimit,
        string SwitchVersion,
        bool ExperimentalFeatures,
        double TotalDrawTimeSeconds,
        double TspTimeLimit,
        string? AppVersion,
        string? Controller
    );

    internal record BucketLayerDecisionDto(
        int Clicks,
        int BucketedPixelCount,
        bool Skipped,
        bool? BucketWon,
        double? MarginSeconds
    );

    internal record BucketRoutingEventDto(
        string? AppVersion,
        int SkipThresholdUsed,
        List<BucketLayerDecisionDto> Layers
    );

    [JsonSerializable(typeof(StartupEventDto))]
    [JsonSerializable(typeof(ImageEventDto))]
    [JsonSerializable(typeof(BucketRoutingEventDto))]
    internal partial class TelemetryJsonContext : JsonSerializerContext { }

    /// <summary>TelemetryService handles reporting basic telemetry data to the developer. This is optional.</summary>
    internal class TelemetryService
    {
        public bool TelemetryEnabled { get; set; } = false;

        private const string TELEMETRY_URL = "https://telemetry.l7y.media/";

        private readonly HttpClient _http;

        public TelemetryService()
        {
            _http = new HttpClient
            {
                BaseAddress = new Uri(TELEMETRY_URL),
                Timeout = TimeSpan.FromSeconds(3),
            };
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("TomodachiDrawer");
        }

        /// <summary>Sends startup data. Gets it all itself, just needs called.</summary>
        /// <returns>Sent or not</returns>
        public async Task<bool> ReportStart()
        {
            if (!TelemetryEnabled)
                return false;

            string os =
                OperatingSystem.IsLinux() ? "Linux"
                : OperatingSystem.IsWindows() ? "Windows"
                : OperatingSystem.IsMacOS() ? "macOS"
                : "Unknown";
            string osVersion = RuntimeInformation.OSDescription;
            string arch = RuntimeInformation.ProcessArchitecture.ToString();
            var currentVersion = Assembly
                .GetEntryAssembly()
                ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;
            var obj = new StartupEventDto(os, osVersion, arch, currentVersion ?? "unknown");

            try
            {
                var response = await _http.PostAsJsonAsync(
                    "tomodachidrawer/startup",
                    obj,
                    TelemetryJsonContext.Default.StartupEventDto
                );
                return response.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> ReportImage(ImageEventDto imageData)
        {
            if (!TelemetryEnabled)
                return false;

            try
            {
                var response = await _http.PostAsJsonAsync(
                    "tomodachidrawer/image",
                    imageData,
                    TelemetryJsonContext.Default.ImageEventDto
                );
                return response.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> ReportBucketRouting(BucketRoutingEventDto routingData)
        {
            if (!TelemetryEnabled)
                return false;

            try
            {
                var response = await _http.PostAsJsonAsync(
                    "tomodachidrawer/bucketrouting",
                    routingData,
                    TelemetryJsonContext.Default.BucketRoutingEventDto
                );
                return response.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
