using System;
using UnityEngine;
using ElementalSiege.Input;
using ElementalSiege.Orbs;

namespace ElementalSiege.Launcher
{
    /// <summary>
    /// Slingshot-style launcher that lets the player drag backward to aim and release to launch
    /// elemental orbs. Manages rubber-band visuals, launch physics, and state transitions.
    /// </summary>
    public class Catapult : MonoBehaviour
    {
        #region Enums

        /// <summary>Current operational state of the catapult.</summary>
        public enum CatapultState
        {
            /// <summary>Ready and waiting for the player to begin aiming.</summary>
            Idle,
            /// <summary>Player is dragging to set direction and power.</summary>
            Aiming,
            /// <summary>Orb has been launched; tracking until it settles.</summary>
            Launched,
            /// <summary>Previous orb resolved; waiting for the next orb to be loaded.</summary>
            WaitingForOrb
        }

        #endregion

        #region Events

        /// <summary>Fired when an orb is launched from the catapult.</summary>
        public event Action<OrbBase> OnOrbLaunched;

        /// <summary>Fired every frame during aiming with the current aim direction and normalised power (0-1).</summary>
        public event Action<Vector2, float> OnAiming;

        /// <summary>Fired whenever the catapult state changes.</summary>
        public event Action<CatapultState> OnStateChanged;

        #endregion

        #region Inspector Fields

        [Header("References")]
        [SerializeField]
        [Tooltip("The InputManager used to receive drag events.")]
        private InputManager _inputManager;

        [SerializeField]
        [Tooltip("Transform marking the launch origin (where the orb sits).")]
        private Transform _launchPoint;

        [SerializeField]
        [Tooltip("Left rubber-band anchor point.")]
        private Transform _bandAnchorLeft;

        [SerializeField]
        [Tooltip("Right rubber-band anchor point.")]
        private Transform _bandAnchorRight;

        [SerializeField]
        [Tooltip("LineRenderer used to draw the rubber band while aiming.")]
        private LineRenderer _rubberBandRenderer;

        [Header("Launch Settings")]
        [SerializeField]
        [Tooltip("Multiplier applied to drag distance to compute launch force magnitude.")]
        private float _forceMultiplier = 15f;

        [SerializeField]
        [Tooltip("Maximum drag distance in world units. Drags beyond this are clamped.")]
        private float _maxDragDistance = 3f;

        [SerializeField]
        [Tooltip("Absolute maximum launch force regardless of multiplier.")]
        private float _maxForce = 50f;

        [Header("Settle Detection")]
        [SerializeField]
        [Tooltip("Velocity threshold below which the launched orb is considered settled.")]
        private float _settleVelocityThreshold = 0.15f;

        [SerializeField]
        [Tooltip("Time in seconds the orb must remain below the velocity threshold to count as settled.")]
        private float _settleTime = 0.6f;

        [SerializeField]
        [Tooltip("World-space bounds; orb is considered off-screen if it exits this rect.")]
        private Rect _levelBounds = new Rect(-20f, -15f, 60f, 40f);

        #endregion

        #region Private State

        private CatapultState _state = CatapultState.WaitingForOrb;
        private OrbBase _currentOrb;
        private Vector3 _dragWorldPos;
        private float _settleTimer;

        #endregion

        #region Properties

        /// <summary>Current state of the catapult.</summary>
        public CatapultState State => _state;

        /// <summary>World-space position of the launch point.</summary>
        public Vector3 LaunchPosition => _launchPoint != null ? _launchPoint.position : transform.position;

        /// <summary>The current aim direction (normalised, from drag origin toward launch point).</summary>
        public Vector2 AimDirection { get; private set; }

        /// <summary>Current launch power normalised to 0-1 range.</summary>
        public float NormalisedPower { get; private set; }

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            if (_inputManager != null)
            {
                _inputManager.OnDragStarted += HandleDragStarted;
                _inputManager.OnDragUpdated += HandleDragUpdated;
                _inputManager.OnDragEnded += HandleDragEnded;
            }
        }

