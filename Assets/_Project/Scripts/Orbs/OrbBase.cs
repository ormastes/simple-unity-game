using System;
using UnityEngine;
using ElementalSiege.Elements;

namespace ElementalSiege.Orbs
{
    /// <summary>
    /// Defines the lifecycle states of an orb from creation to destruction.
    /// </summary>
    public enum OrbState
    {
        /// <summary>Orb is loaded in the launcher, awaiting player input.</summary>
        Loaded,
        /// <summary>Orb has been launched and is travelling through the air.</summary>
        InFlight,
        /// <summary>Player tapped mid-flight to activate the element's special ability.</summary>
        AbilityActivated,
        /// <summary>Orb has come to rest after impact.</summary>
        Settled,
        /// <summary>Orb has been destroyed and is pending cleanup.</summary>
        Destroyed
    }

    /// <summary>
    /// Abstract base class for all elemental orbs. Handles physics, lifecycle state
    /// management, trail rendering, settling detection, and auto-destruction. Concrete
    /// subclasses implement <see cref="OnAbilityActivated"/> for element-specific behavior.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public abstract class OrbBase : MonoBehaviour
    {
        [Header("Element Configuration")]

        /// <summary>The element type ScriptableObject defining this orb's properties.</summary>
        [SerializeField] private ElementType elementType;

        [Header("Lifecycle")]

        /// <summary>Maximum time in seconds before the orb auto-destroys.</summary>
        [SerializeField] private float maxLifetime = 10f;

        /// <summary>Velocity magnitude below which the orb is considered "at rest".</summary>
        [SerializeField] private float settleVelocityThreshold = 0.15f;

        /// <summary>Duration in seconds the orb must remain below settle velocity to count as settled.</summary>
        [SerializeField] private float settleTimeRequired = 1.0f;

        [Header("Trail")]

        /// <summary>Optional trail renderer activated during flight.</summary>
        [SerializeField] private TrailRenderer trailRenderer;

        // --- Events ---

        /// <summary>Raised when the orb comes to rest after impact.</summary>
        public event Action<OrbBase> OnOrbSettled;

        /// <summary>Raised when the orb is destroyed (auto-timeout or explicit destruction).</summary>
        public event Action<OrbBase> OnOrbDestroyed;

        // --- Runtime State ---

        private OrbState _currentState = OrbState.Loaded;
        private float _lifetimeTimer;
        private float _settleTimer;
        private bool _abilityUsed;

        // --- Cached Components ---

        private Rigidbody2D _rigidbody;
        private CircleCollider2D _collider;
        private AudioSource _audioSource;

        // --- Public Properties ---

        /// <summary>The element type assigned to this orb.</summary>
        public ElementType ElementType => elementType;

        /// <summary>Current lifecycle state of the orb.</summary>
        public OrbState CurrentState => _currentState;

        /// <summary>Cached Rigidbody2D for subclass access.</summary>
        protected Rigidbody2D Rb => _rigidbody;

        /// <summary>Cached CircleCollider2D for subclass access.</summary>
        protected CircleCollider2D Col => _collider;

        /// <summary>Whether the mid-flight ability has already been used.</summary>
        protected bool AbilityUsed => _abilityUsed;

        // --- Unity Lifecycle ---

        protected virtual void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            _collider = GetComponent<CircleCollider2D>();
            _audioSource = GetComponent<AudioSource>();

            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();

            _audioSource.playOnAwake = false;

            if (trailRenderer != null)
                trailRenderer.emitting = false;
        }

        protected virtual void Update()
        {
            if (_currentState == OrbState.Destroyed)
                return;

            // Lifetime auto-destroy
            if (_currentState == OrbState.InFlight || _currentState == OrbState.AbilityActivated
                || _currentState == OrbState.Settled)
            {
                _lifetimeTimer += Time.deltaTime;
                if (_lifetimeTimer >= maxLifetime)
                {
                    DestroyOrb();
                    return;
                }
            }

            // Settling detection (only while in flight or after ability)
            if (_currentState == OrbState.InFlight || _currentState == OrbState.AbilityActivated)
            {
                if (_rigidbody.linearVelocity.magnitude < settleVelocityThreshold)
                {
                    _settleTimer += Time.deltaTime;
                    if (_settleTimer >= settleTimeRequired)
                    {
                        TransitionToSettled();
                    }
                }
                else
                {
                    _settleTimer = 0f;
                }
            }
        }

