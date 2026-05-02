using SkiaSharp;

namespace TomodachiDrawer.Core.ImageProcessing.Denoising
{
    public static class ImageDenoiser
    {
        public static readonly Dictionary<string, IImageDenoiser> Denoisers = new()
        {
            ["Median"] = new MedianDenoiser(),
        };

        public static SKBitmap DenoiseImage(SKBitmap source, string? denoiserName)
        {
            if (string.IsNullOrEmpty(denoiserName) || denoiserName == "None")
                return source;
            return Denoisers[denoiserName].DenoiseImage(source);
        }
    }
}
