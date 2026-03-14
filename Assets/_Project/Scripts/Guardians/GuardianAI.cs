using System;
using UnityEngine;
using ElementalSiege.Core;

namespace ElementalSiege.Guardians
{
    /// <summary>
    /// Finite state machine AI controller for the boss guardian.
    /// Manages states: Idle, Alert, Attack, Vulnerable, PhaseTransition, Defeated.
    /// </summary>
    [RequireComponent(typeof(BossGuardian))]
    public class GuardianAI : MonoBehaviour
    {
        #region Nested Types

        /// <summary>
        /// FSM states for the guardian AI.
        /// </summary>
        public enum AIState
        {
            /// <summary>Default state — gentle floating, waiting for action.</summary>
            Idle,

            /// <summary>Triggered when an orb is launched; guardian becomes aware.</summary>
            Alert,

            /// <summary>Guardian fires a projectile toward the catapult area.</summary>
            Attack,

            /// <summary>Post-attack window where the guardian takes extra damage.</summary>
            Vulnerable,

            /// <summary>Invulnerable transition between health phases.</summary>
            PhaseTransition,

            /// <summary>Guardian has been defeated.</summary>
            Defeated
        }

        #endregion

        #region Serialized Fields

        [Header("State Timing")]
        [SerializeField] private float _idleMinDuration = 2f;
        [SerializeField] private float _idleMaxDuration = 4f;
        [SerializeField] private float _alertDuration = 1.5f;
        [SerializeField] private float _vulnerableDuration = 2f;

        [Header("Attack Pattern")]
        [SerializeField] private float _attackInterval = 4f;
        [SerializeField] private float _attackIntervalVariance = 1f;
        [SerializeField] private int _attacksBeforeRest = 3;
        [SerializeField] private float _restDuration = 3f;

        [Header("Alert Detection")]
        [SerializeField] private float _alertDetectionRadius = 15f;
        [SerializeField] private LayerMask _orbLayer;

        [Header("Visual Feedback")]
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private Color _idleColor = Color.white;
        [SerializeField] private Color _alertColor = new Color(1f, 0.8f, 0.3f);
        [SerializeField] private Color _attackColor = new Color(1f, 0.3f, 0.2f);
        [SerializeField] private Color _vulnerableColor = new Color(0.5f, 1f, 0.5f);
        [SerializeField] private Color _transitionColor = new Color(0.8f, 0.4f, 1f);
        [SerializeField] private float _colorLerpSpeed = 5f;

        [Header("Idle Float")]
        [SerializeField] private float _idleFloatAmplitude = 0.2f;
        [SerializeField] private float _idleFloatSpeed = 1.5f;

        #endregion

        #region Events

        /// <summary>Raised when the AI transitions to a new state.</summary>
        public event Action<AIState, AIState> OnStateChanged;

        #endregion

        #region Properties

        /// <summary>The current AI state.</summary>
        public AIState CurrentState { get; private set; } = AIState.Idle;

        #endregion

        #region Private State

        private BossGuardian _boss;
        private float _stateTimer;
        private float _nextAttackTime;
        private int _attackCount;
        private Color _targetColor;
        private Vector3 _basePosition;
        private bool _orbDetected;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _boss = GetComponent<BossGuardian>();
            _basePosition = transform.position;
            _targetColor = _idleColor;
        }

        private void OnEnable()
        {
            if (_boss != null)
            {
                _boss.OnPhaseChanged += HandlePhaseChanged;
                _boss.OnGuardianDefeated += HandleDefeated;
            }
        }

        private void OnDisable()
        {
            if (_boss != null)
            {
                _boss.OnPhaseChanged -= HandlePhaseChanged;
                _boss.OnGuardianDefeated -= HandleDefeated;
            }
        }

        private void Update()
        {
            if (CurrentState == AIState.Defeated) return;

            _stateTimer += Time.deltaTime;

            // Check for phase transition override
            if (_boss != null && _boss.IsInTransition && CurrentState != AIState.PhaseTransition)
            {
                TransitionTo(AIState.PhaseTransition);
            }

            UpdateCurrentState();
            UpdateVisuals();
        }

