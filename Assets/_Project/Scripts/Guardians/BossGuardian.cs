using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ElementalSiege.Core;

namespace ElementalSiege.Guardians
{
    /// <summary>
    /// World 8 boss guardian with multiple health phases, element vulnerability
    /// rotation, minion spawning, special attacks, and dramatic phase transitions.
    /// </summary>
    public class BossGuardian : Guardian
    {
        #region Nested Types

        /// <summary>
        /// Defines a single boss health phase.
        /// </summary>
        [Serializable]
        public class BossPhase
        {
            /// <summary>Display name (e.g., "Phase 1: Inferno").</summary>
            public string phaseName;

            /// <summary>Health percentage threshold to enter this phase (1.0 = full).</summary>
            [Range(0f, 1f)]
            public float healthThreshold = 1f;

            /// <summary>Element the boss is vulnerable to during this phase.</summary>
            public ElementType vulnerability;

            /// <summary>Color tint representing this phase.</summary>
            public Color phaseColor = Color.white;

            /// <summary>Attack speed multiplier during this phase.</summary>
            [Range(0.5f, 3f)]
            public float attackSpeedMultiplier = 1f;

            /// <summary>Number of minions to spawn during phase transition.</summary>
            public int minionCount = 2;

            /// <summary>Projectile prefab for attacks in this phase.</summary>
            public GameObject projectilePrefab;

            /// <summary>Barrier prefab spawned during this phase.</summary>
            public GameObject barrierPrefab;
        }

        #endregion

        #region Serialized Fields

        [Header("Boss Phases")]
        [SerializeField] private List<BossPhase> _phases = new List<BossPhase>();
        [SerializeField] private int _currentPhaseIndex;

        [Header("Minions")]
        [SerializeField] private GameObject _minionPrefab;
        [SerializeField] private Transform[] _minionSpawnPoints;
        [SerializeField] private float _minionSpawnDelay = 0.5f;

        [Header("Attacks")]
        [SerializeField] private Transform _projectileSpawnPoint;
        [SerializeField] private Transform _targetArea;
        [SerializeField] private float _projectileSpeed = 10f;
        [SerializeField] private float _attackCooldown = 3f;
        [SerializeField] private float _vulnerableDuration = 2f;

        [Header("Barriers")]
        [SerializeField] private Transform[] _barrierSpawnPoints;

        [Header("Phase Transition")]
        [SerializeField] private float _phaseTransitionDuration = 3f;
        [SerializeField] private ParticleSystem _phaseTransitionVFX;
        [SerializeField] private AudioClip _phaseTransitionSound;
        [SerializeField] private float _invulnerableFlashSpeed = 10f;

        [Header("Boss Visuals")]
        [SerializeField] private SpriteRenderer _bossSpriteRenderer;
        [SerializeField] private float _bossIdleFloatAmplitude = 0.3f;
        [SerializeField] private float _bossIdleFloatSpeed = 1f;
        [SerializeField] private Transform _bossScaleTarget;

        [Header("Camera")]
        [SerializeField] private float _cameraShakeOnPhaseChange = 0.5f;

        #endregion

        #region Events

        /// <summary>Raised when the boss enters a new phase. Passes phase index.</summary>
        public event Action<int> OnPhaseChanged;

        /// <summary>Raised when the boss spawns minions. Passes spawned list.</summary>
        public event Action<List<Guardian>> OnMinionsSpawned;

        /// <summary>Raised when the boss fires a projectile.</summary>
        public event Action<Vector3> OnProjectileFired;

        /// <summary>Raised when the boss creates a barrier.</summary>
        public event Action OnBarrierCreated;

        #endregion

        #region Properties

        /// <summary>The current boss phase data.</summary>
        public BossPhase CurrentPhase =>
            _currentPhaseIndex >= 0 && _currentPhaseIndex < _phases.Count
                ? _phases[_currentPhaseIndex]
                : null;

        /// <summary>Current phase index (0-based).</summary>
        public int CurrentPhaseIndex => _currentPhaseIndex;

        /// <summary>Total number of phases.</summary>
        public int TotalPhases => _phases.Count;

        /// <summary>Whether the boss is currently in a phase transition (invulnerable).</summary>
        public bool IsInTransition { get; private set; }

        /// <summary>Whether the boss is currently in a vulnerable window after attacking.</summary>
        public bool IsVulnerable { get; private set; }

