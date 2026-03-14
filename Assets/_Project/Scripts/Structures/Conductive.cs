using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ElementalSiege.Elements;
using ElementalSiege.Environment;
using ElementCategory = ElementalSiege.Elements.ElementCategory;

namespace ElementalSiege.Structures
{
    /// <summary>
    /// Makes a structure conduct lightning. When charged, the structure glows,
    /// passes the charge to nearby conductive objects, and can trigger mechanical parts.
    /// Metal structures are conductive by default.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [DisallowMultipleComponent]
    public class Conductive : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Conductivity Settings")]

        /// <summary>How well this object passes lightning (0 = insulator, 1 = perfect conductor).</summary>
        [SerializeField]
        [Tooltip("0 = no conduction, 1 = perfect conductor. Determines damage falloff in chains.")]
        [Range(0f, 1f)]
        private float conductivity = 0.8f;

        /// <summary>Maximum distance lightning can jump from this object to another conductor.</summary>
        [SerializeField]
        [Tooltip("World-space radius for lightning to chain to nearby Conductive objects.")]
        [Min(0f)]
        private float chainRadius = 3f;

        /// <summary>Duration in seconds that this object remains charged after receiving lightning.</summary>
        [SerializeField]
        [Tooltip("How long the charged state persists.")]
        [Min(0.1f)]
        private float chargeDuration = 1.0f;

        /// <summary>Damage dealt to StructureHealth when this object becomes charged.</summary>
        [SerializeField]
        [Tooltip("Lightning damage applied to this structure's health on charge.")]
        [Min(0f)]
        private float selfDamage = 15f;

        [Header("Visual Effects")]

        /// <summary>Color of the glow effect when charged.</summary>
        [SerializeField]
        [Tooltip("Sprite tint when conducting electricity.")]
        private Color chargedGlowColor = new Color(0.5f, 0.8f, 1f, 1f);

        /// <summary>Particle system for spark/glow effect when charged.</summary>
        [SerializeField]
        [Tooltip("Spark particle effect activated while charged.")]
        private ParticleSystem sparkVFX;

        [Header("Chain Settings")]

        /// <summary>Delay before lightning chains to the next conductor.</summary>
        [SerializeField]
        [Tooltip("Seconds before the charge propagates to nearby conductors.")]
        [Min(0f)]
        private float chainDelay = 0.1f;

        /// <summary>Maximum number of chain hops from this object.</summary>
        [SerializeField]
        [Tooltip("Limits how many times lightning can chain from this point.")]
        [Min(0)]
        private int maxChainDepth = 5;

        #endregion

        #region Public Properties

        /// <summary>Whether this object is currently conducting electricity.</summary>
        public bool IsCharged { get; private set; }

        /// <summary>The conductivity value (0-1).</summary>
        public float Conductivity => conductivity;

        /// <summary>The chain radius for lightning propagation.</summary>
        public float ChainRadius => chainRadius;

        #endregion

        #region Cached References

        private SpriteRenderer spriteRenderer;
        private StructureHealth healthComponent;
        private Color originalColor;
        private Coroutine chargeCoroutine;

        /// <summary>Tracks which objects have been charged in the current chain to prevent loops.</summary>
        private static readonly HashSet<int> currentChainVisited = new HashSet<int>();

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            healthComponent = GetComponent<StructureHealth>();

