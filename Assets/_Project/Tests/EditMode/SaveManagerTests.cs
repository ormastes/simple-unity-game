using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace ElementalSiege.Tests.EditMode
{
    [TestFixture]
    public class SaveManagerTests
    {
        private SaveManager _saveManager;
        private string _testSavePath;

        [SetUp]
        public void SetUp()
        {
            _testSavePath = Path.Combine(Application.temporaryCachePath, "test_save.json");
            _saveManager = new SaveManager(_testSavePath);
            _saveManager.ResetProgress();
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_testSavePath))
            {
                File.Delete(_testSavePath);
            }
        }

        [Test]
        public void SaveAndLoadProgress_RoundTrips()
        {
            _saveManager.SetLevelComplete("world1_level3", 2, 450);
            _saveManager.Save();

            var loadedManager = new SaveManager(_testSavePath);
            loadedManager.Load();

            LevelSaveData data = loadedManager.GetLevelData("world1_level3");
            Assert.AreEqual(2, data.Stars);
            Assert.AreEqual(450, data.HighScore);
        }

        [Test]
        public void SetLevelComplete_UpdatesStars_OnlyIfHigher()
        {
            _saveManager.SetLevelComplete("world1_level1", 3, 800);
            _saveManager.SetLevelComplete("world1_level1", 1, 200);

            LevelSaveData data = _saveManager.GetLevelData("world1_level1");
            Assert.AreEqual(3, data.Stars, "Stars should not decrease when a lower value is set");
            Assert.AreEqual(800, data.HighScore, "High score should not decrease");
        }

        [Test]
        public void GetLevelData_ReturnsDefault_WhenNotSaved()
        {
            LevelSaveData data = _saveManager.GetLevelData("nonexistent_level");

            Assert.AreEqual(0, data.Stars);
            Assert.AreEqual(0, data.HighScore);
            Assert.IsFalse(data.IsCompleted);
        }

        [Test]
        public void IsWorldUnlocked_ChecksStarThreshold()
        {
            // World 1 is always unlocked
            Assert.IsTrue(_saveManager.IsWorldUnlocked(0),
                "World 1 (index 0) should always be unlocked");

            // World 2 requires 5 stars
            Assert.IsFalse(_saveManager.IsWorldUnlocked(1),
                "World 2 should be locked with zero stars");

            // Earn enough stars
            _saveManager.SetLevelComplete("world1_level1", 3, 900);
            _saveManager.SetLevelComplete("world1_level2", 2, 500);

            Assert.IsTrue(_saveManager.IsWorldUnlocked(1),
                "World 2 should be unlocked once 5+ stars are earned");
        }

        [Test]
        public void ResetProgress_ClearsAllData()
        {
            _saveManager.SetLevelComplete("world1_level1", 3, 999);
            _saveManager.SetLevelComplete("world2_level1", 2, 500);

            _saveManager.ResetProgress();

            LevelSaveData data = _saveManager.GetLevelData("world1_level1");
            Assert.AreEqual(0, data.Stars, "Stars should be zero after reset");
            Assert.AreEqual(0, data.HighScore, "High score should be zero after reset");
        }

        [Test]
        public void SavePersistsToFile()
        {
            _saveManager.SetLevelComplete("world1_level1", 2, 350);
            _saveManager.Save();

            Assert.IsTrue(File.Exists(_testSavePath),
                "Save file should exist on disk after saving");

            string contents = File.ReadAllText(_testSavePath);
            Assert.IsTrue(contents.Length > 0,
                "Save file should not be empty");
        }
    }

    /// <summary>
    /// Stub save data structure for test compilation.
    /// </summary>
    public struct LevelSaveData
    {
        public int Stars;
        public int HighScore;
        public bool IsCompleted;

        public static LevelSaveData Default => new LevelSaveData
        {
            Stars = 0,
            HighScore = 0,
            IsCompleted = false
        };
    }

    /// <summary>
    /// Stub SaveManager for test compilation.
    /// Replace with actual game class reference once production code is available.
    /// </summary>
    public class SaveManager
    {
        private readonly string _savePath;
        private System.Collections.Generic.Dictionary<string, LevelSaveData> _data;
        private static readonly int[] WorldStarThresholds = { 0, 5, 12, 21, 33 };

        public SaveManager(string savePath)
        {
            _savePath = savePath;
            _data = new System.Collections.Generic.Dictionary<string, LevelSaveData>();
        }

        public void SetLevelComplete(string levelId, int stars, int score)
        {
            if (_data.TryGetValue(levelId, out var existing))
            {
                _data[levelId] = new LevelSaveData
                {
                    Stars = Mathf.Max(existing.Stars, stars),
                    HighScore = Mathf.Max(existing.HighScore, score),
                    IsCompleted = true
                };
            }
            else
            {
                _data[levelId] = new LevelSaveData
                {
                    Stars = stars,
                    HighScore = score,
                    IsCompleted = true
                };
            }
        }

        public LevelSaveData GetLevelData(string levelId)
        {
            return _data.TryGetValue(levelId, out var data) ? data : LevelSaveData.Default;
        }

        public int GetTotalStars()
        {
            int total = 0;
            foreach (var kvp in _data)
            {
                total += kvp.Value.Stars;
            }
            return total;
        }

        public bool IsWorldUnlocked(int worldIndex)
        {
            if (worldIndex < 0 || worldIndex >= WorldStarThresholds.Length)
                return false;
            return GetTotalStars() >= WorldStarThresholds[worldIndex];
        }

        public void ResetProgress()
        {
            _data.Clear();
        }

        public void Save()
        {
            string json = JsonUtility.ToJson(new SerializableData(_data), true);
            File.WriteAllText(_savePath, json);
        }

        public void Load()
        {
            if (!File.Exists(_savePath)) return;
            string json = File.ReadAllText(_savePath);
            var loaded = JsonUtility.FromJson<SerializableData>(json);
            _data = loaded.ToDictionary();
        }

        [System.Serializable]
        private class SerializableData
        {
            public System.Collections.Generic.List<string> Keys = new System.Collections.Generic.List<string>();
            public System.Collections.Generic.List<LevelSaveData> Values = new System.Collections.Generic.List<LevelSaveData>();

            public SerializableData() { }

            public SerializableData(System.Collections.Generic.Dictionary<string, LevelSaveData> dict)
            {
                foreach (var kvp in dict)
                {
                    Keys.Add(kvp.Key);
                    Values.Add(kvp.Value);
                }
            }

            public System.Collections.Generic.Dictionary<string, LevelSaveData> ToDictionary()
            {
                var dict = new System.Collections.Generic.Dictionary<string, LevelSaveData>();
                for (int i = 0; i < Keys.Count; i++)
                {
                    dict[Keys[i]] = Values[i];
                }
                return dict;
            }
        }
    }
}
