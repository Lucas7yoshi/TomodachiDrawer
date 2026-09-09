namespace TomodachiDrawer.Core.OutputSinks
{
    /// <summary>Boolean Pressed/Released buttons</summary>
    public enum Button : byte
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
    public enum DPad : byte
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
    public enum Stick : byte
    {
        LX,
        LY,
        RX,
        RY,
    }
}
