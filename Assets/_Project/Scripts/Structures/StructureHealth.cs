using System;
using System.Collections;
using UnityEngine;

namespace ElementalSiege.Structures
{
    /// <summary>
    /// Health system for destructible structures. Handles collision-based damage,
    /// elemental damage multipliers, visual feedback, and invulnerability.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [DisallowMultipleComponent]
    public class StructureHealth : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Health")]

        /// <summary>Maximum health of this structure.</summary>
        [SerializeField]
        [Tooltip("Maximum health. Initialized from StructureBlock material if zero.")]
        private float maxHealth;

        /// <summary>Minimum collision force magnitude required to inflict damage.</summary>
        [SerializeField]
        [Tooltip("Collisions below this relative velocity magnitude are ignored.")]
        [Min(0f)]
        private float damageThreshold = 1.5f;

        /// <summary>Multiplier applied to collision force when calculating damage.</summary>
        [SerializeField]
        [Tooltip("Scales the raw collision impulse into a damage value.")]
        [Min(0f)]
        private float collisionDamageScale = 1.0f;

        [Header("Elemental Multipliers")]

        /// <summary>Damage multiplier when hit by a Fire element.</summary>
        [SerializeField] [Min(0f)] private float fireDamageMultiplier = 1.0f;

        /// <summary>Damage multiplier when hit by an Ice element.</summary>
        [SerializeField] [Min(0f)] private float iceDamageMultiplier = 1.0f;

        /// <summary>Damage multiplier when hit by a Lightning element.</summary>
        [SerializeField] [Min(0f)] private float lightningDamageMultiplier = 1.0f;

        /// <summary>Damage multiplier when hit by a Wind element.</summary>
        [SerializeField] [Min(0f)] private float windDamageMultiplier = 1.0f;

        /// <summary>Damage multiplier when hit by a Crystal element.</summary>
        [SerializeField] [Min(0f)] private float crystalDamageMultiplier = 1.0f;

        [Header("Visual Feedback")]

        /// <summary>Color flashed on the sprite when damage is taken.</summary>
        [SerializeField]
        [Tooltip("Sprite tint applied briefly when damage is received.")]
        private Color damageFlashColor = Color.red;

        /// <summary>Duration of the damage flash in seconds.</summary>
        [SerializeField]
        [Tooltip("How long the damage flash lasts.")]
        [Min(0.01f)]
        private float damageFlashDuration = 0.1f;

        [Header("Invulnerability")]

        /// <summary>When true, this structure cannot take damage.</summary>
        [SerializeField]
        [Tooltip("Makes this structure immune to all damage (for tutorials/fixed blocks).")]
        private bool isInvulnerable;

        #endregion

        #region Events

        /// <summary>
        /// Raised whenever health changes. Parameters: (currentHealth, maxHealth).
        /// </summary>
        public event Action<float, float> OnHealthChanged;

        /// <summary>Raised when health reaches zero.</summary>
        public event Action OnDeath;

        #endregion

        #region Public Properties

        /// <summary>Current health of this structure.</summary>
        public float CurrentHealth { get; private set; }

        /// <summary>Maximum health of this structure.</summary>
        public float MaxHealth => maxHealth;

        /// <summary>Whether this structure is invulnerable.</summary>
        public bool IsInvulnerable
        {
            get => isInvulnerable;
            set => isInvulnerable = value;
        }

        /// <summary>Whether this structure is dead (health &lt;= 0).</summary>
        public bool IsDead { get; private set; }

        /// <summary>Current health as a 0-1 ratio.</summary>
        public float HealthPercent => maxHealth > 0f ? Mathf.Clamp01(CurrentHealth / maxHealth) : 0f;

        #endregion

        #region Cached References

        private SpriteRenderer spriteRenderer;
        private Rigidbody2D rb;
        private Color originalColor;
        private Coroutine flashCoroutine;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();

            if (spriteRenderer != null)
            {
                originalColor = spriteRenderer.color;
            }

            InitializeHealth();
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (IsDead || isInvulnerable) return;

            float impactForce = collision.relativeVelocity.magnitude;

            if (impactForce < damageThreshold) return;

            // Factor in the mass of the colliding object for heavier impacts
            float otherMass = 1f;
            if (collision.rigidbody != null)
            {
                otherMass = collision.rigidbody.mass;
            }

            float damage = impactForce * otherMass * collisionDamageScale;
            TakeDamage(damage);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Applies raw damage to this structure (before elemental multipliers).
        /// </summary>
        /// <param name="damage">Amount of damage to apply.</param>
        public void TakeDamage(float damage)
        {
            if (IsDead || isInvulnerable) return;
            if (damage <= 0f) return;

            CurrentHealth -= damage;
            CurrentHealth = Mathf.Max(CurrentHealth, 0f);

            OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
            FlashDamageColor();

            if (CurrentHealth <= 0f)
            {
                Die();
            }
        }

        /// <summary>
        /// Applies elemental damage with the appropriate multiplier.
        /// </summary>
        /// <param name="baseDamage">Raw damage before multiplier.</param>
        /// <param name="elementType">The element type of the incoming damage.</param>
        public void TakeElementalDamage(float baseDamage, ElementType elementType)
        {
            float multiplier = GetElementMultiplier(elementType);
            TakeDamage(baseDamage * multiplier);
        }

