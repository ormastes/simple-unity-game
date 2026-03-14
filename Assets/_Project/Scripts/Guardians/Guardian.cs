using System;
using System.Collections;
using UnityEngine;
using ElementalSiege.Elements;
using ElementCategory = ElementalSiege.Elements.ElementCategory;

namespace ElementalSiege.Guardians
{
    /// <summary>
    /// Base crystal guardian enemy. Has health, idle animation, damage reactions,
    /// death effects, and optional elemental shield. Protected by surrounding structures.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Guardian : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Health")]
        [SerializeField] private float _maxHealth = 100f;
        [SerializeField] private float _currentHealth;

        [Header("Shield")]
        [SerializeField] private bool _hasShield;
        [SerializeField] private ElementCategory _shieldWeakness = ElementCategory.Fire;
        [SerializeField] private float _shieldHealth = 50f;
        [SerializeField] private GameObject _shieldVisual;
        [SerializeField] private Color _shieldColor = new Color(0.5f, 0.8f, 1f, 0.6f);

        [Header("Score")]
        [SerializeField] private int _scoreValue = 500;

        [Header("Idle Animation")]
        [SerializeField] private float _bobAmplitude = 0.15f;
        [SerializeField] private float _bobSpeed = 2f;
        [SerializeField] private float _pulseMinScale = 0.95f;
        [SerializeField] private float _pulseMaxScale = 1.05f;
        [SerializeField] private float _pulseSpeed = 1.5f;

        [Header("Damage Reaction")]
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private Color _flashColor = Color.white;
        [SerializeField] private float _flashDuration = 0.1f;
        [SerializeField] private float _shakeIntensity = 0.1f;
        [SerializeField] private float _shakeDuration = 0.2f;

        [Header("Death")]
        [SerializeField] private ParticleSystem _shatterParticles;
        [SerializeField] private float _deathAnimDuration = 0.5f;
        [SerializeField] private AudioClip _deathSound;
        [SerializeField] private AudioClip _hitSound;

        #endregion

        #region Events

        /// <summary>Raised when the guardian takes damage. Passes current health ratio (0–1).</summary>
        public event Action<Guardian, float> OnGuardianDamaged;

        /// <summary>Raised when the guardian is defeated. Passes the score value.</summary>
        public event Action<Guardian, int> OnGuardianDefeated;

        #endregion

        #region Properties

        /// <summary>Current health value.</summary>
        public float CurrentHealth => _currentHealth;

        /// <summary>Maximum health value.</summary>
        public float MaxHealth => _maxHealth;

        /// <summary>Health ratio from 0 (dead) to 1 (full).</summary>
        public float HealthRatio => _maxHealth > 0 ? _currentHealth / _maxHealth : 0f;

        /// <summary>Whether the guardian is still alive.</summary>
        public bool IsAlive => _currentHealth > 0f;

        /// <summary>Whether the shield is still active.</summary>
        public bool IsShielded => _hasShield && _shieldHealth > 0f;

        /// <summary>Score awarded on defeat.</summary>
        public int ScoreValue => _scoreValue;

        #endregion

        #region Private State

        private Vector3 _basePosition;
        private Color _originalColor;
        private Coroutine _flashCoroutine;
        private Coroutine _shakeCoroutine;
        private bool _isDead;

        #endregion

        #region Unity Lifecycle

        protected virtual void Awake()
        {
            _currentHealth = _maxHealth;
            _basePosition = transform.position;

            if (_spriteRenderer != null)
                _originalColor = _spriteRenderer.color;

            if (_shieldVisual != null)
            {
                _shieldVisual.SetActive(_hasShield);
                var sr = _shieldVisual.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = _shieldColor;
            }
        }

