using TomodachiDrawer.Core.Interfaces;
using TomodachiDrawer.Core.OutputSinks;

namespace TomodachiDrawer.Core
{
    public class CanvasToolbar
    {
        // Toolbar/Brush menu
        private const int ToolbarItemCount = 12;
        private const int ToolbarBrushIndex = 8;
        private const int BrushSubmenuColumns = 6;
        private const int BrushSubmenuRows = 2;
        private bool _toolbarHomed = false;
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

        private ISwitchOutput _output;
        public CanvasToolbar(ISwitchOutput output)
        {
            _output = output;
        }

        /// <returns>Whether or not it actually moved</returns>
        public bool SelectBrush(int brushSize)
        {
            int targetColumn = BrushColumnBySize[brushSize];

            if (_lastBrushColumn == targetColumn)
            {
                return false;
            }

            _output.Tap(Button.X);
            _output.Delay(250);
            if (!_toolbarHomed)
            {
                for (int i = 0; i < ToolbarItemCount; i++)
                    _output.Tap(DPad.LEFT); // Slam to left
                for (int i = 0; i < ToolbarBrushIndex; i++)
                    _output.Tap(DPad.RIGHT); // Go to brush
                _toolbarHomed = true;
            }

            // open submenu
            _output.Tap(Button.X);
            _output.Delay(250);

            int currentColumn = _lastBrushColumn;
            if (currentColumn < 0)
            {
                for (int i = 0; i < BrushSubmenuRows; i++)
                    _output.Tap(DPad.UP);
                for (int i = 0; i < BrushSubmenuColumns; i++)
                    _output.Tap(DPad.LEFT);

                _output.Tap(DPad.DOWN);
                _output.Tap(DPad.DOWN);
                currentColumn = 0;
            }

            int deltaX = targetColumn - currentColumn;
            var dir = deltaX > 0 ? DPad.RIGHT : DPad.LEFT;
            for (int i = 0; i < Math.Abs(deltaX); i++)
                _output.Tap(dir);
            _lastBrushColumn = targetColumn;

            // Confirm and return to canvas.
            _output.Tap(Button.A);
            _output.Delay(250);
            _output.Tap(Button.A);
            _output.Delay(500);

            return true;
        }
    }
}
