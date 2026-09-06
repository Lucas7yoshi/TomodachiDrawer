using TomodachiDrawer.Core.Models;
using TomodachiDrawer.Core.OutputSinks;

namespace TomodachiDrawer.Core
{
    public class CanvasToolbar(ISwitchOutput output, SwitchVersion switchVersion)
    {
        // Toolbar
        // 0: Undo
        // 1: Redo
        // 2: Move
        // 3: Select
        // 4: Text
        // 5: Stamp
        // 6: Shape
        // 7: Bucket
        // 8: Brush
        // 9: Eraser
        // 10: Eyedropper
        // 11: effects.
        // 12: settings
        private const int ToolbarSelectIndex = 3; // Used for canvas homing.
        private const int ToolbarBucketIndex = 7;
        private const int ToolbarBrushIndex = 8;
        private const int ToolbarEraserIndex = 9;
        private const int ToolbarItemCount = 12;

        private int _toolbarCurrentIndex = -1;

        // Brush
        private const int BrushSubmenuColumns = 6;
        private const int BrushSubmenuRows = 2;

        // Bucket
        private const int BucketSubmenuColumns = 7;
        private const int BucketSubmenuRows = 2;
        private bool _bucketSubmenuHomed = false;

        // Eraser
        private const int EraserSubmenuColumns = 6;
        private const int EraserSubmenuRows = 6;
        private const int EraserSubmenuEraseAllRow = 4; // NOTE: Erase all jumps to ToolbarBrushIndex after use!

        private int _lastBrushColumn = -1; // Brush menu remains on the previous

        // Used to streamline the "Testing" of various paths for drawing and how expensive they are, particularly as a result of
        // the paranoia mode for handling the bucket adding a not insignificant amount of time.
        public readonly record struct ToolbarState(
            int ToolbarIndex,
            int LastBrushColumn,
            bool BucketSubmenuHomed
        );

        public static readonly Dictionary<int, int> BrushColumnBySize = new()
        {
            [1] = 0,
            [3] = 1,
            [7] = 2,
            [13] = 3,
            [19] = 4,
            [27] = 5,
        };

        public static readonly Dictionary<int, int> SizeByBrushColumn = new()
        {
            [0] = 1,
            [1] = 3,
            [2] = 7,
            [3] = 13,
            [4] = 19,
            [5] = 27,
        };

        private readonly ISwitchOutput _realOutput = output;
        private readonly SwitchVersion _switchVersion = switchVersion;

        // Grabs the current state of toolbar for restoration.
        public ToolbarState Snapshot() =>
            new(_toolbarCurrentIndex, _lastBrushColumn, _bucketSubmenuHomed);

        // Restores.
        public void Restore(ToolbarState state)
        {
            _toolbarCurrentIndex = state.ToolbarIndex;
            _lastBrushColumn = state.LastBrushColumn;
            _bucketSubmenuHomed = state.BucketSubmenuHomed;
        }

        private void HomeToolbar(ISwitchOutput output)
        {
            if (_toolbarCurrentIndex == -1)
            {
                // Make it something known. for consistency, just slam it to the left then set index to 0
                for (int i = 0; i < ToolbarItemCount; i++)
                    output.Tap(DPad.LEFT);
                _toolbarCurrentIndex = 0;
            }
        }

        private void GoToToolbarIndex(ISwitchOutput output, int targetIndex)
        {
            if (_toolbarCurrentIndex == -1)
                HomeToolbar(output);
            var delta = targetIndex - _toolbarCurrentIndex;
            DPad dir = delta > 0 ? DPad.RIGHT : DPad.LEFT;
            for (int i = 0; i < Math.Abs(delta); i++) // wont run if already there.
            {
                output.Tap(dir);
            }
            output.Delay(300);
            _toolbarCurrentIndex = targetIndex;
        }

