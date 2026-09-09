using JeremyAnsel.ColorQuant;
using SkiaSharp;

namespace TomodachiDrawer.Core.ImageProcessing.Quantizers
{
    /// <summary>
    /// Arbitrary quantizer for N colours using WuQuantizer. This depends on Full colour range support.
    /// </summary>
    public class ArbitraryColourQuantizer
    {
        public static SKBitmap Quantize(SKBitmap input, int colourCount)
        {
            if (colourCount < 1 || colourCount > 256)
                throw new ArgumentOutOfRangeException(
                    nameof(colourCount),
                    "Colour count must be at least 1 and less than or equal to 256."
                );

            int pixelCount = input.Width * input.Height;
            SKColor[] srcPixels = input.Pixels;

            // WuAlphaColorQuantizer takes in a byte array instead of an ImageSharp type so we need to convert to that.
            // https://github.com/JeremyAnsel/JeremyAnsel.ColorQuant/blob/6d79217e72af9e3af1a8a29c606732adac1e8d87/JeremyAnsel.ColorQuant/JeremyAnsel.ColorQuant/WuAlphaColorQuantizer2.cs#L454-L457
            // Also uses this opportunity to flatten any errant transparency.
            byte[] bgra = new byte[pixelCount * 4];
            for (int i = 0; i < pixelCount; i++)
            {
                SKColor p = srcPixels[i];
                if (p.Alpha < 128)
                {
                    bgra[i * 4 + 0] = 0;
                    bgra[i * 4 + 1] = 0;
                    bgra[i * 4 + 2] = 0;
                    bgra[i * 4 + 3] = 0;
                }
                else
                {
                    bgra[i * 4 + 0] = p.Blue;
                    bgra[i * 4 + 1] = p.Green;
                    bgra[i * 4 + 2] = p.Red;
                    bgra[i * 4 + 3] = 255;
                }
            }

            var quantizer = new WuAlphaColorQuantizer();
            ColorQuantizerResult result = quantizer.Quantize(bgra, colourCount);

            // Kicks out a Palette and bytes array of indices into that palette. Limit is 256 colours because of this.
            var bitmap = new SKBitmap(input.Width, input.Height);
            SKColor[] dstPixels = new SKColor[pixelCount];
            for (int i = 0; i < pixelCount; i++)
            {
                int idx = result.Bytes[i] * 4;
                byte b = result.Palette[idx + 0];
                byte g = result.Palette[idx + 1];
                byte r = result.Palette[idx + 2];
                byte a = result.Palette[idx + 3];
                dstPixels[i] = a < 128 ? SKColors.Transparent : new SKColor(r, g, b, 255);
            }

            bitmap.Pixels = dstPixels;
            return bitmap;
        }
    }
}
