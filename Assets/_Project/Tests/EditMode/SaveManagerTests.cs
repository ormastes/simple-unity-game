using System.IO;
using NUnit.Framework;
using UnityEngine;
using ElementalSiege.Core;

namespace ElementalSiege.Tests.EditMode
{
    /// <summary>
    /// Tests for SaveManager using the real ElementalSiege.Core assembly.
    /// SaveManager is a MonoBehaviour, so we use AddComponent and let Awake initialize it.
    /// </summary>
    [TestFixture]
    public class SaveManagerTests
    {
        private GameObject _saveManagerObject;
        private SaveManager _saveManager;

        [SetUp]
        public void SetUp()
        {
            _saveManagerObject = new GameObject("TestSaveManager");
            _saveManager = _saveManagerObject.AddComponent<SaveManager>();
            // Awake() runs automatically, initializing _savePath and loading progress.
            // Call ResetProgress to start clean for each test.
            _saveManager.ResetProgress();
        }

        [TearDown]
        public void TearDown()
        {
            if (_saveManager != null)
            {
                _saveManager.ResetProgress();
            }
            if (_saveManagerObject != null)
            {
                Object.DestroyImmediate(_saveManagerObject);
            }
        }

        [Test]
        public void SetLevelComplete_UpdatesStars_OnlyIfHigher()
        {
            _saveManager.SetLevelComplete("world1_level1", 3, 800);
            _saveManager.SetLevelComplete("world1_level1", 1, 200);

            SaveManager.LevelSaveData data = _saveManager.GetLevelData("world1_level1");
            Assert.AreEqual(3, data.Stars, "Stars should not decrease when a lower value is set");
            Assert.AreEqual(800, data.HighScore, "High score should not decrease");
        }

        [Test]
        public void GetLevelData_ReturnsDefault_WhenNotSaved()
        {
            SaveManager.LevelSaveData data = _saveManager.GetLevelData("nonexistent_level");

            Assert.AreEqual(0, data.Stars);
            Assert.AreEqual(0, data.HighScore);
            Assert.IsFalse(data.Completed);
        }

        [Test]
        public void SetLevelComplete_MarksLevelCompleted()
        {
            _saveManager.SetLevelComplete("world1_level1", 2, 500);

            SaveManager.LevelSaveData data = _saveManager.GetLevelData("world1_level1");
            Assert.IsTrue(data.Completed, "Level should be marked as completed");
            Assert.IsTrue(data.Unlocked, "Completed level should be unlocked");
        }

        [Test]
        public void GetTotalStars_SumsAllLevelStars()
        {
            _saveManager.SetLevelComplete("world1_level1", 3, 900);
            _saveManager.SetLevelComplete("world1_level2", 2, 500);

            int totalStars = _saveManager.GetTotalStars();
            Assert.AreEqual(5, totalStars,
                "Total stars should be the sum of all level stars");
        }

        [Test]
        public void ResetProgress_ClearsAllData()
        {
            _saveManager.SetLevelComplete("world1_level1", 3, 999);
            _saveManager.SetLevelComplete("world2_level1", 2, 500);

            _saveManager.ResetProgress();

            SaveManager.LevelSaveData data = _saveManager.GetLevelData("world1_level1");
            Assert.AreEqual(0, data.Stars, "Stars should be zero after reset");
            Assert.AreEqual(0, data.HighScore, "High score should be zero after reset");
        }

        [Test]
        public void CurrentData_IsNotNull_AfterInit()
        {
            Assert.IsNotNull(_saveManager.CurrentData,
                "CurrentData should not be null after initialization");
            Assert.IsNotNull(_saveManager.CurrentData.Levels,
                "Levels dictionary should not be null");
        }

        [Test]
        public void IsWorldUnlocked_FirstWorld_AlwaysTrue()
        {
            Assert.IsTrue(_saveManager.IsWorldUnlocked(0),
                "World 1 (index 0) should always be unlocked");
        }
    }
}
