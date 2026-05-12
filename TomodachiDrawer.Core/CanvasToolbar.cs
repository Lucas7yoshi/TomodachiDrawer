using TomodachiDrawer.Core.Interfaces;
using TomodachiDrawer.Core.OutputSinks;

namespace TomodachiDrawer.Core
{
    public class CanvasToolbar(ISwitchOutput output)
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
        private const int ToolbarBucketIndex = 7;
        private const int ToolbarBrushIndex = 8;
        private const int ToolbarItemCount = 12;

        private int _toolbarCurrentIndex = -1;

        // Brush
        private const int BrushSubmenuColumns = 6;
        private const int BrushSubmenuRows = 2;

        // Bucket
        private const int BucketSubmenuColumns = 7;
        private const int BucketSubmenuRows = 2;
        private bool _bucketSubmenuHomed = false;


        private int _lastBrushColumn = -1; // Brush menu remains on the previous

        public static readonly Dictionary<int, int> BrushColumnBySize = new()
        {
            [1] = 0,
            [3] = 1,
            [7] = 2,
            [13] = 3,
            [19] = 4,
            [27] = 5,
        };

        private readonly ISwitchOutput _output = output;

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
            _output.Delay(300);
            _toolbarCurrentIndex = targetIndex;
        }

        public bool SelectBrush(int brushSize) => SelectBrush(_output, brushSize);

        /// <returns>Whether or not it actually moved</returns>
        public bool SelectBrush(ISwitchOutput output, int brushSize)
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
            output.Delay(500);

            // Go to brush.
            GoToToolbarIndex(output, ToolbarBrushIndex);

            // open submenu
            output.Tap(Button.X, 50, 25);
            output.Delay(500);

            int currentColumn = _lastBrushColumn;

            bool needsHomed = currentColumn < 0;
            if (needsHomed)
            {
                for (int i = 0; i < BrushSubmenuRows; i++)
                    output.Tap(DPad.UP);
                for (int i = 0; i < BrushSubmenuColumns; i++)
                    output.Tap(DPad.LEFT);

                // Go to the square brush area, at the 1 pixel brush.
                output.Tap(DPad.DOWN);
                output.Tap(DPad.DOWN);
                currentColumn = 0;
            }

            // After 
            int deltaX = targetColumn - currentColumn;
            var dir = deltaX > 0 ? DPad.RIGHT : DPad.LEFT;
            for (int i = 0; i < Math.Abs(deltaX); i++)
                output.Tap(dir);

            // We need two taps if we are selecting something
            // not previously selected, one selects it, one confirms.
            // Technically we do not know for sure if it needs it after homing, but this is still safest at the moment.
            bool needsTwoTaps = deltaX != 0 || needsHomed;

            _lastBrushColumn = targetColumn;

            // Confirm
            if (needsTwoTaps)
            {
                output.Tap(Button.A, 50, 25); // Switch 1 seems to want the press to last longer oddly. Hold for 50ms instead of 25.
                output.Delay(500);
            }
            // Close
            output.Tap(Button.A, 50, 25);
            output.Delay(600);

            return true;
        }

        public void SelectBucket() => SelectBucket(_output);

        /// <summary>Important note: Using the brush seems mildly laggy. Be generous with delays.</summary>
        public void SelectBucket(ISwitchOutput output)
        {
            output.Tap(Button.X);
            output.Delay(500);

            GoToToolbarIndex(output, ToolbarBucketIndex);


            // We really do not care about any of the other options but the top left default one
            // Homing is probably not needed but might as well.
            if (!_bucketSubmenuHomed)
            {
                output.Tap(Button.X, 50, 25);
                output.Delay(400);
                // 7 wide 2 tall
                for (int i = 0; i < BucketSubmenuRows - 1; i++)
                    output.Tap(DPad.UP);
                for (int i = 0; i < BucketSubmenuRows - 1; i++)
                    output.Tap(DPad.LEFT);

                _bucketSubmenuHomed = true;
            }

            // Pressing A in the submenu goes straight to canvas, pressing A from the toolbar does the same
            // so only one A press ever needed.
            output.Tap(Button.A, 50, 25);
            output.Delay(500);
        }
    }
}
