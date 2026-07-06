using System.Diagnostics;
using Google.OrTools.ConstraintSolver;
using TomodachiDrawer.Core.Models;

namespace TomodachiDrawer.Core
{
    // All TSP routing logic, recommended values, and fallbacks and shortcuts.
    public partial class CanvasDrawer
    {
        private const int ExactMaxPoints = 16;

        // Early-exit (improvement limit) tuning. See PerformTSP.
        // Testing seemed to suggest this provided atleast some improvements, not for all image sizes or point counts.
        // I genuinely have no idea what these numbers mean. 
        private const double EarlyExitRateCoefficient = 0.05;
        private const int EarlyExitSolutionsDistance = 10;

        public static float GetRecommendedTSPSolveTime(int width, int height)
        {
            const int squared64 = 64 * 64;
            const int squared128 = 128 * 128;
            const int squared192 = 192 * 192;
            const int squared256 = 256 * 256;

            int pixels = width * height;
            if (pixels <= squared64)
                return 1.0f;
            else if (pixels <= squared128)
                return 3.0f;
            else if (pixels <= squared192)
                return 4.0f;
            else if (pixels <= squared256)
                return 5.0f;
            else
            {
                return 5.0f; // should never reach here...
            }
        }

        // common distance function.
        private static int Chebyshev(CanvasPoint a, CanvasPoint b) =>
            Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

        /// <summary>Index of the point nearest the current cursor, used as the route start.</summary>
        private int NearestIndexToCursor(CanvasPoint[] points)
        {
            int best = 0;
            var bestDist = MeasureDistanceToFromCurrent(points[0].X, points[0].Y);
            for (int i = 1; i < points.Length; i++)
            {
                var d = MeasureDistanceToFromCurrent(points[i].X, points[i].Y);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = i;
                }
            }
            return best;
        }

        // Held Karp TSP solver.
        // Full disclosure, this ONE function is written by AI as i was struggling to get it to work.
        // However this is a fairly standard algorithm so i don't feel too bad about this particular case but I nonetheless believe
        // in being transparent about it.
        // This is for brute-force-ish solving low point count things 100% optimally, really quickly.
        private List<CanvasPoint> HeldKarpRoute(CanvasPoint[] points)
        {
#if DEBUG
            var sw = Stopwatch.StartNew();
#endif
            int n = points.Length;
            int start = NearestIndexToCursor(points);

            // Relabel so the start is node 0, keeps the DP masks simple.
            var nodes = new CanvasPoint[n];
            nodes[0] = points[start];
            for (int i = 0, w = 1; i < n; i++)
                if (i != start)
                    nodes[w++] = points[i];

            int full = 1 << n;
            const int INF = int.MaxValue / 2;
            var dp = new int[full, n]; // dp[mask, j] = cheapest path from start visiting mask, ending at j
            var parent = new int[full, n];

            for (int mask = 0; mask < full; mask++)
                for (int j = 0; j < n; j++)
                {
                    dp[mask, j] = INF;
                    parent[mask, j] = -1;
                }

            dp[1, 0] = 0; // just the start

            for (int mask = 1; mask < full; mask++)
            {
                if ((mask & 1) == 0)
                    continue; // every path includes the start
                for (int j = 0; j < n; j++)
                {
                    if (dp[mask, j] == INF || (mask & (1 << j)) == 0)
                        continue;
                    for (int k = 0; k < n; k++)
                    {
                        if ((mask & (1 << k)) != 0)
                            continue;
                        int next = mask | (1 << k);
                        int cost = dp[mask, j] + Chebyshev(nodes[j], nodes[k]);
                        if (cost < dp[next, k])
                        {
                            dp[next, k] = cost;
                            parent[next, k] = j;
                        }
                    }
                }
            }

            // Cheapest endpoint over the full set (open path, no return to start).
            int last = 0,
                bestCost = INF;
            for (int j = 0; j < n; j++)
                if (dp[full - 1, j] < bestCost)
                {
                    bestCost = dp[full - 1, j];
                    last = j;
                }

            // Walk parents back to rebuild the order.
            var order = new CanvasPoint[n];
            int m = full - 1;
            for (int idx = n - 1; idx >= 0; idx--)
            {
                order[idx] = nodes[last];
                int prev = parent[m, last];
                m ^= 1 << last;
                last = prev;
            }

#if DEBUG
            _log($"\tHeld-Karp TSP took {sw.ElapsedMilliseconds}ms");
#endif

            return order.ToList();
        }

