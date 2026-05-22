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

            var output = new SKBitmap(input.Width, input.Height);
            for (int y = 0; y < mask.Height; y++)
            {
                for (int x = 0; x < mask.Width; x++)
                {
                    var maskPixel = mask.GetPixel(x, y);
                    var inputPixel = input.GetPixel(x, y);

                    if (maskPixel.Alpha == 255) // full white and no transparency is masked OUT
                        output.SetPixel(x, y, SKColors.Transparent); // Transparent
                    else
                        output.SetPixel(x, y, inputPixel);
                }
            }

            return output;
        }
    }
}
