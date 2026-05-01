using TomodachiDrawer.Core.Interfaces;

namespace TomodachiDrawer.Core.OutputSinks
{
    /// <summary>
    /// Tracks how long all inputs fed to it would take, and records them for later replay.
    /// Timing is accumulated from delay calls (which .Tap in ISwitchOutput calls),
    /// and the actions are recorded into the list and played back when needed. This is mostly for TSP solves
    /// to keep code from being a mess.
    /// </summary>
    public sealed class TimingSink : ISwitchOutput
    {
        //
        private readonly List<Action<ISwitchOutput>> _actions = [];
        private double _totalMilliseconds;

        public TimeSpan TotalTime => TimeSpan.FromMilliseconds(_totalMilliseconds);
        public double TotalMilliseconds => _totalMilliseconds;
        public double TotalSeconds => _totalMilliseconds / 1000.0;

        public void ReplayTo(ISwitchOutput target)
        {
            foreach (var action in _actions)
                action(target);
        }

        public void Delay(double milliseconds)
        {
            _totalMilliseconds += milliseconds;
            _actions.Add(o => o.Delay(milliseconds));
        }

        public void Press(Button btn) => _actions.Add(o => o.Press(btn));
        public void Release(Button btn) => _actions.Add(o => o.Release(btn));
        public void Press(DPad dir) => _actions.Add(o => o.Press(dir));
        public void Release(DPad dir) => _actions.Add(o => o.Release(dir));
        public void ReleaseAll() => _actions.Add(o => o.ReleaseAll());
        public void SetStick(Stick stick, byte value) => _actions.Add(o => o.SetStick(stick, value));

        void ISwitchOutput.Tap(Button btn, double holdDuration, double releaseDuration)
        {
            if (holdDuration == 25.0 && releaseDuration == 25.0 )
            {
                //WriteNibbleRecord(Opcode.TapButton, (byte)btn);
                _totalMilliseconds += holdDuration + releaseDuration;
                _actions.Add(o => o.Tap(btn, holdDuration, releaseDuration));
                return;
            }

            Press(btn);
            Delay(holdDuration);
            Release(btn);
            Delay(releaseDuration);
        }

        void ISwitchOutput.Tap(DPad dir, double holdDuration, double releaseDuration)
        {
            if (holdDuration == 25.0 && releaseDuration == 25.0)
            {
                //WriteNibbleRecord(Opcode.TapDPad, (byte)dir);
                _totalMilliseconds += holdDuration + releaseDuration;
                _actions.Add(o => o.Tap(dir, holdDuration, releaseDuration));
                return;
            }

            Press(dir);
            Delay(holdDuration);
            Release(dir);
            Delay(releaseDuration);
        }
        public void Dispose() { }
    }
}
