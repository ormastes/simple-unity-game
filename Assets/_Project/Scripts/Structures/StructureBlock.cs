using System;
using UnityEngine;

namespace ElementalSiege.Structures
{
    /// <summary>
    /// Defines the material types available for destructible blocks.
    /// Each material determines base health, density, score value, and visual appearance.
    /// </summary>
    public enum MaterialType
    {
        Wood,
        Stone,
        Metal,
        Glass,
        Ice,
        Crystal
    }

    /// <summary>
    /// Base component for all destructible structure blocks in Elemental Siege.
    /// Manages material properties, visual damage states, destruction behavior, and scoring.
    /// Attach to any GameObject that should act as a destructible building block.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    [DisallowMultipleComponent]
    public class StructureBlock : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Material Configuration")]

        /// <summary>The material type of this block, determining base stats and behavior.</summary>
        [SerializeField]
        [Tooltip("Determines base health, density, score value, and elemental interactions.")]
        private MaterialType materialType = MaterialType.Wood;

        [Header("Damage Visuals")]

        /// <summary>
        /// Sprites representing progressive damage states.
        /// Index 0 = healthy, last index = most damaged (just before destruction).
        /// </summary>
        [SerializeField]
        [Tooltip("Sprites for each damage state. Index 0 = healthy, last = nearly destroyed.")]
        private Sprite[] damageSprites;

        [Header("Destruction Effects")]

        /// <summary>Particle system prefab spawned when this block is destroyed.</summary>
        [SerializeField]
        [Tooltip("Prefab instantiated at the block's position on destruction.")]
        private ParticleSystem debrisParticlePrefab;

        /// <summary>Number of debris particles to emit on destruction.</summary>
        [SerializeField]
        [Tooltip("Burst count for the debris particle effect.")]
        [Range(1, 100)]
        private int debrisParticleCount = 15;

        [Header("Score")]

        /// <summary>Override score value. If zero, uses material-based default.</summary>
        [SerializeField]
        [Tooltip("Score awarded when this block is destroyed. 0 = use material default.")]
        private int scoreOverride;

        #endregion

        #region Events

        /// <summary>Raised when this block takes damage. Parameter is the damage amount.</summary>
        public event Action<float> OnDamaged;

        /// <summary>Raised when this block is destroyed.</summary>
        public event Action OnDestroyed;

        #endregion

        #region Public Properties

        /// <summary>The material type assigned to this block.</summary>
        public MaterialType Material => materialType;

        /// <summary>The base health value derived from the material type.</summary>
        public float BaseHealth => GetBaseHealth(materialType);

        /// <summary>The density value derived from the material type.</summary>
        public float Density => GetDensity(materialType);

        /// <summary>The score value awarded when this block is destroyed.</summary>
        public int ScoreValue => scoreOverride > 0 ? scoreOverride : GetDefaultScore(materialType);

        /// <summary>Whether this block has been destroyed.</summary>
        public bool IsDestroyed { get; private set; }

        #endregion

        #region Cached References

        private Rigidbody2D rb;
        private SpriteRenderer spriteRenderer;
        private StructureHealth healthComponent;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            healthComponent = GetComponent<StructureHealth>();