        private void OnDisable()
        {
            if (_inputManager != null)
            {
                _inputManager.OnDragStarted -= HandleDragStarted;
                _inputManager.OnDragUpdated -= HandleDragUpdated;
                _inputManager.OnDragEnded -= HandleDragEnded;
            }
        }

        private void Update()
        {
            switch (_state)
            {
                case CatapultState.Aiming:
                    UpdateRubberBand();
                    break;

                case CatapultState.Launched:
                    UpdateLaunchedOrb();
                    break;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Loads an orb onto the catapult, placing it at the launch point and transitioning to Idle.
        /// </summary>
        /// <param name="orb">The orb to load.</param>
        public void LoadOrb(OrbBase orb)
        {
            if (orb == null)
            {
                Debug.LogWarning("[Catapult] Attempted to load a null orb.");
                return;
            }

            _currentOrb = orb;
            _currentOrb.transform.position = LaunchPosition;

            // Ensure the orb is kinematic while on the catapult.
            Rigidbody2D rb = _currentOrb.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }

            SetState(CatapultState.Idle);
            HideRubberBand();
        }

        /// <summary>
        /// Forces the catapult to the WaitingForOrb state (e.g. when no orbs remain).
        /// </summary>
        public void SetWaitingForOrb()
        {
            _currentOrb = null;
            SetState(CatapultState.WaitingForOrb);
            HideRubberBand();
        }

        /// <summary>
        /// Calculates the launch velocity for a given drag world position.
        /// </summary>
        /// <param name="dragPosition">The world position where the player is dragging.</param>
        /// <returns>The velocity vector that would be applied to the orb.</returns>
        public Vector2 CalculateLaunchVelocity(Vector3 dragPosition)
        {
            Vector3 offset = LaunchPosition - dragPosition;
            float distance = Mathf.Min(offset.magnitude, _maxDragDistance);
            Vector2 direction = ((Vector2)offset).normalized;
            float forceMagnitude = Mathf.Min(distance * _forceMultiplier, _maxForce);
            return direction * forceMagnitude;
        }

        #endregion

        #region Input Handlers

        private void HandleDragStarted(Vector2 screenPos)
        {
            if (_state != CatapultState.Idle || _currentOrb == null)
                return;

            // Only start aiming if the touch is near the catapult / orb.
            Vector3 worldPos = _inputManager.ScreenToWorldPosition(screenPos);
            float distanceToLaunch = Vector2.Distance(worldPos, LaunchPosition);
            if (distanceToLaunch > _maxDragDistance * 1.5f)
                return;

            SetState(CatapultState.Aiming);
            _dragWorldPos = worldPos;
            ShowRubberBand();
            UpdateAimValues();
        }

        private void HandleDragUpdated(Vector2 screenPos)
        {
            if (_state != CatapultState.Aiming)
                return;

            _dragWorldPos = _inputManager.ScreenToWorldPosition(screenPos);
            ClampDragPosition();
            UpdateAimValues();

            // Move the orb toward the dragged position for visual feedback.
            if (_currentOrb != null)
            {
                _currentOrb.transform.position = _dragWorldPos;
            }

            OnAiming?.Invoke(AimDirection, NormalisedPower);
        }

        private void HandleDragEnded(Vector2 screenPos)
        {
            if (_state != CatapultState.Aiming || _currentOrb == null)
                return;

            _dragWorldPos = _inputManager.ScreenToWorldPosition(screenPos);
            ClampDragPosition();

            LaunchCurrentOrb();
        }

        #endregion

        #region Launch Logic

        private void LaunchCurrentOrb()
        {
            Vector2 launchVelocity = CalculateLaunchVelocity(_dragWorldPos);

            Rigidbody2D rb = _currentOrb.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.linearVelocity = launchVelocity;
            }

            _currentOrb.OnLaunched();
            SetState(CatapultState.Launched);
            HideRubberBand();

            _settleTimer = 0f;

            OnOrbLaunched?.Invoke(_currentOrb);
        }

