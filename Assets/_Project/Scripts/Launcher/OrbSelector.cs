using System;
using System.Collections.Generic;
using UnityEngine;

namespace ElementalSiege.Launcher
{
    /// <summary>
    /// Enumerates the elemental types available for orbs.
    /// </summary>
    public enum ElementType
    {
        Fire,
        Ice,
        Lightning,
        Earth,
        Wind,
        Water,
        Void
    }

    /// <summary>
    /// Manages the queue of elemental orbs for the current level.
    /// Reads the orb list from level data, spawns orb prefabs, and feeds them to the catapult.
    /// </summary>
    public class OrbSelector : MonoBehaviour
    {
        #region Events

        /// <summary>Fired when the active orb type changes (next orb loaded onto catapult).</summary>
        public event Action<ElementType> OnOrbChanged;

        /// <summary>Fired when no orbs remain in the queue.</summary>
        public event Action OnOrbsEmpty;

        #endregion

        #region Inspector Fields

        [Header("References")]
        [SerializeField]
        [Tooltip("The Catapult that orbs will be loaded onto.")]
        private Catapult _catapult;

        [SerializeField]
        [Tooltip("Transform marking where the next orb spawns before being placed on the catapult.")]
        private Transform _spawnPoint;

        [Header("Orb Prefabs")]
        [SerializeField]
        [Tooltip("Array of orb prefabs indexed by ElementType enum value. Must match enum order.")]
        private GameObject[] _orbPrefabs;

        [Header("Settings")]
        [SerializeField]
        [Tooltip("Delay in seconds before loading the next orb after the previous one settles.")]
        private float _loadDelay = 0.5f;

        #endregion

        #region Private State

        private readonly Queue<ElementType> _orbQueue = new Queue<ElementType>();
        private OrbBase _currentOrb;
        private bool _isLoading;

        #endregion

        #region Properties

        /// <summary>Number of orbs remaining in the queue (not counting the currently loaded orb).</summary>
        public int RemainingCount => _orbQueue.Count;

        /// <summary>Total orbs including the currently loaded one.</summary>
        public int TotalRemaining => _orbQueue.Count + (_currentOrb != null ? 1 : 0);

        /// <summary>Whether there are any orbs left to fire.</summary>
        public bool HasOrbs => _orbQueue.Count > 0 || _currentOrb != null;

        /// <summary>The element type of the currently loaded orb, or null if none.</summary>
        public ElementType? CurrentElementType { get; private set; }

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            if (_catapult != null)
            {
                _catapult.OnStateChanged += HandleCatapultStateChanged;
            }
        }

        private void OnDisable()
        {
            if (_catapult != null)
            {
                _catapult.OnStateChanged -= HandleCatapultStateChanged;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Loads a list of element types into the orb queue and immediately loads the first orb.
        /// Call this at the start of a level.
        /// </summary>
        /// <param name="orbs">Ordered list of element types for the level.</param>
        public void LoadOrbs(List<ElementType> orbs)
        {
            if (orbs == null || orbs.Count == 0)
            {
                Debug.LogWarning("[OrbSelector] LoadOrbs called with null or empty list.");
                OnOrbsEmpty?.Invoke();
                return;
            }

            _orbQueue.Clear();
            foreach (ElementType element in orbs)
            {
                _orbQueue.Enqueue(element);
            }

            LoadNextOrb();
        }

        /// <summary>
        /// Dequeues and returns the next orb, spawning it into the scene.
        /// Returns null if no orbs remain.
        /// </summary>
        /// <returns>The spawned OrbBase instance, or null.</returns>
        public OrbBase GetNextOrb()
        {
            if (_orbQueue.Count == 0)
            {
                OnOrbsEmpty?.Invoke();
                return null;
            }

            ElementType element = _orbQueue.Dequeue();
            OrbBase orb = SpawnOrb(element);

            CurrentElementType = element;
            OnOrbChanged?.Invoke(element);

            return orb;
        }

        /// <summary>
        /// Peeks at the next element type without removing it from the queue.
        /// Returns null if the queue is empty.
        /// </summary>
        /// <returns>The next ElementType, or null if empty.</returns>
        public ElementType? PeekNextOrb()
        {
            if (_orbQueue.Count == 0)
                return null;

            return _orbQueue.Peek();
        }

        #endregion

        #region Private Methods

        private void HandleCatapultStateChanged(Catapult.CatapultState newState)
        {
            if (newState == Catapult.CatapultState.WaitingForOrb && !_isLoading)
            {
                if (_orbQueue.Count > 0)
                {
                    Invoke(nameof(LoadNextOrb), _loadDelay);
                    _isLoading = true;
                }
                else
                {
                    OnOrbsEmpty?.Invoke();
                }
            }
        }

        private void LoadNextOrb()
        {
            _isLoading = false;

            OrbBase orb = GetNextOrb();
            if (orb == null)
                return;

            _currentOrb = orb;
            _catapult.LoadOrb(orb);
        }

        private OrbBase SpawnOrb(ElementType element)
        {
            int prefabIndex = (int)element;

            if (_orbPrefabs == null || prefabIndex < 0 || prefabIndex >= _orbPrefabs.Length)
            {
                Debug.LogError($"[OrbSelector] No prefab assigned for element type {element} (index {prefabIndex}).");
                return null;
            }

            GameObject prefab = _orbPrefabs[prefabIndex];
            if (prefab == null)
            {
                Debug.LogError($"[OrbSelector] Prefab for element type {element} is null.");
                return null;
            }

            Vector3 spawnPos = _spawnPoint != null ? _spawnPoint.position : _catapult.LaunchPosition;
            GameObject instance = Instantiate(prefab, spawnPos, Quaternion.identity);
            instance.name = $"Orb_{element}";

            OrbBase orbComponent = instance.GetComponent<OrbBase>();
            if (orbComponent == null)
            {
                Debug.LogError($"[OrbSelector] Prefab for {element} is missing an OrbBase component.");
                Destroy(instance);
                return null;
            }

            return orbComponent;
        }

        #endregion
    }
}