        /// <summary>Homes the canvas to the very very top left pixel of the area. For 256x256 drawings.</summary>
        /// <param name="output"></param>
        public void HomeCanvasToTopLeft(ISwitchOutput output)
        {
            // Zoom out
            output.SetStick(Stick.RY, 255);
            output.Delay(1400);
            output.ReleaseAll();
            output.Delay(75);

            // Open toolbar
            output.Tap(Button.X);
            output.Delay(500);

            GoToToolbarIndex(output, ToolbarSelectIndex); // Move is closest to the the

            // 7 down 16 right
            const int downCount = 7;
            const int rightCount = 16;
            // for efficiency, we do diagonals which eats up all the down inputs.
            output.Tap(DPad.DOWN);
            output.Delay(250);
            for (int i = 0; i < downCount - 1; i++)
            {
                output.Tap(DPad.DOWNRIGHT);
            }
            for (int i = 0; i < rightCount - (downCount - 1); i++) // this feels worse than magic numbers...
            {
                output.Tap(DPad.RIGHT);
            }

            // should be at perfect top left of image.

            output.Delay(100); // for good measure.

            // Because we Go to the Select index on the toolbar, but we dont select it, we just go down
            // our position on the toolbar actually is whatever it was before homing so we'll need to home again
            _toolbarCurrentIndex = -1;
        }

        public bool SelectBrush(int brushSize, bool paranoid = false) =>
            SelectBrush(_realOutput, brushSize, paranoid);

        public void ReselectLastBrush(bool paranoid = false) =>
            ReselectLastBrush(_realOutput, paranoid);

        public void ReselectLastBrush(ISwitchOutput output, bool paranoid = false)
        {
            if (_lastBrushColumn < 0)
            {
                // We haven't selected anything so just pick 1 since its a fair bet. Ideally this shouldnt ever be hit but just to be paranoid.
                SelectBrush(output, 1);
                return;
            }

            SelectBrush(output, SizeByBrushColumn[_lastBrushColumn], paranoid);
        }

        /// <returns>Whether or not it actually moved</returns>
        public bool SelectBrush(ISwitchOutput output, int brushSize, bool paranoid = false)
        {
            int targetColumn = BrushColumnBySize[brushSize];

            // If we are already selected on the brush, and on the right brush size
            // do nothing.
            if (_lastBrushColumn == targetColumn && _toolbarCurrentIndex == ToolbarBrushIndex)
            {
                return false;
            }

            // Open toolbar.
            output.Tap(Button.X);
            output.Delay(paranoid ? 750 : 500);

            // Go to brush.
            GoToToolbarIndex(output, ToolbarBrushIndex);

            // open submenu
            output.Tap(Button.X, paranoid ? 250 : 175, 50); // bumped even more due to desyncs on the switch 2 (from 50/25)
            output.Delay(paranoid ? 600 : 400);

            int currentColumn = _lastBrushColumn;

            bool needsHomed = currentColumn < 0;
            if (needsHomed) // Paranoia mode shouldnt ever be triggered when we need homed.
            {
                for (int i = 0; i < BrushSubmenuRows; i++)
                    output.Tap(DPad.UP);
                for (int i = 0; i < BrushSubmenuColumns; i++)
                    output.Tap(DPad.LEFT);

                // Go to the square brush area, at the 1 pixel brush.
                output.Tap(DPad.DOWN);
                for (int i = 0; i < 5; i++)
                    output.Tap(DPad.RIGHT);
                output.Tap(Button.A); // Select a brush that we dont actually use so we KNOW we will need two A presses. avoids a accidental click through draw
                output.Delay(_switchVersion == SwitchVersion.Switch1 ? 450 : 350);

                // Switch 1 lags with the largest sphere brush, because of course it does, so we more generously delay the taps back to the left.
                // This doesn't happen on my Switch 1 which is annoying but whatever...
                int tapDuration = _switchVersion == SwitchVersion.Switch1 ? 75 : 25;

                for (int i = 0; i < 5; i++)
                    output.Tap(DPad.LEFT, tapDuration, tapDuration);
                if (_switchVersion == SwitchVersion.Switch1)
                    output.Tap(Button.A, 75, 200); // Select smallest circle brush to avoid lag... hopefully
                output.Tap(DPad.DOWN);
                currentColumn = 0;
                output.Delay(100);
            }

            // After
            int deltaX = targetColumn - currentColumn;
            var dir = deltaX > 0 ? DPad.RIGHT : DPad.LEFT;
            for (int i = 0; i < Math.Abs(deltaX); i++)
                output.Tap(dir, paranoid ? 75 : default, paranoid ? 50 : default);

            // We need two taps if we are selecting something
            // not previously selected, one selects it, one confirms.
            // Technically we do not know for sure if it needs it after homing, but this is still safest at the moment.
            bool needsTwoTaps = deltaX != 0 || needsHomed;

            _lastBrushColumn = targetColumn;

            // Confirm
            if (needsTwoTaps)
            {
                output.Tap(Button.A, paranoid ? 100 : 50, paranoid ? 50 : 25); // Switch 1 seems to want the press to last longer oddly. Hold for 50ms instead of 25.
                output.Delay(paranoid ? 475 : 350);
            }
            // Close
            output.Tap(Button.A, paranoid ? 100 : 50, paranoid ? 50 : 25);
            output.Delay(500); // Shouldn't need paranoia here.

            return true;
        }

