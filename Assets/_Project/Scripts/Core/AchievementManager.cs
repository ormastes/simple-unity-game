using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ElementalSiege.Core
{
    /// <summary>
    /// Condition types that trigger achievement unlocks.
    /// </summary>
    public enum AchievementCondition
    {
        TotalStars,
        WorldComplete,
        PerfectLevel,
        ElementCombo,
        TotalLevels,
        BossDefeated,
        NoOrbsWasted,
        SpeedRun
    }

    /// <summary>
    /// ScriptableObject that defines a single achievement's metadata and unlock criteria.
    /// </summary>
    [CreateAssetMenu(fileName = "NewAchievement", menuName = "ElementalSiege/Achievement Data")]
    public class AchievementData : ScriptableObject
    {
        /// <summary>Unique identifier for this achievement.</summary>
        [SerializeField] private string _id;
        public string Id => _id;

        /// <summary>Display name shown in the UI.</summary>
        [SerializeField] private string _name;
        public string Name => _name;

        /// <summary>Description of how to earn this achievement.</summary>
        [SerializeField, TextArea(2, 4)] private string _description;
        public string Description => _description;

        /// <summary>Icon displayed in the achievement popup and gallery.</summary>
        [SerializeField] private Sprite _icon;
        public Sprite Icon => _icon;

        /// <summary>If true, the achievement is hidden until unlocked.</summary>
        [SerializeField] private bool _isHidden;
        public bool IsHidden => _isHidden;

        /// <summary>Condition type required to unlock this achievement.</summary>
        [SerializeField] private AchievementCondition _unlockCondition;
        public AchievementCondition UnlockCondition => _unlockCondition;

        /// <summary>Threshold value the condition must reach or exceed.</summary>
        [SerializeField] private int _requiredValue = 1;
        public int RequiredValue => _requiredValue;
    }

    /// <summary>
    /// Serializable container for persisted achievement state.
    /// </summary>
    [Serializable]
    public class AchievementSaveData
    {
        public List<string> unlockedIds = new List<string>();
        public Dictionary<string, int> progressMap = new Dictionary<string, int>();
    }

    /// <summary>
    /// Manages unlockable achievements, persisting state to JSON alongside save data.
    /// Provides built-in achievements and fires events on unlock.
    /// </summary>
    public class AchievementManager : MonoBehaviour
    {
        /// <summary>Fired when any achievement is unlocked.</summary>
        public event Action<AchievementData> OnAchievementUnlocked;

        private static AchievementManager _instance;

        /// <summary>Global singleton accessor.</summary>
        public static AchievementManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[AchievementManager]");
                    _instance = go.AddComponent<AchievementManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        [SerializeField]
        private List<AchievementData> _allAchievements = new List<AchievementData>();

        private HashSet<string> _unlockedIds = new HashSet<string>();
        private Dictionary<string, int> _progressMap = new Dictionary<string, int>();
        private string _savePath;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            _savePath = Path.Combine(Application.persistentDataPath, "achievements.json");
            LoadAchievements();
            RegisterBuiltInAchievements();
        }

        /// <summary>
        /// Registers the built-in achievement definitions if no ScriptableObjects are assigned.
        /// </summary>
        private void RegisterBuiltInAchievements()
        {
            if (_allAchievements.Count > 0) return;

            _allAchievements.AddRange(CreateBuiltInAchievements());
        }

        /// <summary>
        /// Creates runtime achievement data for the built-in set.
        /// </summary>
        private List<AchievementData> CreateBuiltInAchievements()
        {
            var achievements = new List<AchievementData>();

            achievements.Add(CreateRuntimeAchievement(
                "first_blood", "First Blood", "Complete your first level",
                AchievementCondition.TotalLevels, 1, false));

            achievements.Add(CreateRuntimeAchievement(
                "pyromaniac", "Pyromaniac", "Complete all fire-themed levels",
                AchievementCondition.WorldComplete, 1, false));

            achievements.Add(CreateRuntimeAchievement(
                "alchemist", "Alchemist", "Trigger every elemental combo at least once",
                AchievementCondition.ElementCombo, 10, false));

            achievements.Add(CreateRuntimeAchievement(
                "three_star_general", "Three Star General", "Earn three stars on every level",
                AchievementCondition.TotalStars, 999, false));

            achievements.Add(CreateRuntimeAchievement(
                "void_master", "Void Master", "Defeat the final boss",
                AchievementCondition.BossDefeated, 1, false));

            achievements.Add(CreateRuntimeAchievement(
                "perfectionist", "Perfectionist", "Complete a level with a perfect score",
                AchievementCondition.PerfectLevel, 1, false));

            achievements.Add(CreateRuntimeAchievement(
                "no_waste", "Not a Single Orb Wasted", "Complete a level using the minimum orbs",
                AchievementCondition.NoOrbsWasted, 1, false));

            achievements.Add(CreateRuntimeAchievement(
                "speed_demon", "Speed Demon", "Complete any level in under 10 seconds",
                AchievementCondition.SpeedRun, 1, false));

            achievements.Add(CreateRuntimeAchievement(
                "veteran", "Veteran", "Complete 50 levels",
                AchievementCondition.TotalLevels, 50, false));

            achievements.Add(CreateRuntimeAchievement(
                "hidden_master", "???", "Discover the secret combo",
                AchievementCondition.ElementCombo, 1, true));

            return achievements;
        }

        /// <summary>
        /// Creates a runtime AchievementData ScriptableObject instance (not saved as an asset).
        /// </summary>
        private AchievementData CreateRuntimeAchievement(string id, string name, string description,
            AchievementCondition condition, int requiredValue, bool isHidden)
        {
            var data = ScriptableObject.CreateInstance<AchievementData>();

            // Use serialized field injection via SerializedObject equivalent at runtime
            var json = JsonUtility.ToJson(data);
            var wrapper = new AchievementJsonWrapper
            {
                _id = id,
                _name = name,
                _description = description,
                _isHidden = isHidden,
                _unlockCondition = condition,
                _requiredValue = requiredValue
            };
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(wrapper), data);
            return data;
        }

        /// <summary>
        /// Internal wrapper for JSON-based field injection into AchievementData.
        /// </summary>
        [Serializable]
        private class AchievementJsonWrapper
        {
            public string _id;
            public string _name;
            public string _description;
            public bool _isHidden;
            public AchievementCondition _unlockCondition;
            public int _requiredValue;
        }

        /// <summary>
        /// Checks whether an achievement condition is met and unlocks if so.
        /// </summary>
        /// <param name="condition">The condition type being reported.</param>
        /// <param name="value">The current value to compare against the threshold.</param>
        public void CheckAchievement(AchievementCondition condition, int value)
        {
            foreach (var achievement in _allAchievements)
            {
                if (achievement == null) continue;
                if (_unlockedIds.Contains(achievement.Id)) continue;
                if (achievement.UnlockCondition != condition) continue;

                // Update progress tracking
                if (!_progressMap.ContainsKey(achievement.Id))
                {
                    _progressMap[achievement.Id] = 0;
                }
                _progressMap[achievement.Id] = Mathf.Max(_progressMap[achievement.Id], value);

                if (value >= achievement.RequiredValue)
                {
                    UnlockAchievement(achievement);
                }
            }
        }

        /// <summary>
        /// Unlocks a specific achievement and fires the unlock event.
        /// </summary>
        private void UnlockAchievement(AchievementData achievement)
        {
            if (_unlockedIds.Contains(achievement.Id)) return;

            _unlockedIds.Add(achievement.Id);
            _progressMap[achievement.Id] = achievement.RequiredValue;

            Debug.Log($"[AchievementManager] Achievement unlocked: {achievement.Name}");
            OnAchievementUnlocked?.Invoke(achievement);
            SaveAchievements();
        }

        /// <summary>
        /// Returns all currently unlocked achievements.
        /// </summary>
        public List<AchievementData> GetUnlockedAchievements()
        {
            return _allAchievements
                .Where(a => a != null && _unlockedIds.Contains(a.Id))
                .ToList();
        }

        /// <summary>
        /// Returns all registered achievements (including locked and hidden).
        /// </summary>
        public List<AchievementData> GetAllAchievements()
        {
            return new List<AchievementData>(_allAchievements);
        }

        /// <summary>
        /// Returns the progress value for a given achievement ID.
        /// </summary>
        /// <param name="achievementId">The achievement's unique identifier.</param>
        public int GetProgress(string achievementId)
        {
            return _progressMap.TryGetValue(achievementId, out int val) ? val : 0;
        }

        /// <summary>
        /// Checks whether a specific achievement has been unlocked.
        /// </summary>
        /// <param name="achievementId">The achievement's unique identifier.</param>
        public bool IsUnlocked(string achievementId)
        {
            return _unlockedIds.Contains(achievementId);
        }

        /// <summary>
        /// Persists the current achievement state to a JSON file.
        /// </summary>
        private void SaveAchievements()
        {
            var saveData = new AchievementSaveData
            {
                unlockedIds = new List<string>(_unlockedIds),
                progressMap = new Dictionary<string, int>(_progressMap)
            };

            // Dictionary is not directly serializable by JsonUtility; use a list of pairs
            var serializable = new SerializableAchievementSave
            {
                unlockedIds = saveData.unlockedIds,
                progressKeys = new List<string>(saveData.progressMap.Keys),
                progressValues = new List<int>(saveData.progressMap.Values)
            };

            string json = JsonUtility.ToJson(serializable, true);
            File.WriteAllText(_savePath, json);
        }

        /// <summary>
        /// Loads achievement state from the JSON save file.
        /// </summary>
        private void LoadAchievements()
        {
            if (!File.Exists(_savePath)) return;

            try
            {
                string json = File.ReadAllText(_savePath);
                var serializable = JsonUtility.FromJson<SerializableAchievementSave>(json);

                if (serializable != null)
                {
                    _unlockedIds = new HashSet<string>(serializable.unlockedIds);
                    _progressMap = new Dictionary<string, int>();

                    int count = Mathf.Min(serializable.progressKeys.Count,
                                          serializable.progressValues.Count);
                    for (int i = 0; i < count; i++)
                    {
                        _progressMap[serializable.progressKeys[i]] = serializable.progressValues[i];
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AchievementManager] Failed to load achievements: {e.Message}");
            }
        }

        /// <summary>
        /// Serializable save format that avoids Dictionary (unsupported by JsonUtility).
        /// </summary>
        [Serializable]
        private class SerializableAchievementSave
        {
            public List<string> unlockedIds = new List<string>();
            public List<string> progressKeys = new List<string>();
            public List<int> progressValues = new List<int>();
        }
    }
}
