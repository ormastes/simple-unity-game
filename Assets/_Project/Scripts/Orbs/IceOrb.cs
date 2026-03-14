using UnityEngine;
using ElementalSiege.Elements;

namespace ElementalSiege.Orbs
{
    /// <summary>
    /// The Ice orb — flash-freezes objects in a radius, making them brittle and
    /// easy to shatter. Creates low-friction ice surfaces and can freeze water
    /// into solid platforms.
    /// </summary>
    public class IceOrb : OrbBase
    {
        [Header("Ice — Flash Freeze")]

        /// <summary>Radius of the flash freeze effect in world units.</summary>
        [SerializeField] private float freezeRadius = 3.5f;

        /// <summary>Duration in seconds that frozen objects remain brittle.</summary>
        [SerializeField] private float freezeDuration = 5f;

        /// <summary>
        /// Multiplier applied to joint break forces on frozen objects.
        /// Values less than 1.0 make joints weaker (more brittle).
        /// </summary>
        [SerializeField, Range(0.01f, 1f)] private float brittleBreakForceMultiplier = 0.2f;

        [Header("Ice Surface")]

        /// <summary>Physics material applied to surfaces touched by the freeze to reduce friction.</summary>
        [SerializeField] private PhysicsMaterial2D iceSurfaceMaterial;

        /// <summary>Prefab for the ice platform created when freezing water.</summary>
        [SerializeField] private GameObject icePlatformPrefab;

        [Header("Ice Effects")]

        /// <summary>Particle system prefab for the frost spread visual.</summary>
        [SerializeField] private GameObject frostSpreadPrefab;

        /// <summary>Prefab for the flash freeze burst visual.</summary>
        [SerializeField] private GameObject flashFreezePrefab;

        /// <summary>Material or shader applied to frozen objects for the frost visual.</summary>
        [SerializeField] private Material frozenOverlayMaterial;

        /// <summary>
        /// Flash Freeze — freezes all freezable objects in radius, reducing joint
        /// break forces and applying ice surface materials. Water objects become
        /// solid ice platforms.
        /// </summary>
        protected override void OnAbilityActivated()
        {
            Vector2 center = transform.position;

            // Spawn flash freeze visual
            if (flashFreezePrefab != null)
            {
                var effect = Instantiate(flashFreezePrefab, center, Quaternion.identity);
                Destroy(effect, 4f);
            }

            // Find all objects in freeze radius
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, freezeRadius);

            foreach (var hit in hits)
            {
                if (hit.gameObject == gameObject)
                    continue;

                // Freeze freezable objects
                var freezable = hit.GetComponent<IFreezable>();
                if (freezable != null)
                {
                    freezable.Freeze(freezeDuration, brittleBreakForceMultiplier, frozenOverlayMaterial);
                }

                // Make joints brittle
                MakeJointsBrittle(hit.gameObject);

                // Apply ice surface material for low friction
                ApplyIceSurface(hit);

                // Freeze water into platforms
                var water = hit.GetComponent<IWaterSurface>();
                if (water != null && icePlatformPrefab != null)
                {
                    water.FreezeIntoIce(icePlatformPrefab, freezeDuration);
                }
            }

            // Spawn frost spread effect
            if (frostSpreadPrefab != null)
            {
                var frost = Instantiate(frostSpreadPrefab, center, Quaternion.identity);
                frost.transform.localScale = Vector3.one * (freezeRadius * 2f);
                Destroy(frost, freezeDuration);
            }
        }

        protected override void HandleImpact(Collision2D collision)
        {
            base.HandleImpact(collision);

            // Freeze on direct contact
            var freezable = collision.gameObject.GetComponent<IFreezable>();
            if (freezable != null)
            {
                freezable.Freeze(freezeDuration, brittleBreakForceMultiplier, frozenOverlayMaterial);
            }

            // Check if we hit water
            var water = collision.gameObject.GetComponent<IWaterSurface>();
            if (water != null && icePlatformPrefab != null)
            {
                Vector2 contactPoint = collision.contactCount > 0
                    ? collision.GetContact(0).point
                    : (Vector2)transform.position;
                water.FreezeIntoIce(icePlatformPrefab, freezeDuration);
            }
        }

        /// <summary>
        /// Reduces the break force of all joints on the target object,
        /// making the structure easier to shatter.
        /// </summary>
        private void MakeJointsBrittle(GameObject target)
        {
            var joints = target.GetComponents<Joint2D>();
            foreach (var joint in joints)
            {
                if (joint.breakForce < Mathf.Infinity)
                {
                    joint.breakForce *= brittleBreakForceMultiplier;
                }
                if (joint.breakTorque < Mathf.Infinity)
                {
                    joint.breakTorque *= brittleBreakForceMultiplier;
                }
            }
        }

        /// <summary>
        /// Applies the low-friction ice physics material to the collider.
        /// </summary>
        private void ApplyIceSurface(Collider2D col)
        {
            if (iceSurfaceMaterial != null)
            {
                col.sharedMaterial = iceSurfaceMaterial;
            }
        }
    }

    /// <summary>
    /// Interface for objects that can be frozen by the Ice orb.
    /// Implement on structures, enemies, and dynamic objects.
    /// </summary>
    public interface IFreezable
    {
        /// <summary>
        /// Freezes this object for the specified duration.
        /// </summary>
        /// <param name="duration">Freeze duration in seconds.</param>
        /// <param name="brittleMultiplier">Multiplier for joint break forces.</param>
        /// <param name="frozenMaterial">Visual overlay material for the frozen state.</param>
        void Freeze(float duration, float brittleMultiplier, Material frozenMaterial);
    }

    /// <summary>
    /// Interface for water surfaces that can be frozen into solid platforms.
    /// </summary>
    public interface IWaterSurface
    {
        /// <summary>
        /// Converts this water surface into an ice platform.
        /// </summary>
        /// <param name="icePlatformPrefab">Prefab to instantiate as the frozen platform.</param>
        /// <param name="duration">How long the ice platform persists before melting.</param>
        void FreezeIntoIce(GameObject icePlatformPrefab, float duration);
    }
}
