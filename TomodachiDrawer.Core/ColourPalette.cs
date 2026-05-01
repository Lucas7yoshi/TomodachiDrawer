using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Security;
using System.Text;
using SkiaSharp;
using TomodachiDrawer.Core.ImageProcessing;
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
            C("", "#EEF0F6", 1, 0),
            C("", "#EFF1F5", 2, 0),
            C("", "#EEF8FC", 3, 0),
            C("", "#EFFBF2", 4, 0),
            C("", "#EFF3EE", 5, 0),
            C("", "#F3FAEF", 6, 0),
            C("", "#FBFCEE", 7, 0),
            C("", "#FAF3ED", 8, 0),
            C("", "#F7F1EE", 9, 0),
            C("", "#FAEEDB", 10, 0),
            C("Red", "#FC0100", 11, 0),
            // ===== Row 1 (y=1) =====
            C("", "#EBEBEB", 0, 1),
            C("", "#CEC9E6", 1, 1),
            C("", "#C7CDE3", 2, 1),
            C("", "#C7E7FA", 3, 1),
            C("", "#C6F1D7", 4, 1),
            C("", "#C6DAC7", 5, 1),
            C("", "#D8EDC6", 6, 1),
            C("", "#F8F9C7", 7, 1),
            C("", "#F9D7C7", 8, 1),
            C("", "#ECC8C7", 9, 1),
            C("", "#E2CFB0", 10, 1),
            C("Yellow", "#FCFF00", 11, 1),
            // ===== Row 2 (y=2) =====
            C("", "#D5D5D3", 0, 2),
            C("", "#A493D3", 1, 2),
            C("", "#919ED1", 2, 2),
            C("", "#91D6FA", 3, 2),
            C("", "#8FE6B8", 4, 2),
            C("", "#91BD93", 5, 2),
            C("", "#BAE192", 6, 2),
            C("", "#F7F591", 7, 2),
            C("", "#F7B492", 8, 2),
            C("", "#DF9691", 9, 2),
            C("", "#C7A976", 10, 2),
            C("Lime", "#FCFF00", 11, 2),
            // ===== Row 3 (y=3) =====
            C("", "#BCBCBC", 0, 3),
            C("", "#6200C1", 1, 3),
            C("", "#004BBC", 2, 3),
            C("", "#0BC2FA", 3, 3),
            C("", "#03DA91", 4, 3),
            C("", "#009614", 5, 3),
            C("", "#90D314", 6, 3),
            C("", "#F6F000", 7, 3),
            C("", "#F48400", 8, 3),
            C("", "#D12700", 9, 3),
            C("", "#8E620C", 10, 3),
            C("Cyan", "#0FFFFC", 11, 3),
            // ===== Row 4 (y=4) =====
            C("", "#999C99", 0, 4),
            C("", "#5201A5", 1, 4),
            C("", "#0040A1", 2, 4),
            C("", "#04A6D5", 3, 4),
            C("", "#02BC7A", 4, 4),
            C("", "#02800D", 5, 4),
            C("", "#7BB40B", 6, 4),
            C("", "#D3CE00", 7, 4),
            C("", "#D27000", 8, 4),
            C("", "#B32300", 9, 4),
            C("", "#754100", 10, 4),
            C("Blue", "#0000FC", 11, 4),
            // ===== Row 5 (y=5) =====
            C("", "#727272", 0, 5),
            C("", "#400081", 1, 5),
            C("", "#00317E", 2, 5),
            C("", "#0582A7", 3, 5),
            C("", "#039360", 4, 5),
            C("", "#00650C", 5, 5),
            C("", "#628E0C", 6, 5),
            C("", "#A5A200", 7, 5),
            C("", "#A45800", 8, 5),
            C("", "#8E1600", 9, 5),
            C("", "#5A380C", 10, 5),
            C("Purple", "#8600FD", 11, 5),
            // ===== Row 6 (y=6) =====
            C("Black", "#000000", 0, 6),
            C("", "#1F0047", 1, 6),
            C("", "#001647", 2, 6),
            C("", "#01495E", 3, 6),
            C("", "#005433", 4, 6),
            C("", "#003800", 5, 6),
            C("", "#345100", 6, 6),
            C("", "#5E5C00", 7, 6),
            C("", "#5E2E00", 8, 6),
            C("", "#4F0C00", 9, 6),
            C("", "#33210B", 10, 6),
            C("Magenta", "#FD02C1", 11, 6),
        ];

        public ColourPalette(ISwitchOutput outputSink)
        {
            _output = outputSink;
        }

        // Helper function that takes in an image and returns a preview of it
        // ran through the IImageQuantizer of their choosing.
        public SKBitmap PreviewColourMapping(SKBitmap source, string quantizerName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(quantizerName);
            ArgumentNullException.ThrowIfNull(source);
            var quantizer = Quantizers[quantizerName](Colours);
            var result = new SKBitmap(source.Width, source.Height);

            // TODO: GetPixel is slow
            for (int x = 0; x < source.Width; x++)
            {
                for (int y = 0; y < source.Height; y++)
                {
                    var before = source.GetPixel(x, y);
                    if (before.Alpha < 128)
                    {
                        result.SetPixel(x, y, new SKColor(0, 0, 0, 0));
                        continue;
                    }
                    var after = quantizer.FindClosestColour(before.Red, before.Green, before.Blue);
                    result.SetPixel(x, y, after.skColor);
                }
            }
            return result;
        }

        /// <summary>Map an image to PaletteColours</summary>
        /// <returns>2D array of PaletteColour? [x, y] of the appropriate colours.</returns>
        public PaletteColour?[,] QuantizeImage(SKBitmap source, string quantizerName)
        {
            var quantizer = Quantizers[quantizerName](Colours);
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

            // Move to right spot, from the
            int deltaX = target.GridX - _lastGridX;
            int deltaY = target.GridY - _lastGridY;

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

            _lastGridX = target.GridX;
            _lastGridY = target.GridY;
        }
    }
}
