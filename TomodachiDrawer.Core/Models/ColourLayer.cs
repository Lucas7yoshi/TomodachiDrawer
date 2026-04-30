using TomodachiDrawer.Core.ImageProcessing;

namespace TomodachiDrawer.Core.Models
{
    public class ColourLayer
    {
        public required PaletteColour Colour { get; set; }

        /// <summary>Extents of the layer.</summary>
        public LayerExtents Extents { get; set; }

        /// <summary>
        /// A list of points for each stamp size to be drawn.
        /// From largest to smallest, they should generally not overlap but may later for some
        /// cases like filling in a 3 wide 2 tall area next to a larger stamp, since the 3x3 stamp would
        /// be quicker than 6 1x1 points.
        /// Key is stamp size, value is the list of points. Should be TSP solved.
        /// </summary>
        public Dictionary<int, List<CanvasPoint>> StampsBySize = new();

        /// <summary>1x1 points for individual drawing.</summary>
        public HashSet<CanvasPoint> FineDetailPoints = new();
    }
}