        private void UpdateLaunchedOrb()
        {
            if (_currentOrb == null)
            {
                SetState(CatapultState.WaitingForOrb);
                return;
            }

            // Check if orb left the level bounds.
            Vector2 orbPos = _currentOrb.transform.position;
            if (!_levelBounds.Contains(orbPos))
            {
                OnOrbSettledOrLost();
                return;
            }

            // Check if the orb has settled.
            Rigidbody2D rb = _currentOrb.GetComponent<Rigidbody2D>();
            if (rb != null && rb.linearVelocity.sqrMagnitude < _settleVelocityThreshold * _settleVelocityThreshold)
            {
                _settleTimer += Time.deltaTime;
                if (_settleTimer >= _settleTime)
                {
                    OnOrbSettledOrLost();
                }
            }
            else
            {
                _settleTimer = 0f;
            }
        }

        private void OnOrbSettledOrLost()
        {
            if (_currentOrb != null)
            {
                _currentOrb.OnSettled();
            }

            _currentOrb = null;
            SetState(CatapultState.WaitingForOrb);
        }

        #endregion

        #region Rubber Band Visuals

        private void ShowRubberBand()
        {
            if (_rubberBandRenderer != null)
            {
                _rubberBandRenderer.enabled = true;
                _rubberBandRenderer.positionCount = 3;
            }
        }

        private void HideRubberBand()
        {
            if (_rubberBandRenderer != null)
            {
                _rubberBandRenderer.enabled = false;
            }
        }

        private void UpdateRubberBand()
        {
            if (_rubberBandRenderer == null || !_rubberBandRenderer.enabled)
                return;

            Vector3 leftAnchor = _bandAnchorLeft != null ? _bandAnchorLeft.position : LaunchPosition + Vector3.left * 0.5f;
            Vector3 rightAnchor = _bandAnchorRight != null ? _bandAnchorRight.position : LaunchPosition + Vector3.right * 0.5f;
            Vector3 orbPos = _currentOrb != null ? _currentOrb.transform.position : _dragWorldPos;

            _rubberBandRenderer.SetPosition(0, leftAnchor);
            _rubberBandRenderer.SetPosition(1, orbPos);
            _rubberBandRenderer.SetPosition(2, rightAnchor);
        }

        #endregion

        #region Helpers

        private void ClampDragPosition()
        {
            Vector3 offset = _dragWorldPos - LaunchPosition;
            if (offset.magnitude > _maxDragDistance)
            {
                _dragWorldPos = LaunchPosition + offset.normalized * _maxDragDistance;
            }
        }

        private void UpdateAimValues()
        {
            Vector3 offset = LaunchPosition - _dragWorldPos;
            float distance = Mathf.Min(offset.magnitude, _maxDragDistance);

            AimDirection = ((Vector2)offset).normalized;
            NormalisedPower = distance / _maxDragDistance;
        }

        private void SetState(CatapultState newState)
        {
            if (_state == newState)
                return;

            _state = newState;
            OnStateChanged?.Invoke(_state);
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            // Draw max drag radius.
            Vector3 origin = _launchPoint != null ? _launchPoint.position : transform.position;
            Gizmos.color = new Color(1f, 0.6f, 0f, 0.3f);
            Gizmos.DrawWireSphere(origin, _maxDragDistance);

            // Draw level bounds.
            Gizmos.color = new Color(0f, 1f, 0f, 0.2f);
            Vector3 boundsCenter = new Vector3(_levelBounds.center.x, _levelBounds.center.y, 0f);
            Vector3 boundsSize = new Vector3(_levelBounds.width, _levelBounds.height, 0f);
            Gizmos.DrawWireCube(boundsCenter, boundsSize);
        }

        #endregion
    }

}
