using SkiaSharp;

namespace TomodachiDrawer.Core.ImageProcessing
{
    public static class ImageMasker
    {
        private const string ResourcePath = "TomodachiDrawer.Core.Assets.Masks.";

        public static SKBitmap? GetMask(TomodachiLifeMask mask)
        {
            var targetResourceName = ResourcePath + mask.GetFileName();
            var assembly = typeof(CanvasDrawer).Assembly;
            var resourceNames = assembly.GetManifestResourceNames();
            if (resourceNames.Contains(targetResourceName))
            {
                using var stream = assembly.GetManifestResourceStream(targetResourceName);
                return SKBitmap.Decode(stream);
            }
            else
            {
                return null;
            }
        }

        public static SKBitmap MaskImage(SKBitmap input, SKBitmap mask)
        {
            if (input.Width != mask.Width || input.Height != mask.Height)
                throw new ArgumentException("Input and mask must be the same size.");

            // The tomodachi life masks are WHITE for excluded areas, and BLACK&TRANSPARENT for included areas.
            // So we remove anything from the input thats in the white areas of the mask.
            // For simplicities sake, just using .Alpha as the mask.

            var maskPixels = mask.Pixels;
            var inputPixels = input.Pixels;
            var outputPixels = new SKColor[inputPixels.Length];

            for (int i = 0; i < maskPixels.Length; i++)
            {
                outputPixels[i] =
                    maskPixels[i].Alpha == 255 ? SKColors.Transparent : inputPixels[i];
            }

            return new SKBitmap(input.Width, input.Height) { Pixels = outputPixels };
        }
    }
}
