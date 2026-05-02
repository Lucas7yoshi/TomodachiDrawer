namespace TomodachiDrawer.Core
{
    public class FullColourRange
    {
        // The full colour range works as follows:
        // ZL/ZR move the Hue slider left to right
        private const int HUE_SLIDER_STEP_COUNT = -1; // Need to painstakingly count
        // In the large rectangle area, the X axis is Saturation, the Y axis is Value/Brightness/Whatever
        
        // Left/Right
        private const int SATURATION_STEP_COUNT = -1; // Need to painstakingly count
        // Up/Down
        private const int VALUE_STEP_COUNT = -1; // Need to painstakingly count

        // From what I can tell it seems to be the complete range so it should be fairly straight forward once I know
        // how many steps there are.
        // Additionally, input turbo-acceleration here seems to possibly more sane so may be worth attempting to reverse and use.


        // Left Right 0.0 to 360.0 or 0.0 to 1.0 depending on what i go for.
        // 
        // We'll be mapping from RGB anyhow.

    }
}
