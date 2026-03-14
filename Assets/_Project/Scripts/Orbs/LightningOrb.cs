using System.Collections.Generic;
using UnityEngine;
using ElementalSiege.Elements;

namespace ElementalSiege.Orbs
{
    /// <summary>
    /// The Lightning orb — chain lightning bolts jump between conductive objects,
    /// dealing cascading damage. Excellent for triggering mechanisms and switches
    /// across connected circuits of conductive structures.
    /// </summary>
    public class LightningOrb : OrbBase
    {
        [Header("Lightning — Chain Lightning")]

        /// <summary>Maximum number of targets the lightning can chain through.</summary>
        [SerializeField] private int maxChainCount = 5;

        /// <summary>Maximum distance between chain targets in world units.</summary>
        [SerializeField] private float chainRadius = 4f;

        /// <summary>Damage falloff multiplier per chain jump (applied cumulatively).</summary>
        [SerializeField, Range(0.1f, 1f)] private float chainDamageFalloff = 0.7f;

        /// <summary>Duration in seconds the lightning bolt visual persists per segment.</summary>
        [SerializeField] private float boltVisualDuration = 0.4f;

        /// <summary>Number of zigzag segments per bolt for visual effect.</summary>
        [SerializeField] private int boltSegments = 8;

        /// <summary>Maximum random offset for zigzag bolt segments.</summary>
        [SerializeField] private float boltJitter = 0.3f;

        [Header("Lightning Effects")]

        /// <summary>Prefab with LineRenderer for the lightning bolt visual.</summary>
        [SerializeField] private GameObject boltPrefab;

        /// <summary>Prefab spawned at each chain target for the electric spark effect.</summary>
        [SerializeField] private GameObject sparkPrefab;

        /// <summary>Layer mask for conductive objects to target with raycasts.</summary>
        [SerializeField] private LayerMask conductiveLayerMask;

        /// <summary>
        /// Chain Lightning — fires a bolt that jumps between nearby conductive objects,
        /// dealing diminishing damage with each hop and triggering mechanisms.
        /// </summary>
        protected override void OnAbilityActivated()
        {
            Vector2 origin = transform.position;
            PerformChainLightning(origin, ElementType != null ? ElementType.BaseDamage : 10f);
        }

        /// <summary>
        /// Executes the chain lightning sequence from the origin point.
        /// Uses OverlapCircle to find conductive targets and chains between them.
        /// </summary>
        /// <param name="origin">Starting position for the chain.</param>
        /// <param name="baseDamage">Initial damage before falloff.</param>
        private void PerformChainLightning(Vector2 origin, float baseDamage)
        {
            var hitTargets = new HashSet<Collider2D>();
            Vector2 currentPosition = origin;
            float currentDamage = baseDamage;

            for (int i = 0; i < maxChainCount; i++)
            {
                // Find the nearest conductive target not yet hit
                Collider2D nextTarget = FindNearestConductive(currentPosition, hitTargets);

                if (nextTarget == null)
                    break;

                hitTargets.Add(nextTarget);

                Vector2 targetPosition = nextTarget.transform.position;

                // Spawn bolt visual between current and target positions
                SpawnBoltVisual(currentPosition, targetPosition);

                // Spawn spark effect at the target
                if (sparkPrefab != null)
                {
                    var spark = Instantiate(sparkPrefab, targetPosition, Quaternion.identity);
                    Destroy(spark, 2f);
                }

                // Deal damage with falloff
                var destructible = nextTarget.GetComponent<IDestructible>();
                if (destructible != null && ElementType != null)
                {
                    destructible.TakeDamage(currentDamage, ElementType.Category);
                }

                // Trigger mechanisms and switches
                var mechanism = nextTarget.GetComponent<IElectricalMechanism>();
                if (mechanism != null)
                {
                    mechanism.Energize(currentDamage);
                }

                // Apply falloff for next jump
                currentDamage *= chainDamageFalloff;
                currentPosition = targetPosition;
            }
        }

        /// <summary>
        /// Finds the nearest conductive collider within chain radius that hasn't
        /// already been targeted in this chain sequence.
        /// </summary>
        /// <param name="position">Search center point.</param>
        /// <param name="alreadyHit">Set of colliders to exclude.</param>
        /// <returns>The nearest valid conductive collider, or null.</returns>
        private Collider2D FindNearestConductive(Vector2 position, HashSet<Collider2D> alreadyHit)
        {
            Collider2D[] candidates = Physics2D.OverlapCircleAll(position, chainRadius, conductiveLayerMask);

            Collider2D nearest = null;
            float nearestDist = float.MaxValue;

            foreach (var candidate in candidates)
            {
                if (candidate.gameObject == gameObject)
                    continue;
                if (alreadyHit.Contains(candidate))
                    continue;

                // Verify line of sight with a raycast
                Vector2 candidatePos = candidate.transform.position;
                Vector2 direction = candidatePos - position;
                float distance = direction.magnitude;

                RaycastHit2D lineOfSight = Physics2D.Raycast(
                    position, direction.normalized, distance, conductiveLayerMask);

                if (lineOfSight.collider == candidate && distance < nearestDist)
                {
                    nearest = candidate;
                    nearestDist = distance;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Spawns a zigzag lightning bolt visual between two points using a LineRenderer.
        /// </summary>
        /// <param name="from">Start position of the bolt.</param>
        /// <param name="to">End position of the bolt.</param>
        private void SpawnBoltVisual(Vector2 from, Vector2 to)
        {
            GameObject boltObj;
            LineRenderer lr;

            if (boltPrefab != null)
            {
                boltObj = Instantiate(boltPrefab);
                lr = boltObj.GetComponent<LineRenderer>();
                if (lr == null)
                    lr = boltObj.AddComponent<LineRenderer>();
            }
            else
            {
                boltObj = new GameObject("LightningBolt");
                lr = boltObj.AddComponent<LineRenderer>();
                lr.startWidth = 0.08f;
                lr.endWidth = 0.04f;
                lr.material = new Material(Shader.Find("Sprites/Default"));
                lr.startColor = new Color(0.6f, 0.8f, 1f, 1f);
                lr.endColor = new Color(0.3f, 0.5f, 1f, 0.5f);
            }

            // Generate zigzag bolt path
            lr.positionCount = boltSegments + 1;
            lr.SetPosition(0, from);
            lr.SetPosition(boltSegments, to);

            Vector2 direction = to - from;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x).normalized;

            for (int i = 1; i < boltSegments; i++)
            {
                float t = (float)i / boltSegments;
                Vector2 basePos = Vector2.Lerp(from, to, t);
                float offset = Random.Range(-boltJitter, boltJitter);
                Vector2 jitteredPos = basePos + perpendicular * offset;
                lr.SetPosition(i, jitteredPos);
            }

            Destroy(boltObj, boltVisualDuration);
        }

        protected override void HandleImpact(Collision2D collision)
        {
            base.HandleImpact(collision);

            // Trigger mechanism on direct impact too
            var mechanism = collision.gameObject.GetComponent<IElectricalMechanism>();
            if (mechanism != null && ElementType != null)
            {
                mechanism.Energize(ElementType.BaseDamage);
            }
        }
    }

    /// <summary>
    /// Interface for electrically activatable mechanisms such as switches,
    /// doors, and circuit-connected devices.
    /// </summary>
    public interface IElectricalMechanism
    {
        /// <summary>
        /// Energizes this mechanism with the given power level.
        /// </summary>
        /// <param name="power">Power level based on the lightning damage value.</param>
        void Energize(float power);
    }
}
