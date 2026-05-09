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
            var delta = targetIndex - _toolbarCurrentIndex;
            DPad dir = delta > 0 ? DPad.RIGHT : DPad.LEFT;
            for (int i = 0; i < Math.Abs(delta); i++) // wont run if already there.
            {
                output.Tap(dir);
            }
            _toolbarCurrentIndex = targetIndex;
        }

        public bool SelectBrush(int brushSize) => SelectBrush(_output, brushSize);

        /// <returns>Whether or not it actually moved</returns>
        public bool SelectBrush(ISwitchOutput output, int brushSize)
        {
            int targetColumn = BrushColumnBySize[brushSize];

            if (_lastBrushColumn == targetColumn)
            {
                return false;
            }

            output.Tap(Button.X);
            output.Delay(400);

            // Both no-ops in 99% of cases, but resiliant to if we start using more tools.
            HomeToolbar(output);
            GoToToolbarIndex(output, ToolbarBrushIndex);

            // open submenu
            output.Tap(Button.X, 50, 25);
            output.Delay(400);

            int currentColumn = _lastBrushColumn;
            if (currentColumn < 0)
            {
                for (int i = 0; i < BrushSubmenuRows; i++)
                    output.Tap(DPad.UP);
                for (int i = 0; i < BrushSubmenuColumns; i++)
                    output.Tap(DPad.LEFT);

                output.Tap(DPad.DOWN);
                output.Tap(DPad.DOWN);
                currentColumn = 0;
            }

            int deltaX = targetColumn - currentColumn;
            var dir = deltaX > 0 ? DPad.RIGHT : DPad.LEFT;
            for (int i = 0; i < Math.Abs(deltaX); i++)
                output.Tap(dir);
            _lastBrushColumn = targetColumn;

            // Confirm and return to canvas.
            output.Tap(Button.A, 50, 25); // Switch 1 seems to want the press to last longer oddly. Hold for 50ms instead of 25.
            output.Delay(400);
            output.Tap(Button.A, 50, 25);
            output.Delay(600);

            return true;
        }

        public void SelectBucket() => SelectBucket(_output);

        /// <summary>Important note: Using the brush seems mildly laggy. Be generous with delays.</summary>
        public void SelectBucket(ISwitchOutput output)
        {
            output.Tap(Button.X);
            output.Delay(400);

            HomeToolbar(output);
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
            output.Delay(400);
        }
    }
}
