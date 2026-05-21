using SkiaSharp;

namespace TomodachiDrawer.Core
{
    /// <summary>Map RGB values to input counts. Used by both ColourPalette and the ColourToHSVSteps tool.</summary>
    public static class ColourPickerRouter
    {
        // Full colour range.
        public const int FCR_HUE_SLIDER_STEP_COUNT = 201;
        public const int FCR_SATURATION_STEP_COUNT = 213;
        public const int FCR_VALUE_STEP_COUNT = 112;

        /// <summary>Translate a Colour to the number of button inputs for the Colour picker</summary>
        /// <param name="skColor">Colour to map.</param>
        /// <returns>Number of button inputs for Hue, Sat and Value.</returns>
        public static (int HueSteps, int SatSteps, int ValSteps) FromColour(SKColor skColor)
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

        // Inverse of FromColour. Given a set of picker step positions, returns the
        // 8-bit sRGB colour the game ends up storing when you confirm at those positions.
        //
        // The in-game picker is HSV but the game stores RGB. On reopen, the game maps
        // that stored RGB back to HSV to position the sliders - and that round-trip
        // is lossy. Black is the worst case: any H,S with V=0 becomes RGB(0,0,0),
        // which re-maps to the bottom-left corner of the SV box regardless of where
        // you originally tapped. By running the same round-trip on our side we can
        // predict where the picker will actually be on reopen, instead of assuming
        // it stayed where we left it.
        public static SKColor ToColour((int HueSteps, int SatSteps, int ValSteps) steps)
        {
            // Step positions back to continuous HSV (inverse of the rounding done in FromColour).
            float h = (1.0f - (float)steps.HueSteps / (FCR_HUE_SLIDER_STEP_COUNT - 1)) * 360.0f;
            float s = 1.0f - (float)steps.SatSteps / (FCR_SATURATION_STEP_COUNT - 1);
            float v = 1.0f - (float)steps.ValSteps / (FCR_VALUE_STEP_COUNT - 1);

            HsvToLinearRgb(h, s, v, out float linR, out float linG, out float linB);

            return new SKColor(ToSrgb8(linR), ToSrgb8(linG), ToSrgb8(linB), 255);
        }

        private static float ToLinear(byte srgb8)
        {
            float c = srgb8 / 255.0f;
            if (c <= 0.04045f)
                return c / 12.92f;
            return MathF.Pow((c + 0.055f) / 1.055f, 2.4f);
        }

        // Inverse of ToLinear - linear-light value back to an 8-bit sRGB byte.
        private static byte ToSrgb8(float linear)
        {
            float c;
            if (linear <= 0.0031308f)
                c = linear * 12.92f;
            else
                c = 1.055f * MathF.Pow(linear, 1.0f / 2.4f) - 0.055f;

            return (byte)Math.Clamp(MathF.Round(c * 255.0f), 0.0f, 255.0f);
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

        // Inverse of LinearRgbToHsv. h in [0, 360], s and v in [0, 1]. Output is
        // linear-light RGB matching the rest of this file's convention.
        private static void HsvToLinearRgb(
            float h,
            float s,
            float v,
            out float r,
            out float g,
            out float b
        )
        {
            float c = v * s;
            float hh = h / 60.0f;
            float x = c * (1.0f - MathF.Abs(hh % 2.0f - 1.0f));
            float m = v - c;

            float r1, g1, b1;
            if (hh < 1.0f) { r1 = c; g1 = x; b1 = 0; }
            else if (hh < 2.0f) { r1 = x; g1 = c; b1 = 0; }
            else if (hh < 3.0f) { r1 = 0; g1 = c; b1 = x; }
            else if (hh < 4.0f) { r1 = 0; g1 = x; b1 = c; }
            else if (hh < 5.0f) { r1 = x; g1 = 0; b1 = c; }
            else { r1 = c; g1 = 0; b1 = x; }

            r = r1 + m;
            g = g1 + m;
            b = b1 + m;
        }
    }
}

