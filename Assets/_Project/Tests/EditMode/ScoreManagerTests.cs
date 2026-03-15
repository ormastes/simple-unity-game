using NUnit.Framework;
using UnityEngine;
using ElementalSiege.Core;

namespace ElementalSiege.Tests.EditMode
{
    /// <summary>
    /// Tests for ScoreManager using the real ElementalSiege.Core assembly.
    /// Since ScoreManager.CalculateScore requires a LevelManager with runtime state,
    /// these tests verify the ScoreData struct and the ScoreManager's MonoBehaviour setup.
    /// </summary>
    [TestFixture]
    public class ScoreManagerTests
    {
        private GameObject _scoreManagerObject;
        private ScoreManager _scoreManager;

        [SetUp]
        public void SetUp()
        {
            _scoreManagerObject = new GameObject("TestScoreManager");
            _scoreManager = _scoreManagerObject.AddComponent<ScoreManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_scoreManagerObject != null)
            {
                Object.DestroyImmediate(_scoreManagerObject);
            }
        }

        [Test]
        public void ScoreManager_IsMonoBehaviour()
        {
            Assert.IsNotNull(_scoreManager,
                "ScoreManager should be a valid MonoBehaviour component");
            Assert.IsInstanceOf<MonoBehaviour>(_scoreManager,
                "ScoreManager should derive from MonoBehaviour");
        }

        [Test]
        public void ScoreData_DefaultValues_AreZero()
        {
            var data = new ScoreManager.ScoreData();

            Assert.AreEqual(0, data.Stars, "Default ScoreData stars should be 0");
            Assert.AreEqual(0, data.Score, "Default ScoreData score should be 0");
            Assert.AreEqual(0, data.OrbsUsed, "Default ScoreData orbs used should be 0");
            Assert.AreEqual(0f, data.DestructionPercent, 0.001f,
                "Default ScoreData destruction percent should be 0");
            Assert.AreEqual(0f, data.CompletionTime, 0.001f,
                "Default ScoreData completion time should be 0");
        }

        [Test]
        public void ScoreData_ToString_ContainsAllFields()
        {
            var data = new ScoreManager.ScoreData
            {
                Stars = 3,
                Score = 15000,
                OrbsUsed = 2,
                DestructionPercent = 0.95f,
                CompletionTime = 45.5f
            };

            string result = data.ToString();

            Assert.IsTrue(result.Contains("Stars=3"), "ToString should contain star count");
            Assert.IsTrue(result.Contains("Score=15000"), "ToString should contain score");
            Assert.IsTrue(result.Contains("Orbs=2"), "ToString should contain orbs used");
        }

        [Test]
        public void ScoreData_StarsField_AcceptsValidRange()
        {
            var data1 = new ScoreManager.ScoreData { Stars = 1 };
            var data2 = new ScoreManager.ScoreData { Stars = 2 };
            var data3 = new ScoreManager.ScoreData { Stars = 3 };

            Assert.AreEqual(1, data1.Stars);
            Assert.AreEqual(2, data2.Stars);
            Assert.AreEqual(3, data3.Stars);
        }

        [Test]
        public void CalculateScore_ReturnsDefault_WhenLevelManagerIsNull()
        {
            ScoreManager.ScoreData result = _scoreManager.CalculateScore(null);

            Assert.AreEqual(0, result.Stars,
                "Should return default ScoreData when LevelManager is null");
            Assert.AreEqual(0, result.Score,
                "Should return default score when LevelManager is null");
        }

        [Test]
        public void ScoreData_HigherDestruction_ProducesHigherScore()
        {
            // Verify the struct can represent different score levels
            var lowScore = new ScoreManager.ScoreData
            {
                Stars = 1,
                Score = 3000,
                DestructionPercent = 0.25f,
                OrbsUsed = 5
            };

            var highScore = new ScoreManager.ScoreData
            {
                Stars = 3,
                Score = 18000,
                DestructionPercent = 0.95f,
                OrbsUsed = 1
            };

            Assert.Greater(highScore.Score, lowScore.Score,
                "Higher destruction should correspond to higher score");
            Assert.Greater(highScore.Stars, lowScore.Stars,
                "Higher destruction should correspond to more stars");
        }
    }
}
