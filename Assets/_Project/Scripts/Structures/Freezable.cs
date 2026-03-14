using System.Collections;
using UnityEngine;
using ElementalSiege.Orbs;

namespace ElementalSiege.Structures
{
    /// <summary>
    /// Makes a structure freezable. When frozen, the structure becomes brittle:
    /// joints are dramatically weakened, the sprite is tinted blue, and any impact
    /// while frozen causes instant destruction. Thaws automatically after a duration.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [DisallowMultipleComponent]
    public class Freezable : MonoBehaviour, IFreezable
    {
        #region Serialized Fields

        [Header("Freeze Settings")]

        /// <summary>Duration in seconds the frozen state lasts before thawing.</summary>
        [SerializeField]
        [Tooltip("How long the object stays frozen before automatically thawing.")]
        [Min(0.5f)]
        private float freezeDuration = 5f;

        /// <summary>Multiplier applied to all joint breakForce values while frozen.</summary>
        [SerializeField]
        [Tooltip("Joint breakForce is multiplied by this value while frozen (lower = weaker).")]
        [Range(0f, 1f)]
        private float frozenJointMultiplier = 0.1f;

        /// <summary>Minimum impact force to trigger shatter while frozen.</summary>
        [SerializeField]
        [Tooltip("Any collision above this force while frozen causes instant destruction.")]
        [Min(0f)]
        private float shatterThreshold = 0.5f;

        [Header("Visual Effects")]

        /// <summary>Tint color applied to the sprite when frozen.</summary>
        [SerializeField]
        [Tooltip("Color blended onto the sprite while frozen.")]
        private Color frozenTint = new Color(0.5f, 0.7f, 1f, 1f);

        /// <summary>Optional particle system for frost/ice crystals while frozen.</summary>
        [SerializeField]
        [Tooltip("Frost particle effect shown while the object is frozen.")]
        private ParticleSystem frostVFX;

        /// <summary>Optional particle system for the shatter effect.</summary>
        [SerializeField]
        [Tooltip("Particle effect spawned when a frozen object shatters.")]
        private ParticleSystem shatterVFX;

        #endregion

        #region Public Properties

        /// <summary>Whether this structure is currently frozen.</summary>
        public bool IsFrozen { get; private set; }

        /// <summary>Remaining time before thaw in seconds.</summary>
        public float FreezeTimeRemaining { get; private set; }

        #endregion

        #region Cached References

        private SpriteRenderer spriteRenderer;
        private StructureHealth healthComponent;
        private StructureBlock blockComponent;
        private Color originalColor;
        private Coroutine freezeCoroutine;

        /// <summary>Stores original breakForce values for restoration on thaw.</summary>
        private float[] originalBreakForces;
        private Joint2D[] joints;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            healthComponent = GetComponent<StructureHealth>();
            blockComponent = GetComponent<StructureBlock>();

            if (spriteRenderer != null)
            {
                originalColor = spriteRenderer.color;
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!IsFrozen) return;

            float impactForce = collision.relativeVelocity.magnitude;

            // Frozen objects shatter on any meaningful impact
            if (impactForce >= shatterThreshold)
            {
                Shatter();
            }
        }

