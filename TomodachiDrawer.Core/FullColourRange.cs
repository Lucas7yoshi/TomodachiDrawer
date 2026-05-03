namespace TomodachiDrawer.Core
{
    public class FullColourRange
    {
        // The full colour range works as follows:
        // ZL/ZR move the Hue slider left to right
        private const int HUE_SLIDER_STEP_COUNT = 201; // Need to painstakingly count
        // In the large rectangle area, the X axis is Saturation, the Y axis is Value/Brightness/Whatever
        
        // Left/Right
        private const int SATURATION_STEP_COUNT = 213; // Total positions, including the 0. So 0 to 212 kinda.
        // Up/Down
        private const int VALUE_STEP_COUNT = 112;

        // From what I can tell it seems to be the complete range so it should be fairly straight forward once I know
        // how many steps there are.
        // Additionally, input turbo-acceleration here seems to possibly more sane so may be worth attempting to reverse and use.


        // Left Right 0.0 to 360.0 or 0.0 to 1.0 depending on what i go for.
        // 
        // We'll be mapping from RGB anyhow.

        // A note from netux on youtube
        // "I noticed while working on my own tools that the gradient used by Nintendo differs slightly from a normal/naive HSL gradiet.
        // I wasn't able to figure out how it works exactly, but I think they at least double the gamma of the colors to reduce the shades of gray"

        // Hue and Value are inverse. // Value has a odd range to it...
    }
}