        private void FixedUpdate()
        {
            if (CurrentState == AIState.Idle)
                DetectOrbs();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Notifies the AI that an orb has been launched, triggering alert state.
        /// </summary>
        public void NotifyOrbLaunched()
        {
            if (CurrentState == AIState.Idle)
            {
                _orbDetected = true;
            }
        }

        /// <summary>
        /// Forces the AI into a specific state (for scripted sequences).
        /// </summary>
        public void ForceState(AIState state)
        {
            TransitionTo(state);
        }

        #endregion

        #region State Machine

        private void UpdateCurrentState()
        {
            switch (CurrentState)
            {
                case AIState.Idle:
                    UpdateIdle();
                    break;
                case AIState.Alert:
                    UpdateAlert();
                    break;
                case AIState.Attack:
                    UpdateAttack();
                    break;
                case AIState.Vulnerable:
                    UpdateVulnerable();
                    break;
                case AIState.PhaseTransition:
                    UpdatePhaseTransition();
                    break;
                case AIState.Defeated:
                    break;
            }
        }

        private void TransitionTo(AIState newState)
        {
            AIState oldState = CurrentState;
            if (oldState == newState) return;

            // Exit current state
            ExitState(oldState);

            CurrentState = newState;
            _stateTimer = 0f;

            // Enter new state
            EnterState(newState);

            OnStateChanged?.Invoke(oldState, newState);
        }

        private void EnterState(AIState state)
        {
            switch (state)
            {
                case AIState.Idle:
                    _targetColor = _idleColor;
                    _orbDetected = false;
                    break;

                case AIState.Alert:
                    _targetColor = _alertColor;
                    break;

                case AIState.Attack:
                    _targetColor = _attackColor;
                    if (_boss != null)
                        _boss.PerformAttack();
                    break;

                case AIState.Vulnerable:
                    _targetColor = _vulnerableColor;
                    break;

                case AIState.PhaseTransition:
                    _targetColor = _transitionColor;
                    _attackCount = 0;
                    break;

                case AIState.Defeated:
                    _targetColor = Color.gray;
                    break;
            }
        }

        private void ExitState(AIState state)
        {
            // Cleanup if needed per state
        }

        #endregion

        #region State Updates

        private void UpdateIdle()
        {
            // Gentle float animation
            Vector3 pos = _basePosition;
            pos.y += Mathf.Sin(Time.time * _idleFloatSpeed) * _idleFloatAmplitude;
            // Position is managed by Guardian base, so we leave this as supplementary

            // Check for orb detection
            if (_orbDetected)
            {
                TransitionTo(AIState.Alert);
                return;
            }

            // Auto-transition to attack after idle duration
            float idleDuration = UnityEngine.Random.Range(_idleMinDuration, _idleMaxDuration);
            if (_stateTimer >= idleDuration)
            {
                // Check if we need to rest after a burst of attacks
                if (_attackCount >= _attacksBeforeRest)
                {
                    if (_stateTimer >= idleDuration + _restDuration)
                    {
                        _attackCount = 0;
                        TransitionTo(AIState.Attack);
                    }
                }
                else
                {
                    TransitionTo(AIState.Attack);
                }
            }
        }

        private void UpdateAlert()
        {
            if (_stateTimer >= _alertDuration)
            {
                TransitionTo(AIState.Attack);
            }
        }

        private void UpdateAttack()
        {
            // The attack itself is driven by BossGuardian.PerformAttack().
            // After the boss finishes its attack coroutine, it sets IsVulnerable.
            if (_boss != null && _boss.IsVulnerable)
            {
                _attackCount++;
                TransitionTo(AIState.Vulnerable);
            }

            // Timeout safety — if attack takes too long, force transition
            float timeout = _attackInterval + _attackIntervalVariance + 5f;
            if (_stateTimer >= timeout)
            {
                _attackCount++;
                TransitionTo(AIState.Idle);
            }
        }

        private void UpdateVulnerable()
        {
            if (_stateTimer >= _vulnerableDuration)
            {
                TransitionTo(AIState.Idle);
            }
        }

        private void UpdatePhaseTransition()
        {
            // Wait until boss reports transition is over
            if (_boss != null && !_boss.IsInTransition)
            {
                TransitionTo(AIState.Idle);
            }
        }

        #endregion

        #region Detection

        private void DetectOrbs()
        {
            Collider2D hit = Physics2D.OverlapCircle(
                transform.position, _alertDetectionRadius, _orbLayer);

            if (hit != null)
            {
                _orbDetected = true;
            }
        }

        #endregion

        #region Visuals

        private void UpdateVisuals()
        {
            if (_spriteRenderer == null) return;

            _spriteRenderer.color = Color.Lerp(
                _spriteRenderer.color, _targetColor, _colorLerpSpeed * Time.deltaTime);
        }

        #endregion

        #region Event Handlers

        private void HandlePhaseChanged(int phaseIndex)
        {
            // Phase transition state is entered in Update when IsInTransition is detected.
        }

        private void HandleDefeated(Guardian guardian, int score)
        {
            TransitionTo(AIState.Defeated);
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, _alertDetectionRadius);
        }

        #endregion
    }
}
