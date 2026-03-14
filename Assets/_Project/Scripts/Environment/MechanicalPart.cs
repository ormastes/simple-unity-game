using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ElementalSiege.Environment
{
    /// <summary>
    /// Defines the types of mechanical components available.
    /// </summary>
    public enum MechanicalType
    {
        Switch,
        Gear,
        Piston,
        Platform,
        Door
    }

    /// <summary>
    /// Defines what can activate this mechanical part.
    /// </summary>
    [Flags]
    public enum ActivationTrigger
    {
        None       = 0,
        Collision  = 1 << 0,
        Lightning  = 1 << 1,
        Weight     = 1 << 2
    }

    /// <summary>
    /// Triggered mechanical component used in puzzle levels (especially World 7 - Clockwork Citadel).
    /// Supports various mechanical types (Switch, Gear, Piston, Platform, Door) with
    /// configurable activation, movement paths, and chain-triggering of other parts.
    /// </summary>
    [DisallowMultipleComponent]
    public class MechanicalPart : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Mechanical Type")]

        /// <summary>The type of mechanical component.</summary>
        [SerializeField]
        [Tooltip("Determines default behavior and animations.")]
        private MechanicalType mechanicalType = MechanicalType.Switch;

        [Header("Activation")]

        /// <summary>What triggers this mechanical part.</summary>
        [SerializeField]
        [Tooltip("Flags for what can activate this part. Multiple triggers can be combined.")]
        private ActivationTrigger activationTriggers = ActivationTrigger.Collision;

        /// <summary>Minimum collision force required to activate via collision.</summary>
        [SerializeField]
        [Tooltip("Collision impulse threshold for activation.")]
        [Min(0f)]
        private float activationThreshold = 5f;

        /// <summary>Minimum weight (mass) on this part to activate via weight trigger.</summary>
        [SerializeField]
        [Tooltip("Total mass of objects on this part required for weight activation.")]
        [Min(0f)]
        private float weightThreshold = 3f;

        /// <summary>Whether this part stays activated after triggering (latch behavior).</summary>
        [SerializeField]
        [Tooltip("If true, stays activated permanently after first trigger.")]
        private bool latchOnActivate;

        /// <summary>Whether this part toggles between active/inactive on each activation.</summary>
        [SerializeField]
        [Tooltip("Toggle mode: each activation flips the state.")]
        private bool toggleMode;

        [Header("Movement")]

        /// <summary>Movement speed in units per second.</summary>
        [SerializeField]
        [Tooltip("Speed of movement or rotation.")]
        [Min(0f)]
        private float moveSpeed = 2f;

        /// <summary>
        /// Path waypoints for Platform/Door/Piston movement (local space offsets from start).
        /// </summary>
        [SerializeField]
        [Tooltip("Waypoints defining the movement path (local space offsets from initial position).")]
        private Vector3[] movementPath;

        /// <summary>For Gear type: rotation speed in degrees per second.</summary>
        [SerializeField]
        [Tooltip("Rotation speed for Gear type (degrees/second).")]
        private float rotationSpeed = 90f;

        /// <summary>Whether movement loops back to start or ping-pongs.</summary>
        [SerializeField]
        [Tooltip("Loop: returns to start. PingPong: reverses direction at endpoints.")]
        private LoopMode loopMode = LoopMode.PingPong;

        [Header("Chain Triggering")]

        /// <summary>Other MechanicalParts to activate when this one activates.</summary>
        [SerializeField]
        [Tooltip("Parts that are chain-triggered when this part activates.")]
        private MechanicalPart[] chainTargets;

        /// <summary>Delay before chain-triggering connected parts.</summary>
        [SerializeField]
        [Tooltip("Seconds before chain targets are activated.")]
        [Min(0f)]
        private float chainDelay = 0.2f;

        [Header("Audio")]

        /// <summary>Sound played when activated.</summary>
        [SerializeField]
        private AudioClip activateSound;

        /// <summary>Sound played when deactivated.</summary>
        [SerializeField]
        private AudioClip deactivateSound;

        #endregion

        #region Enums

        /// <summary>Defines how movement loops when reaching the end of the path.</summary>
        public enum LoopMode
        {
            /// <summary>Does not loop. Stops at the last waypoint.</summary>
            Once,
            /// <summary>Returns to the first waypoint after reaching the last.</summary>
            Loop,
            /// <summary>Reverses direction at each endpoint.</summary>
            PingPong
        }

        #endregion

        #region Events

        /// <summary>Raised when this part is activated.</summary>
        public event Action OnActivated;

        /// <summary>Raised when this part is deactivated.</summary>
        public event Action OnDeactivated;

        #endregion

        #region Public Properties

        /// <summary>Whether this mechanical part is currently in its activated state.</summary>
        public bool IsActivated { get; private set; }

        /// <summary>The mechanical type of this part.</summary>
        public MechanicalType Type => mechanicalType;

        #endregion

        #region Cached References

        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private AudioSource audioSource;
        private Coroutine moveCoroutine;
        private Coroutine chainCoroutine;

        // Weight tracking
        private readonly HashSet<Rigidbody2D> objectsOnPart = new HashSet<Rigidbody2D>();
        private float currentWeight;

        // Movement state
        private int currentWaypointIndex;
        private int waypointDirection = 1;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            initialPosition = transform.localPosition;
            initialRotation = transform.localRotation;

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1f;
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!activationTriggers.HasFlag(ActivationTrigger.Collision)) return;

            float impactForce = collision.relativeVelocity.magnitude;
            if (collision.rigidbody != null)
            {
                impactForce *= collision.rigidbody.mass;
            }

            if (impactForce >= activationThreshold)
            {
                Activate();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!activationTriggers.HasFlag(ActivationTrigger.Weight)) return;

            var rb = other.attachedRigidbody;
            if (rb != null && objectsOnPart.Add(rb))
            {
                RecalculateWeight();
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!activationTriggers.HasFlag(ActivationTrigger.Weight)) return;

            var rb = other.attachedRigidbody;
            if (rb != null && objectsOnPart.Remove(rb))
            {
                RecalculateWeight();
            }
        }

        private void FixedUpdate()
        {
            // Gear continuous rotation while activated
            if (IsActivated && mechanicalType == MechanicalType.Gear)
            {
                transform.Rotate(0f, 0f, rotationSpeed * Time.fixedDeltaTime);
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Activates this mechanical part. Handles toggle and latch logic.
        /// Called directly, by collision, by lightning (via Conductive), or by weight.
        /// </summary>
        public void Activate()
        {
            if (toggleMode)
            {
                if (IsActivated)
                {
                    Deactivate();
                }
                else
                {
                    PerformActivation();
                }
                return;
            }

            if (IsActivated && latchOnActivate) return;

            PerformActivation();
        }

        /// <summary>
        /// Deactivates this mechanical part, reversing its action.
        /// </summary>
        public void Deactivate()
        {
            if (!IsActivated) return;
            if (latchOnActivate) return;

            IsActivated = false;

            PlaySound(deactivateSound);
            OnDeactivated?.Invoke();

            // Reverse movement
            ReverseMovement();
        }

        /// <summary>
        /// Resets the part to its initial state.
        /// </summary>
        public void ResetPart()
        {
            IsActivated = false;

            if (moveCoroutine != null)
            {
                StopCoroutine(moveCoroutine);
                moveCoroutine = null;
            }

            transform.localPosition = initialPosition;
            transform.localRotation = initialRotation;
            currentWaypointIndex = 0;
            waypointDirection = 1;
            objectsOnPart.Clear();
            currentWeight = 0f;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Performs the activation sequence: sets state, plays sound, starts movement,
        /// and triggers chain targets.
        /// </summary>
        private void PerformActivation()
        {
            IsActivated = true;

            PlaySound(activateSound);
            OnActivated?.Invoke();

            StartMovement();
            ChainTrigger();
        }

        /// <summary>
        /// Starts the appropriate movement based on mechanical type.
        /// </summary>
        private void StartMovement()
        {
            switch (mechanicalType)
            {
                case MechanicalType.Switch:
                    // Switches don't move, they just toggle state
                    break;

                case MechanicalType.Gear:
                    // Gear rotation is handled in FixedUpdate
                    break;

                case MechanicalType.Piston:
                case MechanicalType.Platform:
                case MechanicalType.Door:
                    if (movementPath != null && movementPath.Length > 0)
                    {
                        if (moveCoroutine != null)
                        {
                            StopCoroutine(moveCoroutine);
                        }
                        moveCoroutine = StartCoroutine(FollowPathRoutine());
                    }
                    break;
            }
        }

        /// <summary>
        /// Reverses the current movement direction.
        /// </summary>
        private void ReverseMovement()
        {
            switch (mechanicalType)
            {
                case MechanicalType.Gear:
                    rotationSpeed = -rotationSpeed;
                    break;

                case MechanicalType.Piston:
                case MechanicalType.Platform:
                case MechanicalType.Door:
                    if (moveCoroutine != null)
                    {
                        StopCoroutine(moveCoroutine);
                    }
                    moveCoroutine = StartCoroutine(ReturnToStartRoutine());
                    break;
            }
        }

        /// <summary>
        /// Moves the part along the defined waypoint path.
        /// </summary>
        private IEnumerator FollowPathRoutine()
        {
            while (true)
            {
                Vector3 targetLocal = initialPosition + movementPath[currentWaypointIndex];

                while (Vector3.Distance(transform.localPosition, targetLocal) > 0.01f)
                {
                    transform.localPosition = Vector3.MoveTowards(
                        transform.localPosition,
                        targetLocal,
                        moveSpeed * Time.deltaTime
                    );
                    yield return null;
                }

                transform.localPosition = targetLocal;

                // Advance waypoint index based on loop mode
                if (!AdvanceWaypoint())
                {
                    yield break;
                }
            }
        }

        /// <summary>
        /// Returns the part to its initial position.
        /// </summary>
        private IEnumerator ReturnToStartRoutine()
        {
            while (Vector3.Distance(transform.localPosition, initialPosition) > 0.01f)
            {
                transform.localPosition = Vector3.MoveTowards(
                    transform.localPosition,
                    initialPosition,
                    moveSpeed * Time.deltaTime
                );
                yield return null;
            }

            transform.localPosition = initialPosition;
            currentWaypointIndex = 0;
            waypointDirection = 1;
            moveCoroutine = null;
        }

        /// <summary>
        /// Advances to the next waypoint based on the loop mode.
        /// Returns false if movement should stop.
        /// </summary>
        private bool AdvanceWaypoint()
        {
            switch (loopMode)
            {
                case LoopMode.Once:
                    currentWaypointIndex += waypointDirection;
                    if (currentWaypointIndex >= movementPath.Length || currentWaypointIndex < 0)
                    {
                        return false;
                    }
                    return true;

                case LoopMode.Loop:
                    currentWaypointIndex = (currentWaypointIndex + 1) % movementPath.Length;
                    return true;

                case LoopMode.PingPong:
                    currentWaypointIndex += waypointDirection;
                    if (currentWaypointIndex >= movementPath.Length)
                    {
                        waypointDirection = -1;
                        currentWaypointIndex = movementPath.Length - 2;
                        if (currentWaypointIndex < 0) return false;
                    }
                    else if (currentWaypointIndex < 0)
                    {
                        waypointDirection = 1;
                        currentWaypointIndex = 1;
                        if (currentWaypointIndex >= movementPath.Length) return false;
                    }
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Chain-triggers connected MechanicalParts after the configured delay.
        /// </summary>
        private void ChainTrigger()
        {
            if (chainTargets == null || chainTargets.Length == 0) return;

            if (chainCoroutine != null)
            {
                StopCoroutine(chainCoroutine);
            }
            chainCoroutine = StartCoroutine(ChainTriggerRoutine());
        }

        private IEnumerator ChainTriggerRoutine()
        {
            yield return new WaitForSeconds(chainDelay);

            for (int i = 0; i < chainTargets.Length; i++)
            {
                if (chainTargets[i] != null)
                {
                    chainTargets[i].Activate();
                }
            }

            chainCoroutine = null;
        }

        /// <summary>
        /// Recalculates total weight of objects on this part and checks weight threshold.
        /// </summary>
        private void RecalculateWeight()
        {
            currentWeight = 0f;

            // Clean up destroyed objects
            objectsOnPart.RemoveWhere(rb => rb == null);

            foreach (var rb in objectsOnPart)
            {
                currentWeight += rb.mass;
            }

            if (currentWeight >= weightThreshold && !IsActivated)
            {
                Activate();
            }
            else if (currentWeight < weightThreshold && IsActivated && !latchOnActivate)
            {
                Deactivate();
            }
        }

        /// <summary>
        /// Plays an audio clip through the attached AudioSource.
        /// </summary>
        private void PlaySound(AudioClip clip)
        {
            if (clip == null || audioSource == null) return;
            audioSource.PlayOneShot(clip);
        }

        #endregion

        #region Editor Gizmos

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (movementPath == null || movementPath.Length == 0) return;

            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.8f);

            Vector3 basePos = Application.isPlaying ? initialPosition : transform.localPosition;
            Vector3 worldBase = transform.parent != null
                ? transform.parent.TransformPoint(basePos)
                : basePos;

            Vector3 prev = worldBase;
            for (int i = 0; i < movementPath.Length; i++)
            {
                Vector3 worldPoint = transform.parent != null
                    ? transform.parent.TransformPoint(basePos + movementPath[i])
                    : basePos + movementPath[i];

                Gizmos.DrawLine(prev, worldPoint);
                Gizmos.DrawSphere(worldPoint, 0.08f);
                prev = worldPoint;
            }

            if (loopMode == LoopMode.Loop && movementPath.Length > 1)
            {
                Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.4f);
                Gizmos.DrawLine(prev, worldBase);
            }
        }
#endif

        #endregion
    }
}
