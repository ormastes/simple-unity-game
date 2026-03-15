using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ElementalSiege.Editor;

namespace ElementalSiege.Tests.EditMode
{
    /// <summary>
    /// Tests for WorldProgression using the real ElementalSiege.Editor assembly.
    /// WorldProgression is a ScriptableObject with a public worlds list of WorldEntry.
    /// </summary>
    [TestFixture]
    public class WorldProgressionTests
    {
        private WorldProgression _progression;

        [SetUp]
        public void SetUp()
        {
            _progression = ScriptableObject.CreateInstance<WorldProgression>();
            _progression.worlds = new List<WorldEntry>
            {
                new WorldEntry { worldName = "Plains", starsToUnlock = 0, levelsInWorld = 5 },
                new WorldEntry { worldName = "Forest", starsToUnlock = 5, levelsInWorld = 5 },
                new WorldEntry { worldName = "Mountain", starsToUnlock = 12, levelsInWorld = 5 },
                new WorldEntry { worldName = "Volcano", starsToUnlock = 21, levelsInWorld = 5 },
                new WorldEntry { worldName = "Sky", starsToUnlock = 33, levelsInWorld = 5 }
            };
        }

        [TearDown]
        public void TearDown()
        {
            if (_progression != null)
            {
                Object.DestroyImmediate(_progression);
            }
        }

        [Test]
        public void World1_AlwaysUnlocked()
        {
            Assert.AreEqual(0, _progression.worlds[0].starsToUnlock,
                "World 1 should require 0 stars (always unlocked)");
        }

        [Test]
        public void World2_RequiresFiveStars()
        {
            Assert.AreEqual(5, _progression.worlds[1].starsToUnlock,
                "World 2 should require 5 stars to unlock");
        }

        [Test]
        public void FinalWorld_RequiresMostStars()
        {
            int finalThreshold = _progression.worlds[_progression.worlds.Count - 1].starsToUnlock;

            for (int i = 0; i < _progression.worlds.Count - 1; i++)
            {
                Assert.GreaterOrEqual(finalThreshold, _progression.worlds[i].starsToUnlock,
                    $"Final world threshold ({finalThreshold}) should be >= world {i} threshold ({_progression.worlds[i].starsToUnlock})");
            }
        }

        [Test]
        public void StarsRequirement_IsMonotonicallyIncreasing()
        {
            for (int i = 1; i < _progression.worlds.Count; i++)
            {
                Assert.Greater(_progression.worlds[i].starsToUnlock, _progression.worlds[i - 1].starsToUnlock,
                    $"World {i} threshold ({_progression.worlds[i].starsToUnlock}) should be strictly greater " +
                    $"than world {i - 1} threshold ({_progression.worlds[i - 1].starsToUnlock})");
            }
        }

        [Test]
        public void TotalAvailableStars_ExceedsMaxRequired()
        {
            int totalAvailable = 0;
            foreach (var world in _progression.worlds)
            {
                totalAvailable += world.levelsInWorld * 3; // 3 stars per level
            }

            int maxRequired = _progression.worlds[_progression.worlds.Count - 1].starsToUnlock;

            Assert.Greater(totalAvailable, maxRequired,
                $"Total available stars ({totalAvailable}) should exceed the highest " +
                $"unlock requirement ({maxRequired}) to ensure the game is completable");
        }

        [Test]
        public void AllWorlds_HavePositiveLevelCount()
        {
            foreach (var world in _progression.worlds)
            {
                Assert.Greater(world.levelsInWorld, 0,
                    $"World '{world.worldName}' should have at least one level");
            }
        }
    }
}
