using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using Google.OrTools.ConstraintSolver;

using SkiaSharp;

using TomodachiDrawer.Core.Interfaces;
using TomodachiDrawer.Core.Models;
using TomodachiDrawer.Core.OutputSinks;

namespace TomodachiDrawer.Core
{
    public class CanvasDrawer
    {
        public const int CanvasWidth = 256;
        public const int CanvasHeight = 256;

        private int _cursorX = 0;
        private int _cursorY = 0;

        private ISwitchOutput _realOutput;
        private ColourPalette _palette;
        private CanvasToolbar _toolbar;
        private readonly Action<string> _log;

        public CanvasDrawer(ISwitchOutput outputSink, Action<string>? logger = null)
        {
            _realOutput = outputSink;
            _palette = new ColourPalette(outputSink);
            _toolbar = new CanvasToolbar(outputSink);
            _log = logger ?? Console.WriteLine;
        }

        public async Task DrawImage(SKBitmap image, string quantizerName, float tspTimeLimit = 1.0f)
        {
            if (image.Width > CanvasWidth || image.Height > CanvasHeight)
                throw new InvalidDataException($"Image too big. Max is {CanvasWidth}x{CanvasHeight}.");
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(tspTimeLimit, 0.0f, nameof(tspTimeLimit));

            // Stages:
            // 1: Perform Color quantization to the tomodachi life pallete
            // 2: Split colors into distinct ColorLayers/passes (undecided on which)
            // 3: Find uniform areas of the same color for each brush size, ensuring to avoid ones already covered by a larger stamp.
            // 4: Fine detail pass for everything else, effectively subtracting from the colorpasses the stamps and then filling in those remaining
            // pixels. TSP-like optimization should be done here (alternative to snaking)

            // Other things: Color Pass Order Optimizations for both stamp pass and fine detail pass to minimize
            // travel distance. Need to look at the ai-slop version to try and figure out how that works there.

            // Also we need to pass in the quantization method and dithering settings as arguments.

            // Quantized Map is a 2D array of PaletteColours.
            var quantizedMap = _palette.QuantizeImage(image, quantizerName);

            // First off we are just putting all the individual details into the fine detail pass,
            // following passes will start to remove from that and add to the stamp passes.
            // TODO: This doesnt really make too much sense to be in the palette class... Maybe move here?
            var layers = _palette.BuildFineLayers(quantizedMap);

            // !!!
            // TODO: Stamp/Uniform area detection.
            // !!!

            double totalTimeSeconds = 0.0;

            var totalLayers = layers.Count;
            // 80% divided by total layers.
            var perLayerPercent = 80.0 / totalLayers;
            int layerNumber = 0;
            foreach (var l in layers)
            {
                layerNumber++;
                _palette.SelectColour(l.Colour, 25.0);


                // ============= Stamps. TODO ============
                // (like the actual drawing of the big brush goes here)

                // ============= Stamps. Todo. End. ======


                // ============= Fine details
                _toolbar.SelectBrush(1); // no-op if already selected.

                // Dry run both to get timing, TimingSink stores
                // the outputs and time taken so it can be replayed without needing to rerun
                // the tsp solve or snake logic (snake logic is compartively short but it also replays)
                int savedX = _cursorX;
                int savedY = _cursorY;

                var snakeSink = new TimingSink();
                FineDetailSnake(snakeSink, l);

                int afterSnakeX = _cursorX;
                int afterSnakeY = _cursorY;

                _cursorX = savedX;
                _cursorY = savedY;

                var tspSink = new TimingSink();
                FineDetailTsp(tspSink, l, tspTimeLimit);

                int afterTspX = _cursorX;
                int afterTspY = _cursorY;

                //_cursorX = savedX;
                //_cursorY = savedY;

                bool tspHasSolution = tspSink.TotalMilliseconds > 0;
                bool usedSnake = !tspHasSolution || snakeSink.TotalMilliseconds <= tspSink.TotalMilliseconds;
                if (usedSnake)
                {
                    snakeSink.ReplayTo(_realOutput);
                    totalTimeSeconds += snakeSink.TotalTime.TotalSeconds;
                    _cursorX = afterSnakeX;
                    _cursorY = afterSnakeY;
                }
                else
                {
                    tspSink.ReplayTo(_realOutput);
                    totalTimeSeconds += tspSink.TotalTime.TotalSeconds;
                    _cursorX = afterTspX;
                    _cursorY = afterTspY;
                }
                string tspPart = tspHasSolution ? $"{tspSink.TotalTime.TotalSeconds:F3}s" : "no solution";
                _log($"[{layerNumber}/{totalLayers}] {l.Colour.DisplayName}: snake={snakeSink.TotalTime.TotalSeconds:F3}s, tsp={tspPart} → {(usedSnake ? "snake" : "tsp")}");
            }
            _log($"Done! Total draw time: {totalTimeSeconds:F3}s");
        }

