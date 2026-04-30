namespace TomodachiDrawer.Core.Models
{
    public readonly record struct LayerExtents(int MinX, int MaxX, int MinY, int MaxY)
    {
        /// <summary>Width of the extents are, NOT the overall canvas.</summary>
        public int Width => MaxX - MinX + 1;

        /// <summary>Height of the extents are, NOT the overall canvas.</summary>
        public int Height => MaxY - MinY + 1;
    }
}
