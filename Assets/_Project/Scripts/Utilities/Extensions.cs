using UnityEngine;

namespace ElementalSiege.Utilities
{
    /// <summary>
    /// Extension methods for common Unity types used throughout Elemental Siege.
    /// </summary>
    public static class Extensions
    {
        // ──────────────────────────────────────────────
        //  Vector2
        // ──────────────────────────────────────────────

        /// <summary>
        /// Rotates a Vector2 by the given angle in degrees (counter-clockwise).
        /// </summary>
        /// <param name="v">The vector to rotate.</param>
        /// <param name="degrees">Rotation angle in degrees.</param>
        /// <returns>The rotated vector.</returns>
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

        /// <summary>
        /// Returns a copy of the vector with the X component replaced.
        /// </summary>
        public static Vector2 WithX(this Vector2 v, float x)
        {
            return new Vector2(x, v.y);
        }

        /// <summary>
        /// Returns a copy of the vector with the Y component replaced.
        /// </summary>
        public static Vector2 WithY(this Vector2 v, float y)
        {
            return new Vector2(v.x, y);
        }

        // ──────────────────────────────────────────────
        //  Transform
        // ──────────────────────────────────────────────

        /// <summary>
        /// Destroys all child GameObjects of this transform.
        /// Safe to call during gameplay (uses Object.Destroy, not DestroyImmediate).
        /// </summary>
        public static void DestroyChildren(this Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(t.GetChild(i).gameObject);
            }
        }

        // ──────────────────────────────────────────────
        //  Rigidbody2D
        // ──────────────────────────────────────────────

        /// <summary>
        /// Applies an explosion-style radial force to a Rigidbody2D.
        /// Force falls off linearly with distance from the explosion centre.
        /// </summary>
        /// <param name="rb">The rigidbody to push.</param>
        /// <param name="force">Maximum force magnitude at the explosion centre.</param>
        /// <param name="position">World-space centre of the explosion.</param>
        /// <param name="radius">Radius beyond which no force is applied.</param>
        public static void AddExplosionForce(this Rigidbody2D rb, float force, Vector2 position, float radius)
        {
            Vector2 direction = (rb.position - position);
            float distance = direction.magnitude;

            if (distance > radius || distance < 0.001f)
                return;

            float falloff = 1f - (distance / radius);
            Vector2 forceVector = direction.normalized * (force * falloff);
            rb.AddForce(forceVector, ForceMode2D.Impulse);
        }

        // ──────────────────────────────────────────────
        //  float
        // ──────────────────────────────────────────────

        /// <summary>
        /// Remaps a float value from one range to another.
        /// </summary>
        /// <param name="value">The value to remap.</param>
        /// <param name="fromMin">Source range minimum.</param>
        /// <param name="fromMax">Source range maximum.</param>
        /// <param name="toMin">Target range minimum.</param>
        /// <param name="toMax">Target range maximum.</param>
        /// <returns>The remapped value (not clamped).</returns>
        public static float Remap(this float value, float fromMin, float fromMax, float toMin, float toMax)
        {
            if (Mathf.Approximately(fromMax, fromMin))
            {
                Debug.LogWarning("[Extensions] Remap called with zero-width source range. Returning toMin.");
                return toMin;
            }

            float t = (value - fromMin) / (fromMax - fromMin);
            return toMin + t * (toMax - toMin);
        }
    }
}
