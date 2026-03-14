using UnityEngine;
using ElementalSiege.Orbs;

namespace ElementalSiege.Structures
{
    /// <summary>
    /// Makes a structure respond to wind forces. Wind affects this object based on
    /// its drag and lift coefficients, with lighter materials (Wood) being affected
    /// more than heavy ones (Stone, Metal). Responds to WindZone2D areas and direct gusts.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [DisallowMultipleComponent]
    public class WindAffected : MonoBehaviour, IWindAffectable
    {
        #region Serialized Fields

        [Header("Wind Response")]

        /// <summary>
        /// How much horizontal wind force affects this object (0 = immune, 1 = full effect).
        /// </summary>
        [SerializeField]
        [Tooltip("Horizontal wind force multiplier. Higher = more affected by wind.")]
        [Range(0f, 5f)]
        private float dragCoefficient = 1.0f;

        /// <summary>
        /// Vertical force component from wind (simulates lift). Positive = upward lift.
        /// </summary>
        [SerializeField]
        [Tooltip("Vertical lift force multiplier. Positive values push upward in wind.")]
        [Range(-2f, 2f)]
        private float liftCoefficient = 0.2f;

        /// <summary>
        /// If true, automatically adjusts coefficients based on StructureBlock material.
        /// Lighter materials get higher coefficients.
        /// </summary>
        [SerializeField]
        [Tooltip("Auto-scale wind coefficients based on material weight.")]
        private bool autoScaleByMaterial = true;

        [Header("Limits")]

        /// <summary>Maximum force magnitude that can be applied by wind in a single frame.</summary>
        [SerializeField]
        [Tooltip("Caps the maximum wind force to prevent objects from flying off uncontrollably.")]
        [Min(0f)]
        private float maxWindForce = 50f;

        #endregion

        #region Public Properties

        /// <summary>The horizontal drag coefficient.</summary>
        public float DragCoefficient => dragCoefficient;

        /// <summary>The vertical lift coefficient.</summary>
        public float LiftCoefficient => liftCoefficient;

        /// <summary>Whether wind effects are currently enabled.</summary>
        public bool WindEnabled { get; set; } = true;

        #endregion

        #region Cached References

        private Rigidbody2D rb;
        private float materialScale = 1f;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();

            if (autoScaleByMaterial)
            {
                CalculateMaterialScale();
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Applies a wind force to this object. Called by WindZone2D or other wind sources.
        /// The force is scaled by the drag/lift coefficients and material weight.
        /// </summary>
        /// <param name="windDirection">Direction of the wind (not necessarily normalized).</param>
        /// <param name="windStrength">Base strength/magnitude of the wind force.</param>
        public void ApplyWind(Vector2 windDirection, float windStrength)
        {
            if (!WindEnabled) return;
            if (rb == null || rb.bodyType == RigidbodyType2D.Static) return;

            Vector2 normalizedDir = windDirection.normalized;

            // Calculate horizontal (drag) force
            Vector2 horizontalForce = normalizedDir * windStrength * dragCoefficient * materialScale;

            // Calculate vertical (lift) force — perpendicular to wind direction
            Vector2 liftForce = Vector2.up * windStrength * liftCoefficient * materialScale;

            Vector2 totalForce = horizontalForce + liftForce;

            // Clamp to max force
            if (totalForce.magnitude > maxWindForce)
            {
                totalForce = totalForce.normalized * maxWindForce;
            }

            rb.AddForce(totalForce, ForceMode2D.Force);
        }

        /// <summary>
        /// Applies an instantaneous wind gust (impulse) to this object.
        /// Used for sudden bursts from WindOrb or environmental triggers.
        /// </summary>
        /// <param name="gustDirection">Direction of the gust.</param>
        /// <param name="gustForce">Impulse magnitude of the gust.</param>
        public void ApplyGust(Vector2 gustDirection, float gustForce)
        {
            if (!WindEnabled) return;
            if (rb == null || rb.bodyType == RigidbodyType2D.Static) return;

            Vector2 force = gustDirection.normalized * gustForce * dragCoefficient * materialScale;

            // Clamp to max force
            if (force.magnitude > maxWindForce)
            {
                force = force.normalized * maxWindForce;
            }

            rb.AddForce(force, ForceMode2D.Impulse);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Calculates a material-based scale factor. Lighter materials receive a higher
        /// scale, making them more susceptible to wind.
        /// </summary>
        private void CalculateMaterialScale()
        {
            var block = GetComponent<StructureBlock>();
            if (block == null)
            {
                materialScale = 1f;
                return;
            }

            // Inverse relationship: lighter materials are more affected
            // Base density is Wood (1.0), so scale relative to that
            float density = StructureBlock.GetDensity(block.Material);
            materialScale = Mathf.Clamp(1f / density, 0.1f, 3f);
        }

        #endregion
    }
}