        protected virtual void Update()
        {
            if (_isDead) return;

            AnimateIdle();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Applies damage to the guardian, considering shield mechanics.
        /// </summary>
        /// <param name="amount">Raw damage amount.</param>
        /// <param name="element">Element type of the attack.</param>
        /// <returns>Actual damage dealt.</returns>
        public virtual float TakeDamage(float amount, ElementCategory element)
        {
            if (_isDead) return 0f;

            float actualDamage = amount;

            // Shield check
            if (IsShielded)
            {
                if (element == _shieldWeakness)
                {
                    // Effective against shield
                    _shieldHealth -= amount;
                    if (_shieldHealth <= 0f)
                    {
                        _shieldHealth = 0f;
                        DestroyShield();
                    }
                    PlayDamageReaction();
                    return amount;
                }
                else
                {
                    // Shield absorbs damage with reduction
                    actualDamage *= 0.2f;
                }
            }

            _currentHealth -= actualDamage;
            _currentHealth = Mathf.Max(0f, _currentHealth);

            PlayDamageReaction();
            OnGuardianDamaged?.Invoke(this, HealthRatio);

            if (_currentHealth <= 0f)
            {
                Die();
            }

            return actualDamage;
        }

        /// <summary>
        /// Instantly kills the guardian.
        /// </summary>
        public void ForceKill()
        {
            if (_isDead) return;
            _currentHealth = 0f;
            Die();
        }

        #endregion

        #region Idle Animation

        private void AnimateIdle()
        {
            // Bob up and down
            Vector3 pos = _basePosition;
            pos.y += Mathf.Sin(Time.time * _bobSpeed) * _bobAmplitude;
            transform.position = pos;

            // Pulse scale
            float t = (Mathf.Sin(Time.time * _pulseSpeed) + 1f) * 0.5f;
            float scale = Mathf.Lerp(_pulseMinScale, _pulseMaxScale, t);
            transform.localScale = Vector3.one * scale;
        }

        #endregion

        #region Damage Reaction

        /// <summary>
        /// Plays the flash and shake damage reaction effects.
        /// </summary>
        protected void PlayDamageReaction()
        {
            if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
            _flashCoroutine = StartCoroutine(FlashRoutine());

            if (_shakeCoroutine != null) StopCoroutine(_shakeCoroutine);
            _shakeCoroutine = StartCoroutine(ShakeRoutine());

            if (_hitSound != null && TryGetComponent<AudioSource>(out var audio))
                audio.PlayOneShot(_hitSound);
        }

        private IEnumerator FlashRoutine()
        {
            if (_spriteRenderer == null) yield break;

            _spriteRenderer.color = _flashColor;
            yield return new WaitForSeconds(_flashDuration);
            _spriteRenderer.color = _originalColor;
            _flashCoroutine = null;
        }

        private IEnumerator ShakeRoutine()
        {
            float elapsed = 0f;
            while (elapsed < _shakeDuration)
            {
                elapsed += Time.deltaTime;
                float x = UnityEngine.Random.Range(-_shakeIntensity, _shakeIntensity);
                float y = UnityEngine.Random.Range(-_shakeIntensity, _shakeIntensity);
                transform.position = _basePosition + new Vector3(x, y, 0f);
                yield return null;
            }
            _shakeCoroutine = null;
        }

        #endregion

        #region Shield

        private void DestroyShield()
        {
            _hasShield = false;
            if (_shieldVisual != null)
            {
                // Quick scale-down animation
                StartCoroutine(ShieldBreakRoutine());
            }
        }

        private IEnumerator ShieldBreakRoutine()
        {
            if (_shieldVisual == null) yield break;

            float elapsed = 0f;
            float duration = 0.3f;
            Vector3 startScale = _shieldVisual.transform.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                _shieldVisual.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
                yield return null;
            }

            _shieldVisual.SetActive(false);
            _shieldVisual.transform.localScale = startScale;
        }

        #endregion

        #region Death

        /// <summary>
        /// Handles the guardian's death sequence.
        /// Override in subclasses for custom death behavior.
        /// </summary>
        protected virtual void Die()
        {
            if (_isDead) return;
            _isDead = true;

            OnGuardianDefeated?.Invoke(this, _scoreValue);
            StartCoroutine(DeathRoutine());
        }

        private IEnumerator DeathRoutine()
        {
            // Spawn shatter particles
            if (_shatterParticles != null)
            {
                var particles = Instantiate(_shatterParticles, transform.position, Quaternion.identity);
                particles.Play();
                Destroy(particles.gameObject, particles.main.duration + particles.main.startLifetime.constantMax);
            }

            // Play death sound
            if (_deathSound != null && TryGetComponent<AudioSource>(out var audio))
                audio.PlayOneShot(_deathSound);

            // Shrink and fade
            float elapsed = 0f;
            Vector3 startScale = transform.localScale;

            while (elapsed < _deathAnimDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _deathAnimDuration);
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);

                if (_spriteRenderer != null)
                {
                    Color c = _spriteRenderer.color;
                    c.a = 1f - t;
                    _spriteRenderer.color = c;
                }
                yield return null;
            }

            Destroy(gameObject);
        }

        #endregion
    }
}
