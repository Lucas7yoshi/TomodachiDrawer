using SkiaSharp;

namespace TomodachiDrawer.Core.ImageProcessing.Quantizers
{
    /// <summary>
    /// This pisses me off to write, but this is not my code annoyingly.
    /// I was trying to find some resources, but they all seemed suboptimal, or were literally 14 years old and probably sucked
    /// and ran like ass. This was AI generated, and it annoys me. If theres a human made library out there that solves this problem
    /// I am more than welcome to replacing this with that. For now, this is quarantined in here.
    /// 
    /// This particularly annoys me because I wanted .Core to be slop-free bweh
    /// 
    /// - Lucas7yoshi
    /// </summary>
    public class ArbitraryColourQuantizer
    {
        public static SKBitmap Quantize(SKBitmap input, int colourCount, bool useDithering = true, int maxIterations = 20)
        {
            if (colourCount < 1) throw new ArgumentOutOfRangeException(nameof(colourCount), "Colour count must be at least 1.");

            SKColor[] pixels = input.Pixels;

            // 1. Separate visible pixels from fully transparent pixels
            // (If you want hard/binary edges without semi-transparency, change to `p.Alpha >= 128`)
            SKColor[] visiblePixels = pixels.Where(p => p.Alpha > 0).ToArray();
            bool hasTransparency = visiblePixels.Length < pixels.Length;

            // If the image is entirely transparent, return immediately
            if (visiblePixels.Length == 0) return input.Copy();

            // 2. Adjust target colour count to accommodate the reserved Transparent slot
            int k = hasTransparency ? colourCount - 1 : colourCount;

            // Edge case: They requested exactly 1 colour, and the image has transparency.
            // We must return a fully transparent image to stay within the 1-colour limit.
            if (k <= 0)
            {
                SKBitmap empty = new SKBitmap(input.Info);
                SKColor[] emptyPixels = new SKColor[pixels.Length];
                Array.Fill(emptyPixels, SKColors.Transparent);
                empty.Pixels = emptyPixels;
                return empty;
            }

            // 3. Initialize and run K-Means ONLY on the visible pixels
            List<SKColor> palette = InitializeCentroidsKMeansPlusPlus(visiblePixels, k);

            int[] assignments = new int[visiblePixels.Length];
            bool changed = true;
            int iteration = 0;

            while (changed && iteration < maxIterations)
            {
                changed = false;
                iteration++;

                long[] rSum = new long[k];
                long[] gSum = new long[k];
                long[] bSum = new long[k];
                long[] aSum = new long[k];
                int[] counts = new int[k];

                for (int i = 0; i < visiblePixels.Length; i++)
                {
                    int bestIndex = FindNearestColourIndex(visiblePixels[i], palette);

                    if (assignments[i] != bestIndex)
                    {
                        assignments[i] = bestIndex;
                        changed = true;
                    }

                    rSum[bestIndex] += visiblePixels[i].Red;
                    gSum[bestIndex] += visiblePixels[i].Green;
                    bSum[bestIndex] += visiblePixels[i].Blue;
                    aSum[bestIndex] += visiblePixels[i].Alpha;
                    counts[bestIndex]++;
                }

                for (int c = 0; c < k; c++)
                {
                    if (counts[c] > 0)
                    {
                        palette[c] = new SKColor(
                            (byte)(rSum[c] / counts[c]),
                            (byte)(gSum[c] / counts[c]),
                            (byte)(bSum[c] / counts[c]),
                            (byte)(aSum[c] / counts[c])
                        );
                    }
                }
            }

            // 4. Map the original image back to the palette
            SKColor[] resultPixels = new SKColor[pixels.Length];
            int width = input.Width;
            int height = input.Height;

            float[]? rBuf = null, gBuf = null, bBuf = null, aBuf = null;
            if (useDithering)
            {
                rBuf = pixels.Select(p => (float)p.Red).ToArray();
                gBuf = pixels.Select(p => (float)p.Green).ToArray();
                bBuf = pixels.Select(p => (float)p.Blue).ToArray();
                aBuf = pixels.Select(p => (float)p.Alpha).ToArray();
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int idx = y * width + x;

                    // --- STRICT TRANSPARENCY CHECK ---
                    // If the original pixel was fully transparent, keep it fully transparent.
                    // We check the ORIGINAL pixel, not the dither buffer, so bleeding error is ignored.
                    if (pixels[idx].Alpha == 0)
                    {
                        resultPixels[idx] = SKColors.Transparent;
                        continue; // Skip dithering error calculations entirely
                    }

                    if (!useDithering)
                    {
                        resultPixels[idx] = palette[FindNearestColourIndex(pixels[idx], palette)];
                    }
                    else if (rBuf != null && gBuf != null && bBuf != null && aBuf != null)
                    {
                        float r = Math.Clamp(rBuf[idx], 0f, 255f);
                        float g = Math.Clamp(gBuf[idx], 0f, 255f);
                        float b = Math.Clamp(bBuf[idx], 0f, 255f);
                        float a = Math.Clamp(aBuf[idx], 0f, 255f);

                        int bestIndex = FindNearestColourIndex(r, g, b, a, palette);
                        SKColor newColour = palette[bestIndex];
                        resultPixels[idx] = newColour;

                        float errR = r - newColour.Red;
                        float errG = g - newColour.Green;
                        float errB = b - newColour.Blue;
                        float errA = a - newColour.Alpha;

                        DistributeError(rBuf, gBuf, bBuf, aBuf, x + 1, y, width, height, errR, errG, errB, errA, 7f / 16f);
                        DistributeError(rBuf, gBuf, bBuf, aBuf, x - 1, y + 1, width, height, errR, errG, errB, errA, 3f / 16f);
                        DistributeError(rBuf, gBuf, bBuf, aBuf, x, y + 1, width, height, errR, errG, errB, errA, 5f / 16f);
                        DistributeError(rBuf, gBuf, bBuf, aBuf, x + 1, y + 1, width, height, errR, errG, errB, errA, 1f / 16f);
                    }
                    else
                        throw new Exception("This should never be hit, the previous if stuff is to silence some warnings.");
                }
            }

