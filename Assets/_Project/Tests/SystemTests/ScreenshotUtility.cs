using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace ElementalSiege.Tests.SystemTests
{
    /// <summary>
    /// Result of a screenshot comparison.
    /// </summary>
    public class ScreenshotResult
    {
        public float MatchPercent { get; set; }
        public Texture2D DiffImage { get; set; }
        public bool Passed { get; set; }
    }

    /// <summary>
    /// Utility for capturing and comparing screenshots during system tests.
    /// </summary>
    public static class ScreenshotUtility
    {
        private static readonly string ScreenshotBasePath =
            Path.Combine(Application.dataPath, "_Project", "Tests", "Screenshots");

        /// <summary>
        /// Returns the standardized screenshot path for the given test name.
        /// </summary>
        public static string GetScreenshotPath(string name)
        {
            if (!Directory.Exists(ScreenshotBasePath))
            {
                Directory.CreateDirectory(ScreenshotBasePath);
            }

            return Path.Combine(ScreenshotBasePath, name);
        }

        /// <summary>
        /// Captures the current screen as a PNG and saves it with a timestamped filename.
        /// </summary>
        /// <param name="testName">Identifier for the test producing this screenshot.</param>
        /// <returns>The full path to the saved screenshot file.</returns>
        public static string CaptureScreenshot(string testName)
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            string filename = $"{testName}_{timestamp}.png";
            string fullPath = GetScreenshotPath(filename);

            ScreenCapture.CaptureScreenshot(fullPath);
            Debug.Log($"[ScreenshotUtility] Screenshot saved: {fullPath}");

            return fullPath;
        }

        /// <summary>
        /// Captures the screen into a Texture2D (reads pixels from the current frame).
        /// Must be called at the end of a frame (after rendering).
        /// </summary>
        /// <returns>A new Texture2D containing the current screen contents.</returns>
        public static Texture2D CaptureScreenToTexture()
        {
            var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Compares two screenshots pixel-by-pixel within a given tolerance.
        /// </summary>
        /// <param name="actual">The screenshot captured during the test.</param>
        /// <param name="reference">The reference/baseline screenshot.</param>
        /// <param name="tolerance">Per-channel tolerance (0.0 = exact, 1.0 = anything matches).</param>
        /// <returns>A ScreenshotResult with match percentage, diff image, and pass/fail.</returns>
        public static ScreenshotResult CompareScreenshots(Texture2D actual, Texture2D reference, float tolerance = 0.01f)
        {
            if (actual.width != reference.width || actual.height != reference.height)
            {
                Debug.LogWarning(
                    $"[ScreenshotUtility] Size mismatch: actual={actual.width}x{actual.height}, " +
                    $"reference={reference.width}x{reference.height}");
                return new ScreenshotResult
                {
                    MatchPercent = 0f,
                    DiffImage = null,
                    Passed = false
                };
            }

            Color[] actualPixels = actual.GetPixels();
            Color[] referencePixels = reference.GetPixels();
            int totalPixels = actualPixels.Length;
            int matchingPixels = 0;

            var diffTex = new Texture2D(actual.width, actual.height, TextureFormat.RGB24, false);
            Color[] diffPixels = new Color[totalPixels];

            for (int i = 0; i < totalPixels; i++)
            {
                float rDiff = Mathf.Abs(actualPixels[i].r - referencePixels[i].r);
                float gDiff = Mathf.Abs(actualPixels[i].g - referencePixels[i].g);
                float bDiff = Mathf.Abs(actualPixels[i].b - referencePixels[i].b);

                bool pixelMatches = rDiff <= tolerance && gDiff <= tolerance && bDiff <= tolerance;

                if (pixelMatches)
                {
                    matchingPixels++;
                    diffPixels[i] = Color.black;
                }
                else
                {
                    // Highlight differences in red, intensity proportional to difference
                    float maxDiff = Mathf.Max(rDiff, Mathf.Max(gDiff, bDiff));
                    diffPixels[i] = new Color(maxDiff, 0f, 0f, 1f);
                }
            }

            diffTex.SetPixels(diffPixels);
            diffTex.Apply();

            float matchPercent = (float)matchingPixels / totalPixels;

            return new ScreenshotResult
            {
                MatchPercent = matchPercent,
                DiffImage = diffTex,
                Passed = matchPercent >= (1f - tolerance)
            };
        }

        /// <summary>
        /// Generates a diff image highlighting differences between two textures in red.
        /// </summary>
        public static Texture2D HighlightDifferences(Texture2D a, Texture2D b)
        {
            var result = CompareScreenshots(a, b, 0.01f);
            return result.DiffImage;
        }

        /// <summary>
        /// Coroutine that waits for the specified number of frames to ensure rendering completes.
        /// </summary>
        /// <param name="frames">Number of frames to wait (default 3).</param>
        public static IEnumerator WaitForRender(int frames = 3)
        {
            for (int i = 0; i < frames; i++)
            {
                yield return new WaitForEndOfFrame();
            }
        }

        /// <summary>
        /// Saves a Texture2D to disk as a PNG file.
        /// </summary>
        public static string SaveTexture(Texture2D texture, string testName)
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            string filename = $"{testName}_{timestamp}.png";
            string fullPath = GetScreenshotPath(filename);

            byte[] pngData = texture.EncodeToPNG();
            File.WriteAllBytes(fullPath, pngData);
            Debug.Log($"[ScreenshotUtility] Texture saved: {fullPath}");

            return fullPath;
        }
    }
}