        private static readonly int[] LargeBrushSizes = [27, 19, 13, 7, 3];

        /// <summary>Takes in a ColourLayer and detects large areas that can be better drawn with stamps.</summary>
        /// <param name="l"></param>
        private void DetectUniformAreas(ColourLayer l)
        {
            // need to build a more useful 2d array for scanning since l.FineDetailPoints is uh well, just a hashset of points.
            var points = new bool[256,256];
            foreach (var p in l.FineDetailPoints)
                points[p.X, p.Y] = true;

            foreach (var brushSize in LargeBrushSizes)
            {
                int half = brushSize / 2; // rounds down. which is fine.
                // TODO: Pickup from here.
            }
        }

        private bool IsUniformArea(bool[,] map, int cx, int cy, int brushSize)
        {
            int half = brushSize / 2; // rounds down.
            for (int dy = -half; dy <= half; dy++)
            {
                for (int dx = -half; dx <= half; dx++)
                    if (!map[cy + dy, cx + dx])
                        return false;
            }

            return true;
        }

        private void FineDetailSnake(ISwitchOutput output, ColourLayer l)
        {

            // find the nearest edge.
            int topLeft = MeasureDistanceToFromCurrent(l.Extents.MinX, l.Extents.MinY);
            int topRight = MeasureDistanceToFromCurrent(l.Extents.MaxX, l.Extents.MinY);
            int bottomLeft = MeasureDistanceToFromCurrent(l.Extents.MinX, l.Extents.MaxY);
            int bottomRight = MeasureDistanceToFromCurrent(l.Extents.MaxX, l.Extents.MaxY);

            int bestDist = Math.Min(
                Math.Min(topLeft, topRight),
                Math.Min(bottomLeft, bottomRight)
            );

            bool goingDown = false;
            bool goingRight = false;

            // todo: probably a cleaner way to match.
            if (topLeft == bestDist)
            {
                NavigateTo(output, l.Extents.MinX, l.Extents.MinY);
                goingRight = true;
                goingDown = true;
            }
            else if (topRight == bestDist)
            {
                NavigateTo(output, l.Extents.MaxX, l.Extents.MinY);
                goingRight = false;
                goingDown = true;
            }
            else if (bottomLeft == bestDist)
            {
                NavigateTo(output, l.Extents.MinX, l.Extents.MaxY);
                goingRight = true;
                goingDown = false;
            }
            else // br
            {
                NavigateTo(output, l.Extents.MaxX, l.Extents.MaxY);
                goingRight = false;
                goingDown = false;
            }

            // <TODO: MILD AI SLOP, REVIEW!!!!
            int startY = goingDown ? l.Extents.MinY : l.Extents.MaxY;
            int endY = goingDown ? l.Extents.MaxY : l.Extents.MinY;
            int yStep = goingDown ? 1 : -1;

            for (int y = startY; goingDown ? y <= endY : y >= endY; y += yStep)
            {
                if (!l.FineDetailPoints.Any(p => p.Y == y))
                {
                    // If theres literally nothing remaining then we dont even bother going up or down
                    // in the event that doing so would get us further from the next point, it also just wastes
                    // up/down inputs
                    var isThereAnyAtAllLeft = l.FineDetailPoints.Any(p =>
                        goingDown ? p.Y > y : p.Y < y
                    );
                    if (y != endY && isThereAnyAtAllLeft)
                    {
                        if (goingDown)
                        {
                            output.Tap(DPad.DOWN);
                            _cursorY++;
                        }
                        else
                        {
                            output.Tap(DPad.UP);
                            _cursorY--;
                        }
                    }
                    // If there is !isThereAnyAtAllLeft we are just done.
                    continue;
                }

                // everything for the for loop
                // only goes left/right to the extents of the layer (TODO: Do just for the row! Would need to NavigateTo for first on next row tho)
                // and goes the correct direction, which is only flipped when we actually do a row.
                //int startX = goingRight ? l.Extents.MinX : l.Extents.MaxX;
                //int endX = goingRight ? l.Extents.MaxX : l.Extents.MinX;

                int startX, endX;
                if (goingRight)
                {
                    startX = l.FineDetailPoints.Where(p => p.Y == y).Min(p => p.X);
                    endX = l.FineDetailPoints.Where(p => p.Y == y).Max(p => p.X);
                }
                else
                {
                    startX = l.FineDetailPoints.Where(p => p.Y == y).Max(p => p.X);
                    endX = l.FineDetailPoints.Where(p => p.Y == y).Min(p => p.X);
                }

                int xStep = goingRight ? 1 : -1;
                bool holdingA = false;

                // since our x extents change each y layer, need to NavigateTo the start point
                var firstPoint = new CanvasPoint(startX, y);
                NavigateTo(output, firstPoint);

                for (int x = startX; goingRight ? x <= endX : x >= endX; x += xStep)
                {
                    bool isCurrentPoint = l.FineDetailPoints.Contains(new CanvasPoint(x, y));
                    if (isCurrentPoint && !holdingA)
                    {
                        output.Press(Button.A);
                        output.Delay(25.0);
                        holdingA = true;
                    }

                    if (x == endX)
                    {
                        if (holdingA)
                        {
                            output.Release(Button.A);
                            output.Delay(25.0);
                            holdingA = false;
                        }
                        break;
                    }

                    bool isNextPoint = l.FineDetailPoints.Contains(
                        new CanvasPoint(x + xStep, y)
                    );
                    if (holdingA && !isNextPoint)
                    {
                        output.Release(Button.A);
                        output.Delay(25.0);
                        holdingA = false;
                    }

                    if (goingRight)
                    {
                        output.Tap(DPad.RIGHT);
                        _cursorX++;
                    }
                    else
                    {
                        output.Tap(DPad.LEFT);
                        _cursorX--;
                    }
                }

                goingRight = !goingRight;

                if (y != endY)
                {
                    if (goingDown)
                    {
                        output.Tap(DPad.DOWN);
                        _cursorY++;
                    }
                    else
                    {
                        output.Tap(DPad.UP);
                        _cursorY--;
                    }
                }
            }
        }