        public void SelectBucket(bool paranoid = false) => SelectBucket(_realOutput, paranoid);

        /// <summary>Important note: Using the brush seems mildly laggy. Be generous with delays.</summary>
        public void SelectBucket(ISwitchOutput output, bool paranoid = false)
        {
            output.Tap(Button.X);
            output.Delay(paranoid ? 750 : 500);

            GoToToolbarIndex(output, ToolbarBucketIndex);

            // We really do not care about any of the other options but the top left default one
            // Homing is probably not needed but might as well.
            if (!_bucketSubmenuHomed)
            {
                output.Tap(Button.X, paranoid ? 250 : 50, paranoid ? 50 : 25);
                output.Delay(paranoid ? 600 : 400);
                // 7 wide 2 tall
                for (int i = 0; i < BucketSubmenuRows - 1; i++)
                    output.Tap(DPad.UP, paranoid ? 75 : default, paranoid ? 50 : default);
                for (int i = 0; i < BucketSubmenuColumns - 1; i++)
                    output.Tap(DPad.LEFT, paranoid ? 75 : default, paranoid ? 50 : default);

                _bucketSubmenuHomed = true;
            }

            // Pressing A in the submenu goes straight to canvas, pressing A from the toolbar does the same
            // so only one A press ever needed.
            output.Tap(Button.A, paranoid ? 100 : 50, paranoid ? 50 : 25);
            output.Delay(paranoid ? 750 : 500);
        }

        public void ClearCanvas() => ClearCanvas(_realOutput);

        public void ClearCanvas(ISwitchOutput output)
        {
            output.Tap(Button.X);
            output.Delay(500);

            GoToToolbarIndex(output, ToolbarEraserIndex);

            output.Tap(Button.X, 50, 25); // Open Eraser submenu
            output.Delay(400);
            // slam to the top left
            for (int i = 0; i < EraserSubmenuColumns - 1; i++)
                output.Tap(DPad.LEFT);
            for (int i = 0; i < EraserSubmenuRows - 1; i++)
                output.Tap(DPad.UP);

            // Go down to the Erase All Button. Appears to be accessible from any row, could probably remove the column homing
            for (int i = 0; i < EraserSubmenuEraseAllRow - 1; i++)
                output.Tap(DPad.DOWN);

            // Perform clear
            output.Tap(Button.A, 50, 25);
            output.Delay(500);
            // After performing the Erase All the game automatically selects the brush so.
            _toolbarCurrentIndex = ToolbarBrushIndex;
        }
    }
}
