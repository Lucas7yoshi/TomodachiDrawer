namespace TomodachiDrawer.Core.OutputSinks
{
    public sealed class DummySink : ISwitchOutput
    {
        public void Delay(float milliseconds) { }

        public void Dispose() { }

        public void Press(Button btn) { }

        public void Press(DPad dir) { }

        public void Release(Button btn) { }

        public void Release(DPad dir) { }

        public void ReleaseAll() { }

        public void SetStick(Stick stick, byte value) { }
    }
}
