using System.Collections.Generic;
using UnityEngine;

namespace ElementalSiege.Environment
{
    /// <summary>
    /// 2D wind area that applies directional force to objects with the
    /// <see cref="Structures.WindAffected"/> component. Supports constant wind,
    /// periodic gusts, visual wind particles, and toggling for puzzle mechanics.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    [DisallowMultipleComponent]
    public class WindZone2D : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Wind Settings")]

        /// <summary>Direction of the wind force.</summary>
        [SerializeField]
        [Tooltip("Direction the wind blows. Does not need to be normalized.")]
        private Vector2 windDirection = Vector2.right;

        /// <summary>Base force magnitude of the wind.</summary>
        [SerializeField]
        [Tooltip("Base wind force strength.")]
        [Min(0f)]
        private float windForce = 10f;

        /// <summary>Whether this wind zone is currently active.</summary>
        [SerializeField]
        [Tooltip("Toggle wind on/off for puzzle mechanics.")]
        private bool isActive = true;

        [Header("Gusts")]

        /// <summary>If true, wind force varies periodically to simulate gusts.</summary>
        [SerializeField]
        [Tooltip("Enable periodic wind force variation.")]
        private bool enableGusts;

        /// <summary>Time in seconds between gust peaks.</summary>
        [SerializeField]
        [Tooltip("Period of gust oscillation in seconds.")]
        [Min(0.1f)]
        private float gustPeriod = 3f;

        /// <summary>Maximum additional force during a gust peak.</summary>
        [SerializeField]
        [Tooltip("Extra force added at gust peaks.")]
        [Min(0f)]
        private float gustStrength = 15f;

        [Header("Visual Effects")]

        /// <summary>Particle system showing wind direction and strength.</summary>
        [SerializeField]
        [Tooltip("Particle effect indicating wind flow. Automatically oriented to wind direction.")]
        private ParticleSystem windParticles;

        [Header("Area Effector (Optional)")]

        /// <summary>
        /// If true, also uses an AreaEffector2D for basic physics force on all Rigidbody2D objects
        /// (even those without WindAffected). WindAffected objects get the customized force instead.
        /// </summary>
        [SerializeField]
        [Tooltip("Use AreaEffector2D for default physics force on non-WindAffected objects.")]
        private bool useAreaEffector;

        #endregion

        #region Public Properties

        /// <summary>Whether this wind zone is active.</summary>
        public bool IsActive
        {
            get => isActive;
            set
            {
                isActive = value;
                UpdateVisuals();
            }
        }

        /// <summary>Current effective wind force including gust variation.</summary>
        public float CurrentWindForce { get; private set; }

        /// <summary>The wind direction (normalized).</summary>
        public Vector2 WindDirection => windDirection.normalized;

        #endregion

        #region Cached References

        private BoxCollider2D zoneCollider;
        private AreaEffector2D areaEffector;
        private readonly HashSet<Structures.WindAffected> affectedObjects =
            new HashSet<Structures.WindAffected>();

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            zoneCollider = GetComponent<BoxCollider2D>();
            zoneCollider.isTrigger = true;

            if (useAreaEffector)
            {
                SetupAreaEffector();
            }

