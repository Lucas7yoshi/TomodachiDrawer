namespace TomodachiDrawer.Core.ImageProcessing.Quantizers
{
    public record QuantizerSettings(
        string quantizerName,
        int? colourCount = null,
        bool? useDithering = null
    );
}
