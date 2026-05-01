using System.Runtime.InteropServices.Marshalling;

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
            output.Delay(250);
            if (!_toolbarHomed)
            {
                for (int i = 0; i < ToolbarItemCount; i++)
                    output.Tap(DPad.LEFT); // Slam to left
                for (int i = 0; i < ToolbarBrushIndex; i++)
                    output.Tap(DPad.RIGHT); // Go to brush
                _toolbarHomed = true;
            }

            // open submenu
            output.Tap(Button.X);
            output.Delay(250);

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
            output.Tap(Button.A);
            output.Delay(250);
            output.Tap(Button.A);
            output.Delay(500);

            return true;
        }
    }
}