            UpdateVisuals();
        }

        private void FixedUpdate()
        {
            if (!isActive) return;

            CalculateCurrentForce();
            ApplyWindToAffected();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var windAffected = other.GetComponent<Structures.WindAffected>();
            if (windAffected == null && other.attachedRigidbody != null)
            {
                windAffected = other.attachedRigidbody.GetComponent<Structures.WindAffected>();
            }

            if (windAffected != null)
            {
                affectedObjects.Add(windAffected);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            var windAffected = other.GetComponent<Structures.WindAffected>();
            if (windAffected == null && other.attachedRigidbody != null)
            {
                windAffected = other.attachedRigidbody.GetComponent<Structures.WindAffected>();
            }

            if (windAffected != null)
            {
                affectedObjects.Remove(windAffected);
            }
        }

        private void OnDisable()
        {
            affectedObjects.Clear();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Toggles the wind zone on or off.
        /// </summary>
        public void Toggle()
        {
            IsActive = !IsActive;
        }

        /// <summary>
        /// Sets the wind direction and force.
        /// </summary>
        /// <param name="direction">New wind direction.</param>
        /// <param name="force">New base wind force.</param>
        public void SetWind(Vector2 direction, float force)
        {
            windDirection = direction;
            windForce = force;

            if (areaEffector != null)
            {
                UpdateAreaEffector();
            }

            UpdateVisuals();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Calculates the current effective wind force including gust oscillation.
        /// </summary>
        private void CalculateCurrentForce()
        {
            CurrentWindForce = windForce;

            if (enableGusts)
            {
                // Sine-based gust oscillation, only additive (no negative gusts)
                float gustFactor = (Mathf.Sin(Time.time * (2f * Mathf.PI / gustPeriod)) + 1f) * 0.5f;
                CurrentWindForce += gustStrength * gustFactor;
            }
        }

        /// <summary>
        /// Applies wind force to all WindAffected objects currently inside the zone.
        /// </summary>
        private void ApplyWindToAffected()
        {
            // Clean up destroyed objects
            affectedObjects.RemoveWhere(obj => obj == null);

            foreach (var affected in affectedObjects)
            {
                affected.ApplyWind(windDirection, CurrentWindForce);
            }
        }

        /// <summary>
        /// Sets up the AreaEffector2D for basic physics wind on non-WindAffected objects.
        /// </summary>
        private void SetupAreaEffector()
        {
            areaEffector = GetComponent<AreaEffector2D>();
            if (areaEffector == null)
            {
                areaEffector = gameObject.AddComponent<AreaEffector2D>();
            }

            zoneCollider.usedByEffector = true;
            UpdateAreaEffector();
        }

        /// <summary>
        /// Syncs the AreaEffector2D settings with the current wind parameters.
        /// </summary>
        private void UpdateAreaEffector()
        {
            if (areaEffector == null) return;

            float angle = Mathf.Atan2(windDirection.y, windDirection.x) * Mathf.Rad2Deg;
            areaEffector.forceAngle = angle;
            areaEffector.forceMagnitude = windForce;
            areaEffector.useGlobalAngle = true;
        }

        /// <summary>
        /// Updates visual effects to reflect the current wind state.
        /// </summary>
        private void UpdateVisuals()
        {
            if (windParticles == null) return;

            if (isActive)
            {
                // Orient particles to wind direction
                float angle = Mathf.Atan2(windDirection.y, windDirection.x) * Mathf.Rad2Deg;
                windParticles.transform.rotation = Quaternion.Euler(0f, 0f, angle);

                if (!windParticles.isPlaying)
                {
                    windParticles.Play();
                }
            }
            else
            {
                if (windParticles.isPlaying)
                {
                    windParticles.Stop();
                }
            }
        }

        #endregion

        #region Editor Gizmos

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = isActive ? new Color(0.3f, 0.8f, 1f, 0.3f) : new Color(0.5f, 0.5f, 0.5f, 0.2f);

            var col = GetComponent<BoxCollider2D>();
            if (col != null)
            {
                Vector3 center = transform.position + (Vector3)col.offset;
                Vector3 size = Vector3.Scale(col.size, transform.lossyScale);
                Gizmos.DrawWireCube(center, size);

                // Draw wind direction arrow
                if (isActive)
                {
                    Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.8f);
                    Vector3 dir = (Vector3)windDirection.normalized * Mathf.Min(size.x, size.y) * 0.4f;
                    Gizmos.DrawLine(center, center + dir);
                    Gizmos.DrawSphere(center + dir, 0.1f);
                }
            }
        }
#endif

        #endregion
    }
}
