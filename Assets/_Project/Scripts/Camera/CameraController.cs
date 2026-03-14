using System.Collections;
using UnityEngine;
using Unity.Cinemachine;
using ElementalSiege.Launcher;
using ElementalSiege.Orbs;

namespace ElementalSiege.Camera
{
    /// <summary>
    /// Cinemachine-based 2D camera controller that manages focus transitions between
    /// the catapult, launched orb, and full-level overview. Supports pinch-to-zoom on
    /// mobile and scroll-wheel zoom on desktop.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Cinemachine References")]
        [SerializeField]
        [Tooltip("Virtual camera used for the full-level overview.")]
        private CinemachineCamera _overviewCamera;

        [SerializeField]
        [Tooltip("Virtual camera focused on the catapult area during aiming.")]
        private CinemachineCamera _catapultCamera;

        [SerializeField]
        [Tooltip("Virtual camera that follows the launched orb.")]
        private CinemachineCamera _followCamera;

        [SerializeField]
        [Tooltip("Cinemachine Confiner source (collider defining level bounds).")]
        private Collider2D _confinerBounds;

        [Header("Game References")]
        [SerializeField]
        [Tooltip("The catapult to observe for state changes.")]
        private Catapult _catapult;

        [SerializeField]
        [Tooltip("The screen shake component for impact effects.")]
        private ScreenShake _screenShake;

        [Header("Follow Settings")]
        [SerializeField]
        [Tooltip("Damping applied when the camera follows the orb.")]
        private float _followDamping = 1.5f;

        [SerializeField]
        [Tooltip("Lookahead time for the follow camera to anticipate orb movement.")]
        private float _followLookahead = 0.3f;

        [Header("Zoom Settings")]
        [SerializeField]
        [Tooltip("Orthographic size for the full-level overview.")]
        private float _overviewOrthoSize = 12f;

        [SerializeField]
        [Tooltip("Orthographic size when focused on the catapult.")]
        private float _catapultOrthoSize = 6f;

        [SerializeField]
        [Tooltip("Amount to zoom in on impact (subtracted from current ortho size).")]
        private float _impactZoomAmount = 1.5f;

        [SerializeField]
        [Tooltip("Minimum orthographic size (maximum zoom in).")]
        private float _minOrthoSize = 3f;

        [SerializeField]
        [Tooltip("Maximum orthographic size (maximum zoom out).")]
        private float _maxOrthoSize = 20f;

        [SerializeField]
        [Tooltip("Scroll wheel zoom speed.")]
        private float _scrollZoomSpeed = 2f;

        [Header("Timing")]
        [SerializeField]
        [Tooltip("Delay in seconds before returning to overview after the orb settles.")]
        private float _settleDelay = 1.0f;

        [SerializeField]
        [Tooltip("Duration of the impact zoom-in effect.")]
        private float _impactZoomDuration = 0.4f;

        #endregion

        #region Private State

        private enum CameraMode
        {
            Overview,
            Catapult,
            FollowOrb,
            Impact
        }

        private CameraMode _currentMode = CameraMode.Overview;
        private Transform _followTarget;
        private float _manualZoomOffset;
        private Coroutine _returnToOverviewCoroutine;

        // Pinch-to-zoom state.
        private bool _isPinching;
        private float _initialPinchDistance;
        private float _initialPinchOrthoSize;

        #endregion

        #region Properties

