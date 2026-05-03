using System.Runtime.InteropServices;
using SkiaSharp;
using TomodachiDrawer.Core.ImageProcessing;
using TomodachiDrawer.Core.ImageProcessing.Denoising;
using TomodachiDrawer.Core.ImageProcessing.Quantizers;
using TomodachiDrawer.Core.Interfaces;
using TomodachiDrawer.Core.Models;
using TomodachiDrawer.Core.OutputSinks;

namespace TomodachiDrawer.Core
{
    public class ColourPalette
    {
        // in game stuff
        private const int GridWidth = 12;
        private const int GridHeight = 7;
        private const int HotbarSlots = 9;
        private const int HotbarHeaderRows = 2; // Used for homing.

        // In game stuff, relevant to current draw session.
        // Used to track where our last colour was, so we can minimize inputs to change palette.
        private int _lastGridX = 0; // The first hotbar slot is by default black
        private int _lastGridY = 6; // which is at (0, 6) (aka, the bottom left)

        // We cannot home the grid because it loops around so this has to be set by the user
        // (but it should be by default)

        private bool _hotbarHomed = false; // If not, we home on first colour set.

        private ISwitchOutput _output;

        public static readonly Dictionary<
            string,
            Func<IEnumerable<PaletteColour>, IImageQuantizer>
        > Quantizers = new()
        {
            ["Euclidean"] = (p => new EuclideanColourMatch(p)),
            ["Redmean"] = (p => new RedmeanColourMatch(p)),
            ["CieLab"] = (p => new CieLabColourMatch(p)),
        };

        private static PaletteColour C(string name, string hex, int x, int y) =>
            PaletteColour.FromHex(name, hex, x, y);

        public List<PaletteColour> Colours { get; } =
        [
            // ===== Row 0 (y=0) =====
            C("White", "#FFFFFF", 0, 0),
            C("", "#F1F0F8", 1, 0),
            C("", "#F0F1F8", 2, 0),
            C("", "#F0F8FF", 3, 0),
            C("", "#F0FBF4", 4, 0),
            C("", "#F0F4EF", 5, 0),
            C("", "#F4FAEF", 6, 0),
            C("", "#FDFCEF", 7, 0),
            C("", "#FDF3EF", 8, 0),
            C("", "#FAF1EF", 9, 0),
            C("", "#FCEDDD", 10, 0),
            C("Red", "#FF0000", 11, 0),
            // ===== Row 1 (y=1) =====
            C("", "#EBEBEB", 0, 1),
            C("", "#D0C8E9", 1, 1),
            C("", "#C8CDE7", 2, 1),
            C("", "#C8E8FD", 3, 1),
            C("", "#C8F1D8", 4, 1),
            C("", "#C8DAC8", 5, 1),
            C("", "#DAEEC8", 6, 1),
            C("", "#FCF9C8", 7, 1),
            C("", "#FCD6C8", 8, 1),
            C("", "#EFC9C8", 9, 1),
            C("", "#E5CFB1", 10, 1),
            C("Yellow", "#FFFF00", 11, 1),
            // ===== Row 2 (y=2) =====
            C("", "#D5D5D4", 0, 2),
            C("", "#A692D6", 1, 2),
            C("", "#929ED4", 2, 2),
            C("", "#92D6FD", 3, 2),
            C("", "#92E6B9", 4, 2),
            C("", "#92BD94", 5, 2),
            C("", "#BBE194", 6, 2),
            C("", "#FAF492", 7, 2),
            C("", "#F9B492", 8, 2),
            C("", "#E29692", 9, 2),
            C("", "#CBA977", 10, 2),
            C("Lime", "#00FF00", 11, 2),
            // ===== Row 3 (y=3) =====
            C("", "#BCBCBC", 0, 3),
            C("", "#6500C3", 1, 3),
            C("", "#004BBE", 2, 3),
            C("", "#00C2FC", 3, 3),
            C("", "#00DA91", 4, 3),
            C("", "#009616", 5, 3),
            C("", "#92D316", 6, 3),
            C("", "#F8F000", 7, 3),
            C("", "#F78400", 8, 3),
            C("", "#D52600", 9, 3),
            C("", "#91620D", 10, 3),
            C("Cyan", "#00FFFF", 11, 3),
            // ===== Row 4 (y=4) =====
            C("", "#9C9C9B", 0, 4),
            C("", "#5500A8", 1, 4),
            C("", "#0040A5", 2, 4),
            C("", "#00A6D8", 3, 4),
            C("", "#00BC7B", 4, 4),
            C("", "#00800D", 5, 4),
            C("", "#7DB50D", 6, 4),
            C("", "#D5CE00", 7, 4),
            C("", "#D47100", 8, 4),
            C("", "#B62200", 9, 4),
            C("", "#774200", 10, 4),
            C("Blue", "#0000FF", 11, 4),
            // ===== Row 5 (y=5) =====
            C("", "#727272", 0, 5),
            C("", "#420084", 1, 5),
            C("", "#003281", 2, 5),
            C("", "#0083AB", 3, 5),
            C("", "#009360", 4, 5),
            C("", "#00650D", 5, 5),
            C("", "#628E0D", 6, 5),
            C("", "#A8A200", 7, 5),
            C("", "#A75800", 8, 5),
            C("", "#901600", 9, 5),
            C("", "#5D380D", 10, 5),
            C("Purple", "#8800FF", 11, 5),
            // ===== Row 6 (y=6) =====
            C("Black", "#000000", 0, 6),
            C("", "#22004B", 1, 6),
            C("", "#001649", 2, 6),
            C("", "#004962", 3, 6),
            C("", "#005535", 4, 6),
            C("", "#003800", 5, 6),
            C("", "#355100", 6, 6),
            C("", "#605D00", 7, 6),
            C("", "#602E00", 8, 6),
            C("", "#510D00", 9, 6),
            C("", "#35220D", 10, 6),
            C("Pink", "#FF00C3", 11, 6),
        ];

