using TomodachiDrawer.Core.ImageProcessing.Quantizers;

namespace TomodachiDrawer.Core.Models
{
    public class DrawImageSettings
    {
        public required QuantizerSettings QuantizerSettings { get; set; }

        public string? DenoiserName { get; set; } = null;

        public float TSPTimeLimit { get; set; } = 1.0f;

        /// <summary>Disables "stamp" detection, which is areas that could be drawn with 3x3, 5x5, 9x9, etc brushes to save time.</summary>
        public bool DisableLargeBrush { get; set; } = false;

        /// <summary>Enables stuff that may be prone to desyncs or other instabilities.</summary>
        public bool EnableExperimentalFeatures { get; set; } = false;

        public bool HomeToTopLeft { get; set; } = false;
    }
}