        // TSP with Google.OrTools nuget package
        // https://developers.google.com/optimization/routing/tsp#c_1
        // It is possible for it to NOT find a solution.
        private void FineDetailTsp(ISwitchOutput output, ColourLayer l, float timeLimitSeconds)
        {
            // Find start point, this logic will need adjusted in time
            // when we eventually reorder layers to be the most optimal.

            var points = l.FineDetailPoints.ToArray();
            var closestPointIndex = 0;
            var closestPointDist = MeasureDistanceToFromCurrent(points[0].X, points[0].Y);
            for (int i = 0; i < points.Length; i++)
            {
                var p = points[i];
                var distance = MeasureDistanceToFromCurrent(p.X, p.Y);
                if (distance < closestPointDist)
                {
                    closestPointIndex = i;
                    closestPointDist = distance;
                }
            }


            var manager = new RoutingIndexManager(l.FineDetailPoints.Count, 1, closestPointIndex);
            var routing = new RoutingModel(manager);

            int transitCallbackIndex = routing.RegisterTransitCallback(
                (long fromIndex, long toIndex) =>
            {

                var fromNode = manager.IndexToNode(fromIndex);
                var toNode = manager.IndexToNode(toIndex);
                // return Math.Max(Math.Abs(_cursorX - targetX), Math.Abs(_cursorY - targetY));
                return Math.Max(
                    Math.Abs(points[fromNode].X - points[toNode].X),
                    Math.Abs(points[fromNode].Y - points[toNode].Y)
                );
            });

            routing.SetArcCostEvaluatorOfAllVehicles(transitCallbackIndex);

            var searchParameters = operations_research_constraint_solver.DefaultRoutingSearchParameters();
            searchParameters.FirstSolutionStrategy = FirstSolutionStrategy.Types.Value.PathCheapestArc;
            searchParameters.LocalSearchMetaheuristic = LocalSearchMetaheuristic.Types.Value.GuidedLocalSearch;
            // need to get int seconds and int nanoseconds because... google.
            int seconds = (int)timeLimitSeconds;
            int nanoseconds = (int)((timeLimitSeconds - seconds) * 1_000_000_000);
            searchParameters.TimeLimit = new Google.Protobuf.WellKnownTypes.Duration { Seconds = seconds, Nanos = nanoseconds };

            var sw = Stopwatch.StartNew();
            var solution = routing.SolveWithParameters(searchParameters);
            sw.Stop();

            if (solution is null)
                return;

            var optimizedRoute = new List<CanvasPoint>(points.Length);
            long index = routing.Start(0);
            while (routing.IsEnd(index) == false)
            {
                optimizedRoute.Add(points[manager.IndexToNode(index)]);
                index = solution.Value(routing.NextVar(index));
            }

            // Navigate through the optimised route.
            // A is held across consecutive points that are exactly 1 step apart (Chebyshev == 1).
            // For isolated points (no adjacent neighbour) a plain Tap is used.
            bool isAHeld = false;
            for (int idx = 0; idx < optimizedRoute.Count; idx++)
            {
                var point = optimizedRoute[idx];
                NavigateTo(output, point);

                bool nextIsAdjacent = idx + 1 < optimizedRoute.Count
                    && Math.Max(
                        Math.Abs(optimizedRoute[idx + 1].X - point.X),
                        Math.Abs(optimizedRoute[idx + 1].Y - point.Y)
                    ) == 1;

                if (isAHeld)
                {
                    // We arrived here via a 1-step NavigateTo with A held, which painted this cell.
                    if (!nextIsAdjacent)
                    {
                        output.Release(Button.A);
                        output.Delay(25.0);
                        isAHeld = false;
                    }
                    // else: next is also adjacent, keep holding
                }
                else if (nextIsAdjacent)
                {
                    // Start of an adjacent run — press and hold.
                    output.Press(Button.A);
                    output.Delay(25.0);
                    isAHeld = true;
                }
                else
                {
                    // Isolated point — plain tap, no hold.
                    output.Tap(Button.A);
                }
            }
        }