            ApplyMaterialProperties();
        }

        private void OnEnable()
        {
            if (healthComponent != null)
            {
                healthComponent.OnHealthChanged += HandleHealthChanged;
                healthComponent.OnDeath += HandleDeath;
            }
        }

        private void OnDisable()
        {
            if (healthComponent != null)
            {
                healthComponent.OnHealthChanged -= HandleHealthChanged;
                healthComponent.OnDeath -= HandleDeath;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Applies damage to this block and raises the <see cref="OnDamaged"/> event.
        /// Delegates actual health reduction to <see cref="StructureHealth"/> if present.
        /// </summary>
        /// <param name="damage">The amount of damage to apply.</param>
        public void ApplyDamage(float damage)
        {
            if (IsDestroyed) return;

            OnDamaged?.Invoke(damage);

            if (healthComponent != null)
            {
                healthComponent.TakeDamage(damage);
            }
        }

        /// <summary>
        /// Forces immediate destruction of this block regardless of remaining health.
        /// </summary>
        public void ForceDestroy()
        {
            if (IsDestroyed) return;
            DestroyBlock();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Configures the Rigidbody2D mass based on the material's density.
        /// </summary>
        private void ApplyMaterialProperties()
        {
            rb.mass = Density;
        }

        /// <summary>
        /// Updates the damage sprite based on current vs max health ratio.
        /// </summary>
        /// <param name="current">Current health value.</param>
        /// <param name="max">Maximum health value.</param>
        private void HandleHealthChanged(float current, float max)
        {
            UpdateDamageSprite(current, max);
        }

        /// <summary>
        /// Handles the death event from the health component.
        /// </summary>
        private void HandleDeath()
        {
            DestroyBlock();
        }

        /// <summary>
        /// Swaps the displayed sprite based on the health percentage and the available
        /// damage sprites array.
        /// </summary>
        /// <param name="current">Current health.</param>
        /// <param name="max">Maximum health.</param>
        private void UpdateDamageSprite(float current, float max)
        {
            if (damageSprites == null || damageSprites.Length == 0) return;

            float healthPercent = Mathf.Clamp01(current / max);
            // Map health percentage to sprite index (0 = healthy = full health)
            int index = Mathf.FloorToInt((1f - healthPercent) * (damageSprites.Length - 1));
            index = Mathf.Clamp(index, 0, damageSprites.Length - 1);

            if (damageSprites[index] != null)
            {
                spriteRenderer.sprite = damageSprites[index];
            }
        }

        /// <summary>
        /// Performs destruction: spawns debris, raises event, and removes the GameObject.
        /// </summary>
        private void DestroyBlock()
        {
            if (IsDestroyed) return;
            IsDestroyed = true;

            SpawnDebris();
            OnDestroyed?.Invoke();
            Destroy(gameObject);
        }

        /// <summary>
        /// Instantiates the debris particle prefab and emits the configured burst.
        /// </summary>
        private void SpawnDebris()
        {
            if (debrisParticlePrefab == null) return;

            ParticleSystem debris = Instantiate(
                debrisParticlePrefab,
                transform.position,
                Quaternion.identity
            );

            var emission = debris.emission;
            emission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, debrisParticleCount)
            });

            debris.Play();

            // Auto-destroy the particle system after it finishes
            Destroy(debris.gameObject, debris.main.duration + debris.main.startLifetime.constantMax);
        }

        #endregion

        #region Static Material Data

        /// <summary>Returns the base health for the given material type.</summary>
        public static float GetBaseHealth(MaterialType type)
        {
            return type switch
            {
                MaterialType.Wood    => 80f,
                MaterialType.Stone   => 150f,
                MaterialType.Metal   => 200f,
                MaterialType.Glass   => 40f,
                MaterialType.Ice     => 60f,
                MaterialType.Crystal => 120f,
                _                    => 100f
            };
        }

        /// <summary>Returns the density (Rigidbody2D mass) for the given material type.</summary>
        public static float GetDensity(MaterialType type)
        {
            return type switch
            {
                MaterialType.Wood    => 1.0f,
                MaterialType.Stone   => 3.0f,
                MaterialType.Metal   => 5.0f,
                MaterialType.Glass   => 1.5f,
                MaterialType.Ice     => 1.2f,
                MaterialType.Crystal => 2.5f,
                _                    => 1.0f
            };
        }

        /// <summary>Returns the default score value for destroying a block of the given material.</summary>
        public static int GetDefaultScore(MaterialType type)
        {
            return type switch
            {
                MaterialType.Wood    => 100,
                MaterialType.Stone   => 200,
                MaterialType.Metal   => 300,
                MaterialType.Glass   => 150,
                MaterialType.Ice     => 150,
                MaterialType.Crystal => 250,
                _                    => 100
            };
        }

        #endregion
    }
}
