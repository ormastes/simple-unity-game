using UnityEngine;
using ElementalSiege.Elements;

namespace ElementalSiege.Orbs
{
    /// <summary>
    /// The Crystal orb — splits into multiple sub-orbs in a fan pattern for
    /// multi-target coverage. Bounces off crystal surfaces with perfect reflection,
    /// enabling trick shots and ricochet-based puzzle solutions.
    /// </summary>
    public class CrystalOrb : OrbBase
    {
        [Header("Crystal — Prism Split")]

        /// <summary>Number of sub-orbs spawned on ability activation.</summary>
        [SerializeField] private int splitCount = 3;

        /// <summary>Damage multiplier for each sub-orb (relative to base damage).</summary>
        [SerializeField, Range(0.1f, 1f)] private float subOrbDamageMultiplier = 0.4f;

        /// <summary>Total fan angle in degrees for the split pattern.</summary>
        [SerializeField] private float fanAngle = 45f;

        /// <summary>Speed of each sub-orb at spawn.</summary>
        [SerializeField] private float subOrbSpeed = 8f;

        /// <summary>Scale multiplier for sub-orbs (smaller than the original).</summary>
        [SerializeField, Range(0.1f, 1f)] private float subOrbScaleMultiplier = 0.6f;

        /// <summary>Prefab used for the sub-orbs. Should be a simplified crystal orb.</summary>
        [SerializeField] private GameObject subOrbPrefab;

        [Header("Crystal Bounce")]

        /// <summary>
        /// Physics material with high bounciness for crystal-to-crystal reflection.
        /// Applied at Awake for the orb's entire lifetime.
        /// </summary>
        [SerializeField] private PhysicsMaterial2D crystalBounceMaterial;

        [Header("Crystal Effects")]

        /// <summary>Prefab for the prism split visual burst.</summary>
        [SerializeField] private GameObject splitEffectPrefab;

        /// <summary>Prefab for the bounce sparkle effect on crystal surface reflection.</summary>
        [SerializeField] private GameObject bounceSparkPrefab;

        protected override void Awake()
        {
            base.Awake();

            // Apply high-bounciness physics material
            if (crystalBounceMaterial != null)
            {
                Col.sharedMaterial = crystalBounceMaterial;
            }
        }

        /// <summary>
        /// Prism Split — splits the orb into multiple smaller sub-orbs in a fan
        /// pattern. Each sub-orb deals reduced damage but covers a wider area.
        /// The original orb is destroyed after splitting.
        /// </summary>
        protected override void OnAbilityActivated()
        {
            Vector2 center = transform.position;
            Vector2 velocity = Rb.linearVelocity;

            // If velocity is near zero, use the orb's facing direction
            if (velocity.sqrMagnitude < 0.01f)
                velocity = transform.right;

            float baseAngle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;

            // Spawn split visual
            if (splitEffectPrefab != null)
            {
                var effect = Instantiate(splitEffectPrefab, center, Quaternion.identity);
                Destroy(effect, 2f);
            }

            // Spawn sub-orbs in a fan pattern
            for (int i = 0; i < splitCount; i++)
            {
                float t = splitCount > 1 ? (float)i / (splitCount - 1) : 0.5f;
                float angle = baseAngle - fanAngle * 0.5f + fanAngle * t;
                float rad = angle * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

                SpawnSubOrb(center, direction);
            }

            // Destroy the original orb after splitting
            DestroyOrb();
        }

        /// <summary>
        /// Spawns a single sub-orb at the given position travelling in the specified direction.
        /// </summary>
        /// <param name="position">Spawn position in world space.</param>
        /// <param name="direction">Normalized direction for the sub-orb's velocity.</param>
        private void SpawnSubOrb(Vector2 position, Vector2 direction)
        {
            // Determine the prefab to use
            GameObject prefab = subOrbPrefab;
            if (prefab == null && ElementType != null)
                prefab = ElementType.OrbPrefab;
            if (prefab == null)
                return;

            // Offset spawn position slightly to avoid self-collision
            Vector2 spawnPos = position + direction * 0.3f;

            GameObject subOrbObj = Instantiate(prefab, spawnPos, Quaternion.identity);
            subOrbObj.transform.localScale = transform.localScale * subOrbScaleMultiplier;

            // Configure the sub-orb's rigidbody
            Rigidbody2D subRb = subOrbObj.GetComponent<Rigidbody2D>();
            if (subRb != null)
            {
                subRb.bodyType = RigidbodyType2D.Dynamic;
                subRb.linearVelocity = direction * subOrbSpeed;
            }

            // If the sub-orb has an OrbBase, configure it as a launched sub-projectile
            var subOrb = subOrbObj.GetComponent<OrbBase>();
            if (subOrb != null)
            {
                // Sub-orbs are already in flight
                subOrb.OnLaunch(Vector2.zero); // Transition state; velocity already set
            }
        }

        protected override void HandleImpact(Collision2D collision)
        {
            base.HandleImpact(collision);

            // Check for crystal surface for perfect reflection sparkle
            var crystalSurface = collision.gameObject.GetComponent<ICrystalSurface>();
            if (crystalSurface != null && bounceSparkPrefab != null && collision.contactCount > 0)
            {
                Vector2 contactPoint = collision.GetContact(0).point;
                var spark = Instantiate(bounceSparkPrefab, contactPoint, Quaternion.identity);
                Destroy(spark, 1.5f);
            }
        }
    }

    /// <summary>
    /// Marker interface for surfaces that provide perfect crystal reflection.
    /// Objects with this component cause crystal orbs to bounce with zero energy loss.
    /// </summary>
    public interface ICrystalSurface { }
}
