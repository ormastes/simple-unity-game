using NUnit.Framework;

namespace ElementalSiege.Tests.EditMode
{
    [TestFixture]
    public class WorldProgressionTests
    {
        private WorldProgression _progression;

        [SetUp]
        public void SetUp()
        {
            _progression = new WorldProgression();
        }

        [Test]
        public void World1_AlwaysUnlocked()
        {
            bool unlocked = _progression.IsWorldUnlocked(worldIndex: 0, totalStars: 0);

            Assert.IsTrue(unlocked,
                "World 1 (index 0) should always be unlocked regardless of star count");
        }

        [Test]
        public void World2_RequiresFiveStars()
        {
            Assert.IsFalse(_progression.IsWorldUnlocked(worldIndex: 1, totalStars: 4),
                "World 2 should be locked with only 4 stars");

            Assert.IsTrue(_progression.IsWorldUnlocked(worldIndex: 1, totalStars: 5),
                "World 2 should be unlocked with exactly 5 stars");

            Assert.IsTrue(_progression.IsWorldUnlocked(worldIndex: 1, totalStars: 10),
                "World 2 should be unlocked with more than 5 stars");
        }

        [Test]
        public void FinalWorld_RequiresMostStars()
        {
            int[] thresholds = _progression.GetAllThresholds();
            int finalThreshold = thresholds[thresholds.Length - 1];

            for (int i = 0; i < thresholds.Length - 1; i++)
            {
                Assert.GreaterOrEqual(finalThreshold, thresholds[i],
                    $"Final world threshold ({finalThreshold}) should be >= world {i} threshold ({thresholds[i]})");
            }
        }

        [Test]
        public void StarsRequirement_IsMonotonicallyIncreasing()
        {
            int[] thresholds = _progression.GetAllThresholds();

            for (int i = 1; i < thresholds.Length; i++)
            {
                Assert.Greater(thresholds[i], thresholds[i - 1],
                    $"World {i} threshold ({thresholds[i]}) should be strictly greater " +
                    $"than world {i - 1} threshold ({thresholds[i - 1]})");
            }
        }

        [Test]
        public void TotalAvailableStars_ExceedsMaxRequired()
        {
            int totalAvailable = _progression.GetTotalAvailableStars();
            int[] thresholds = _progression.GetAllThresholds();
            int maxRequired = thresholds[thresholds.Length - 1];

            Assert.Greater(totalAvailable, maxRequired,
                $"Total available stars ({totalAvailable}) should exceed the highest " +
                $"unlock requirement ({maxRequired}) to ensure the game is completable");
        }
    }

    /// <summary>
    /// Stub WorldProgression for test compilation.
    /// </summary>
    public class WorldProgression
    {
        // Star thresholds required to unlock each world (index 0 = world 1)
        private static readonly int[] StarThresholds = { 0, 5, 12, 21, 33 };

        // Total levels across all worlds, each awarding up to 3 stars
        private const int TotalLevels = 15;
        private const int MaxStarsPerLevel = 3;

        public bool IsWorldUnlocked(int worldIndex, int totalStars)
        {
            if (worldIndex < 0 || worldIndex >= StarThresholds.Length)
                return false;
            return totalStars >= StarThresholds[worldIndex];
        }

        public int[] GetAllThresholds()
        {
            return (int[])StarThresholds.Clone();
        }

        public int GetTotalAvailableStars()
        {
            return TotalLevels * MaxStarsPerLevel;
        }
    }
}
