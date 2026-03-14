using UnityEngine;
using ElementalSiege.Elements;

namespace ElementalSiege.Orbs
{
    /// <summary>
    /// The Fire orb — unleashes a fireball burst that sets flammable objects ablaze.
    /// Fire spreads to adjacent flammable structures over time, making it excellent
    /// for destroying wood and rope-based constructions.
    /// </summary>
    public class FireOrb : OrbBase
    {
        [Header("Fire — Fireball Burst")]

        /// <summary>Radius of the fireball explosion in world units.</summary>
        [SerializeField] private float explosionRadius = 3f;

        /// <summary>Force applied to objects within the explosion radius.</summary>
        [SerializeField] private float explosionForce = 500f;

        /// <summary>Duration in seconds that fire persists on ignited objects.</summary>
        [SerializeField] private float burnDuration = 4f;

        /// <summary>Damage per second dealt to burning objects.</summary>
        [SerializeField] private float burnDamagePerSecond = 5f;

        /// <summary>Delay in seconds before fire spreads to adjacent flammable objects.</summary>
        [SerializeField] private float fireSpreadDelay = 1f;

        /// <summary>Radius to check for adjacent flammable objects for fire spread.</summary>
        [SerializeField] private float fireSpreadRadius = 1.5f;

        [Header("Fire Effects")]

        /// <summary>Particle system prefab for the fire trail while in flight.</summary>
        [SerializeField] private GameObject fireTrailPrefab;

        /// <summary>Particle system prefab for the explosion on ability activation.</summary>
        [SerializeField] private GameObject explosionPrefab;

        /// <summary>Particle system prefab attached to burning objects.</summary>
        [SerializeField] private GameObject burningEffectPrefab;

        private GameObject _activeTrail;

        protected override void OnLaunched()
        {
            // Attach fire trail particle system
            if (fireTrailPrefab != null)
            {
                _activeTrail = Instantiate(fireTrailPrefab, transform);
                _activeTrail.transform.localPosition = Vector3.zero;
            }
        }

        /// <summary>
        /// Fireball Burst — explodes in a radius, applying force and igniting
        /// all flammable objects within range.
        /// </summary>
        protected override void OnAbilityActivated()
        {
            Vector2 center = transform.position;

            // Spawn explosion visual
            if (explosionPrefab != null)
            {
                var explosion = Instantiate(explosionPrefab, center, Quaternion.identity);
                Destroy(explosion, 3f);
            }

            // Find all colliders in the explosion radius
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, explosionRadius);

            foreach (var hit in hits)
            {
                if (hit.gameObject == gameObject)
                    continue;

                // Apply explosion force
                Rigidbody2D hitRb = hit.attachedRigidbody;
                if (hitRb != null)
                {
                    Vector2 direction = ((Vector2)hit.transform.position - center).normalized;
                    float distance = Vector2.Distance(center, hit.transform.position);
                    float falloff = 1f - Mathf.Clamp01(distance / explosionRadius);
                    hitRb.AddForce(direction * explosionForce * falloff, ForceMode2D.Impulse);
                }

                // Apply damage
                var destructible = hit.GetComponent<IDestructible>();
                if (destructible != null && ElementType != null)
                {
                    destructible.TakeDamage(ElementType.BaseDamage * 0.5f, ElementType.Category);
                }

                // Ignite flammable objects
                var flammable = hit.GetComponent<IFlammable>();
                if (flammable != null)
                {
                    flammable.Ignite(burnDuration, burnDamagePerSecond, fireSpreadDelay,
                        fireSpreadRadius, burningEffectPrefab);
                }
            }
        }

        protected override void HandleImpact(Collision2D collision)
        {
            base.HandleImpact(collision);

            // Also ignite on direct impact
            var flammable = collision.gameObject.GetComponent<IFlammable>();
            if (flammable != null)
            {
                flammable.Ignite(burnDuration, burnDamagePerSecond, fireSpreadDelay,
                    fireSpreadRadius, burningEffectPrefab);
            }
        }
    }

    /// <summary>
    /// Interface for objects that can be set on fire.
    /// Implement on wood, rope, and other combustible structures.
    /// </summary>
    public interface IFlammable
    {
        /// <summary>
        /// Sets this object on fire.
        /// </summary>
        /// <param name="duration">How long the fire burns in seconds.</param>
        /// <param name="damagePerSecond">Damage dealt per second while burning.</param>
        /// <param name="spreadDelay">Delay before fire spreads to neighbors.</param>
        /// <param name="spreadRadius">Radius to search for adjacent flammable objects.</param>
        /// <param name="burningEffectPrefab">Visual effect to attach while burning.</param>
        void Ignite(float duration, float damagePerSecond, float spreadDelay,
            float spreadRadius, GameObject burningEffectPrefab);
    }
}