        /// <summary>Greedy nearest-neighbour routing. Last-resort fallback for when OrTools returns no
        /// solution at all (very large layers).</summary>
        private List<CanvasPoint> NearestNeighbourRoute(List<CanvasPoint> inputPoints)
        {
#if DEBUG
            var sw = Stopwatch.StartNew();
#endif
            var points = inputPoints.ToArray();

            var ordered = new List<CanvasPoint>(points.Length);

            if (inputPoints.Count == 0)
            {
                return ordered;
            }
            else if (inputPoints.Count == 1)
            {
                ordered.Add(points[0]);
                return ordered;
            }

            // We are just going to go to the nearest point repeatedly.
            var currentIndex = NearestIndexToCursor(points);
            ordered.Add(points[currentIndex]);
            var visited = new bool[points.Length];
            visited[currentIndex] = true;

            for (int i = 0; i < points.Length - 1; i++)
            {
                var cur = points[currentIndex];
                int nearestIndex = -1;
                int nearestDist = int.MaxValue;

                for (int j = 0; j < points.Length; j++)
                {
                    if (visited[j])
                        continue;
                    int dist = Math.Max(
                        Math.Abs(points[j].X - cur.X),
                        Math.Abs(points[j].Y - cur.Y)
                    );
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearestIndex = j;
                    }
                }

                visited[nearestIndex] = true;
                ordered.Add(points[nearestIndex]);
                currentIndex = nearestIndex;
            }
#if DEBUG
            sw.Stop();
            _log($"\tNearest-neighbour TSP took {sw.ElapsedMilliseconds}ms");
#endif

            return ordered;
        }

        /// <summary>Routes a set of points into a draw order. Brute-forced for tiny layers, OrTools for the
        /// rest, with a nearest-neighbour fallback if OrTools returns no solution (huge layers). Always
        /// returns a usable route.</summary>
        private List<CanvasPoint> PerformTSP(List<CanvasPoint> inputPoints, float timeLimitSeconds)
        {
            var points = inputPoints.ToArray();

            if (points.Length <= 1)
                return inputPoints.ToList();

            // Small enough to solve exactly - faster and better than the heuristic, and dodges the
            // improvement limit being flaky on tiny instances.
            if (points.Length <= ExactMaxPoints)
                return HeldKarpRoute(points);

            int closestPointIndex = NearestIndexToCursor(points);

            using var manager = new RoutingIndexManager(points.Length, 1, closestPointIndex);
            using var routing = new RoutingModel(manager);

            int transitCallbackIndex = routing.RegisterTransitCallback(
                (fromIndex, toIndex) =>
                {
                    var fromNode = manager.IndexToNode(fromIndex);
                    var toNode = manager.IndexToNode(toIndex);
                    // A note: during testing I made a change trying to incentivize adjacent things
                    // since it can just hold A during... but the lowest value this can return is 1
                    // so there was no gain, it was already trying to do that lol.
                    return Math.Max(
                        Math.Abs(points[fromNode].X - points[toNode].X),
                        Math.Abs(points[fromNode].Y - points[toNode].Y)
                    );
                }
            );

            routing.SetArcCostEvaluatorOfAllVehicles(transitCallbackIndex);

            var searchParameters =
                operations_research_constraint_solver.DefaultRoutingSearchParameters();
            searchParameters.FirstSolutionStrategy = FirstSolutionStrategy
                .Types
                .Value
                .PathCheapestArc;
            searchParameters.LocalSearchMetaheuristic = LocalSearchMetaheuristic
                .Types
                .Value
                .GuidedLocalSearch;
            // need to get int seconds and int nanoseconds because... google.
            int seconds = (int)timeLimitSeconds;
            int nanoseconds = (int)((timeLimitSeconds - seconds) * 1_000_000_000);
            searchParameters.TimeLimit = new Google.Protobuf.WellKnownTypes.Duration
            {
                Seconds = seconds,
                Nanos = nanoseconds,
            };

            // These options, when both configured, enable early exits.
            // From testing the defaults SEEM to be decent, but theres a sore point around 16-40 points where
            // it often won't settle.
            if (_earlyExitEnabled)
                searchParameters.ImprovementLimitParameters =
                    new RoutingSearchParameters.Types.ImprovementSearchLimitParameters
                    {
                        ImprovementRateCoefficient = _earlyExitRateCoefficient,
                        ImprovementRateSolutionsDistance = _earlyExitSolutionsDistance,
                    };

            var sw = Stopwatch.StartNew();
            var solution = routing.SolveWithParameters(searchParameters);
            sw.Stop();

            if (solution is null)
            {
                _log(
                    $"\tTSP found no solution for {points.Length} pts in {sw.ElapsedMilliseconds}ms, "
                        + "falling back to nearest-neighbour."
                );
                return NearestNeighbourRoute(inputPoints);
            }

            float timeLimitMs = timeLimitSeconds * 1000.0f;
            bool hitCeiling = sw.ElapsedMilliseconds >= timeLimitMs * 0.95f;
            if (!hitCeiling && _earlyExitEnabled)
                _log($"\tTSP converged early ({sw.ElapsedMilliseconds}ms vs {timeLimitMs:0}ms)");

            var optimizedRoute = new List<CanvasPoint>(points.Length);
            long index = routing.Start(0);
            while (routing.IsEnd(index) == false)
            {
                optimizedRoute.Add(points[manager.IndexToNode(index)]);
                index = solution.Value(routing.NextVar(index));
            }

            return optimizedRoute;
        }
    }
}
