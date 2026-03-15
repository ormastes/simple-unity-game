using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using ElementalSiege.Core;
using ElementalSiege.Elements;

namespace ElementalSiege.Tests.EditMode
{
    /// <summary>
    /// Tests for CoreLevelData using the real ElementalSiege.Core assembly.
    /// CoreLevelData is a ScriptableObject with private serialized fields,
    /// so we use reflection to set values after CreateInstance.
    /// </summary>
    [TestFixture]
    public class CoreLevelDataTests
    {
        private CoreLevelData[] _testLevels;

        /// <summary>
        /// Helper to create a CoreLevelData instance with specified values via reflection.
        /// </summary>
        private static CoreLevelData CreateLevelData(
            string levelId, int worldIndex, Difficulty difficulty,
            ElementCategory[] availableOrbs, int[] starThresholds)
        {
            var level = ScriptableObject.CreateInstance<CoreLevelData>();
            var type = typeof(CoreLevelData);
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;

            type.GetField("_levelId", flags)?.SetValue(level, levelId);
            type.GetField("_worldIndex", flags)?.SetValue(level, worldIndex);
            type.GetField("_difficulty", flags)?.SetValue(level, difficulty);
            type.GetField("_availableOrbs", flags)?.SetValue(level, availableOrbs);
            type.GetField("_starThresholds", flags)?.SetValue(level, starThresholds);

            return level;
        }

        [SetUp]
        public void SetUp()
        {
            _testLevels = new[]
            {
                CreateLevelData(
                    "world1_level1", 0, Difficulty.Easy,
                    new[] { ElementCategory.Fire, ElementCategory.Stone },
                    new[] { 100, 300, 500 }),
                CreateLevelData(
                    "world1_level2", 0, Difficulty.Easy,
                    new[] { ElementCategory.Fire, ElementCategory.Ice },
                    new[] { 150, 400, 650 }),
                CreateLevelData(
                    "world2_level1", 1, Difficulty.Medium,
                    new[] { ElementCategory.Ice, ElementCategory.Wind, ElementCategory.Stone },
                    new[] { 200, 500, 800 }),
                CreateLevelData(
                    "world3_level1", 2, Difficulty.Hard,
                    new[] { ElementCategory.Lightning, ElementCategory.Fire },
                    new[] { 300, 600, 1000 })
            };
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var level in _testLevels)
            {
                Object.DestroyImmediate(level);
            }
        }

        [Test]
        public void StarThresholds_AreInAscendingOrder()
        {
            foreach (var level in _testLevels)
            {
                for (int i = 1; i < level.StarThresholds.Length; i++)
                {
                    Assert.Greater(level.StarThresholds[i], level.StarThresholds[i - 1],
                        $"Level '{level.LevelId}': star threshold [{i}] ({level.StarThresholds[i]}) " +
                        $"should be greater than [{i - 1}] ({level.StarThresholds[i - 1]})");
                }
            }
        }

        [Test]
        public void AvailableOrbs_NotEmpty()
        {
            foreach (var level in _testLevels)
            {
                Assert.IsNotNull(level.AvailableOrbs,
                    $"Level '{level.LevelId}' available orbs should not be null");
                Assert.Greater(level.AvailableOrbs.Length, 0,
                    $"Level '{level.LevelId}' should have at least one available orb type");
            }
        }

        [Test]
        public void LevelId_IsNotNullOrEmpty()
        {
            foreach (var level in _testLevels)
            {
                Assert.IsFalse(string.IsNullOrEmpty(level.LevelId),
                    "Every level must have a non-null, non-empty LevelId");
            }
        }

        [Test]
        public void WorldIndex_IsInValidRange()
        {
            int maxWorlds = 5;

            foreach (var level in _testLevels)
            {
                Assert.GreaterOrEqual(level.WorldIndex, 0,
                    $"Level '{level.LevelId}' WorldIndex should be >= 0");
                Assert.Less(level.WorldIndex, maxWorlds,
                    $"Level '{level.LevelId}' WorldIndex should be < {maxWorlds}");
            }
        }

        [Test]
        public void Difficulty_MatchesWorldProgression()
        {
            // Levels in higher worlds should generally have higher or equal difficulty
            for (int i = 1; i < _testLevels.Length; i++)
            {
                if (_testLevels[i].WorldIndex > _testLevels[i - 1].WorldIndex)
                {
                    Assert.GreaterOrEqual(_testLevels[i].LevelDifficulty, _testLevels[i - 1].LevelDifficulty,
                        $"Level '{_testLevels[i].LevelId}' (world {_testLevels[i].WorldIndex}) " +
                        $"should have difficulty >= level '{_testLevels[i - 1].LevelId}' " +
                        $"(world {_testLevels[i - 1].WorldIndex})");
                }
            }
        }

        [Test]
        public void CalculateStars_ReturnsCorrectStarCount()
        {
            var level = _testLevels[0]; // thresholds: 100, 300, 500

            Assert.AreEqual(0, level.CalculateStars(50),
                "Score below first threshold should yield 0 stars");
            Assert.AreEqual(1, level.CalculateStars(100),
                "Score at first threshold should yield 1 star");
            Assert.AreEqual(2, level.CalculateStars(300),
                "Score at second threshold should yield 2 stars");
            Assert.AreEqual(3, level.CalculateStars(500),
                "Score at third threshold should yield 3 stars");
        }

        [Test]
        public void HasElement_ReturnsCorrectResults()
        {
            var level = _testLevels[0]; // Fire, Stone

            Assert.IsTrue(level.HasElement(ElementCategory.Fire),
                "Level should have Fire element");
            Assert.IsTrue(level.HasElement(ElementCategory.Stone),
                "Level should have Stone element");
            Assert.IsFalse(level.HasElement(ElementCategory.Ice),
                "Level should not have Ice element");
        }
    }
}
