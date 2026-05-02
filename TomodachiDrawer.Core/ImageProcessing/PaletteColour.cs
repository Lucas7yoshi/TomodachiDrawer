using System.Globalization;
using SkiaSharp;

namespace TomodachiDrawer.Core.ImageProcessing
{
    public record PaletteColour(
        string Name,
        byte R,
        byte G,
        byte B,
        int GridX,
        int GridY,
        SKColor skColor
    )
    {
        public string DisplayName => string.IsNullOrEmpty(Name) ? $"({R}, {G}, {B})" : Name;

        public static PaletteColour FromHex(string name, string hex, int gridX, int gridY)
        {
            ReadOnlySpan<char> s = hex.AsSpan().TrimStart('#');
            if (s.Length != 6)
                throw new ArgumentOutOfRangeException(
                    nameof(hex),
                    "Hex must be #RRGGBB i.e #FFAA00"
                );
            byte r = byte.Parse(s[0..2], NumberStyles.HexNumber);
            byte g = byte.Parse(s[2..4], NumberStyles.HexNumber);
            byte b = byte.Parse(s[4..6], NumberStyles.HexNumber);
            return new(name, r, g, b, gridX, gridY, new SKColor(r, g, b, 255));
        }
    }
}
