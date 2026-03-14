using UnityEngine;
using ElementalSiege.Elements;

namespace ElementalSiege.Orbs
{
    /// <summary>
    /// The Wind orb — creates a powerful gust that pushes objects away from the
    /// orb's position. Excellent for pushing structures off edges, redirecting
    /// other orbs mid-flight, and fanning existing fires to accelerate spread.
    /// </summary>
    public class WindOrb : OrbBase
    {
        [Header("Wind — Gust")]

        /// <summary>Radius of the gust push effect in world units.</summary>
        [SerializeField] private float gustRadius = 5f;

        /// <summary>Force magnitude applied to objects within the gust radius.</summary>
        [SerializeField] private float gustForce = 400f;

        /// <summary>Duration in seconds the temporary wind zone persists.</summary>
        [SerializeField] private float windZoneDuration = 2.5f;

        /// <summary>Force magnitude of the persistent wind zone AreaEffector2D.</summary>
        [SerializeField] private float windZoneForce = 15f;

        [Header("Fire Interaction")]

        /// <summary>Multiplier applied to fire spread rate when wind fans flames.</summary>
        [SerializeField] private float fireSpreadMultiplier = 2f;

        [Header("Wind Effects")]

        /// <summary>Prefab for the gust visual burst effect.</summary>
        [SerializeField] private GameObject gustEffectPrefab;

        /// <summary>Prefab for the persistent wind zone visual (with AreaEffector2D).</summary>
        [SerializeField] private GameObject windZonePrefab;

        /// <summary>
        /// Gust — pushes all rigidbodies away from the orb and creates a temporary
        /// wind zone. Fans existing fires and can redirect other orbs mid-flight.
        /// </summary>
        protected override void OnAbilityActivated()
        {
            Vector2 center = transform.position;

            // Spawn gust visual
            if (gustEffectPrefab != null)
            {
                var gust = Instantiate(gustEffectPrefab, center, Quaternion.identity);
                Destroy(gust, 3f);
            }

            // Apply push force to all objects in radius
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, gustRadius);

            foreach (var hit in hits)
            {
                if (hit.gameObject == gameObject)
                    continue;

                Rigidbody2D hitRb = hit.attachedRigidbody;
                if (hitRb != null)
                {
                    Vector2 direction = ((Vector2)hit.transform.position - center).normalized;
                    float distance = Vector2.Distance(center, hit.transform.position);
                    float falloff = 1f - Mathf.Clamp01(distance / gustRadius);
                    hitRb.AddForce(direction * gustForce * falloff, ForceMode2D.Impulse);
                }

                // Fan existing fires — increase their spread rate
                var windAffectable = hit.GetComponent<IWindAffectable>();
                if (windAffectable != null)
                {
                    windAffectable.ApplyWind(
                        ((Vector2)hit.transform.position - center).normalized,
                        fireSpreadMultiplier
                    );
                }
            }

            // Create temporary wind zone with AreaEffector2D
            CreateWindZone(center);
        }

        /// <summary>
        /// Creates a temporary wind zone at the specified position. The zone uses
        /// an AreaEffector2D to continuously push objects in the orb's forward direction.
        /// </summary>
        /// <param name="position">World position for the wind zone center.</param>
        private void CreateWindZone(Vector2 position)
        {
            GameObject windZone;

            if (windZonePrefab != null)
            {
                windZone = Instantiate(windZonePrefab, position, Quaternion.identity);
            }
            else
            {
                // Create a procedural wind zone if no prefab is assigned
                windZone = new GameObject("WindZone_Temp");
                windZone.transform.position = position;

                var col = windZone.AddComponent<CircleCollider2D>();
                col.radius = gustRadius * 0.8f;
                col.isTrigger = true;

                var effector = windZone.AddComponent<AreaEffector2D>();
                effector.useColliderMask = true;
                effector.forceMagnitude = windZoneForce;

                // Direct the wind in the orb's travel direction
                Vector2 travelDir = Rb.linearVelocity.normalized;
                if (travelDir.sqrMagnitude < 0.01f)
                    travelDir = Vector2.right;

                float angle = Mathf.Atan2(travelDir.y, travelDir.x) * Mathf.Rad2Deg;
                effector.forceAngle = angle;

                col.usedByEffector = true;
            }

            Destroy(windZone, windZoneDuration);
        }

        protected override void HandleImpact(Collision2D collision)
        {
            base.HandleImpact(collision);

            // Small push on direct impact
            Rigidbody2D hitRb = collision.rigidbody;
            if (hitRb != null)
            {
                Vector2 pushDir = (collision.transform.position - transform.position).normalized;
                hitRb.AddForce(pushDir * gustForce * 0.3f, ForceMode2D.Impulse);
            }
        }
    }

    /// <summary>
    /// Interface for objects that react to wind, such as burning objects
    /// whose fire spread can be accelerated.
    /// </summary>
    public interface IWindAffectable
    {
        /// <summary>
        /// Applies wind influence to this object.
        /// </summary>
        /// <param name="direction">Direction the wind is blowing.</param>
        /// <param name="spreadMultiplier">Multiplier for fire spread or similar effects.</param>
        void ApplyWind(Vector2 direction, float spreadMultiplier);
    }
}
