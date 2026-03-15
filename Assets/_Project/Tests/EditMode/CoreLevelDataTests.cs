using NUnit.Framework;

namespace ElementalSiege.Tests.EditMode
{
    [TestFixture]
    public class CoreLevelDataTests
    {
        private CoreLevelData[] _testLevels;

        [SetUp]
        public void SetUp()
        {
            _testLevels = new[]
            {
                new CoreLevelData
                {
                    LevelId = "world1_level1",
                    WorldIndex = 0,
                    Difficulty = 1,
                    AvailableOrbs = new[] { ElementCategory.Fire, ElementCategory.Stone },
                    StarThresholds = new[] { 100, 300, 500 }
                },
                new CoreLevelData
                {
                    LevelId = "world1_level2",
                    WorldIndex = 0,
                    Difficulty = 2,
                    AvailableOrbs = new[] { ElementCategory.Fire, ElementCategory.Ice },
                    StarThresholds = new[] { 150, 400, 650 }
                },
                new CoreLevelData
                {
                    LevelId = "world2_level1",
                    WorldIndex = 1,
                    Difficulty = 3,
                    AvailableOrbs = new[] { ElementCategory.Ice, ElementCategory.Wind, ElementCategory.Stone },
                    StarThresholds = new[] { 200, 500, 800 }
                },
                new CoreLevelData
                {
                    LevelId = "world3_level1",
                    WorldIndex = 2,
                    Difficulty = 5,
                    AvailableOrbs = new[] { ElementCategory.Lightning, ElementCategory.Fire },
                    StarThresholds = new[] { 300, 600, 1000 }
                }
            };
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
            // Levels in higher worlds should generally have higher difficulty
            for (int i = 1; i < _testLevels.Length; i++)
            {
                if (_testLevels[i].WorldIndex > _testLevels[i - 1].WorldIndex)
                {
                    Assert.GreaterOrEqual(_testLevels[i].Difficulty, _testLevels[i - 1].Difficulty,
                        $"Level '{_testLevels[i].LevelId}' (world {_testLevels[i].WorldIndex}) " +
                        $"should have difficulty >= level '{_testLevels[i - 1].LevelId}' " +
                        $"(world {_testLevels[i - 1].WorldIndex})");
                }
            }
        }
    }

    /// <summary>
    /// Stub level data structure for test compilation.
    /// </summary>
    public class CoreLevelData
    {
        public string LevelId;
        public int WorldIndex;
        public int Difficulty;
        public ElementCategory[] AvailableOrbs;
        public int[] StarThresholds;
    }
}