        // --- Public API ---

        /// <summary>
        /// Launches the orb by applying the given force vector. Transitions state
        /// to InFlight and activates the trail renderer.
        /// </summary>
        /// <param name="force">Force vector to apply to the orb's Rigidbody2D.</param>
        public void OnLaunch(Vector2 force)
        {
            if (_currentState != OrbState.Loaded)
            {
                Debug.LogWarning($"[OrbBase] Cannot launch orb in state {_currentState}.");
                return;
            }

            _currentState = OrbState.InFlight;
            _rigidbody.bodyType = RigidbodyType2D.Dynamic;
            _rigidbody.AddForce(force, ForceMode2D.Impulse);

            if (trailRenderer != null)
                trailRenderer.emitting = true;

            PlaySound(elementType != null ? elementType.LaunchSound : null);

            OnLaunched();
        }

        /// <summary>
        /// Attempts to activate the orb's mid-flight ability. Can only be used once
        /// and only while the orb is in flight.
        /// </summary>
        public void TryActivateAbility()
        {
            if (_currentState != OrbState.InFlight || _abilityUsed)
                return;

            _abilityUsed = true;
            _currentState = OrbState.AbilityActivated;

            PlaySound(elementType != null ? elementType.AbilitySound : null);

            OnAbilityActivated();
        }

        // --- Abstract / Virtual Methods for Subclasses ---

        /// <summary>
        /// Called when the orb's mid-flight ability is activated by the player.
        /// Each element subclass implements its unique ability here.
        /// </summary>
        protected abstract void OnAbilityActivated();

        /// <summary>
        /// Called immediately after the orb is launched. Override for launch-specific setup.
        /// </summary>
        protected virtual void OnLaunched() { }

        /// <summary>
        /// Called when the orb collides with another object. Base implementation
        /// applies damage and spawns the impact effect. Override to add element-specific
        /// impact behavior, but call base to retain default handling.
        /// </summary>
        /// <param name="collision">The collision data from Unity's physics engine.</param>
        protected virtual void HandleImpact(Collision2D collision)
        {
            if (elementType == null)
                return;

            // Spawn impact effect via the pooled factory
            ElementEffectFactory.CreateImpactEffect(
                elementType,
                collision.GetContact(0).point,
                elementType.AbilityRadius
            );

            PlaySound(elementType.ImpactSound);

            // Apply damage to destructible targets
            var destructible = collision.gameObject.GetComponent<IDestructible>();
            if (destructible != null)
            {
                destructible.TakeDamage(elementType.BaseDamage, elementType.Category);
            }
        }

        // --- Collision Callbacks ---

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (_currentState == OrbState.InFlight || _currentState == OrbState.AbilityActivated)
            {
                HandleImpact(collision);
            }
        }

        // --- State Transitions ---

        /// <summary>
        /// Transitions the orb to the Settled state and disables the trail.
        /// </summary>
        private void TransitionToSettled()
        {
            if (_currentState == OrbState.Settled || _currentState == OrbState.Destroyed)
                return;

            _currentState = OrbState.Settled;

            if (trailRenderer != null)
                trailRenderer.emitting = false;

            OnOrbSettled?.Invoke(this);
        }

        /// <summary>
        /// Destroys the orb, fires the destroyed event, and cleans up the GameObject.
        /// </summary>
        protected void DestroyOrb()
        {
            if (_currentState == OrbState.Destroyed)
                return;

            _currentState = OrbState.Destroyed;

            if (trailRenderer != null)
                trailRenderer.emitting = false;

            OnOrbDestroyed?.Invoke(this);

            Destroy(gameObject);
        }

        // --- Utility ---

        /// <summary>
        /// Plays a one-shot audio clip through the orb's AudioSource.
        /// </summary>
        /// <param name="clip">The clip to play, or null to skip.</param>
        protected void PlaySound(AudioClip clip)
        {
            if (clip != null && _audioSource != null)
                _audioSource.PlayOneShot(clip);
        }
    }

    /// <summary>
    /// Interface for objects that can receive damage from orbs.
    /// Implement on destructible structures, enemies, and interactive objects.
    /// </summary>
    public interface IDestructible
    {
        /// <summary>
        /// Applies damage to this object.
        /// </summary>
        /// <param name="damage">Amount of damage to apply.</param>
        /// <param name="element">The element category that dealt the damage.</param>
        void TakeDamage(float damage, ElementCategory element);
    }
}
