namespace TomodachiDrawer.Core.OutputSinks
{
    /// <summary>Boolean Pressed/Released buttons</summary>
    public enum Button
    {
        A,
        B,
        X,
        Y,
        L,
        R,
        ZL,
        ZR,
        MINUS,
        PLUS,
        LCLICK,
        RCLICK,
        HOME,
        CAPTURE,
    }

    /// <summary>DPad, only one can be active at once.</summary>
    public enum DPad
    {
        UP,
        UPRIGHT,
        RIGHT,
        DOWNRIGHT,
        DOWN,
        DOWNLEFT,
        LEFT,
        UPLEFT,
    }

    /// <summary>Analog sticks axes</summary>
    public enum Stick
    {
        LX,
        LY,
        RX,
        RY,
    }
}
