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
        private enum Op : byte
        {
            Press,
            Release,
            DPadPress,
            DPadRelease,
            ReleaseAll,
            SetStick,
            Delay,
            Tap,
            TapDPad,
            TapStick,
        }

        private struct Instruction
        {
            public Op Op;
            public Button Button;
            public DPad DPad;
            public Stick Stick;
            public byte Value;
            public float A; // Double is excessivbe and literally I only did
            public float B;
        }

        private readonly List<Instruction> _log = [];
        private double _totalMilliseconds;

        public TimeSpan TotalTime => TimeSpan.FromMilliseconds(_totalMilliseconds);
        public double TotalMilliseconds => _totalMilliseconds;
        public double TotalSeconds => _totalMilliseconds / 1000.0;

        public void ReplayTo(ISwitchOutput target)
        {
            foreach (var i in _log)
            {
                switch (i.Op)
                {
                    case Op.Press:
                        target.Press(i.Button);
                        break;
                    case Op.Release:
                        target.Release(i.Button);
                        break;
                    case Op.DPadPress:
                        target.Press(i.DPad);
                        break;
                    case Op.DPadRelease:
                        target.Release(i.DPad);
                        break;
                    case Op.ReleaseAll:
                        target.ReleaseAll();
                        break;
                    case Op.SetStick:
                        target.SetStick(i.Stick, i.Value);
                        break;
                    case Op.Delay:
                        target.Delay(i.A);
                        break;
                    case Op.Tap:
                        target.Tap(i.Button, i.A, i.B);
                        break;
                    case Op.TapDPad:
                        target.Tap(i.DPad, i.A, i.B);
                        break;
                    case Op.TapStick:
                        target.TapStick(i.Stick, i.Value, i.A, i.B);
                        break;
                }
            }
        }

        public void Delay(float milliseconds)
        {
            _totalMilliseconds += milliseconds;
            _log.Add(new Instruction { Op = Op.Delay, A = milliseconds });
        }

        public void Press(Button btn) => _log.Add(new Instruction { Op = Op.Press, Button = btn });

        public void Release(Button btn) =>
            _log.Add(new Instruction { Op = Op.Release, Button = btn });

        public void Press(DPad dir) => _log.Add(new Instruction { Op = Op.DPadPress, DPad = dir });

        public void Release(DPad dir) =>
            _log.Add(new Instruction { Op = Op.DPadRelease, DPad = dir });

        public void ReleaseAll() => _log.Add(new Instruction { Op = Op.ReleaseAll });

        public void SetStick(Stick stick, byte value) =>
            _log.Add(
                new Instruction
                {
                    Op = Op.SetStick,
                    Stick = stick,
                    Value = value,
                }
            );

        void ISwitchOutput.Tap(Button btn, float holdDuration, float releaseDuration)
        {
            if (holdDuration == 25.0f && releaseDuration == 25.0f)
            {
                _totalMilliseconds += holdDuration + releaseDuration;
                _log.Add(
                    new Instruction
                    {
                        Op = Op.Tap,
                        Button = btn,
                        A = (float)holdDuration,
                        B = (float)releaseDuration,
                    }
                );
                return;
            }

            Press(btn);
            Delay(holdDuration);
            Release(btn);
            Delay(releaseDuration);
        }

        void ISwitchOutput.Tap(DPad dir, float holdDuration, float releaseDuration)
        {
            if (holdDuration == 25.0f && releaseDuration == 25.0f)
            {
                _totalMilliseconds += holdDuration + releaseDuration;
                _log.Add(
                    new Instruction
                    {
                        Op = Op.TapDPad,
                        DPad = dir,
                        A = (float)holdDuration,
                        B = (float)releaseDuration,
                    }
                );
                return;
            }

            Press(dir);
            Delay(holdDuration);
            Release(dir);
            Delay(releaseDuration);
        }

        void ISwitchOutput.TapStick(
            Stick stick,
            byte value,
            float holdDuration,
            float releaseDuration
        )
        {
            _totalMilliseconds += holdDuration + releaseDuration;
            _log.Add(
                new Instruction
                {
                    Op = Op.TapStick,
                    Stick = stick,
                    Value = value,
                    A = holdDuration,
                    B = releaseDuration,
                }
            );
        }

        public void Dispose() { }
    }
}
