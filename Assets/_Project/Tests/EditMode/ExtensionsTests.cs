using NUnit.Framework;
using UnityEngine;

namespace ElementalSiege.Tests.EditMode
{
    [TestFixture]
    public class ExtensionsTests
    {
        private const float Epsilon = 0.0001f;

        [Test]
        public void Vector2_Rotate_90Degrees_Correct()
        {
            Vector2 original = new Vector2(1f, 0f);
            Vector2 rotated = original.Rotate(90f);

            Assert.AreEqual(0f, rotated.x, Epsilon,
                "Rotating (1,0) by 90 degrees should give x ~= 0");
            Assert.AreEqual(1f, rotated.y, Epsilon,
                "Rotating (1,0) by 90 degrees should give y ~= 1");
        }

        [Test]
        public void Vector2_Rotate_360Degrees_ReturnsOriginal()
        {
            Vector2 original = new Vector2(3f, 4f);
            Vector2 rotated = original.Rotate(360f);

            Assert.AreEqual(original.x, rotated.x, Epsilon,
                "360-degree rotation should return original x");
            Assert.AreEqual(original.y, rotated.y, Epsilon,
                "360-degree rotation should return original y");
        }

        [Test]
        public void Vector2_WithX_ReplacesXOnly()
        {
            Vector2 original = new Vector2(5f, 10f);
            Vector2 modified = original.WithX(99f);

            Assert.AreEqual(99f, modified.x, Epsilon, "WithX should replace the x component");
            Assert.AreEqual(10f, modified.y, Epsilon, "WithX should not modify the y component");
        }

        [Test]
        public void Vector2_WithY_ReplacesYOnly()
        {
            Vector2 original = new Vector2(5f, 10f);
            Vector2 modified = original.WithY(99f);

            Assert.AreEqual(5f, modified.x, Epsilon, "WithY should not modify the x component");
            Assert.AreEqual(99f, modified.y, Epsilon, "WithY should replace the y component");
        }

        [Test]
        public void Float_Remap_MapsCorrectly()
        {
            // Remap 5 from [0,10] to [0,100] => 50
            float result = 5f.Remap(0f, 10f, 0f, 100f);

            Assert.AreEqual(50f, result, Epsilon,
                "Remapping 5 from [0,10] to [0,100] should produce 50");
        }

        [Test]
        public void Float_Remap_ClampsToRange()
        {
            // Remap 15 from [0,10] to [0,100], clamped => 100
            float result = 15f.Remap(0f, 10f, 0f, 100f, clamp: true);

            Assert.AreEqual(100f, result, Epsilon,
                "Remapping a value above the source range with clamping should clamp to the target max");

            // Remap -5 from [0,10] to [0,100], clamped => 0
            float resultBelow = (-5f).Remap(0f, 10f, 0f, 100f, clamp: true);

            Assert.AreEqual(0f, resultBelow, Epsilon,
                "Remapping a value below the source range with clamping should clamp to the target min");
        }
    }

    /// <summary>
    /// Extension methods for Vector2 and float. Replace with actual utility references.
    /// </summary>
    public static class GameExtensions
    {
        public static Vector2 Rotate(this Vector2 v, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            return new Vector2(
                v.x * cos - v.y * sin,
                v.x * sin + v.y * cos
            );
        }

        public static Vector2 WithX(this Vector2 v, float x)
        {
            return new Vector2(x, v.y);
        }

        public static Vector2 WithY(this Vector2 v, float y)
        {
            return new Vector2(v.x, y);
        }

        public static float Remap(this float value, float fromMin, float fromMax, float toMin, float toMax, bool clamp = false)
        {
            float t = (value - fromMin) / (fromMax - fromMin);
            if (clamp)
            {
                t = Mathf.Clamp01(t);
            }
            return Mathf.Lerp(toMin, toMax, t);
        }
    }
}