        #endregion

        #region Private State

        private readonly List<Guardian> _activeMinions = new List<Guardian>();
        private Coroutine _attackCoroutine;
        private Coroutine _transitionCoroutine;
        private Vector3 _bossBasePosition;

        #endregion

        #region Unity Lifecycle

        protected override void Awake()
        {
            base.Awake();
            _currentPhaseIndex = 0;
            _bossBasePosition = transform.position;

            ApplyPhaseVisuals();
        }

        protected override void Update()
        {
            if (!IsAlive) return;

            if (!IsInTransition)
            {
                AnimateBossIdle();
            }
            else
            {
                AnimateTransitionFlash();
            }
        }

        #endregion

        #region Damage Override

        /// <summary>
        /// Applies damage with phase-awareness. Invulnerable during transitions.
        /// Extra damage when vulnerable or hit by the phase's weakness element.
        /// </summary>
        public override float TakeDamage(float amount, ElementType element)
        {
            if (IsInTransition) return 0f;

            float multiplier = 1f;

            // Vulnerability bonus
            BossPhase phase = CurrentPhase;
            if (phase != null && element == phase.vulnerability)
                multiplier = 2f;

            // Extra damage during vulnerable window
            if (IsVulnerable)
                multiplier *= 1.5f;

            float damage = base.TakeDamage(amount * multiplier, element);

            // Check for phase transition
            CheckPhaseTransition();

            return damage;
        }

        #endregion

        #region Phase Management

        private void CheckPhaseTransition()
        {
            if (IsInTransition) return;

            int nextPhase = _currentPhaseIndex + 1;
            if (nextPhase >= _phases.Count) return;

            float threshold = _phases[nextPhase].healthThreshold;
            if (HealthRatio <= threshold)
            {
                StartPhaseTransition(nextPhase);
            }
        }

        private void StartPhaseTransition(int newPhaseIndex)
        {
            if (_transitionCoroutine != null)
                StopCoroutine(_transitionCoroutine);

            _transitionCoroutine = StartCoroutine(PhaseTransitionRoutine(newPhaseIndex));
        }

        private IEnumerator PhaseTransitionRoutine(int newPhaseIndex)
        {
            IsInTransition = true;

            // Stop any active attacks
            if (_attackCoroutine != null)
            {
                StopCoroutine(_attackCoroutine);
                _attackCoroutine = null;
            }

            // Play transition VFX
            if (_phaseTransitionVFX != null)
                _phaseTransitionVFX.Play();

            if (_phaseTransitionSound != null && TryGetComponent<AudioSource>(out var audio))
                audio.PlayOneShot(_phaseTransitionSound);

            // Camera shake
            // In production, call CameraShakeManager.Shake(_cameraShakeOnPhaseChange)

            // Dramatic pause with scale animation
            float elapsed = 0f;
            float halfDuration = _phaseTransitionDuration * 0.5f;

            // Scale up
            Vector3 originalScale = transform.localScale;
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                float scale = Mathf.Lerp(1f, 1.3f, t);
                transform.localScale = originalScale * scale;
                yield return null;
            }

            // Spawn minions
            BossPhase phase = _phases[newPhaseIndex];
            yield return StartCoroutine(SpawnMinions(phase.minionCount));

            // Spawn barriers if applicable
            if (phase.barrierPrefab != null)
                SpawnBarriers(phase);

            // Switch phase
            _currentPhaseIndex = newPhaseIndex;
            ApplyPhaseVisuals();
            OnPhaseChanged?.Invoke(_currentPhaseIndex);

