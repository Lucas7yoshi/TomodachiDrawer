using SkiaSharp;

namespace TomodachiDrawer.Core.ImageProcessing.Denoising
{
    public interface IImageDenoiser
    {
        public SKBitmap DenoiseImage(SKBitmap image);
    }
}
