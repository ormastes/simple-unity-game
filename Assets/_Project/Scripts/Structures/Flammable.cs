using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ElementalSiege.Elements;
using ElementalSiege.Orbs;
using ElementCategory = ElementalSiege.Elements.ElementCategory;

namespace ElementalSiege.Structures
{
    /// <summary>
    /// Makes a structure burnable. When ignited, the structure takes continuous fire damage,
    /// weakens its joints, and can spread fire to nearby Flammable objects.
    /// Can be extinguished by Ice or Water interactions.
    /// </summary>
    [RequireComponent(typeof(StructureHealth))]
    [DisallowMultipleComponent]
    public class Flammable : MonoBehaviour, IFlammable
    {
        #region Serialized Fields

        [Header("Burn Settings")]

        /// <summary>Health drained per second while on fire.</summary>
        [SerializeField]
        [Tooltip("Health points lost per second while burning.")]
        [Min(0.1f)]
        private float burnRate = 10f;

        /// <summary>Maximum radius within which fire can spread to other Flammable objects.</summary>
        [SerializeField]
        [Tooltip("Distance (world units) fire can jump to nearby Flammable objects.")]
        [Min(0f)]
        private float spreadRadius = 2f;

        /// <summary>Delay in seconds before fire attempts to spread after ignition.</summary>
        [SerializeField]
        [Tooltip("Time after ignition before fire attempts to spread.")]
        [Min(0f)]
        private float spreadDelay = 1.5f;

        /// <summary>How quickly joints weaken while burning (breakForce reduction per second).</summary>
        [SerializeField]
        [Tooltip("Joint breakForce reduced per second while on fire.")]
        [Min(0f)]
        private float jointWeakeningRate = 50f;

        [Header("Visual Effects")]

        /// <summary>Particle system to activate when the structure is on fire.</summary>
        [SerializeField]
        [Tooltip("Fire VFX particle system. Activated on ignition, deactivated on extinguish.")]
        private ParticleSystem fireVFX;

        /// <summary>Color tint applied to the sprite while burning.</summary>
        [SerializeField]
        [Tooltip("Tint applied to the SpriteRenderer while on fire.")]
        private Color burnTint = new Color(1f, 0.6f, 0.3f, 1f);

        [Header("Auto-Ignite")]

        /// <summary>If true, this object starts on fire when the scene begins.</summary>
        [SerializeField]
        [Tooltip("Immediately ignite when the object is enabled.")]
        private bool igniteOnStart;

        #endregion

        #region Public Properties

        /// <summary>Whether this structure is currently on fire.</summary>
        public bool IsOnFire { get; private set; }

        /// <summary>The burn rate in health per second.</summary>
        public float BurnRate => burnRate;

        /// <summary>The fire spread radius.</summary>
        public float SpreadRadius => spreadRadius;

        #endregion

        #region Cached References

        private StructureHealth healthComponent;
        private SpriteRenderer spriteRenderer;
        private Color originalColor;
        private Joint2D[] joints;
        private Coroutine burnCoroutine;
        private Coroutine spreadCoroutine;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            healthComponent = GetComponent<StructureHealth>();
            spriteRenderer = GetComponent<SpriteRenderer>();

            if (spriteRenderer != null)
            {
                originalColor = spriteRenderer.color;
            }
        }

        private void Start()
        {
            if (igniteOnStart)
            {
                Ignite();
            }
        }

        private void OnDisable()
        {
            if (IsOnFire)
            {
                StopAllCoroutines();
                IsOnFire = false;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Implements IFlammable.Ignite with full parameters from the orb system.
        /// Delegates to the simpler Ignite() overload after applying orb-provided settings.
        /// </summary>
        public void Ignite(float duration, float damagePerSecond, float spreadDelay,
            float spreadRadius, GameObject burningEffectPrefab)
        {
            this.burnRate = damagePerSecond;
            this.spreadDelay = spreadDelay;
            this.spreadRadius = spreadRadius;
            Ignite();
        }

        /// <summary>
        /// Ignites this structure. Has no effect if already on fire or if the structure is dead.
        /// </summary>
        public void Ignite()
        {
            if (IsOnFire) return;
            if (healthComponent != null && healthComponent.IsDead) return;

            IsOnFire = true;

            // Cache joints for weakening
            joints = GetComponents<Joint2D>();

            // Activate fire VFX
            if (fireVFX != null)
            {
                fireVFX.gameObject.SetActive(true);
                fireVFX.Play();
            }

            // Apply burn tint
            if (spriteRenderer != null)
            {
                originalColor = spriteRenderer.color;
                spriteRenderer.color = burnTint;
            }

            burnCoroutine = StartCoroutine(BurnRoutine());
            spreadCoroutine = StartCoroutine(SpreadFireRoutine());
        }

        /// <summary>
        /// Extinguishes the fire on this structure. Typically called by Ice or Water interactions.
        /// </summary>
        public void Extinguish()
        {
            if (!IsOnFire) return;

            IsOnFire = false;

            if (burnCoroutine != null)
            {
                StopCoroutine(burnCoroutine);
                burnCoroutine = null;
            }

            if (spreadCoroutine != null)
            {
                StopCoroutine(spreadCoroutine);
                spreadCoroutine = null;
            }

            // Deactivate fire VFX
            if (fireVFX != null)
            {
                fireVFX.Stop();
            }

            // Restore original color
            if (spriteRenderer != null)
            {
                spriteRenderer.color = originalColor;
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Continuously drains health and weakens joints while the structure burns.
        /// Ends when health reaches zero (structure is destroyed).
        /// </summary>
        private IEnumerator BurnRoutine()
        {
            while (IsOnFire && healthComponent != null && !healthComponent.IsDead)
            {
                // Drain health
                healthComponent.TakeElementalDamage(burnRate * Time.deltaTime, ElementCategory.Fire);

                // Weaken joints
                WeakenJoints();

                yield return null;
            }

            // Fire burns out when the object is destroyed
            if (healthComponent != null && healthComponent.IsDead)
            {
                IsOnFire = false;
            }
        }

        /// <summary>
        /// After the spread delay, periodically attempts to ignite nearby Flammable objects.
        /// </summary>
        private IEnumerator SpreadFireRoutine()
        {
            yield return new WaitForSeconds(spreadDelay);

            while (IsOnFire)
            {
                SpreadToNearby();
                yield return new WaitForSeconds(spreadDelay);
            }
        }

        /// <summary>
        /// Reduces breakForce on all attached joints over time while burning.
        /// </summary>
        private void WeakenJoints()
        {
            if (joints == null) return;

            for (int i = 0; i < joints.Length; i++)
            {
                if (joints[i] == null) continue;
                if (float.IsInfinity(joints[i].breakForce)) continue;

                joints[i].breakForce -= jointWeakeningRate * Time.deltaTime;

                if (joints[i].breakForce <= 0f)
                {
                    Destroy(joints[i]);
                }
            }
        }

        /// <summary>
        /// Finds nearby Flammable objects within spread radius and ignites them.
        /// </summary>
        private void SpreadToNearby()
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(
                transform.position,
                spreadRadius
            );

            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].gameObject == gameObject) continue;

                var otherFlammable = hits[i].GetComponent<Flammable>();
                if (otherFlammable != null && !otherFlammable.IsOnFire)
                {
                    otherFlammable.Ignite();
                }
            }
        }

        #endregion
    }
}
