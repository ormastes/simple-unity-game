using System.Collections.Generic;
using UnityEngine;

namespace ElementalSiege.Environment
{
    /// <summary>
    /// Localized gravity modification zone that attracts or repels objects within its radius.
    /// Uses a PointEffector2D for physics-based gravity pull/push with configurable force,
    /// radius, and toggle behavior. Affects both orbs and structure debris.
    /// </summary>
    [RequireComponent(typeof(CircleCollider2D))]
    [RequireComponent(typeof(PointEffector2D))]
    [DisallowMultipleComponent]
    public class GravityZone : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Gravity Settings")]

        /// <summary>
        /// Force magnitude. Positive = repel, Negative = attract (matches PointEffector2D convention).
        /// </summary>
        [SerializeField]
        [Tooltip("Gravity force. Negative values attract, positive values repel.")]
        private float gravityForce = -20f;

        /// <summary>Radius of the gravity effect.</summary>
        [SerializeField]
        [Tooltip("World-space radius of the gravity zone.")]
        [Min(0.1f)]
        private float gravityRadius = 5f;

        /// <summary>
        /// How force falls off with distance. 0 = constant, 1 = linear, 2 = inverse square.
        /// </summary>
        [SerializeField]
        [Tooltip("Force falloff: 0 = constant, 1 = linear, 2 = inverse square.")]
        [Range(0f, 2f)]
        private float distanceDecay = 1f;

        /// <summary>Whether this gravity zone is currently active.</summary>
        [SerializeField]
        [Tooltip("Toggle gravity zone on or off.")]
        private bool isActive = true;

        [Header("Activation Mode")]

        /// <summary>How the gravity zone is activated.</summary>
        [SerializeField]
        [Tooltip("Constant = always on. OnTrigger = activates when an object enters.")]
        private ActivationMode activationMode = ActivationMode.Constant;

        /// <summary>Duration the zone stays active after trigger activation (OnTrigger mode).</summary>
        [SerializeField]
        [Tooltip("Seconds the zone remains active after being triggered.")]
        [Min(0.1f)]
        private float triggerDuration = 3f;

        [Header("Visual Effects")]

        /// <summary>Particle system for visual distortion/swirl effect.</summary>
        [SerializeField]
        [Tooltip("Particle swirl or distortion effect showing the gravity field.")]
        private ParticleSystem distortionVFX;

        /// <summary>Color of the zone when attracting.</summary>
        [SerializeField]
        private Color attractColor = new Color(0.3f, 0.3f, 1f, 0.3f);

        /// <summary>Color of the zone when repelling.</summary>
        [SerializeField]
        private Color repelColor = new Color(1f, 0.3f, 0.3f, 0.3f);

        #endregion

        #region Enums

        /// <summary>Defines how the gravity zone is activated.</summary>
        public enum ActivationMode
        {
            /// <summary>Zone is always active when enabled.</summary>
            Constant,
            /// <summary>Zone activates when an object enters, then deactivates after a duration.</summary>
            OnTrigger
        }

        #endregion

        #region Public Properties

        /// <summary>Whether this gravity zone is currently active.</summary>
        public bool IsActive
        {
            get => isActive;
            set => SetActive(value);
        }

        /// <summary>Whether the zone attracts (true) or repels (false).</summary>
        public bool IsAttracting => gravityForce < 0f;

        /// <summary>The effective gravity radius.</summary>
        public float Radius => gravityRadius;

        #endregion

        #region Cached References

        private CircleCollider2D circleCollider;
        private PointEffector2D pointEffector;
        private SpriteRenderer spriteRenderer;
        private float triggerTimer;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            circleCollider = GetComponent<CircleCollider2D>();
            pointEffector = GetComponent<PointEffector2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();

            ConfigureCollider();
            ConfigureEffector();
            UpdateVisuals();
        }

        private void Update()
        {
            if (activationMode == ActivationMode.OnTrigger && isActive)
            {
                triggerTimer -= Time.deltaTime;
                if (triggerTimer <= 0f)
                {
                    SetActive(false);
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (activationMode == ActivationMode.OnTrigger && !isActive)
            {
                // Activate on first object entry
                if (other.attachedRigidbody != null)
                {
                    triggerTimer = triggerDuration;
                    SetActive(true);
                }
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Toggles the gravity zone on or off.
        /// </summary>
        public void Toggle()
        {
            SetActive(!isActive);
        }

        /// <summary>
        /// Sets the gravity force. Negative = attract, positive = repel.
        /// </summary>
        /// <param name="force">New gravity force value.</param>
        public void SetGravityForce(float force)
        {
            gravityForce = force;
            ConfigureEffector();
            UpdateVisuals();
        }

        /// <summary>
        /// Sets the gravity radius.
        /// </summary>
        /// <param name="radius">New radius in world units.</param>
        public void SetRadius(float radius)
        {
            gravityRadius = Mathf.Max(0.1f, radius);
            circleCollider.radius = gravityRadius;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Configures the CircleCollider2D as a trigger with the specified radius.
        /// </summary>
        private void ConfigureCollider()
        {
            circleCollider.isTrigger = true;
            circleCollider.radius = gravityRadius;
        }

        /// <summary>
        /// Configures the PointEffector2D with current gravity settings.
        /// </summary>
        private void ConfigureEffector()
        {
            pointEffector.forceMagnitude = gravityForce;
            pointEffector.forceMode = EffectorForceMode2D.InverseLinear;
            pointEffector.distanceScale = distanceDecay;
            pointEffector.forceSource = EffectorSelection2D.Collider;
            pointEffector.forceTarget = EffectorSelection2D.Rigidbody;

            circleCollider.usedByEffector = true;

            pointEffector.enabled = isActive;
        }

        /// <summary>
        /// Sets the active state and updates the effector and visuals.
        /// </summary>
        /// <param name="active">Whether the zone should be active.</param>
        private void SetActive(bool active)
        {
            isActive = active;
            pointEffector.enabled = isActive;
            UpdateVisuals();
        }

        /// <summary>
        /// Updates visual effects based on current state (active/inactive, attract/repel).
        /// </summary>
        private void UpdateVisuals()
        {
            if (spriteRenderer != null)
            {
                Color targetColor = IsAttracting ? attractColor : repelColor;
                if (!isActive)
                {
                    targetColor.a *= 0.3f;
                }
                spriteRenderer.color = targetColor;
            }

            if (distortionVFX != null)
            {
                if (isActive && !distortionVFX.isPlaying)
                {
                    distortionVFX.Play();
                }
                else if (!isActive && distortionVFX.isPlaying)
                {
                    distortionVFX.Stop();
                }
            }
        }

        #endregion

        #region Editor Gizmos

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Color gizmoColor = gravityForce < 0f
                ? new Color(0.3f, 0.3f, 1f, 0.2f)
                : new Color(1f, 0.3f, 0.3f, 0.2f);

            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(transform.position, gravityRadius);

            // Draw direction indicators
            gizmoColor.a = 0.5f;
            Gizmos.color = gizmoColor;

            int arrowCount = 8;
            for (int i = 0; i < arrowCount; i++)
            {
                float angle = (360f / arrowCount) * i * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                Vector3 start = transform.position + dir * gravityRadius * 0.3f;
                Vector3 end = transform.position + dir * gravityRadius * 0.7f;

                if (gravityForce < 0f)
                {
                    // Attract: arrows point inward
                    Gizmos.DrawLine(end, start);
                }
                else
                {
                    // Repel: arrows point outward
                    Gizmos.DrawLine(start, end);
                }
            }
        }
#endif

        #endregion
    }
}
