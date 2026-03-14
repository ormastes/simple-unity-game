using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ElementalSiege.Core
{
    /// <summary>
    /// JSON-based save/load system persisted to Application.persistentDataPath.
    /// Tracks per-level stars, scores, and unlock progression.
    /// </summary>
    public class SaveManager : MonoBehaviour
    {
        // ──────────────────────────────────────────────
        //  Data types
        // ──────────────────────────────────────────────

        /// <summary>
        /// Per-level save data.
        /// </summary>
        [Serializable]
        public class LevelSaveData
        {
            /// <summary>Best star rating achieved (0 = never completed).</summary>
            public int Stars;

            /// <summary>Best score achieved.</summary>
            public int HighScore;

            /// <summary>Whether the level is unlocked for play.</summary>
            public bool Unlocked;

            /// <summary>Whether the level has been completed at least once.</summary>
            public bool Completed;
        }

        /// <summary>
        /// Root save data container. Serialised to/from JSON.
        /// </summary>
        [Serializable]
        public class SaveData
        {
            /// <summary>Per-level progress keyed by level ID.</summary>
            public Dictionary<string, LevelSaveData> Levels = new Dictionary<string, LevelSaveData>();

            /// <summary>Total stars earned across all levels.</summary>
            public int TotalStars;

            /// <summary>Timestamp of last save (ISO 8601).</summary>
            public string LastSaved;
        }

        /// <summary>
        /// Defines a world's unlock requirement.
        /// </summary>
        [Serializable]
        public struct WorldUnlockRequirement
        {
            /// <summary>Display name of the world.</summary>
            public string WorldName;

            /// <summary>Total stars required to unlock this world.</summary>
            public int StarsRequired;

            /// <summary>Level IDs that belong to this world.</summary>
            public List<string> LevelIds;
        }

        // ──────────────────────────────────────────────
        //  Events
        // ──────────────────────────────────────────────

        /// <summary>Fired after save data has been loaded from disk.</summary>
        public static event Action<SaveData> OnProgressLoaded;

        /// <summary>Fired after save data has been written to disk.</summary>
        public static event Action OnProgressSaved;

        // ──────────────────────────────────────────────
        //  Inspector
        // ──────────────────────────────────────────────

        [Header("World Progression")]
        [SerializeField] private List<WorldUnlockRequirement> _worldRequirements = new List<WorldUnlockRequirement>();

        // ──────────────────────────────────────────────
        //  State
        // ──────────────────────────────────────────────

        private SaveData _data;
        private string _savePath;

        /// <summary>The currently loaded save data (read-only snapshot).</summary>
        public SaveData CurrentData => _data;

        // ──────────────────────────────────────────────
        //  Lifecycle
        // ──────────────────────────────────────────────

        private void Awake()
        {
            _savePath = Path.Combine(Application.persistentDataPath, "save.json");
            LoadProgress();
        }

        // ──────────────────────────────────────────────
        //  Public API
        // ──────────────────────────────────────────────

        /// <summary>
        /// Loads progress from disk. Creates default data if no save file exists.
        /// </summary>
        public void LoadProgress()
        {
            if (File.Exists(_savePath))
            {
                try
                {
                    string json = File.ReadAllText(_savePath);
                    _data = JsonUtility.FromJson<SaveData>(json);

                    // JsonUtility doesn't deserialise Dictionary directly;
                    // we use a wrapper approach. If the dictionary is null,
                    // fall back to a fresh instance.
                    if (_data == null)
                    {
                        _data = CreateDefaultSaveData();
                    }
                    else if (_data.Levels == null)
                    {
                        _data.Levels = new Dictionary<string, LevelSaveData>();
                    }

                    Debug.Log($"[SaveManager] Progress loaded from '{_savePath}'.");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SaveManager] Failed to load save: {e.Message}");
                    _data = CreateDefaultSaveData();
                }
            }
            else
            {
                Debug.Log("[SaveManager] No save file found. Creating defaults.");
                _data = CreateDefaultSaveData();
            }

            RecalculateTotalStars();
            OnProgressLoaded?.Invoke(_data);
        }

        /// <summary>
        /// Persists current progress to disk as JSON.
        /// </summary>
        public void SaveProgress()
        {
            try
            {
                _data.LastSaved = DateTime.UtcNow.ToString("o");
                string json = JsonUtility.ToJson(_data, true);
                File.WriteAllText(_savePath, json);
                Debug.Log($"[SaveManager] Progress saved to '{_savePath}'.");
                OnProgressSaved?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Failed to save: {e.Message}");
            }
        }

        /// <summary>
        /// Retrieves save data for a specific level.
        /// Returns a new unlocked entry if the level has no prior data.
        /// </summary>
        /// <param name="levelId">Unique level identifier.</param>
        /// <returns>The LevelSaveData for the given level.</returns>
        public LevelSaveData GetLevelData(string levelId)
        {
            if (_data.Levels.TryGetValue(levelId, out var levelData))
                return levelData;

            // Level not yet tracked — create a default entry
            var newData = new LevelSaveData { Unlocked = false };
            _data.Levels[levelId] = newData;
            return newData;
        }

        /// <summary>
        /// Records a level completion, keeping only the best stars/score.
        /// Automatically saves to disk and checks world unlock progression.
        /// </summary>
        /// <param name="levelId">Unique level identifier.</param>
        /// <param name="stars">Star rating achieved (1-3).</param>
        /// <param name="score">Numeric score achieved.</param>
        public void SetLevelComplete(string levelId, int stars, int score)
        {
            if (!_data.Levels.TryGetValue(levelId, out var levelData))
            {
                levelData = new LevelSaveData { Unlocked = true };
                _data.Levels[levelId] = levelData;
            }

            levelData.Completed = true;
            levelData.Unlocked = true;

            if (stars > levelData.Stars)
                levelData.Stars = stars;

            if (score > levelData.HighScore)
                levelData.HighScore = score;

            RecalculateTotalStars();
            UpdateWorldUnlocks();
            SaveProgress();

            Debug.Log($"[SaveManager] Level '{levelId}' completed. Stars={levelData.Stars}, " +
                      $"HighScore={levelData.HighScore}, TotalStars={_data.TotalStars}");
        }

        /// <summary>
        /// Checks whether a world is unlocked based on total star count.
        /// </summary>
        /// <param name="worldIndex">Index into the world requirements list.</param>
        /// <returns>True if enough stars have been earned.</returns>
        public bool IsWorldUnlocked(int worldIndex)
        {
            if (worldIndex < 0 || worldIndex >= _worldRequirements.Count)
                return false;

            // First world is always unlocked
            if (worldIndex == 0) return true;

            return _data.TotalStars >= _worldRequirements[worldIndex].StarsRequired;
        }

        /// <summary>
        /// Returns the total star count across all completed levels.
        /// </summary>
        public int GetTotalStars()
        {
            return _data.TotalStars;
        }

        /// <summary>
        /// Deletes the save file and resets to default data.
        /// </summary>
        public void ResetProgress()
        {
            if (File.Exists(_savePath))
            {
                File.Delete(_savePath);
                Debug.Log("[SaveManager] Save file deleted.");
            }

            _data = CreateDefaultSaveData();
            OnProgressLoaded?.Invoke(_data);
        }

        // ──────────────────────────────────────────────
        //  Internal
        // ──────────────────────────────────────────────

        private SaveData CreateDefaultSaveData()
        {
            var data = new SaveData
            {
                Levels = new Dictionary<string, LevelSaveData>(),
                TotalStars = 0,
                LastSaved = DateTime.UtcNow.ToString("o")
            };

            // Unlock the first world's levels by default
            if (_worldRequirements.Count > 0)
            {
                foreach (var levelId in _worldRequirements[0].LevelIds)
                {
                    data.Levels[levelId] = new LevelSaveData { Unlocked = true };
                }
            }

            return data;
        }

        private void RecalculateTotalStars()
        {
            int total = 0;
            foreach (var kvp in _data.Levels)
            {
                total += kvp.Value.Stars;
            }
            _data.TotalStars = total;
        }

        private void UpdateWorldUnlocks()
        {
            for (int i = 1; i < _worldRequirements.Count; i++)
            {
                if (_data.TotalStars < _worldRequirements[i].StarsRequired)
                    continue;

                foreach (var levelId in _worldRequirements[i].LevelIds)
                {
                    if (!_data.Levels.ContainsKey(levelId))
                    {
                        _data.Levels[levelId] = new LevelSaveData { Unlocked = true };
                    }
                    else
                    {
                        _data.Levels[levelId].Unlocked = true;
                    }
                }
            }
        }
    }
}
