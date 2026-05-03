using SkiaSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace TomodachiDrawer.Core.ImageProcessing.Quantizers
{
    public class ArbitraryColourQuantizer
    {
        public static SKBitmap Quantize(SKBitmap input, int colourCount, bool useDithering = true)
        {
            if (colourCount < 1) throw new ArgumentOutOfRangeException(nameof(colourCount), "Colour count must be at least 1.");

            using var image = ToImageSharp(input);

            var quantizer = new WuQuantizer(new QuantizerOptions
            {
                MaxColors = colourCount,
                Dither = useDithering ? KnownDitherings.FloydSteinberg : null
            });

            image.Mutate(x => x.Quantize(quantizer));

            return ToSkBitmap(image);
        }

        private static Image<Rgba32> ToImageSharp(SKBitmap bitmap)
        {
            var image = new Image<Rgba32>(bitmap.Width, bitmap.Height);
            var pixels = bitmap.Pixels;

            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < bitmap.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < bitmap.Width; x++)
                    {
                        var p = pixels[y * bitmap.Width + x];
                        row[x] = new Rgba32(p.Red, p.Green, p.Blue, p.Alpha);
                    }
                }
            });

            return image;
        }

        private static SKBitmap ToSkBitmap(Image<Rgba32> image)
        {
            var bitmap = new SKBitmap(image.Width, image.Height);
            var pixels = new SKColor[image.Width * image.Height];

            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < image.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < image.Width; x++)
                    {
                        var p = row[x];
                        pixels[y * image.Width + x] = p.A < 128
                            ? SKColors.Transparent
                            : new SKColor(p.R, p.G, p.B, 255);
                    }
                }
            });

            bitmap.Pixels = pixels;
            return bitmap;
        }
    }
}