using UnityEngine;

namespace ElementalSiege.Environment
{
    /// <summary>
    /// Reflective surface for crystal orb bouncing. Provides perfect reflection
    /// of incoming projectile trajectories, plays bounce sound effects, and
    /// optionally allows player rotation for advanced puzzle levels.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    [DisallowMultipleComponent]
    public class CrystalSurface : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Reflection")]

        /// <summary>
        /// Tag or layer name used to identify crystal orb projectiles.
        /// Only objects with this tag are reflected; others collide normally.
        /// </summary>
        [SerializeField]
        [Tooltip("Tag of projectiles that should be reflected (e.g., 'CrystalOrb').")]
        private string reflectableTag = "CrystalOrb";

        /// <summary>
        /// Speed multiplier applied after reflection. 1.0 = perfect energy conservation.
        /// </summary>
        [SerializeField]
        [Tooltip("Speed multiplier after bounce. 1.0 = no energy loss.")]
        [Range(0.5f, 1.5f)]
        private float bounceEnergyMultiplier = 1.0f;

        [Header("Audio")]

        /// <summary>Audio clip played when a crystal orb bounces off this surface.</summary>
        [SerializeField]
        [Tooltip("Sound effect played on crystal orb bounce.")]
        private AudioClip bounceSound;

        /// <summary>Volume of the bounce sound effect.</summary>
        [SerializeField]
        [Tooltip("Volume for the bounce sound.")]
        [Range(0f, 1f)]
        private float bounceSoundVolume = 0.7f;

        [Header("Visual Effects")]

        /// <summary>Particle effect spawned at the bounce point.</summary>
        [SerializeField]
        [Tooltip("Sparkle/shimmer effect at the point of reflection.")]
        private ParticleSystem bounceVFX;

        /// <summary>Color of the reflective shimmer on the sprite.</summary>
        [SerializeField]
        [Tooltip("Base color tint for the reflective surface.")]
        private Color reflectiveColor = new Color(0.8f, 0.9f, 1f, 1f);

        [Header("Rotation (Advanced Levels)")]

        /// <summary>Whether the player can rotate this surface.</summary>
        [SerializeField]
        [Tooltip("Enable player rotation of this surface for advanced puzzle levels.")]
        private bool isRotatable;

        /// <summary>Rotation speed in degrees per second when player rotates.</summary>
        [SerializeField]
        [Tooltip("Degrees per second when the player rotates this surface.")]
        [Min(0f)]
        private float rotationSpeed = 90f;

        /// <summary>Minimum rotation angle (degrees, relative to initial rotation).</summary>
        [SerializeField]
        [Tooltip("Minimum allowed rotation angle.")]
        private float minAngle = -90f;

        /// <summary>Maximum rotation angle (degrees, relative to initial rotation).</summary>
        [SerializeField]
        [Tooltip("Maximum allowed rotation angle.")]
        private float maxAngle = 90f;

        #endregion

        #region Public Properties

        /// <summary>Whether this surface can be rotated by the player.</summary>
        public bool IsRotatable => isRotatable;

        /// <summary>Current rotation angle relative to initial orientation.</summary>
        public float CurrentAngle { get; private set; }

        /// <summary>Total number of bounces that have occurred on this surface.</summary>
        public int BounceCount { get; private set; }

        #endregion

        #region Cached References

        private SpriteRenderer spriteRenderer;
        private float initialRotationZ;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            spriteRenderer.color = reflectiveColor;
            initialRotationZ = transform.eulerAngles.z;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!collision.gameObject.CompareTag(reflectableTag)) return;

            ReflectProjectile(collision);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Rotates the surface by the given input direction.
        /// Only works if <see cref="isRotatable"/> is true.
        /// </summary>
        /// <param name="direction">Positive = counter-clockwise, negative = clockwise.</param>
        public void Rotate(float direction)
        {
            if (!isRotatable) return;

            float delta = direction * rotationSpeed * Time.deltaTime;
            CurrentAngle = Mathf.Clamp(CurrentAngle + delta, minAngle, maxAngle);

            transform.rotation = Quaternion.Euler(0f, 0f, initialRotationZ + CurrentAngle);
        }

        /// <summary>
        /// Sets the rotation angle directly, clamped to min/max bounds.
        /// </summary>
        /// <param name="angle">Target angle in degrees.</param>
        public void SetAngle(float angle)
        {
            if (!isRotatable) return;

            CurrentAngle = Mathf.Clamp(angle, minAngle, maxAngle);
            transform.rotation = Quaternion.Euler(0f, 0f, initialRotationZ + CurrentAngle);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Calculates the perfect reflection of the projectile's velocity against
        /// the surface normal and applies it.
        /// </summary>
        /// <param name="collision">The collision data from the incoming projectile.</param>
        private void ReflectProjectile(Collision2D collision)
        {
            var projectileRb = collision.rigidbody;
            if (projectileRb == null) return;

            // Get the collision normal (pointing away from the surface)
            Vector2 normal = collision.GetContact(0).normal;

            // Calculate reflection: v' = v - 2(v . n)n
            Vector2 incomingVelocity = projectileRb.linearVelocity;
            Vector2 reflectedVelocity = Vector2.Reflect(incomingVelocity, normal);

            // Apply energy multiplier
            reflectedVelocity *= bounceEnergyMultiplier;

            projectileRb.linearVelocity = reflectedVelocity;

            BounceCount++;

            // Play sound
            PlayBounceSound(collision.GetContact(0).point);

            // Spawn VFX
            SpawnBounceEffect(collision.GetContact(0).point, normal);
        }

        /// <summary>
        /// Plays the bounce sound effect at the collision point.
        /// </summary>
        /// <param name="point">World position of the bounce.</param>
        private void PlayBounceSound(Vector2 point)
        {
            if (bounceSound == null) return;
            AudioSource.PlayClipAtPoint(bounceSound, point, bounceSoundVolume);
        }

        /// <summary>
        /// Spawns a particle effect at the bounce point, oriented along the surface normal.
        /// </summary>
        /// <param name="point">World position of the bounce.</param>
        /// <param name="normal">Surface normal at the bounce point.</param>
        private void SpawnBounceEffect(Vector2 point, Vector2 normal)
        {
            if (bounceVFX == null) return;

            float angle = Mathf.Atan2(normal.y, normal.x) * Mathf.Rad2Deg;
            ParticleSystem vfx = Instantiate(
                bounceVFX,
                point,
                Quaternion.Euler(0f, 0f, angle)
            );

            vfx.Play();
            Destroy(vfx.gameObject, vfx.main.duration + vfx.main.startLifetime.constantMax);
        }

        #endregion

        #region Editor Gizmos

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // Draw surface normal
            Gizmos.color = new Color(0.8f, 0.9f, 1f, 0.6f);
            Vector3 center = transform.position;
            Vector3 normal = transform.up;
            Gizmos.DrawLine(center, center + normal * 0.5f);

            // Draw the surface line
            Gizmos.color = new Color(0.8f, 0.9f, 1f, 0.8f);
            Vector3 right = transform.right * 0.5f;
            Gizmos.DrawLine(center - right, center + right);
        }
#endif

        #endregion
    }
}
