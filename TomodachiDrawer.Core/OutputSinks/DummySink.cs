using TomodachiDrawer.Core.Interfaces;

namespace TomodachiDrawer.Core.OutputSinks
{
    public class DummySink : ISwitchOutput
    {
        public void Delay(double milliseconds) { }

        public void Dispose() { }

        public void Press(Button btn) { }

        public void Press(DPad dir) { }

        public void Release(Button btn) { }

        public void Release(DPad dir) { }

        public void ReleaseAll() { }

        public void SetStick(Stick stick, byte value) { }
    }
}
