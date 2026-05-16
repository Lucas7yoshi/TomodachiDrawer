using SkiaSharp;

namespace TomodachiDrawer.Core.Models
{
    public record struct ColourGameSteps()
    {
        // Full colour range.
        public const int FCR_HUE_SLIDER_STEP_COUNT = 201;
        public const int FCR_SATURATION_STEP_COUNT = 213;
        public const int FCR_VALUE_STEP_COUNT = 112;

        /// <summary>Button taps to move from the left of the Hue slider</summary>
        public int HueSteps;
        /// <summary>Button taps to move from the right of the Colour gradient</summary>
        public int SatSteps;
        /// <summary>Button taps to move from the top of the Colour gradient</summary>
        public int ValSteps;

        public static ColourGameSteps FromColour(SKColor skColor)
        {
            // TLDR: The RGB needs to be Linearized from sRGB then turned to HSV.
            // This seemingly is a 1:1 match.
            float linR = ToLinear(skColor.Red);
            float linG = ToLinear(skColor.Green);
            float linB = ToLinear(skColor.Blue);

            LinearRgbToHsv(linR, linG, linB, out float h, out float s, out float v);

            // Figure out the steps first off
            int hueSteps = (int)
                Math.Round((1.0f - h / 360.0f) * (FCR_HUE_SLIDER_STEP_COUNT - 1));
            int satSteps = (int)Math.Round((1.0f - s) * (FCR_SATURATION_STEP_COUNT - 1));
            int valSteps = (int)Math.Round((1.0f - v) * (FCR_VALUE_STEP_COUNT - 1));

            return new()
            {
                HueSteps = hueSteps,
                SatSteps = satSteps,
                ValSteps = valSteps,
            };
        }

        private static float ToLinear(byte srgb8)
        {
            float c = srgb8 / 255.0f;
            if (c <= 0.04045f)
                return c / 12.92f;
            return MathF.Pow((c + 0.055f) / 1.055f, 2.4f);
        }

        private static void LinearRgbToHsv(
            float r,
            float g,
            float b,
            out float h,
            out float s,
            out float v
        )
        {
            float min = Math.Min(r, Math.Min(g, b));
            float max = Math.Max(r, Math.Max(g, b));
            float delta = max - min;

            v = max;
            s = max == 0 ? 0 : delta / max;

            if (delta == 0)
                h = 0;
            else if (max == r)
                h = 60 * (((g - b) / delta) % 6);
            else if (max == g)
                h = 60 * (((b - r) / delta) + 2);
            else
                h = 60 * (((r - g) / delta) + 4);

            if (h < 0)
                h += 360;
        }
    }
}