        /// <summary>The currently active orthographic size (before manual zoom offset).</summary>
        public float CurrentBaseOrthoSize
        {
            get
            {
                return _currentMode switch
                {
                    CameraMode.Catapult => _catapultOrthoSize,
                    CameraMode.Overview => _overviewOrthoSize,
                    _ => _overviewOrthoSize
                };
            }
        }

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            SetCameraMode(CameraMode.Overview);
        }

        private void OnEnable()
        {
            if (_catapult != null)
            {
                _catapult.OnStateChanged += HandleCatapultStateChanged;
                _catapult.OnOrbLaunched += HandleOrbLaunched;
            }
        }

        private void OnDisable()
        {
            if (_catapult != null)
            {
                _catapult.OnStateChanged -= HandleCatapultStateChanged;
                _catapult.OnOrbLaunched -= HandleOrbLaunched;
            }
        }

        private void Update()
        {
            HandleDesktopZoom();
            HandleMobilePinchZoom();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Triggers an impact zoom effect and optional screen shake at the given position.
        /// </summary>
        /// <param name="position">World-space position of the impact.</param>
        /// <param name="force">Force of the impact (scales shake intensity).</param>
        public void OnImpact(Vector2 position, float force)
        {
            if (_screenShake != null)
            {
                _screenShake.ShakeAt(position, force);
            }

            StartCoroutine(ImpactZoomRoutine());
        }

        /// <summary>
        /// Resets the camera to the overview mode showing the full level.
        /// </summary>
        public void ResetToOverview()
        {
            _manualZoomOffset = 0f;
            SetCameraMode(CameraMode.Overview);
        }

        /// <summary>
        /// Configures the overview camera to fit the given level bounds.
        /// </summary>
        /// <param name="levelBounds">The world-space bounds of the level.</param>
        public void FitToLevel(Bounds levelBounds)
        {
            UnityEngine.Camera mainCam = UnityEngine.Camera.main;
            if (mainCam == null)
                return;

            float screenAspect = (float)Screen.width / Screen.height;
            float boundsAspect = levelBounds.size.x / levelBounds.size.y;

            if (boundsAspect > screenAspect)
            {
                _overviewOrthoSize = levelBounds.size.x / (2f * screenAspect);
            }
            else
            {
                _overviewOrthoSize = levelBounds.size.y / 2f;
            }

            _overviewOrthoSize *= 1.05f; // Small padding.

            if (_overviewCamera != null)
            {
                var lens = _overviewCamera.Lens;
                lens.OrthographicSize = _overviewOrthoSize;
                _overviewCamera.Lens = lens;
            }
        }

        #endregion

        #region Event Handlers

        private void HandleCatapultStateChanged(Catapult.CatapultState newState)
        {
            switch (newState)
            {
                case Catapult.CatapultState.Idle:
                    SetCameraMode(CameraMode.Catapult);
                    break;

                case Catapult.CatapultState.Aiming:
                    SetCameraMode(CameraMode.Catapult);
                    break;

                case Catapult.CatapultState.WaitingForOrb:
                    if (_returnToOverviewCoroutine != null)
                        StopCoroutine(_returnToOverviewCoroutine);
                    _returnToOverviewCoroutine = StartCoroutine(ReturnToOverviewRoutine());
                    break;
            }
        }

        private void HandleOrbLaunched(OrbBase orb)
        {
            if (orb != null)
            {
                _followTarget = orb.transform;
                SetCameraMode(CameraMode.FollowOrb);
            }
        }

        #endregion

        #region Camera Mode Management

        private void SetCameraMode(CameraMode mode)
        {
            _currentMode = mode;

            // Set priorities so Cinemachine Brain blends to the correct camera.
            int overviewPriority = 0;
            int catapultPriority = 0;
            int followPriority = 0;

            switch (mode)
            {
                case CameraMode.Overview:
                    overviewPriority = 10;
                    break;
                case CameraMode.Catapult:
                    catapultPriority = 10;
                    break;
                case CameraMode.FollowOrb:
                case CameraMode.Impact:
                    followPriority = 10;
                    break;
            }

            if (_overviewCamera != null)
                _overviewCamera.Priority = overviewPriority;
            if (_catapultCamera != null)
                _catapultCamera.Priority = catapultPriority;
            if (_followCamera != null)
            {
                _followCamera.Priority = followPriority;

                if (mode == CameraMode.FollowOrb && _followTarget != null)
                {
                    _followCamera.Follow = _followTarget;
                }
            }
        }

        #endregion

        #region Zoom Controls

        private void HandleDesktopZoom()
        {
            float scroll = UnityEngine.Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) < 0.01f)
                return;

            _manualZoomOffset -= scroll * _scrollZoomSpeed * Time.unscaledDeltaTime * 10f;
            ApplyManualZoom();
        }

        private void HandleMobilePinchZoom()
        {
            if (UnityEngine.Input.touchCount != 2)
            {
                _isPinching = false;
                return;
            }

            Touch touch0 = UnityEngine.Input.GetTouch(0);
            Touch touch1 = UnityEngine.Input.GetTouch(1);

            float currentDistance = Vector2.Distance(touch0.position, touch1.position);

            if (!_isPinching)
            {
                _isPinching = true;
                _initialPinchDistance = currentDistance;
                _initialPinchOrthoSize = GetActiveOrthoSize();
                return;
            }

            if (_initialPinchDistance < 0.01f)
                return;

            float ratio = _initialPinchDistance / currentDistance;
            float targetSize = _initialPinchOrthoSize * ratio;
            targetSize = Mathf.Clamp(targetSize, _minOrthoSize, _maxOrthoSize);

            _manualZoomOffset = targetSize - CurrentBaseOrthoSize;
            ApplyManualZoom();
        }

        private void ApplyManualZoom()
        {
            float targetSize = Mathf.Clamp(CurrentBaseOrthoSize + _manualZoomOffset, _minOrthoSize, _maxOrthoSize);

            CinemachineCamera activeCam = GetActiveCinemachineCamera();
            if (activeCam != null)
            {
                var lens = activeCam.Lens;
                lens.OrthographicSize = targetSize;
                activeCam.Lens = lens;
            }
        }

        private float GetActiveOrthoSize()
        {
            CinemachineCamera activeCam = GetActiveCinemachineCamera();
            return activeCam != null ? activeCam.Lens.OrthographicSize : _overviewOrthoSize;
        }

        private CinemachineCamera GetActiveCinemachineCamera()
        {
            return _currentMode switch
            {
                CameraMode.Overview => _overviewCamera,
                CameraMode.Catapult => _catapultCamera,
                CameraMode.FollowOrb => _followCamera,
                CameraMode.Impact => _followCamera,
                _ => _overviewCamera
            };
        }

        #endregion

        #region Coroutines

        private IEnumerator ReturnToOverviewRoutine()
        {
            yield return new WaitForSeconds(_settleDelay);
            _manualZoomOffset = 0f;
            SetCameraMode(CameraMode.Overview);
            _returnToOverviewCoroutine = null;
        }

        private IEnumerator ImpactZoomRoutine()
        {
            CinemachineCamera activeCam = GetActiveCinemachineCamera();
            if (activeCam == null)
                yield break;

            float originalSize = activeCam.Lens.OrthographicSize;
            float targetSize = Mathf.Max(originalSize - _impactZoomAmount, _minOrthoSize);

            // Zoom in.
            float elapsed = 0f;
            float halfDuration = _impactZoomDuration * 0.5f;

            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / halfDuration;
                var lens = activeCam.Lens;
                lens.OrthographicSize = Mathf.Lerp(originalSize, targetSize, t);
                activeCam.Lens = lens;
                yield return null;
            }

            // Zoom back out.
            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / halfDuration;
                var lens = activeCam.Lens;
                lens.OrthographicSize = Mathf.Lerp(targetSize, originalSize, t);
                activeCam.Lens = lens;
                yield return null;
            }

            var finalLens = activeCam.Lens;
            finalLens.OrthographicSize = originalSize;
            activeCam.Lens = finalLens;
        }

        #endregion
    }
}