            // Scale back down
            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);
                float scale = Mathf.Lerp(1.3f, 1f, t);
                transform.localScale = originalScale * scale;
                yield return null;
            }

            transform.localScale = originalScale;
            IsInTransition = false;
            _transitionCoroutine = null;
        }

        private void ApplyPhaseVisuals()
        {
            BossPhase phase = CurrentPhase;
            if (phase == null || _bossSpriteRenderer == null) return;

            _bossSpriteRenderer.color = phase.phaseColor;
        }

        #endregion

        #region Minion Spawning

        private IEnumerator SpawnMinions(int count)
        {
            if (_minionPrefab == null || _minionSpawnPoints == null) yield break;

            List<Guardian> spawned = new List<Guardian>();

            for (int i = 0; i < count; i++)
            {
                Transform spawnPoint = _minionSpawnPoints[i % _minionSpawnPoints.Length];
                GameObject minionObj = Instantiate(_minionPrefab, spawnPoint.position, Quaternion.identity);
                Guardian minion = minionObj.GetComponent<Guardian>();

                if (minion != null)
                {
                    spawned.Add(minion);
                    _activeMinions.Add(minion);
                    minion.OnGuardianDefeated += HandleMinionDefeated;
                }

                // Spawn entrance animation (quick scale pop)
                minionObj.transform.localScale = Vector3.zero;
                float elapsed = 0f;
                float dur = 0.3f;
                while (elapsed < dur)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / dur);
                    // Overshoot
                    float s = t < 0.7f ? Mathf.Lerp(0f, 1.2f, t / 0.7f) : Mathf.Lerp(1.2f, 1f, (t - 0.7f) / 0.3f);
                    minionObj.transform.localScale = Vector3.one * s;
                    yield return null;
                }
                minionObj.transform.localScale = Vector3.one;

                yield return new WaitForSeconds(_minionSpawnDelay);
            }

            OnMinionsSpawned?.Invoke(spawned);
        }

        private void HandleMinionDefeated(Guardian minion, int score)
        {
            minion.OnGuardianDefeated -= HandleMinionDefeated;
            _activeMinions.Remove(minion);
        }

        #endregion

        #region Attacks

        /// <summary>
        /// Fires a projectile toward the target area and enters a brief vulnerable state.
        /// Called by GuardianAI during the Attack state.
        /// </summary>
        public void PerformAttack()
        {
            if (_attackCoroutine != null) return;
            _attackCoroutine = StartCoroutine(AttackRoutine());
        }

        private IEnumerator AttackRoutine()
        {
            BossPhase phase = CurrentPhase;
            if (phase == null || phase.projectilePrefab == null)
            {
                _attackCoroutine = null;
                yield break;
            }

            // Windup
            yield return new WaitForSeconds(0.5f / (phase.attackSpeedMultiplier));

            // Spawn projectile
            Vector3 spawnPos = _projectileSpawnPoint != null
                ? _projectileSpawnPoint.position
                : transform.position;

            Vector3 targetPos = _targetArea != null ? _targetArea.position : Vector3.zero;

            GameObject projectile = Instantiate(phase.projectilePrefab, spawnPos, Quaternion.identity);
            Vector3 direction = (targetPos - spawnPos).normalized;

            Rigidbody2D rb = projectile.GetComponent<Rigidbody2D>();
            if (rb != null)
                rb.linearVelocity = direction * _projectileSpeed;

            OnProjectileFired?.Invoke(targetPos);

            // Enter vulnerable state
            IsVulnerable = true;
            yield return new WaitForSeconds(_vulnerableDuration);
            IsVulnerable = false;

            // Cooldown
            yield return new WaitForSeconds(_attackCooldown / (phase.attackSpeedMultiplier));

            _attackCoroutine = null;
        }

        private void SpawnBarriers(BossPhase phase)
        {
            if (phase.barrierPrefab == null || _barrierSpawnPoints == null) return;

            foreach (var point in _barrierSpawnPoints)
            {
                if (point == null) continue;
                Instantiate(phase.barrierPrefab, point.position, Quaternion.identity);
            }

            OnBarrierCreated?.Invoke();
        }

        #endregion

        #region Boss Idle Animation

        private void AnimateBossIdle()
        {
            Vector3 pos = _bossBasePosition;
            pos.y += Mathf.Sin(Time.time * _bossIdleFloatSpeed) * _bossIdleFloatAmplitude;
            transform.position = pos;
        }

        private void AnimateTransitionFlash()
        {
            if (_bossSpriteRenderer == null) return;

            float flash = (Mathf.Sin(Time.time * _invulnerableFlashSpeed) + 1f) * 0.5f;
            Color c = _bossSpriteRenderer.color;
            c.a = Mathf.Lerp(0.3f, 1f, flash);
            _bossSpriteRenderer.color = c;
        }

        #endregion

        #region Death Override

        /// <summary>
        /// Boss death with extended sequence.
        /// </summary>
        protected override void Die()
        {
            // Kill remaining minions
            foreach (var minion in _activeMinions)
            {
                if (minion != null && minion.IsAlive)
                    minion.ForceKill();
            }
            _activeMinions.Clear();

            base.Die();
        }

        #endregion
    }
}
