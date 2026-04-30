namespace TomodachiDrawer.Core.ImageProcessing.Quantizers
{
    // 🚨AI SLOP ALERT🚨
    // This was a bit above my paygrade...
    public class CieLabColourMatch : IImageQuantizer
    {
        private readonly IEnumerable<PaletteColour> _palette;

        public CieLabColourMatch(IEnumerable<PaletteColour> palette)
        {
            _palette = palette;
            BuildLabCache();
        }

        public PaletteColour FindClosestColour(byte r, byte g, byte b)
        {
            RgbToLab(r, g, b, out double L, out double A, out double B_);

            var best = _palette.First();
            double bestDist = double.MaxValue;

            foreach (var color in _palette)
            {
                var (cL, cA, cB) = _labCache[color];

                double dL = L - cL;
                double dA = A - cA;
                double dB = B_ - cB;

                double dist = dL * dL + dA * dA + dB * dB;

                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = color;
                }
            }

            return best;
        }

        /// <summary>
        /// Pre-computed CIELAB values for every palette color, built once at
        /// construction time so FindClosestColor only converts the input pixel.
        /// </summary>
        private readonly Dictionary<PaletteColour, (double L, double A, double B)> _labCache = [];

        /// <summary>
        /// Populates <see cref="_labCache"/> for all palette colors.
        /// Called from the constructor.
        /// </summary>
        private void BuildLabCache()
        {
            foreach (var color in _palette)
            {
                RgbToLab(color.R, color.G, color.B, out double L, out double a, out double b);
                _labCache[color] = (L, a, b);
            }
        }

        /// <summary>
        /// Converts sRGB (0–255) to CIELAB (L*, a*, b*) via the D65 XYZ
        /// intermediate. Uses the standard sRGB gamma and CIE f(t) formulas.
        /// </summary>
        private static void RgbToLab(
            byte r,
            byte g,
            byte b,
            out double L,
            out double a,
            out double bOut
        )
        {
            // sRGB to linear RGB
            double rl = SrgbToLinear(r / 255.0);
            double gl = SrgbToLinear(g / 255.0);
            double bl = SrgbToLinear(b / 255.0);

            // Linear RGB to XYZ (D65 reference white)
            double x = (0.4124564 * rl + 0.3575761 * gl + 0.1804375 * bl) / 0.95047;
            double y = (0.2126729 * rl + 0.7151522 * gl + 0.0721750 * bl) / 1.00000;
            double z = (0.0193339 * rl + 0.1191920 * gl + 0.9503041 * bl) / 1.08883;

            // XYZ to Lab
            double fx = LabF(x);
            double fy = LabF(y);
            double fz = LabF(z);

            L = 116.0 * fy - 16.0;
            a = 500.0 * (fx - fy);
            bOut = 200.0 * (fy - fz);
        }

        private static double SrgbToLinear(double c) =>
            c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

        private static double LabF(double t) =>
            t > 0.008856 ? Math.Cbrt(t) : (7.787 * t) + (16.0 / 116.0);
    }
}