        public ColourPalette(ISwitchOutput outputSink)
        {
            _output = outputSink;
        }

        // Helper function that takes in an image and returns a preview of it
        // ran through the IImageQuantizer of their choosing.
        public SKBitmap PreviewColourMapping(
            SKBitmap source,
            QuantizerSettings quantizerSettings,
            string? denoiserName
        )
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(quantizerSettings.quantizerName);
            ArgumentNullException.ThrowIfNull(source);

            var trueSource = source;
            if (!string.IsNullOrEmpty(denoiserName))
            {
                trueSource = ImageDenoiser.DenoiseImage(source, denoiserName);
            }

            var quantized = QuantizeImage(trueSource, quantizerSettings);

            // make a bitmap out of it
            var output = new SKBitmap(source.Width, source.Height);
            for (int x = 0; x < quantized.GetLength(0); x++)
            {
                for (int y = 0; y < quantized.GetLength(1); y++)
                {
                    var paletteColour = quantized[x, y];
                    if (paletteColour != null)
                    {
                        output.SetPixel(x, y, paletteColour.skColor);
                    }
                }
            }

            return output;
        }



        /// <summary>Map an image to PaletteColours</summary>
        /// <returns>2D array of PaletteColour? [x, y] of the appropriate colours.</returns>
        public PaletteColour?[,] QuantizeImage(SKBitmap source, QuantizerSettings quantizerSettings)
        {
            if (quantizerSettings.quantizerName == "Arbitrary")
            {
                if (quantizerSettings.colourCount == null)
                    throw new ArgumentNullException(nameof(quantizerSettings.colourCount), "colourCount must be set for Arbitrary quantizer");
                SKBitmap quantized = ArbitraryColourQuantizer.Quantize(source, (int)quantizerSettings.colourCount, quantizerSettings.useDithering ?? default);
                // need to make our palette now.
                // Find all distinct colours in the image and make the PaletteColour's
                var skToPalette = MemoryMarshal.Cast<byte, SKColor>(quantized.GetPixelSpan())
                    .ToArray()
                    .Where(c => c.Alpha > 128)
                    .Distinct()
                    .ToDictionary(
                        d => d,
                        d => new PaletteColour($"({d.Red}, {d.Green}, {d.Blue})", d.Red, d.Green, d.Blue, null, null, d, true)
                    );
                var output = new PaletteColour?[source.Width, source.Height];
                for (int x = 0; x < quantized.Width; x++)
                {
                    for (int y = 0; y < quantized.Height; y++)
                    {
                        var pixel = quantized.GetPixel(x, y);
                        if (pixel.Alpha <= 128)
                            output[x, y] = null;
                        else
                            output[x, y] = skToPalette[pixel];
                    }
                }
                return output;
            }
            else
            {
                var quantizer = Quantizers[quantizerSettings.quantizerName](Colours);
                var result = new PaletteColour?[source.Width, source.Height];

                // TODO: GetPixel is slow
                for (int x = 0; x < source.Width; x++)
                {
                    for (int y = 0; y < source.Height; y++)
                    {
                        var pixel = source.GetPixel(x, y);
                        if (pixel.Alpha <= 128)
                            result[x, y] = null;
                        else
                            result[x, y] = quantizer.FindClosestColour(
                                pixel.Red,
                                pixel.Green,
                                pixel.Blue
                            );
                    }
                }

                return result;
            }
        }