            SKBitmap result = new SKBitmap(input.Info);
            result.Pixels = resultPixels;
            return result;
        }

        private static List<SKColor> InitializeCentroidsKMeansPlusPlus(SKColor[] pixels, int k)
        {
            var centroids = new List<SKColor>(k);
            var random = new Random();

            centroids.Add(pixels[random.Next(pixels.Length)]);

            long[] distances = new long[pixels.Length];
            Array.Fill(distances, long.MaxValue);

            for (int i = 1; i < k; i++)
            {
                long totalDistance = 0;
                SKColor lastCentroid = centroids[i - 1];

                for (int p = 0; p < pixels.Length; p++)
                {
                    long dist = ColourDistance(pixels[p], lastCentroid);
                    if (dist < distances[p]) distances[p] = dist;
                    totalDistance += distances[p];
                }

                long randomValue = (long)(random.NextDouble() * totalDistance);
                long runningSum = 0;

                for (int p = 0; p < pixels.Length; p++)
                {
                    runningSum += distances[p];
                    if (runningSum >= randomValue)
                    {
                        centroids.Add(pixels[p]);
                        break;
                    }
                }
            }

            return centroids;
        }

        private static int FindNearestColourIndex(SKColor target, List<SKColor> palette)
        {
            int bestIdx = 0;
            long bestDist = long.MaxValue;

            for (int i = 0; i < palette.Count; i++)
            {
                long dist = ColourDistance(target, palette[i]);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIdx = i;
                }
            }
            return bestIdx;
        }

        private static int FindNearestColourIndex(float r, float g, float b, float a, List<SKColor> palette)
        {
            int bestIdx = 0;
            float bestDist = float.MaxValue;

            for (int i = 0; i < palette.Count; i++)
            {
                float dr = r - palette[i].Red;
                float dg = g - palette[i].Green;
                float db = b - palette[i].Blue;
                float da = a - palette[i].Alpha;

                float dist = (dr * dr * 3) + (dg * dg * 4) + (db * db * 2) + (da * da);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIdx = i;
                }
            }
            return bestIdx;
        }

        private static long ColourDistance(SKColor c1, SKColor c2)
        {
            long dr = c1.Red - c2.Red;
            long dg = c1.Green - c2.Green;
            long db = c1.Blue - c2.Blue;
            long da = c1.Alpha - c2.Alpha;

            return (dr * dr * 3) + (dg * dg * 4) + (db * db * 2) + (da * da);
        }

        private static void DistributeError(float[] rBuf, float[] gBuf, float[] bBuf, float[] aBuf,
                                            int x, int y, int width, int height,
                                            float errR, float errG, float errB, float errA, float weight)
        {
            if (x < 0 || x >= width || y < 0 || y >= height) return;

            int idx = y * width + x;
            rBuf[idx] += errR * weight;
            gBuf[idx] += errG * weight;
            bBuf[idx] += errB * weight;
            aBuf[idx] += errA * weight;
        }
    }
}
