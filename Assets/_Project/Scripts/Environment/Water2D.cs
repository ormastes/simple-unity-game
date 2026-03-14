using System.Collections.Generic;
using UnityEngine;
using ElementalSiege.Elements;
using ElementalSiege.Orbs;
using ElementalSiege.Structures;
using ElementCategory = ElementalSiege.Elements.ElementCategory;

namespace ElementalSiege.Environment
{
    /// <summary>
    /// Simple 2D water simulation with buoyancy, splash effects, damping, freezing,
    /// and lightning conduction. Uses a trigger collider to define the water area
    /// and a LineRenderer for surface wave visuals.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    [DisallowMultipleComponent]
    public class Water2D : MonoBehaviour, IWaterSurface
    {
        #region Serialized Fields

        [Header("Buoyancy")]

        /// <summary>Upward force applied per unit of submerged depth.</summary>
        [SerializeField]
        [Tooltip("Buoyancy force multiplier. Higher = stronger upward push on submerged objects.")]
        [Min(0f)]
        private float buoyancyForce = 15f;

        /// <summary>Density of the water. Objects with lower density float more.</summary>
        [SerializeField]
        [Tooltip("Water density. Objects less dense than this float.")]
        [Min(0.1f)]
        private float waterDensity = 1.5f;

        [Header("Damping")]

        /// <summary>Linear drag applied to objects while submerged.</summary>
        [SerializeField]
        [Tooltip("Extra linear drag for objects in water. Slows horizontal movement.")]
        [Min(0f)]
        private float linearDamping = 3f;

        /// <summary>Angular drag applied to objects while submerged.</summary>
        [SerializeField]
        [Tooltip("Extra angular drag for objects in water. Slows rotation.")]
        [Min(0f)]
        private float angularDamping = 2f;

        [Header("Splash Effects")]

        /// <summary>Particle system prefab spawned when an object enters the water.</summary>
        [SerializeField]
        [Tooltip("Splash particle effect instantiated at the water surface on entry.")]
        private ParticleSystem splashPrefab;

        /// <summary>Minimum entry velocity to trigger a splash effect.</summary>
        [SerializeField]
        [Tooltip("Objects entering slower than this don't create splashes.")]
        [Min(0f)]
        private float minSplashVelocity = 1f;

        [Header("Freeze")]

        /// <summary>Whether this water is currently frozen (acts as a solid platform).</summary>
        [SerializeField]
        [Tooltip("When frozen, the collider becomes solid and buoyancy is disabled.")]
        private bool isFrozen;

        /// <summary>Sprite to display when the water is frozen.</summary>
        [SerializeField]
        [Tooltip("Replaces the water visual with an ice surface when frozen.")]
        private Sprite frozenSprite;

        [Header("Lightning Conduction")]

        /// <summary>Damage dealt to all objects in water when lightning strikes.</summary>
        [SerializeField]
        [Tooltip("Lightning damage applied to every object currently in the water.")]
        [Min(0f)]
        private float lightningDamage = 50f;

        [Header("Surface Waves")]

        /// <summary>LineRenderer used to display the water surface.</summary>
        [SerializeField]
        [Tooltip("LineRenderer for the animated water surface.")]
        private LineRenderer surfaceRenderer;

        /// <summary>Number of points in the surface line.</summary>
        [SerializeField]
        [Tooltip("Resolution of the surface wave line.")]
        [Range(3, 100)]
        private int surfacePointCount = 20;

        /// <summary>Amplitude of the surface wave oscillation.</summary>
        [SerializeField]
        [Tooltip("Height of surface waves.")]
        [Min(0f)]
        private float waveAmplitude = 0.1f;

        /// <summary>Frequency of the surface wave.</summary>
        [SerializeField]
        [Tooltip("Speed of surface wave oscillation.")]
        [Min(0f)]
        private float waveFrequency = 2f;

        /// <summary>Speed at which waves move horizontally.</summary>
        [SerializeField]
        [Min(0f)]
        private float waveSpeed = 1f;

        #endregion

        #region Public Properties

        /// <summary>Whether the water is currently frozen.</summary>
        public bool IsFrozen => isFrozen;

        #endregion

        #region Cached References

        private BoxCollider2D waterCollider;
        private SpriteRenderer spriteRenderer;
        private Sprite originalSprite;

        /// <summary>Tracks objects currently in the water with their original drag values.</summary>
        private readonly Dictionary<Rigidbody2D, DragData> submergedBodies =
            new Dictionary<Rigidbody2D, DragData>();

        private struct DragData
        {
            public float OriginalLinearDrag;
            public float OriginalAngularDrag;
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            waterCollider = GetComponent<BoxCollider2D>();
            waterCollider.isTrigger = true;

            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                originalSprite = spriteRenderer.sprite;
            }

            InitializeSurfaceRenderer();
        }

        private void FixedUpdate()
        {
            if (isFrozen) return;

            ApplyBuoyancyToSubmerged();
        }

        private void Update()
        {
            if (!isFrozen)
            {
                UpdateSurfaceWaves();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (isFrozen) return;

            var rb = other.attachedRigidbody;
            if (rb == null || rb.bodyType == RigidbodyType2D.Static) return;

            if (!submergedBodies.ContainsKey(rb))
            {
                submergedBodies[rb] = new DragData
                {
                    OriginalLinearDrag = rb.drag,
                    OriginalAngularDrag = rb.angularDrag
                };

                rb.drag = linearDamping;
                rb.angularDrag = angularDamping;
            }

            SpawnSplash(other.transform.position, rb.linearVelocity);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            var rb = other.attachedRigidbody;
            if (rb == null) return;

            if (submergedBodies.TryGetValue(rb, out DragData data))
            {
                rb.drag = data.OriginalLinearDrag;
                rb.angularDrag = data.OriginalAngularDrag;
                submergedBodies.Remove(rb);
            }
        }

        private void OnDisable()
        {
            // Restore original drag on all submerged bodies
            foreach (var kvp in submergedBodies)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.drag = kvp.Value.OriginalLinearDrag;
                    kvp.Key.angularDrag = kvp.Value.OriginalAngularDrag;
                }
            }
            submergedBodies.Clear();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Implements IWaterSurface.FreezeIntoIce — freezes this water using the orb system.
        /// </summary>
        public void FreezeIntoIce(GameObject icePlatformPrefab, float duration)
        {
            FreezeWater();
            // Auto-thaw after duration
            if (duration > 0f)
            {
                StartCoroutine(ThawAfterDelay(duration));
            }
        }

