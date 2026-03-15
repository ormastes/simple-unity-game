using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ElementalSiege.Core
{
    /// <summary>
    /// Types of objectives that can be assigned to a level.
    /// </summary>
    public enum ObjectiveType
    {
        /// <summary>Destroy a target number of guardians.</summary>
        DestroyGuardians,

        /// <summary>Destroy a target number of structures.</summary>
        DestroyStructures,

        /// <summary>Complete the level using at most N orbs.</summary>
        UseMaxOrbs,

        /// <summary>Trigger a specific elemental combo.</summary>
        TriggerCombo,

        /// <summary>Keep a specific structure from being destroyed.</summary>
        ProtectStructure,

        /// <summary>Complete the level within a time limit.</summary>
        SpeedClear
    }

    /// <summary>
    /// Represents a single objective within a level, tracking progress toward completion.
    /// </summary>
    [Serializable]
    public class Objective
    {
        /// <summary>The type of objective.</summary>
        [SerializeField] private ObjectiveType _type;
        public ObjectiveType Type => _type;

        /// <summary>Human-readable description of the objective.</summary>
        [SerializeField, TextArea(1, 3)] private string _description;
        public string Description => _description;

        /// <summary>Value that must be reached (or not exceeded, depending on type) to complete.</summary>
        [SerializeField] private int _targetValue;
        public int TargetValue => _targetValue;

        /// <summary>Current progress toward the target value.</summary>
        [SerializeField] private int _currentValue;
        public int CurrentValue
        {
            get => _currentValue;
            set => _currentValue = value;
        }

        /// <summary>Whether this objective is a primary requirement or a bonus objective.</summary>
        [SerializeField] private bool _isPrimary = true;
        public bool IsPrimary => _isPrimary;

        /// <summary>Whether this objective has been completed.</summary>
        public bool IsComplete
        {
            get
            {
                switch (_type)
                {
                    case ObjectiveType.UseMaxOrbs:
                        // Must use at most the target value
                        return _currentValue <= _targetValue && _currentValue > 0;
                    case ObjectiveType.ProtectStructure:
                        // Current value tracks damage; 0 damage = success
                        return _currentValue == 0;
                    case ObjectiveType.SpeedClear:
                        // Current value is elapsed time in seconds; must be <= target
                        return _currentValue > 0 && _currentValue <= _targetValue;
                    default:
                        // Must reach or exceed the target
                        return _currentValue >= _targetValue;
                }
            }
        }

        /// <summary>
        /// Creates a new objective with the specified parameters.
        /// </summary>
        public Objective(ObjectiveType type, string description, int targetValue, bool isPrimary = true)
        {
            _type = type;
            _description = description;
            _targetValue = targetValue;
            _isPrimary = isPrimary;
            _currentValue = 0;
        }

        /// <summary>
        /// Resets the objective progress to zero.
        /// </summary>
        public void Reset()
        {
            _currentValue = 0;
        }
    }

    /// <summary>
    /// Manages per-level objectives including primary win conditions and optional bonus objectives.
    /// Integrates with level data and fires events on objective completion.
    /// </summary>
    public class ObjectiveManager : MonoBehaviour
    {
        /// <summary>Fired when a single objective is completed.</summary>
        public event Action<Objective> OnObjectiveCompleted;

        /// <summary>Fired when all primary objectives are completed.</summary>
        public event Action OnAllObjectivesComplete;

        /// <summary>Fired when a bonus objective is completed.</summary>
        public event Action<Objective> OnBonusObjectiveCompleted;

        private static ObjectiveManager _instance;

        /// <summary>Global singleton accessor.</summary>
        public static ObjectiveManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[ObjectiveManager]");
                    _instance = go.AddComponent<ObjectiveManager>();
                }
                return _instance;
            }
        }

        [SerializeField, Tooltip("List of objectives for the current level")]
        private List<Objective> _objectives = new List<Objective>();

        /// <summary>Read-only access to all current objectives.</summary>
        public IReadOnlyList<Objective> Objectives => _objectives;

        /// <summary>Whether all primary objectives have been completed.</summary>
        public bool AllPrimaryComplete => _objectives
            .Where(o => o.IsPrimary)
            .All(o => o.IsComplete);

        /// <summary>Whether all objectives (primary and bonus) have been completed.</summary>
        public bool AllComplete => _objectives.All(o => o.IsComplete);

        /// <summary>Number of bonus objectives completed.</summary>
        public int BonusObjectivesCompleted => _objectives
            .Count(o => !o.IsPrimary && o.IsComplete);

        /// <summary>Total number of bonus objectives.</summary>
        public int TotalBonusObjectives => _objectives.Count(o => !o.IsPrimary);

        private readonly HashSet<Objective> _completedObjectives = new HashSet<Objective>();

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        /// <summary>
        /// Initializes the objective manager with a list of objectives for a new level.
        /// </summary>
        /// <param name="objectives">List of objectives to track.</param>
        public void InitializeObjectives(List<Objective> objectives)
        {
            _objectives = objectives ?? new List<Objective>();
            _completedObjectives.Clear();

            foreach (var objective in _objectives)
            {
                objective.Reset();
            }

            Debug.Log($"[ObjectiveManager] Initialized with {_objectives.Count} objectives " +
                      $"({_objectives.Count(o => o.IsPrimary)} primary, " +
                      $"{_objectives.Count(o => !o.IsPrimary)} bonus)");
        }

        /// <summary>
        /// Updates progress for all objectives matching the given type.
        /// </summary>
        /// <param name="type">The type of objective to update.</param>
        /// <param name="value">The new value to apply.</param>
        public void CheckObjective(ObjectiveType type, int value)
        {
            foreach (var objective in _objectives)
            {
                if (objective.Type != type) continue;
                if (_completedObjectives.Contains(objective)) continue;

                bool wasPreviouslyComplete = objective.IsComplete;

                switch (type)
                {
                    case ObjectiveType.UseMaxOrbs:
                    case ObjectiveType.SpeedClear:
                        // These types set the value directly (not cumulative)
                        objective.CurrentValue = value;
                        break;

                    case ObjectiveType.ProtectStructure:
                        // Increment damage counter
                        objective.CurrentValue += value;
                        break;

                    default:
                        // Cumulative types: add the value
                        objective.CurrentValue += value;
                        break;
                }

                if (!wasPreviouslyComplete && objective.IsComplete)
                {
                    _completedObjectives.Add(objective);

                    Debug.Log($"[ObjectiveManager] Objective completed: {objective.Description}");
                    OnObjectiveCompleted?.Invoke(objective);

                    if (!objective.IsPrimary)
                    {
                        OnBonusObjectiveCompleted?.Invoke(objective);
                    }

                    // Check if all primary objectives are now complete
                    if (AllPrimaryComplete)
                    {
                        Debug.Log("[ObjectiveManager] All primary objectives complete!");
                        OnAllObjectivesComplete?.Invoke();
                    }
                }
            }
        }

        /// <summary>
        /// Returns all primary objectives.
        /// </summary>
        public List<Objective> GetPrimaryObjectives()
        {
            return _objectives.Where(o => o.IsPrimary).ToList();
        }

        /// <summary>
        /// Returns all bonus (secondary) objectives.
        /// </summary>
        public List<Objective> GetBonusObjectives()
        {
            return _objectives.Where(o => !o.IsPrimary).ToList();
        }

        /// <summary>
        /// Returns a summary string of current objective progress.
        /// </summary>
        public string GetProgressSummary()
        {
            var completed = _objectives.Count(o => o.IsComplete);
            return $"{completed}/{_objectives.Count} objectives complete " +
                   $"({BonusObjectivesCompleted}/{TotalBonusObjectives} bonus)";
        }

        /// <summary>
        /// Calculates bonus stars earned from optional objectives.
        /// Returns 0, 1, or 2 bonus stars based on completion.
        /// </summary>
        public int CalculateBonusStars()
        {
            if (TotalBonusObjectives == 0) return 0;

            float completionRatio = (float)BonusObjectivesCompleted / TotalBonusObjectives;

            if (completionRatio >= 1.0f) return 2;
            if (completionRatio >= 0.5f) return 1;
            return 0;
        }

        /// <summary>
        /// Resets all objective progress for a level retry.
        /// </summary>
        public void ResetAll()
        {
            _completedObjectives.Clear();
            foreach (var objective in _objectives)
            {
                objective.Reset();
            }
        }
    }
}
