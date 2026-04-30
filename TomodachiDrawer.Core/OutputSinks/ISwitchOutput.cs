using System;
using TomodachiDrawer.Core.OutputSinks;

namespace TomodachiDrawer.Core.Interfaces
{
    public interface ISwitchOutput : IDisposable
    {
        /// <summary>
        /// Presses the specified button.
        /// </summary>
        void Press(Button btn);

        /// <summary>
        /// Releases the specified button.
        /// </summary>
        void Release(Button btn);

        /// <summary>
        /// Presses the specified d-pad direction.
        /// </summary>
        void Press(DPad dir);

        /// <summary>
        /// Releases the specified d-pad direction.
        /// </summary>
        void Release(DPad dir);

        /// <summary>
        /// Releases all currently held inputs.
        /// </summary>
        void ReleaseAll();

        /// <summary>
        /// Sets the analog stick position. 0 = Min, 128 = Center, 255 = Max.
        /// </summary>
        void SetStick(Stick stick, byte value);

        /// <summary>
        /// Emits or records a delay between input operations.
        /// </summary>
        void Delay(double milliseconds);

        /// <summary>
        /// Presses a button, waits for the specified delay, then releases it.
        /// </summary>
        /// <param name="btn">Button to tap</param>
        /// <param name="holdDuration">Duration for the button-down to stay</param>
        /// <param name="releaseDuration">Duration to pause before resuming anything else.</param>
        void Tap(Button btn, double holdDuration = 25.0, double releaseDuration = 25.0)
        {
            Press(btn);
            Delay(holdDuration);
            Release(btn);
            Delay(releaseDuration);
        }

        /// <summary>
        /// Presses a d-pad direction, waits for the specified delay, then releases it.
        /// </summary>
        /// <param name="dir">DPad direction to tap</param>
        /// <param name="holdDuration">Duration for the dpad-down to stay</param>
        /// <param name="releaseDuration">Duration to pause before resuming anything else.</param>
        void Tap(DPad dir, double holdDuration = 25.0, double releaseDuration = 25.0)
        {
            Press(dir);
            Delay(holdDuration);
            Release(dir);
            Delay(releaseDuration);
        }

        /// <summary>
        /// Centers all analog sticks.
        /// </summary>
        void CenterSticks()
        {
            SetStick(Stick.LX, 128);
            SetStick(Stick.LY, 128);
            SetStick(Stick.RX, 128);
            SetStick(Stick.RY, 128);
        }

        /// <summary>
        /// Deflects an analog stick for the specified hold time, then returns it to center.
        /// </summary>
        void TapStick(Stick stick, byte value, double holdDuration, double releaseDuration)
        {
            SetStick(stick, value);
            Delay(holdDuration);
            SetStick(stick, 128);
            Delay(releaseDuration);
        }
    }
}
