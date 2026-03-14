using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ElementalSiege.Core
{
    /// <summary>
    /// Manages the active level: spawning structures and orbs from a LevelData
    /// ScriptableObject, tracking win/fail conditions, and monitoring physics settling.
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        // ──────────────────────────────────────────────
        //  Data types
        // ──────────────────────────────────────────────

        /// <summary>
        /// Placement entry for a prefab within a level.
        /// </summary>
        [Serializable]
        public struct PlacementData
        {
            public GameObject Prefab;
            public Vector2 Position;
            public float Rotation;
        }

        /// <summary>
        /// ScriptableObject that defines a level's layout and orb loadout.
        /// Create via Assets > Create > ElementalSiege > LevelData.
        /// </summary>
        [CreateAssetMenu(fileName = "NewLevel", menuName = "ElementalSiege/LevelData")]
        public class LevelData : ScriptableObject
        {
            [Header("Identity")]
            public string LevelId;
            public string DisplayName;

            [Header("Orbs")]
            public List<GameObject> OrbPrefabs = new List<GameObject>();

            [Header("Structures")]
            public List<PlacementData> Structures = new List<PlacementData>();

            [Header("Guardians")]
            public List<PlacementData> Guardians = new List<PlacementData>();

            [Header("Scoring")]
            public float ParTime = 60f;
        }

        // ──────────────────────────────────────────────
        //  Events
        // ──────────────────────────────────────────────

        /// <summary>Fired after a level has been fully loaded and spawned.</summary>
        public static event Action<LevelData> OnLevelLoaded;

        /// <summary>Fired when all guardians are destroyed.</summary>
        public static event Action OnLevelComplete;

        /// <summary>Fired when all orbs are used and guardians remain.</summary>
        public static event Action OnLevelFailed;

        /// <summary>Fired when an orb is consumed (launched or destroyed).</summary>
        public static event Action<int> OnOrbCountChanged;

        /// <summary>Fired when a guardian is destroyed.</summary>
        public static event Action<int> OnGuardianCountChanged;

        // ──────────────────────────────────────────────
        //  Inspector fields
        // ──────────────────────────────────────────────

        [Header("Level")]
        [SerializeField] private LevelData _levelData;

        [Header("Spawn Parents")]
        [SerializeField] private Transform _structureParent;
        [SerializeField] private Transform _guardianParent;
        [SerializeField] private Transform _orbParent;

        [Header("Physics Settling")]
        [Tooltip("Time in seconds to wait for physics to settle before checking win/fail.")]
        [SerializeField] private float _settleTime = 2.0f;

        [Tooltip("Velocity threshold below which objects are considered settled.")]
        [SerializeField] private float _settleVelocityThreshold = 0.05f;

        // ──────────────────────────────────────────────
        //  Runtime state
        // ──────────────────────────────────────────────

        private readonly List<GameObject> _spawnedStructures = new List<GameObject>();
        private readonly List<GameObject> _spawnedGuardians = new List<GameObject>();
        private readonly List<GameObject> _availableOrbs = new List<GameObject>();

        private int _totalOrbCount;
        private int _orbsUsed;
        private int _totalGuardianCount;
        private bool _levelActive;
        private bool _isCheckingOutcome;
        private float _levelStartTime;

        /// <summary>Number of orbs remaining (not yet launched).</summary>
        public int OrbsRemaining => _availableOrbs.Count;

        /// <summary>Number of orbs used so far.</summary>
        public int OrbsUsed => _orbsUsed;

        /// <summary>Total orbs allocated for this level.</summary>
        public int TotalOrbs => _totalOrbCount;

        /// <summary>Number of guardians still alive.</summary>
        public int GuardiansRemaining => CountActiveGuardians();

        /// <summary>Total guardians at level start.</summary>
        public int TotalGuardians => _totalGuardianCount;

        /// <summary>The currently loaded level data.</summary>
        public LevelData CurrentLevelData => _levelData;

        /// <summary>Elapsed time since level start in seconds.</summary>
        public float ElapsedTime => _levelActive ? Time.time - _levelStartTime : 0f;

        /// <summary>Destruction percentage (structures destroyed / total structures).</summary>
        public float DestructionPercent
        {
            get
            {
                if (_spawnedStructures.Count == 0) return 1f;
                int destroyed = 0;
                foreach (var s in _spawnedStructures)
                {
                    if (s == null) destroyed++;
                }
                return (float)destroyed / _spawnedStructures.Count;
            }
        }

        // ──────────────────────────────────────────────
        //  Lifecycle
        // ──────────────────────────────────────────────

        private void Start()
        {
            if (_levelData != null)
            {
                LoadLevel(_levelData);
            }
        }

        private void OnEnable()
        {
            GameManager.OnGameStateChanged += HandleGameStateChanged;
        }

        private void OnDisable()
        {
            GameManager.OnGameStateChanged -= HandleGameStateChanged;
        }

        // ──────────────────────────────────────────────
        //  Public API
        // ──────────────────────────────────────────────

        /// <summary>
        /// Loads and spawns a level from its data asset.
        /// </summary>
        /// <param name="data">The level data to load.</param>
        public void LoadLevel(LevelData data)
        {
            if (data == null)
            {
                Debug.LogError("[LevelManager] LevelData is null.");
                return;
            }

            ClearLevel();
            _levelData = data;

            // Spawn structures
            foreach (var placement in data.Structures)
            {
                var obj = SpawnPlacement(placement, GetOrCreateParent(ref _structureParent, "Structures"));
                _spawnedStructures.Add(obj);
            }

            // Spawn guardians
            foreach (var placement in data.Guardians)
            {
                var obj = SpawnPlacement(placement, GetOrCreateParent(ref _guardianParent, "Guardians"));
                _spawnedGuardians.Add(obj);
            }
            _totalGuardianCount = _spawnedGuardians.Count;

            // Spawn orbs
            foreach (var orbPrefab in data.OrbPrefabs)
            {
                if (orbPrefab == null) continue;
                var orb = Instantiate(orbPrefab, GetOrCreateParent(ref _orbParent, "Orbs"));
                orb.SetActive(false);
                _availableOrbs.Add(orb);
            }
            _totalOrbCount = _availableOrbs.Count;
            _orbsUsed = 0;

            _levelActive = true;
            _isCheckingOutcome = false;
            _levelStartTime = Time.time;

            Debug.Log($"[LevelManager] Level '{data.DisplayName}' loaded. " +
                      $"Orbs: {_totalOrbCount}, Guardians: {_totalGuardianCount}, " +
                      $"Structures: {_spawnedStructures.Count}");

            OnLevelLoaded?.Invoke(data);
        }

        /// <summary>
        /// Consumes the next available orb and returns it, or null if none remain.
        /// </summary>
        /// <returns>The next orb GameObject, already activated, or null.</returns>
        public GameObject ConsumeOrb()
        {
            if (_availableOrbs.Count == 0) return null;

            var orb = _availableOrbs[0];
            _availableOrbs.RemoveAt(0);
            orb.SetActive(true);
            _orbsUsed++;

            OnOrbCountChanged?.Invoke(_availableOrbs.Count);

            return orb;
        }

        /// <summary>
        /// Call when an orb has finished its trajectory (landed, exploded, etc.).
        /// Triggers the physics-settle check.
        /// </summary>
        public void OnOrbFinished()
        {
            if (!_levelActive || _isCheckingOutcome) return;
            StartCoroutine(WaitForSettleThenCheck());
        }

        /// <summary>
        /// Notifies the level manager that a guardian was destroyed.
        /// </summary>
        public void NotifyGuardianDestroyed()
        {
            int remaining = CountActiveGuardians();
            OnGuardianCountChanged?.Invoke(remaining);

            if (remaining <= 0 && _levelActive)
            {
                _levelActive = false;
                Debug.Log("[LevelManager] All guardians destroyed!");
                OnLevelComplete?.Invoke();
                GameManager.Instance.CompleteLevel();
            }
        }

        // ──────────────────────────────────────────────
        //  Physics settling & outcome check
        // ──────────────────────────────────────────────

        private IEnumerator WaitForSettleThenCheck()
        {
            _isCheckingOutcome = true;

            // Wait minimum settle time
            yield return new WaitForSeconds(_settleTime);

            // Wait until all active rigidbodies are nearly stationary
            float timeout = 5f;
            float elapsed = 0f;
            while (elapsed < timeout)
            {
                if (ArePhysicsSettled())
                    break;

                elapsed += Time.deltaTime;
                yield return null;
            }

            _isCheckingOutcome = false;

            if (!_levelActive) yield break;

            CheckOutcome();
        }

        private bool ArePhysicsSettled()
        {
            var bodies = FindObjectsByType<Rigidbody2D>(FindObjectsSortMode.None);
            foreach (var rb in bodies)
            {
                if (rb == null || rb.bodyType == RigidbodyType2D.Static) continue;
                if (rb.linearVelocity.sqrMagnitude > _settleVelocityThreshold * _settleVelocityThreshold)
                    return false;
            }
            return true;
        }

        private void CheckOutcome()
        {
            int guardiansRemaining = CountActiveGuardians();

            if (guardiansRemaining <= 0)
            {
                _levelActive = false;
                OnLevelComplete?.Invoke();
                GameManager.Instance.CompleteLevel();
            }
            else if (_availableOrbs.Count <= 0)
            {
                _levelActive = false;
                Debug.Log($"[LevelManager] No orbs remaining. {guardiansRemaining} guardian(s) survive.");
                OnLevelFailed?.Invoke();
                GameManager.Instance.FailLevel();
            }
        }

        // ──────────────────────────────────────────────
        //  Helpers
        // ──────────────────────────────────────────────

        private int CountActiveGuardians()
        {
            int count = 0;
            foreach (var g in _spawnedGuardians)
            {
                if (g != null && g.activeInHierarchy) count++;
            }
            return count;
        }

        private GameObject SpawnPlacement(PlacementData placement, Transform parent)
        {
            if (placement.Prefab == null)
            {
                Debug.LogWarning("[LevelManager] Null prefab in placement data.");
                return null;
            }

            var obj = Instantiate(placement.Prefab, parent);
            obj.transform.position = new Vector3(placement.Position.x, placement.Position.y, 0f);
            obj.transform.rotation = Quaternion.Euler(0f, 0f, placement.Rotation);
            return obj;
        }

        private Transform GetOrCreateParent(ref Transform parentField, string name)
        {
            if (parentField != null) return parentField;

            var go = new GameObject(name);
            go.transform.SetParent(transform);
            parentField = go.transform;
            return parentField;
        }

        private void ClearLevel()
        {
            _levelActive = false;
            StopAllCoroutines();

            foreach (var obj in _spawnedStructures)
                if (obj != null) Destroy(obj);
            foreach (var obj in _spawnedGuardians)
                if (obj != null) Destroy(obj);
            foreach (var obj in _availableOrbs)
                if (obj != null) Destroy(obj);

            _spawnedStructures.Clear();
            _spawnedGuardians.Clear();
            _availableOrbs.Clear();
        }

        private void HandleGameStateChanged(GameManager.GameState previous, GameManager.GameState current)
        {
            if (current == GameManager.GameState.Playing && previous == GameManager.GameState.Paused)
            {
                // Resumed — nothing special needed
            }
        }
    }
}
