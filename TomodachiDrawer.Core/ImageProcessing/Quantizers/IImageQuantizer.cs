namespace TomodachiDrawer.Core.ImageProcessing.Quantizers
{
    public interface IImageQuantizer
    {
        PaletteColour FindClosestColour(byte r, byte g, byte b);
    }
}
