using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ElementalSiege.Input
{
    /// <summary>
    /// Wraps Unity's new Input System to provide unified touch and mouse input handling.
    /// Converts raw pointer actions into semantic game events (drag, tap, pause)
    /// and transforms screen positions to world coordinates.
    /// </summary>
    [RequireComponent(typeof(PlayerInput))]
    public class InputManager : MonoBehaviour
    {
        #region Events

        /// <summary>Fired when the player begins a drag gesture.</summary>
        public event Action<Vector2> OnDragStarted;

        /// <summary>Fired each frame while the player drags (screen-space position).</summary>
        public event Action<Vector2> OnDragUpdated;

        /// <summary>Fired when the player releases a drag gesture.</summary>
        public event Action<Vector2> OnDragEnded;

        /// <summary>Fired on a quick tap (used for mid-flight ability activation).</summary>
        public event Action<Vector2> OnTapPerformed;

        /// <summary>Fired when the pause button is pressed.</summary>
        public event Action OnPausePressed;

        #endregion

        #region Inspector Fields

        [Header("References")]
        [SerializeField]
        [Tooltip("Override camera used for screen-to-world conversion. Falls back to Camera.main.")]
        private Camera _gameCamera;

        [Header("Drag Settings")]
        [SerializeField]
        [Tooltip("Minimum distance in screen pixels before a press is promoted to a drag.")]
        private float _dragThreshold = 10f;

        [SerializeField]
        [Tooltip("Maximum duration in seconds for a press-release to register as a tap instead of a drag.")]
        private float _tapMaxDuration = 0.2f;

        #endregion

        #region Private State

        private PlayerInput _playerInput;
        private InputAction _pointerPositionAction;
        private InputAction _pointerContactAction;
        private InputAction _tapAction;
        private InputAction _pauseAction;

        private bool _isPointerDown;
        private bool _isDragging;
        private Vector2 _pointerDownPosition;
        private float _pointerDownTime;

        #endregion

        #region Properties

        /// <summary>Whether a drag gesture is currently active.</summary>
        public bool IsDragging => _isDragging;

        /// <summary>The camera used for screen-to-world conversion.</summary>
        public Camera GameCamera
        {
            get
            {
                if (_gameCamera == null)
                    _gameCamera = Camera.main;
                return _gameCamera;
            }
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _playerInput = GetComponent<PlayerInput>();

            // Resolve actions from the PlayerInput component's assigned actions asset.
            _pointerPositionAction = _playerInput.actions["Gameplay/Drag"];
            _pointerContactAction = _playerInput.actions["Gameplay/PointerContact"];
            _tapAction = _playerInput.actions["Gameplay/Tap"];
            _pauseAction = _playerInput.actions["Gameplay/Pause"];
        }

        private void OnEnable()
        {
            if (_pointerContactAction != null)
            {
                _pointerContactAction.started += HandlePointerContactStarted;
                _pointerContactAction.canceled += HandlePointerContactCanceled;
            }

            if (_tapAction != null)
            {
                _tapAction.performed += HandleTapPerformed;
            }

            if (_pauseAction != null)
            {
                _pauseAction.performed += HandlePausePerformed;
            }
        }

        private void OnDisable()
        {
            if (_pointerContactAction != null)
            {
                _pointerContactAction.started -= HandlePointerContactStarted;
                _pointerContactAction.canceled -= HandlePointerContactCanceled;
            }

            if (_tapAction != null)
            {
                _tapAction.performed -= HandleTapPerformed;
            }

            if (_pauseAction != null)
            {
                _pauseAction.performed -= HandlePausePerformed;
            }

            // Clean up any in-progress drag.
            if (_isDragging)
            {
                _isDragging = false;
                _isPointerDown = false;
            }
        }

        private void Update()
        {
            if (!_isPointerDown)
                return;

            Vector2 currentPosition = _pointerPositionAction.ReadValue<Vector2>();

            if (!_isDragging)
            {
                float distance = Vector2.Distance(_pointerDownPosition, currentPosition);
                if (distance >= _dragThreshold)
                {
                    _isDragging = true;
                    OnDragStarted?.Invoke(currentPosition);
                }
            }
            else
            {
                OnDragUpdated?.Invoke(currentPosition);
            }
        }

        #endregion

        #region Input Callbacks

        private void HandlePointerContactStarted(InputAction.CallbackContext context)
        {
            _isPointerDown = true;
            _isDragging = false;
            _pointerDownPosition = _pointerPositionAction.ReadValue<Vector2>();
            _pointerDownTime = Time.unscaledTime;
        }

        private void HandlePointerContactCanceled(InputAction.CallbackContext context)
        {
            Vector2 releasePosition = _pointerPositionAction.ReadValue<Vector2>();

            if (_isDragging)
            {
                OnDragEnded?.Invoke(releasePosition);
            }

            _isDragging = false;
            _isPointerDown = false;
        }

        private void HandleTapPerformed(InputAction.CallbackContext context)
        {
            // Only fire tap if we are NOT in a drag; drags consume the gesture.
            if (_isDragging)
                return;

            float elapsed = Time.unscaledTime - _pointerDownTime;
            if (elapsed <= _tapMaxDuration)
            {
                Vector2 tapPosition = _pointerPositionAction.ReadValue<Vector2>();
                OnTapPerformed?.Invoke(tapPosition);
            }
        }

        private void HandlePausePerformed(InputAction.CallbackContext context)
        {
            OnPausePressed?.Invoke();
        }

        #endregion

        #region Public Helpers

        /// <summary>
        /// Converts a screen-space position to world-space using the game camera.
        /// Returns a point on the z = 0 plane.
        /// </summary>
        /// <param name="screenPos">Position in screen pixels.</param>
        /// <returns>World-space position on the z = 0 plane.</returns>
        public Vector3 ScreenToWorldPosition(Vector2 screenPos)
        {
            Camera cam = GameCamera;
            if (cam == null)
            {
                Debug.LogWarning("[InputManager] No camera available for screen-to-world conversion.");
                return Vector3.zero;
            }

            Vector3 screenPoint = new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z);
            return cam.ScreenToWorldPoint(screenPoint);
        }

        /// <summary>
        /// Switches the active action map on the underlying PlayerInput component.
        /// </summary>
        /// <param name="mapName">The action map name (e.g. "Gameplay" or "UI").</param>
        public void SwitchActionMap(string mapName)
        {
            if (_playerInput != null)
            {
                _playerInput.SwitchCurrentActionMap(mapName);
            }
        }

        #endregion
    }
}
