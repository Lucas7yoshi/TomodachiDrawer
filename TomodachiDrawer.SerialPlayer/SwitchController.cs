using System.Diagnostics;
using System.IO.Ports;
using System.Runtime.CompilerServices;

// !!! WARNING !!!
// Slop-Era Code.
// (Pretty understandable though)
namespace TomodachiDrawer.SerialPlayer
{
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

    public enum Stick
    {
        LX,
        LY,
        RX,
        RY,
    }

    public class SwitchController : IDisposable
    {
        private readonly SerialPort _port;
        public bool IsConnected { get; private set; }

        //115200, 230400, 460800
        public SwitchController(string portName = "/dev/ttyAMA0", int baudRate = 230400)
        {
            _port = new SerialPort(portName, baudRate)
            {
                NewLine = "\n", // Crucial for Linux / RP2040 parser
                DtrEnable = true,
                RtsEnable = true,
                ReadTimeout = 2000,
            };
        }

        public bool Connect()
        {
            try
            {
                if (!_port.IsOpen)
                    _port.Open();

                // Clear out any old junk in the buffer
                _port.DiscardInBuffer();

                Console.WriteLine("Pinging RP2040...");
                _port.WriteLine("PING");

                string response = _port.ReadLine().Trim();
                if (response == "PONG")
                {
                    IsConnected = true;
                    ReleaseAll(); // Ensure a clean state on start
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Connection failed: {ex.Message}");
            }

            return false;
        }

        // --- Core Commands --- //

        private void Send(string command)
        {
            if (IsConnected && _port.IsOpen)
            {
                _port.WriteLine(command);
            }
        }

        public void Press(Button btn) => Send($"PRESS {btn}");

        public void Release(Button btn) => Send($"RELEASE {btn}");

        public void Press(DPad dir) => Send($"PRESS {dir}");

        public void Release(DPad dir) => Send($"RELEASE {dir}");

        public void ReleaseAll() => Send("RELEASE ALL");

        /// <summary>
        /// Sets the analog stick position. 0 = Min, 128 = Center, 255 = Max.
        /// </summary>
        public void SetStick(Stick stick, byte value) => Send($"STICK {stick} {value}");

        // --- Convenience Methods --- //

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void PreciseDelay(double milliseconds)
        {
            long ticksToWait = (long)(milliseconds * Stopwatch.Frequency / 1000.0);
            long startTick = Stopwatch.GetTimestamp();

            // Loop continuously until the exact hardware tick is reached
            while (Stopwatch.GetTimestamp() - startTick < ticksToWait)
            {
                // Thread.SpinWait(1) tells the CPU to perform a tiny hardware-level
                // pause (a NOP instruction) so it doesn't overheat, but without
                // yielding the thread to the OS scheduler.
                Thread.SpinWait(1);
            }
        }

        /// <summary>
        /// Presses a button, waits for the specified delay, then releases it.
        /// </summary>
        public void Tap(Button btn, double delayMs = 25.0, double releaseDelayMs = 25.0)
        {
            Press(btn);
            PreciseDelay(delayMs);
            Release(btn);
            PreciseDelay(releaseDelayMs);
        }

        public void Tap(DPad dir, double delayMs = 25.0, double releaseDelayMs = 25.0)
        {
            Press(dir);
            PreciseDelay(delayMs);
            Release(dir);
            PreciseDelay(releaseDelayMs);
        }

        /// <summary>
        /// Centers all analog sticks.
        /// </summary>
        public void CenterSticks()
        {
            SetStick(Stick.LX, 128);
            SetStick(Stick.LY, 128);
            SetStick(Stick.RX, 128);
            SetStick(Stick.RY, 128);
        }

        /// <summary>
        /// Deflects an analog stick to <paramref name="value"/> for the specified
        /// hold time, then returns it to center. Useful for testing cursor movement
        /// at varying stick deflections and hold durations.
        /// </summary>
        public void TapStick(
            Stick stick,
            byte value,
            double holdMs = 25.0,
            double releaseDelayMs = 25.0
        )
        {
            SetStick(stick, value);
            PreciseDelay(holdMs);
            SetStick(stick, 128);
            PreciseDelay(releaseDelayMs);
        }

        public void Dispose()
        {
            if (_port.IsOpen)
            {
                ReleaseAll(); // Let go of buttons before shutting down
                _port.Close();
            }
            _port.Dispose();
        }
    }
}