        /// <summary>
        /// Heals the structure by the specified amount, clamped to max health.
        /// </summary>
        /// <param name="amount">Amount of health to restore.</param>
        public void Heal(float amount)
        {
            if (IsDead) return;
            if (amount <= 0f) return;

            CurrentHealth = Mathf.Min(CurrentHealth + amount, maxHealth);
            OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        }

        /// <summary>
        /// Resets health to maximum and clears the dead state.
        /// </summary>
        public void ResetHealth()
        {
            IsDead = false;
            CurrentHealth = maxHealth;
            OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Initializes max health from StructureBlock material data if not manually set.
        /// </summary>
        private void InitializeHealth()
        {
            if (maxHealth <= 0f)
            {
                var block = GetComponent<StructureBlock>();
                if (block != null)
                {
                    maxHealth = block.BaseHealth;
                }
                else
                {
                    maxHealth = 100f;
                }
            }

            CurrentHealth = maxHealth;

            // Apply default elemental multipliers based on material
            var structureBlock = GetComponent<StructureBlock>();
            if (structureBlock != null)
            {
                ApplyDefaultElementalMultipliers(structureBlock.Material);
            }
        }

        /// <summary>
        /// Sets default elemental multipliers based on the material type.
        /// Only overrides if the multiplier is still at the default 1.0 value.
        /// </summary>
        /// <param name="material">The material type of this structure.</param>
        private void ApplyDefaultElementalMultipliers(MaterialType material)
        {
            switch (material)
            {
                case MaterialType.Wood:
                    fireDamageMultiplier = Mathf.Approximately(fireDamageMultiplier, 1f) ? 2.0f : fireDamageMultiplier;
                    iceDamageMultiplier = Mathf.Approximately(iceDamageMultiplier, 1f) ? 0.5f : iceDamageMultiplier;
                    break;
                case MaterialType.Stone:
                    fireDamageMultiplier = Mathf.Approximately(fireDamageMultiplier, 1f) ? 0.5f : fireDamageMultiplier;
                    windDamageMultiplier = Mathf.Approximately(windDamageMultiplier, 1f) ? 0.3f : windDamageMultiplier;
                    break;
                case MaterialType.Metal:
                    lightningDamageMultiplier = Mathf.Approximately(lightningDamageMultiplier, 1f) ? 2.0f : lightningDamageMultiplier;
                    fireDamageMultiplier = Mathf.Approximately(fireDamageMultiplier, 1f) ? 0.3f : fireDamageMultiplier;
                    break;
                case MaterialType.Glass:
                    crystalDamageMultiplier = Mathf.Approximately(crystalDamageMultiplier, 1f) ? 2.0f : crystalDamageMultiplier;
                    break;
                case MaterialType.Ice:
                    fireDamageMultiplier = Mathf.Approximately(fireDamageMultiplier, 1f) ? 3.0f : fireDamageMultiplier;
                    iceDamageMultiplier = Mathf.Approximately(iceDamageMultiplier, 1f) ? 0.0f : iceDamageMultiplier;
                    break;
                case MaterialType.Crystal:
                    lightningDamageMultiplier = Mathf.Approximately(lightningDamageMultiplier, 1f) ? 1.5f : lightningDamageMultiplier;
                    break;
            }
        }

        /// <summary>
        /// Returns the damage multiplier for a given element type.
        /// </summary>
        /// <param name="elementType">The element dealing damage.</param>
        /// <returns>The multiplier to apply to base damage.</returns>
        private float GetElementMultiplier(ElementType elementType)
        {
            return elementType switch
            {
                ElementType.Fire      => fireDamageMultiplier,
                ElementType.Ice       => iceDamageMultiplier,
                ElementType.Lightning => lightningDamageMultiplier,
                ElementType.Wind      => windDamageMultiplier,
                ElementType.Crystal   => crystalDamageMultiplier,
                _                     => 1.0f
            };
        }

        /// <summary>
        /// Triggers the death sequence: raises OnDeath and marks as dead.
        /// </summary>
        private void Die()
        {
            if (IsDead) return;
            IsDead = true;
            OnDeath?.Invoke();
        }

        /// <summary>
        /// Briefly flashes the sprite to the damage color, then restores the original.
        /// </summary>
        private void FlashDamageColor()
        {
            if (spriteRenderer == null) return;

            if (flashCoroutine != null)
            {
                StopCoroutine(flashCoroutine);
            }

            flashCoroutine = StartCoroutine(DamageFlashRoutine());
        }

        private IEnumerator DamageFlashRoutine()
        {
            spriteRenderer.color = damageFlashColor;
            yield return new WaitForSeconds(damageFlashDuration);
            spriteRenderer.color = originalColor;
            flashCoroutine = null;
        }

        #endregion
    }

    /// <summary>
    /// Defines the elemental types used for damage calculations.
    /// </summary>
    public enum ElementType
    {
        None,
        Fire,
        Ice,
        Lightning,
        Wind,
        Crystal
    }
}
