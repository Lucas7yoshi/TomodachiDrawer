namespace TomodachiDrawer.Core.ImageProcessing.Quantizers
{
    public class RedmeanColourMatch(IEnumerable<PaletteColour> palette) : IImageQuantizer
    {
        private readonly PaletteColour[] _palette = palette as PaletteColour[] ?? palette.ToArray();

        public PaletteColour FindClosestColour(byte r, byte g, byte b)
        {
            var best = _palette[0];
            int bestDist = int.MaxValue;

            foreach (var colour in _palette)
            {
                int dr = r - colour.R;
                int dg = g - colour.G;
                int db = b - colour.B;
                int rMean = (r + colour.R) / 2;

                int dist =
                    (((512 + rMean) * dr * dr) / 256)
                    + (4 * dg * dg)
                    + (((767 - rMean) * db * db) / 256);

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
