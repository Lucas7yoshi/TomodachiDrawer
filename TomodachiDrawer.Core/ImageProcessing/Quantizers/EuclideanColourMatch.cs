namespace TomodachiDrawer.Core.ImageProcessing.Quantizers
{
    public class EuclideanColourMatch(IEnumerable<PaletteColour> palette) : IImageQuantizer
    {
        private readonly IEnumerable<PaletteColour> _palette = palette;

        // Stupid simple colour matching.
        // Measures the error across the 3 channels, finds whatever has the lowest over all delta.
        // Has no care for human perception, but sometimes can end up being the best given our palette.

        public PaletteColour FindClosestColour(byte r, byte g, byte b)
        {
            var best = _palette.First();
            var bestDist = int.MaxValue;

            foreach (var colour in _palette)
            {
                int dr = r - colour.R;
                int dg = g - colour.G;
                int db = b - colour.B;
                int dist = dr * dr + dg * dg + db * db;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = colour;
                }
            }
            return best;
        }
    }
}
