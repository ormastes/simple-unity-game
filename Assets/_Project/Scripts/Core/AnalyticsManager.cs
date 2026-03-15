using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ElementalSiege.Core
{
    /// <summary>
    /// Element categories tracked for orb usage analytics.
    /// </summary>
    public enum ElementCategory
    {
        Fire,
        Water,
        Earth,
        Air,
        Lightning,
        Ice,
        Nature,
        Shadow,
        Light,
        Void
    }

    /// <summary>
    /// Data for a single completed or failed level attempt.
    /// </summary>
    [Serializable]
    public class LevelAttemptRecord
    {
        public string levelId;
        public bool completed;
        public int stars;
        public int orbsUsed;
        public float duration;
        public string timestamp;
    }

    /// <summary>
    /// Aggregated session data across a play session.
    /// </summary>
    [Serializable]
    public class SessionData
    {
        /// <summary>Total play time in seconds for this session.</summary>
        public float playTime;

        /// <summary>Number of levels attempted this session.</summary>
        public int levelsAttempted;

        /// <summary>Number of levels successfully completed this session.</summary>
        public int levelsCompleted;

        /// <summary>Session start time as ISO 8601 string.</summary>
        public string sessionStart;
    }

    /// <summary>
    /// Persistent analytics data stored across sessions.
    /// </summary>
    [Serializable]
    public class AnalyticsData
    {
        public float totalPlayTime;
        public int totalLevelsAttempted;
        public int totalLevelsCompleted;
        public List<LevelAttemptRecord> levelAttempts = new List<LevelAttemptRecord>();
        public List<string> elementUsageKeys = new List<string>();
        public List<int> elementUsageValues = new List<int>();
        public List<string> comboNames = new List<string>();
        public List<int> comboCounts = new List<int>();
        public int totalSessions;
    }

    /// <summary>
    /// Local analytics manager that tracks gameplay metrics without external services.
    /// Persists aggregated statistics to JSON on the local filesystem.
    /// </summary>
    public class AnalyticsManager : MonoBehaviour
    {
        private static AnalyticsManager _instance;

        /// <summary>Global singleton accessor.</summary>
        public static AnalyticsManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[AnalyticsManager]");
                    _instance = go.AddComponent<AnalyticsManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private AnalyticsData _data;
        private SessionData _currentSession;
        private Dictionary<ElementCategory, int> _elementUsage = new Dictionary<ElementCategory, int>();
        private Dictionary<string, int> _comboUsage = new Dictionary<string, int>();
        private string _savePath;
        private float _sessionStartTime;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            _savePath = Path.Combine(Application.persistentDataPath, "analytics.json");
            LoadData();
            StartNewSession();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                UpdateSessionTime();
                SaveData();
            }
        }

        private void OnApplicationQuit()
        {
            UpdateSessionTime();
            SaveData();
        }

        /// <summary>
        /// Initializes a new play session and increments the session counter.
        /// </summary>
        private void StartNewSession()
        {
            _sessionStartTime = Time.realtimeSinceStartup;
            _currentSession = new SessionData
            {
                sessionStart = DateTime.UtcNow.ToString("o"),
                playTime = 0f,
                levelsAttempted = 0,
                levelsCompleted = 0
            };

            _data.totalSessions++;
        }

        /// <summary>
        /// Records the start of a level attempt.
        /// </summary>
        /// <param name="levelId">Unique identifier of the level.</param>
        public void TrackLevelStart(string levelId)
        {
            _currentSession.levelsAttempted++;
            _data.totalLevelsAttempted++;

            Debug.Log($"[Analytics] Level started: {levelId}");
        }

        /// <summary>
        /// Records a successful level completion with performance details.
        /// </summary>
        /// <param name="levelId">Unique identifier of the level.</param>
        /// <param name="stars">Stars earned (0-3).</param>
        /// <param name="orbsUsed">Number of orbs used.</param>
        /// <param name="time">Time taken to complete in seconds.</param>
        public void TrackLevelComplete(string levelId, int stars, int orbsUsed, float time)
        {
            _currentSession.levelsCompleted++;
            _data.totalLevelsCompleted++;

            var record = new LevelAttemptRecord
            {
                levelId = levelId,
                completed = true,
                stars = stars,
                orbsUsed = orbsUsed,
                duration = time,
                timestamp = DateTime.UtcNow.ToString("o")
            };

            _data.levelAttempts.Add(record);
            SaveData();

            Debug.Log($"[Analytics] Level completed: {levelId} | Stars: {stars} | Orbs: {orbsUsed} | Time: {time:F2}s");
        }

        /// <summary>
        /// Records a failed level attempt.
        /// </summary>
        /// <param name="levelId">Unique identifier of the level.</param>
        /// <param name="orbsUsed">Number of orbs used before failing.</param>
        public void TrackLevelFail(string levelId, int orbsUsed)
        {
            var record = new LevelAttemptRecord
            {
                levelId = levelId,
                completed = false,
                stars = 0,
                orbsUsed = orbsUsed,
                duration = 0f,
                timestamp = DateTime.UtcNow.ToString("o")
            };

            _data.levelAttempts.Add(record);
            SaveData();

            Debug.Log($"[Analytics] Level failed: {levelId} | Orbs used: {orbsUsed}");
        }

        /// <summary>
        /// Records usage of an elemental combo.
        /// </summary>
        /// <param name="comboName">Name of the combo triggered.</param>
        public void TrackElementCombo(string comboName)
        {
            if (!_comboUsage.ContainsKey(comboName))
            {
                _comboUsage[comboName] = 0;
            }
            _comboUsage[comboName]++;

            Debug.Log($"[Analytics] Combo triggered: {comboName} (total: {_comboUsage[comboName]})");
        }

        /// <summary>
        /// Records usage of a specific element type.
        /// </summary>
        /// <param name="element">The element category used.</param>
        public void TrackOrbUsage(ElementCategory element)
        {
            if (!_elementUsage.ContainsKey(element))
            {
                _elementUsage[element] = 0;
            }
            _elementUsage[element]++;
        }

        /// <summary>
        /// Returns the most frequently used element category.
        /// </summary>
        /// <returns>The most used element, or Fire as default if no data exists.</returns>
        public ElementCategory GetMostUsedElement()
        {
            if (_elementUsage.Count == 0) return ElementCategory.Fire;

            return _elementUsage.OrderByDescending(kv => kv.Value).First().Key;
        }

        /// <summary>
        /// Returns the level ID with the lowest completion rate (most failures relative to attempts).
        /// </summary>
        /// <returns>The level ID of the hardest level, or an empty string if no data exists.</returns>
        public string GetHardestLevel()
        {
            if (_data.levelAttempts.Count == 0) return string.Empty;

            var levelStats = new Dictionary<string, (int attempts, int completions)>();

            foreach (var attempt in _data.levelAttempts)
            {
                if (!levelStats.ContainsKey(attempt.levelId))
                {
                    levelStats[attempt.levelId] = (0, 0);
                }

                var stats = levelStats[attempt.levelId];
                stats.attempts++;
                if (attempt.completed) stats.completions++;
                levelStats[attempt.levelId] = stats;
            }

            // Hardest = lowest completion rate with at least 2 attempts
            string hardest = string.Empty;
            float lowestRate = float.MaxValue;

            foreach (var kv in levelStats)
            {
                if (kv.Value.attempts < 2) continue;

                float rate = (float)kv.Value.completions / kv.Value.attempts;
                if (rate < lowestRate)
                {
                    lowestRate = rate;
                    hardest = kv.Key;
                }
            }

            // If no level has 2+ attempts, return the one with most failures
            if (string.IsNullOrEmpty(hardest))
            {
                hardest = _data.levelAttempts
                    .Where(a => !a.completed)
                    .GroupBy(a => a.levelId)
                    .OrderByDescending(g => g.Count())
                    .Select(g => g.Key)
                    .FirstOrDefault() ?? string.Empty;
            }

            return hardest;
        }

        /// <summary>
        /// Returns the total play time across all sessions in seconds.
        /// </summary>
        public float GetPlayTime()
        {
            UpdateSessionTime();
            return _data.totalPlayTime;
        }

        /// <summary>
        /// Returns the current session data.
        /// </summary>
        public SessionData GetCurrentSession()
        {
            UpdateSessionTime();
            return _currentSession;
        }

        /// <summary>
        /// Returns the total number of play sessions.
        /// </summary>
        public int GetTotalSessions()
        {
            return _data.totalSessions;
        }

        /// <summary>
        /// Returns usage count for a specific element.
        /// </summary>
        public int GetElementUsageCount(ElementCategory element)
        {
            return _elementUsage.TryGetValue(element, out int count) ? count : 0;
        }

        /// <summary>
        /// Returns usage count for a specific combo.
        /// </summary>
        public int GetComboUsageCount(string comboName)
        {
            return _comboUsage.TryGetValue(comboName, out int count) ? count : 0;
        }

        /// <summary>
        /// Updates the session play time from the real-time clock.
        /// </summary>
        private void UpdateSessionTime()
        {
            float elapsed = Time.realtimeSinceStartup - _sessionStartTime;
            float delta = elapsed - _currentSession.playTime;
            _currentSession.playTime = elapsed;
            _data.totalPlayTime += delta;
        }

        /// <summary>
        /// Saves all analytics data to a JSON file.
        /// </summary>
        private void SaveData()
        {
            try
            {
                // Sync dictionaries to serializable lists
                _data.elementUsageKeys = _elementUsage.Keys.Select(k => k.ToString()).ToList();
                _data.elementUsageValues = _elementUsage.Values.ToList();
                _data.comboNames = _comboUsage.Keys.ToList();
                _data.comboCounts = _comboUsage.Values.ToList();

                string json = JsonUtility.ToJson(_data, true);
                File.WriteAllText(_savePath, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Analytics] Failed to save data: {e.Message}");
            }
        }

        /// <summary>
        /// Loads analytics data from the JSON file, or creates fresh data if none exists.
        /// </summary>
        private void LoadData()
        {
            if (File.Exists(_savePath))
            {
                try
                {
                    string json = File.ReadAllText(_savePath);
                    _data = JsonUtility.FromJson<AnalyticsData>(json);

                    // Restore dictionaries from serialized lists
                    _elementUsage = new Dictionary<ElementCategory, int>();
                    int elementCount = Mathf.Min(_data.elementUsageKeys.Count, _data.elementUsageValues.Count);
                    for (int i = 0; i < elementCount; i++)
                    {
                        if (Enum.TryParse<ElementCategory>(_data.elementUsageKeys[i], out var element))
                        {
                            _elementUsage[element] = _data.elementUsageValues[i];
                        }
                    }

                    _comboUsage = new Dictionary<string, int>();
                    int comboCount = Mathf.Min(_data.comboNames.Count, _data.comboCounts.Count);
                    for (int i = 0; i < comboCount; i++)
                    {
                        _comboUsage[_data.comboNames[i]] = _data.comboCounts[i];
                    }

                    Debug.Log("[Analytics] Data loaded successfully.");
                    return;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Analytics] Failed to load data, starting fresh: {e.Message}");
                }
            }

            _data = new AnalyticsData();
            _elementUsage = new Dictionary<ElementCategory, int>();
            _comboUsage = new Dictionary<string, int>();
        }
    }
}