        /// <summary>Creates the ColourLayers for each colour in a quantized image. Puts all pixels into FineDetailPoints for solving later</summary>
        /// <param name="pixels">The Quantized image</param>
        /// <returns>ColourLayers with FineDetails populated with all the points.</returns>
        public List<ColourLayer> BuildFineLayers(PaletteColour?[,] pixels)
        {
            var distinctColours = pixels.OfType<PaletteColour>().Distinct().ToList();

            var outputLayers = new List<ColourLayer>(distinctColours.Count);

            foreach (var colour in distinctColours)
            {
                // Find all the pixels
                var points = new HashSet<CanvasPoint>();
                for (int x = 0; x < pixels.GetLength(0); x++)
                {
                    for (int y = 0; y < pixels.GetLength(1); y++)
                    {
                        var pixelColour = pixels[x, y];
                        if (pixelColour != null && pixelColour == colour)
                            points.Add(new CanvasPoint(x, y));
                    }
                }

                var layer = new ColourLayer()
                {
                    Colour = colour,
                    FineDetailPoints = points,
                    Extents = new LayerExtents( // MinX, MaxX, MinY, MaxY
                        points.Min(p => p.X),
                        points.Max(p => p.X),
                        points.Min(p => p.Y),
                        points.Max(p => p.Y)
                    ),
                };

                outputLayers.Add(layer);
            }

            return outputLayers;
        }

        public void SelectColour(PaletteColour target, double speed)
        {
            _output.Tap(Button.Y, speed, speed);
            _output.Delay(300); // wait for open

            if (!_hotbarHomed)
            {
                Console.WriteLine("Homing hotbar, hasnt been done yet.");
                // We need to home, could be at an unknown position.
                // Slam against the top so we know that, and go down from the header.
                for (int i = 0; i < HotbarSlots + HotbarHeaderRows; i++)
                    _output.Tap(DPad.UP, speed, speed);
                for (int i = 0; i < HotbarHeaderRows; i++)
                    _output.Tap(DPad.DOWN, speed, speed);
                _hotbarHomed = true;
            }

            // We should now be on slot 0 so
            _output.Tap(Button.Y, speed, speed);
            _output.Delay(300);

            // Now in the colour menu. What tab? Shrug!
            if (!target.IsArbitrary && target.GridX != null && target.GridY != null)
            {
                // Move to right spot, from the
                int deltaX = (int)target.GridX - _lastGridX;
                int deltaY = (int)target.GridY - _lastGridY;

                // TODO: Optimize with diagonals.
                DPad YDirection = deltaY > 0 ? DPad.DOWN : DPad.UP;
                DPad XDirection = deltaX > 0 ? DPad.RIGHT : DPad.LEFT;
                for (int i = 0; i < Math.Abs(deltaY); i++)
                    _output.Tap(YDirection, speed, speed);
                for (int i = 0; i < Math.Abs(deltaX); i++)
                    _output.Tap(XDirection, speed, speed);

                // confirm and close out
                _output.Tap(Button.A, speed, speed);
                _output.Delay(300);

                _lastGridX = (int)target.GridX;
                _lastGridY = (int)target.GridY;
            }
            else
            {
                // TODO: PICKUP HERE IN MORNING
                // Ideally this should be able to go back and forth between arbitrary or preset colours but
                // not needed for initial implementation if its a pain.
                throw new NotImplementedException();
            }
        }
    }
}
