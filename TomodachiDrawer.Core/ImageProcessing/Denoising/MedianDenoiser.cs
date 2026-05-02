using SkiaSharp;

namespace TomodachiDrawer.Core.ImageProcessing.Denoising
{
    // This is from the old slop TomodachiDrawerPi version and... im not entirely sure what kind of denoising it...
    internal class MedianDenoiser : IImageDenoiser
    {
        public SKBitmap DenoiseImage(SKBitmap source)
        {
            var result = new SKBitmap(source.Width, source.Height, source.ColorType, source.AlphaType);

            var srcBytes = source.GetPixelSpan();
            var dstBytes = result.GetPixelSpan();

            int width = source.Width;
            int height = source.Height;
            int bpp = source.BytesPerPixel;

            Span<byte> window = stackalloc byte[9];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int idx = (y * width + x) * bpp;

                    byte a = srcBytes[idx + 3];

                    if (a <= 128)
                    {
                        dstBytes[idx + 0] = 0;
                        dstBytes[idx + 1] = 0;
                        dstBytes[idx + 2] = 0;
                        dstBytes[idx + 3] = 0;
                        continue;
                    }

                    dstBytes[idx + 0] = GetMedian(srcBytes, width, height, bpp, x, y, 0, window);
                    dstBytes[idx + 1] = GetMedian(srcBytes, width, height, bpp, x, y, 1, window);
                    dstBytes[idx + 2] = GetMedian(srcBytes, width, height, bpp, x, y, 2, window);
                    dstBytes[idx + 3] = a;
                }
            }

            return result;
        }


        private static byte GetMedian(
            ReadOnlySpan<byte> data, int width, int height, int bpp,
            int cx, int cy, int channel, Span<byte> window)
        {
            int count = 0;
            for (int dy = -1; dy <= 1; dy++)
            {
                int ny = Math.Clamp(cy + dy, 0, height - 1);
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = Math.Clamp(cx + dx, 0, width - 1);
                    window[count++] = data[(ny * width + nx) * bpp + channel];
                }
            }

            window.Sort();
            return window[4];
        }

    }
}
