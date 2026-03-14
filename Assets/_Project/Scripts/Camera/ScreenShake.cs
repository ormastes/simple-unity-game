using UnityEngine;
using Unity.Cinemachine;

namespace ElementalSiege.Camera
{
    /// <summary>
    /// Provides screen shake effects using Cinemachine Impulse.
    /// Attach to any GameObject; the impulse source will be auto-created if not assigned.
    /// </summary>
    public class ScreenShake : MonoBehaviour
    {
        #region Inspector Fields

        [Header("References")]
        [SerializeField]
        [Tooltip("Cinemachine Impulse Source used to generate shake signals. Auto-created if null.")]
        private CinemachineImpulseSource _impulseSource;

        [Header("Default Impulse Settings")]
        [SerializeField]
        [Tooltip("Default amplitude of the shake.")]
        private float _defaultAmplitude = 1f;

        [SerializeField]
        [Tooltip("Default frequency of the shake oscillation.")]
        private float _defaultFrequency = 1f;

        [SerializeField]
        [Tooltip("Default duration of the shake in seconds.")]
        private float _defaultDuration = 0.3f;

        [Header("Scaling")]
        [SerializeField]
        [Tooltip("Multiplier applied to incoming force values to scale the shake intensity.")]
        private float _forceToAmplitudeScale = 0.1f;

        [SerializeField]
        [Tooltip("Maximum amplitude clamp to prevent overly aggressive shakes.")]
        private float _maxAmplitude = 5f;

        [SerializeField]
        [Tooltip("Minimum force required to trigger a shake (prevents micro-shakes).")]
        private float _minForceThreshold = 0.5f;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            EnsureImpulseSource();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Triggers a screen shake at the GameObject's current position with the given force.
        /// Force is scaled by <see cref="_forceToAmplitudeScale"/> to determine amplitude.
        /// </summary>
        /// <param name="force">Raw force value (e.g. from a physics collision).</param>
        public void Shake(float force)
        {
            if (force < _minForceThreshold)
                return;

            float amplitude = Mathf.Min(force * _forceToAmplitudeScale, _maxAmplitude);
            GenerateImpulse(amplitude);
        }

        /// <summary>
        /// Triggers a screen shake originating from a specific world position.
        /// Temporarily moves the impulse source to the position, fires, then restores it.
        /// </summary>
        /// <param name="position">World-space origin of the shake.</param>
        /// <param name="force">Raw force value.</param>
        public void ShakeAt(Vector2 position, float force)
        {
            if (force < _minForceThreshold)
                return;

            Vector3 originalPosition = transform.position;
            transform.position = new Vector3(position.x, position.y, originalPosition.z);

            float amplitude = Mathf.Min(force * _forceToAmplitudeScale, _maxAmplitude);
            GenerateImpulse(amplitude);

            transform.position = originalPosition;
        }

        /// <summary>
        /// Triggers a shake with explicit amplitude, frequency, and duration overrides.
        /// </summary>
        /// <param name="amplitude">Shake amplitude.</param>
        /// <param name="frequency">Shake frequency.</param>
        /// <param name="duration">Shake duration in seconds.</param>
        public void ShakeCustom(float amplitude, float frequency, float duration)
        {
            EnsureImpulseSource();
            ConfigureImpulse(amplitude, frequency, duration);
            _impulseSource.GenerateImpulse();
        }

        #endregion

        #region Private Methods

        private void EnsureImpulseSource()
        {
            if (_impulseSource != null)
                return;

            _impulseSource = GetComponent<CinemachineImpulseSource>();
            if (_impulseSource == null)
            {
                _impulseSource = gameObject.AddComponent<CinemachineImpulseSource>();
            }

            ConfigureImpulse(_defaultAmplitude, _defaultFrequency, _defaultDuration);
        }

        private void ConfigureImpulse(float amplitude, float frequency, float duration)
        {
            if (_impulseSource == null)
                return;

            // Configure the impulse definition for a 2D shake (XY only).
            _impulseSource.ImpulseDefinition.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
            _impulseSource.ImpulseDefinition.ImpulseDuration = duration;

            // Create a custom raw signal shape if needed.
            _impulseSource.DefaultVelocity = new Vector3(
                amplitude,
                amplitude * 0.7f, // Slightly less vertical than horizontal.
                0f
            );
        }

        private void GenerateImpulse(float amplitude)
        {
            EnsureImpulseSource();

            ConfigureImpulse(amplitude, _defaultFrequency, _defaultDuration);
            _impulseSource.GenerateImpulse();
        }

        #endregion
    }
}