        // Commented out for now to avoid me accidentally not doing it correctly.
        //private void NavigateTo(int targetX, int targetY) => NavigateTo(_output, targetX, targetY);
        //private void NavigateX(int targetX) => NavigateX(_output, targetX);
        //private void NavigateY(int targetY) => NavigateY(_output, targetY);
        //private void NavigateTo(CanvasPoint p) => NavigateTo(_output, p.X, p.Y);

        private void NavigateTo(ISwitchOutput output, CanvasPoint p) => NavigateTo(output, p.X, p.Y);

        private void NavigateTo(ISwitchOutput output, int targetX, int targetY)
        {
            // Diaganols.
            while (_cursorX != targetX && _cursorY != targetY)
            {
                var dir = (_cursorX < targetX, _cursorY < targetY) switch
                {
                    (true, true) => DPad.DOWNRIGHT,
                    (true, false) => DPad.UPRIGHT,
                    (false, true) => DPad.DOWNLEFT,
                    (false, false) => DPad.UPLEFT,
                };

                output.Tap(dir);
                _cursorX += _cursorX < targetX ? 1 : -1;
                _cursorY += _cursorY < targetY ? 1 : -1;
            }

            // Finish off remainder.
            NavigateX(output, targetX);
            NavigateY(output, targetY);
        }

        private void NavigateX(ISwitchOutput output, int targetX)
        {
            while (_cursorX < targetX)
            {
                output.Tap(DPad.RIGHT);
                _cursorX++;
            }

            while (_cursorX > targetX)
            {
                output.Tap(DPad.LEFT);
                _cursorX--;
            }
        }

        private void NavigateY(ISwitchOutput output, int targetY)
        {
            while (_cursorY < targetY)
            {
                output.Tap(DPad.DOWN);
                _cursorY++;
            }

            while (_cursorY > targetY)
            {
                output.Tap(DPad.UP);
                _cursorY--;
            }
        }

        /// <summary>
        /// Calculates the number of button inputs to navigate to a point, without any A presses.
        /// <para>This is just the longest distance on axis since diagonal inputs negate any loss</para>
        /// </summary>
        /// <returns>Distance in button presses</returns>
        private int MeasureDistanceToFromCurrent(int targetX, int targetY)
        {
            return Math.Max(Math.Abs(_cursorX - targetX), Math.Abs(_cursorY - targetY));
        }

        public void ConnectAndConfirmController()
        {
            _realOutput.Tap(Button.A);
            _realOutput.Delay(1000);
            _realOutput.Tap(Button.A);
            _realOutput.Delay(500);
            _realOutput.Tap(Button.A, 500);
            _realOutput.Delay(1500);
        }
    }
}
