using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using TomodachiDrawer.Core.Interfaces;

namespace TomodachiDrawer.Core.OutputSinks
{
    public class VirtualGamepadSink(IXbox360Controller gamepad) : ISwitchOutput
    {
        private readonly IXbox360Controller _gamepad = gamepad;

        public void Delay(double milliseconds)
        {
            Thread.Sleep((int)milliseconds);
        }

        public void Press(Button switchBtn)
        {
            var xbox = MapSwitchButton(switchBtn);
            if (xbox.Button != null)
            {
                _gamepad.SetButtonState(xbox.Button, true);
            }
            else if (xbox.Slider != null)
            {
                _gamepad.SetSliderValue(xbox.Slider, 0xFF);
            }
        }

        public void Press(DPad switchDir)
        {
            var xboxBtns = MapSwitchDpad(switchDir);
            foreach (var xboxBtn in xboxBtns)
            {
                _gamepad.SetButtonState(xboxBtn, true);
            }
        }

        public void Release(Button switchBtn)
        {
            var xbox = MapSwitchButton(switchBtn);
            if (xbox.Button != null)
            {
                _gamepad.SetButtonState(xbox.Button, false);
            }
            else if (xbox.Slider != null)
            {
                _gamepad.SetSliderValue(xbox.Slider, 0x00);
            }
        }

        public void Release(DPad switchDir)
        {
            var xboxBtns = MapSwitchDpad(switchDir);
            foreach (var xboxBtn in xboxBtns)
            {
                _gamepad.SetButtonState(xboxBtn, false);
            }
        }

        public void ReleaseAll()
        {
            foreach (var xboxBtn in Xbox360Property.GetAll<Xbox360Button>())
            {
                _gamepad.SetButtonState(xboxBtn, false);
            }

            foreach (var xboxSlider in Xbox360Property.GetAll<Xbox360Slider>())
            {
                _gamepad.SetSliderValue(xboxSlider, 0);
            }

            foreach (var xboxAxis in Xbox360Property.GetAll<Xbox360Axis>())
            {
                _gamepad.SetAxisValue(xboxAxis, 0);
            }
        }

        public void SetStick(Stick stick, byte value)
        {
            var xboxAxis = MapSwitchStick(stick);

            if (xboxAxis == Xbox360Axis.LeftThumbY || xboxAxis == Xbox360Axis.RightThumbY)
            {
                value = (byte)(byte.MaxValue - value);
            }

            short xboxValue = value != byte.MaxValue / 2
                ? (short)((value * (double)(short.MaxValue - short.MinValue) / byte.MaxValue) + short.MinValue)
                : (short) 0;

            _gamepad.SetAxisValue(xboxAxis, xboxValue);
        }

        public void Dispose() { }

        private static (Xbox360Button? Button, Xbox360Slider? Slider) MapSwitchButton(Button switchButton)
        {
            return switchButton switch
            {
                Button.A => (Xbox360Button.B, null),
                Button.B => (Xbox360Button.A, null),
                Button.Y => (Xbox360Button.X, null),
                Button.X => (Xbox360Button.Y, null),
                Button.L => (Xbox360Button.LeftShoulder, null),
                Button.LCLICK => (Xbox360Button.LeftThumb, null),
                Button.ZL => (null, Xbox360Slider.LeftTrigger),
                Button.R => (Xbox360Button.RightShoulder, null),
                Button.RCLICK => (Xbox360Button.RightThumb, null),
                Button.ZR => (null, Xbox360Slider.RightTrigger),
                _ => (null, null),
            };
        }

        private static Xbox360Button[] MapSwitchDpad(DPad switchDir)
        {
            return switchDir switch
            {
                DPad.UP => [Xbox360Button.Up],
                DPad.DOWN => [Xbox360Button.Down],
                DPad.LEFT => [Xbox360Button.Left],
                DPad.RIGHT => [Xbox360Button.Right],
                DPad.UPRIGHT => [Xbox360Button.Up, Xbox360Button.Right],
                DPad.UPLEFT => [Xbox360Button.Up, Xbox360Button.Left],
                DPad.DOWNRIGHT => [Xbox360Button.Down, Xbox360Button.Right],
                DPad.DOWNLEFT => [Xbox360Button.Down, Xbox360Button.Left],
                _ => [],
            };
        }

        private static Xbox360Axis? MapSwitchStick(Stick switchStick)
        {
            return switchStick switch
            {
                Stick.LX => Xbox360Axis.LeftThumbX,
                Stick.LY => Xbox360Axis.LeftThumbY,
                Stick.RX => Xbox360Axis.RightThumbX,
                Stick.RY => Xbox360Axis.RightThumbY,
                _ => null,
            };
        }
    }
}