            if (spriteRenderer != null)
            {
                originalColor = spriteRenderer.color;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Charges this conductive object with lightning. Initiates a new chain if this
        /// is the first object in the chain.
        /// </summary>
        /// <param name="damageMultiplier">Multiplier applied to self-damage, reduced through chain.</param>
        public void Charge(float damageMultiplier = 1f)
        {
            Charge(damageMultiplier, 0);
        }

        /// <summary>
        /// Charges this conductive object with lightning at a specific chain depth.
        /// </summary>
        /// <param name="damageMultiplier">Multiplier for damage, decays through chain.</param>
        /// <param name="depth">Current depth in the chain (0 = origin).</param>
        public void Charge(float damageMultiplier, int depth)
        {
            if (IsCharged) return;
            if (depth > maxChainDepth) return;

            // Start a new chain tracking set at depth 0
            if (depth == 0)
            {
                currentChainVisited.Clear();
            }

            int instanceId = gameObject.GetInstanceID();
            if (currentChainVisited.Contains(instanceId)) return;
            currentChainVisited.Add(instanceId);

            IsCharged = true;

            // Apply self-damage
            if (healthComponent != null && selfDamage > 0f)
            {
                healthComponent.TakeElementalDamage(selfDamage * damageMultiplier, ElementCategory.Lightning);
            }

            // Activate visual effects
            ActivateChargedVisuals();

            // Trigger connected mechanical parts
            TriggerMechanicalParts();

            // Start charge duration and chain propagation
            if (chargeCoroutine != null)
            {
                StopCoroutine(chargeCoroutine);
            }
            chargeCoroutine = StartCoroutine(ChargeRoutine(damageMultiplier, depth));
        }

        /// <summary>
        /// Immediately discharges this object, ending the charged state.
        /// </summary>
        public void Discharge()
        {
            if (!IsCharged) return;

            IsCharged = false;

            if (chargeCoroutine != null)
            {
                StopCoroutine(chargeCoroutine);
                chargeCoroutine = null;
            }

            DeactivateChargedVisuals();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Manages the charge lifecycle: chains to neighbors, then discharges after duration.
        /// </summary>
        private IEnumerator ChargeRoutine(float damageMultiplier, int depth)
        {
            // Brief delay before chaining
            yield return new WaitForSeconds(chainDelay);

            // Chain to nearby conductors
            ChainToNearby(damageMultiplier, depth);

            // Wait for remaining charge duration
            float remainingDuration = Mathf.Max(0f, chargeDuration - chainDelay);
            yield return new WaitForSeconds(remainingDuration);

            // Discharge
            IsCharged = false;
            DeactivateChargedVisuals();
            chargeCoroutine = null;
        }

        /// <summary>
        /// Finds nearby Conductive objects and charges them with reduced damage.
        /// </summary>
        private void ChainToNearby(float damageMultiplier, int currentDepth)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(
                transform.position,
                chainRadius
            );

            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].gameObject == gameObject) continue;

                var otherConductive = hits[i].GetComponent<Conductive>();
                if (otherConductive != null && !otherConductive.IsCharged)
                {
                    // Damage falls off based on conductivity
                    float nextMultiplier = damageMultiplier * conductivity;
                    otherConductive.Charge(nextMultiplier, currentDepth + 1);
                }
            }
        }

        /// <summary>
        /// Triggers any MechanicalPart components on this or connected GameObjects.
        /// </summary>
        private void TriggerMechanicalParts()
        {
            var mechanicalParts = GetComponents<MechanicalPart>();
            for (int i = 0; i < mechanicalParts.Length; i++)
            {
                mechanicalParts[i].Activate();
            }

            // Also check children for connected mechanical parts
            var childParts = GetComponentsInChildren<MechanicalPart>();
            for (int i = 0; i < childParts.Length; i++)
            {
                childParts[i].Activate();
            }
        }

        /// <summary>
        /// Activates glow tint and spark particle effects.
        /// </summary>
        private void ActivateChargedVisuals()
        {
            if (spriteRenderer != null)
            {
                originalColor = spriteRenderer.color;
                spriteRenderer.color = chargedGlowColor;
            }

            if (sparkVFX != null)
            {
                sparkVFX.gameObject.SetActive(true);
                sparkVFX.Play();
            }
        }

        /// <summary>
        /// Restores original sprite color and stops spark effects.
        /// </summary>
        private void DeactivateChargedVisuals()
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = originalColor;
            }

            if (sparkVFX != null)
            {
                sparkVFX.Stop();
            }
        }

        #endregion

        #region Editor Gizmos

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.5f, 0.8f, 1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, chainRadius);
        }
#endif

        #endregion
    }
}