        private System.Collections.IEnumerator ThawAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            ThawWater();
        }

        /// <summary>
        /// Freezes the water, turning it into a solid platform.
        /// All submerged objects have their drag restored.
        /// </summary>
        public void FreezeWater()
        {
            if (isFrozen) return;
            isFrozen = true;

            waterCollider.isTrigger = false;

            // Restore drag on all currently submerged objects
            foreach (var kvp in submergedBodies)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.drag = kvp.Value.OriginalLinearDrag;
                    kvp.Key.angularDrag = kvp.Value.OriginalAngularDrag;
                }
            }
            submergedBodies.Clear();

            if (spriteRenderer != null && frozenSprite != null)
            {
                spriteRenderer.sprite = frozenSprite;
            }

            // Hide surface waves
            if (surfaceRenderer != null)
            {
                surfaceRenderer.enabled = false;
            }
        }

        /// <summary>
        /// Thaws the water, restoring liquid behavior.
        /// </summary>
        public void ThawWater()
        {
            if (!isFrozen) return;
            isFrozen = false;

            waterCollider.isTrigger = true;

            if (spriteRenderer != null && originalSprite != null)
            {
                spriteRenderer.sprite = originalSprite;
            }

            if (surfaceRenderer != null)
            {
                surfaceRenderer.enabled = true;
            }
        }

        /// <summary>
        /// Conducts lightning through the water, damaging all submerged objects.
        /// </summary>
        public void ConductLightning()
        {
            foreach (var kvp in submergedBodies)
            {
                if (kvp.Key == null) continue;

                var health = kvp.Key.GetComponent<StructureHealth>();
                if (health != null)
                {
                    health.TakeElementalDamage(lightningDamage, ElementCategory.Lightning);
                }

                // Also charge conductive objects
                var conductive = kvp.Key.GetComponent<Conductive>();
                if (conductive != null)
                {
                    conductive.Charge();
                }
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Applies buoyancy force to all submerged Rigidbody2D objects each physics step.
        /// </summary>
        private void ApplyBuoyancyToSubmerged()
        {
            float waterSurface = transform.position.y + waterCollider.size.y * 0.5f * transform.lossyScale.y;

            // Use a list to track bodies that need removal (destroyed objects)
            List<Rigidbody2D> toRemove = null;

            foreach (var kvp in submergedBodies)
            {
                if (kvp.Key == null)
                {
                    toRemove ??= new List<Rigidbody2D>();
                    toRemove.Add(kvp.Key);
                    continue;
                }

                // Calculate submersion depth
                float objectY = kvp.Key.position.y;
                float depth = Mathf.Max(0f, waterSurface - objectY);

                // Buoyancy: upward force proportional to depth and water density
                float force = buoyancyForce * waterDensity * depth;
                kvp.Key.AddForce(Vector2.up * force, ForceMode2D.Force);
            }

            if (toRemove != null)
            {
                for (int i = 0; i < toRemove.Count; i++)
                {
                    submergedBodies.Remove(toRemove[i]);
                }
            }
        }

        /// <summary>
        /// Spawns a splash particle effect at the water surface entry point.
        /// </summary>
        private void SpawnSplash(Vector3 entryPosition, Vector2 velocity)
        {
            if (splashPrefab == null) return;
            if (velocity.magnitude < minSplashVelocity) return;

            float waterSurface = transform.position.y + waterCollider.size.y * 0.5f * transform.lossyScale.y;
            Vector3 splashPos = new Vector3(entryPosition.x, waterSurface, entryPosition.z);

            ParticleSystem splash = Instantiate(splashPrefab, splashPos, Quaternion.identity);
            splash.Play();
            Destroy(splash.gameObject, splash.main.duration + splash.main.startLifetime.constantMax);
        }

        /// <summary>
        /// Initializes the surface LineRenderer with the correct number of points.
        /// </summary>
        private void InitializeSurfaceRenderer()
        {
            if (surfaceRenderer == null) return;

            surfaceRenderer.positionCount = surfacePointCount;
            surfaceRenderer.useWorldSpace = true;
        }

        /// <summary>
        /// Animates the water surface using a sine wave pattern.
        /// </summary>
        private void UpdateSurfaceWaves()
        {
            if (surfaceRenderer == null) return;

            float waterSurface = transform.position.y + waterCollider.size.y * 0.5f * transform.lossyScale.y;
            float leftEdge = transform.position.x - waterCollider.size.x * 0.5f * transform.lossyScale.x;
            float width = waterCollider.size.x * transform.lossyScale.x;
            float step = width / (surfacePointCount - 1);

            for (int i = 0; i < surfacePointCount; i++)
            {
                float x = leftEdge + step * i;
                float waveOffset = Mathf.Sin((x * waveFrequency) + (Time.time * waveSpeed)) * waveAmplitude;
                surfaceRenderer.SetPosition(i, new Vector3(x, waterSurface + waveOffset, 0f));
            }
        }

        #endregion
    }
}