        private void OnDisable()
        {
            if (IsFrozen)
            {
                // Clean up without restoring (object is being destroyed)
                IsFrozen = false;
                if (freezeCoroutine != null)
                {
                    StopCoroutine(freezeCoroutine);
                    freezeCoroutine = null;
                }
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Implements IFreezable.Freeze with full parameters from the orb system.
        /// </summary>
        public void Freeze(float duration, float brittleMultiplier, Material frozenMaterial)
        {
            this.frozenJointMultiplier = brittleMultiplier;
            Freeze(duration);
        }

        /// <summary>
        /// Freezes this structure for the configured duration.
        /// If already frozen, resets the freeze timer.
        /// </summary>
        public void Freeze()
        {
            Freeze(freezeDuration);
        }

        /// <summary>
        /// Freezes this structure for a specified duration.
        /// </summary>
        /// <param name="duration">Duration in seconds to remain frozen.</param>
        public void Freeze(float duration)
        {
            if (healthComponent != null && healthComponent.IsDead) return;

            // Extinguish fire if this object is flammable and burning
            var flammable = GetComponent<Flammable>();
            if (flammable != null && flammable.IsOnFire)
            {
                flammable.Extinguish();
            }

            bool wasAlreadyFrozen = IsFrozen;
            IsFrozen = true;
            FreezeTimeRemaining = duration;

            if (!wasAlreadyFrozen)
            {
                ApplyFreezeVisuals();
                WeakenJoints();
            }

            // Restart the freeze timer
            if (freezeCoroutine != null)
            {
                StopCoroutine(freezeCoroutine);
            }
            freezeCoroutine = StartCoroutine(FreezeTimerRoutine(duration));
        }

        /// <summary>
        /// Immediately thaws this structure, restoring normal behavior.
        /// </summary>
        public void Thaw()
        {
            if (!IsFrozen) return;

            IsFrozen = false;
            FreezeTimeRemaining = 0f;

            if (freezeCoroutine != null)
            {
                StopCoroutine(freezeCoroutine);
                freezeCoroutine = null;
            }

            RestoreJoints();
            RemoveFreezeVisuals();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Counts down the freeze duration and thaws when expired.
        /// </summary>
        private IEnumerator FreezeTimerRoutine(float duration)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                FreezeTimeRemaining = Mathf.Max(0f, duration - elapsed);
                yield return null;
            }

            Thaw();
        }

        /// <summary>
        /// Instantly destroys the frozen object with a shatter effect.
        /// </summary>
        private void Shatter()
        {
            if (!IsFrozen) return;

            // Spawn shatter particles
            if (shatterVFX != null)
            {
                ParticleSystem shatter = Instantiate(
                    shatterVFX,
                    transform.position,
                    Quaternion.identity
                );
                shatter.Play();
                Destroy(shatter.gameObject, shatter.main.duration + shatter.main.startLifetime.constantMax);
            }

            // Force destroy through StructureBlock or directly via health
            if (blockComponent != null)
            {
                blockComponent.ForceDestroy();
            }
            else if (healthComponent != null)
            {
                healthComponent.TakeDamage(healthComponent.MaxHealth * 10f);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Applies the frozen tint and activates frost particle effects.
        /// </summary>
        private void ApplyFreezeVisuals()
        {
            if (spriteRenderer != null)
            {
                originalColor = spriteRenderer.color;
                spriteRenderer.color = frozenTint;
            }

            if (frostVFX != null)
            {
                frostVFX.gameObject.SetActive(true);
                frostVFX.Play();
            }
        }

        /// <summary>
        /// Restores the original sprite color and stops frost effects.
        /// </summary>
        private void RemoveFreezeVisuals()
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = originalColor;
            }

            if (frostVFX != null)
            {
                frostVFX.Stop();
            }
        }

        /// <summary>
        /// Stores original joint breakForce values and dramatically reduces them.
        /// </summary>
        private void WeakenJoints()
        {
            joints = GetComponents<Joint2D>();
            originalBreakForces = new float[joints.Length];

            for (int i = 0; i < joints.Length; i++)
            {
                if (joints[i] == null) continue;

                originalBreakForces[i] = joints[i].breakForce;

                if (!float.IsInfinity(joints[i].breakForce))
                {
                    joints[i].breakForce *= frozenJointMultiplier;
                }
            }
        }

        /// <summary>
        /// Restores joint breakForce values to their pre-freeze state.
        /// </summary>
        private void RestoreJoints()
        {
            if (joints == null || originalBreakForces == null) return;

            for (int i = 0; i < joints.Length; i++)
            {
                if (joints[i] == null) continue;
                joints[i].breakForce = originalBreakForces[i];
            }

            joints = null;
            originalBreakForces = null;
        }

        #endregion

        #region Editor Gizmos

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (IsFrozen)
            {
                Gizmos.color = new Color(0.5f, 0.7f, 1f, 0.2f);
                Gizmos.DrawSphere(transform.position, 0.5f);
            }
        }
#endif

        #endregion
    }
}
