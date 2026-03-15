using NUnit.Framework;

namespace ElementalSiege.Tests.EditMode
{
    [TestFixture]
    public class ScoreManagerTests
    {
        private ScoreManager _scoreManager;

        [SetUp]
        public void SetUp()
        {
            _scoreManager = new ScoreManager();
        }

        [Test]
        public void CalculatesCorrectStars_OneStarMinimum()
        {
            // Minimal destruction, all orbs used — should still get at least 1 star
            int stars = _scoreManager.CalculateStars(
                destructionPercent: 0.34f,
                orbsUsed: 5,
                totalOrbs: 5
            );

            Assert.AreEqual(1, stars, "Minimum destruction should yield exactly 1 star");
        }

        [Test]
        public void CalculatesCorrectStars_ThreeStars_AllOrbsRemaining()
        {
            // Full destruction with orbs to spare — perfect 3-star run
            int stars = _scoreManager.CalculateStars(
                destructionPercent: 1.0f,
                orbsUsed: 1,
                totalOrbs: 5
            );

            Assert.AreEqual(3, stars, "Full destruction with many orbs remaining should yield 3 stars");
        }

        [Test]
        public void CalculatesCorrectStars_TwoStars_PartialDestruction()
        {
            // Moderate destruction — should get 2 stars
            int stars = _scoreManager.CalculateStars(
                destructionPercent: 0.75f,
                orbsUsed: 3,
                totalOrbs: 5
            );

            Assert.AreEqual(2, stars, "Partial destruction should yield 2 stars");
        }

        [Test]
        public void ScoreIncreasesWithHigherDestruction()
        {
            int scoreLow = _scoreManager.CalculateScore(
                destructionPercent: 0.25f,
                orbsUsed: 3,
                totalOrbs: 5
            );

            int scoreHigh = _scoreManager.CalculateScore(
                destructionPercent: 0.90f,
                orbsUsed: 3,
                totalOrbs: 5
            );

            Assert.Greater(scoreHigh, scoreLow,
                "Higher destruction percentage should produce a higher score");
        }

        [Test]
        public void ScoreIncreasesWithFewerOrbsUsed()
        {
            int scoreManyOrbs = _scoreManager.CalculateScore(
                destructionPercent: 0.80f,
                orbsUsed: 5,
                totalOrbs: 5
            );

            int scoreFewOrbs = _scoreManager.CalculateScore(
                destructionPercent: 0.80f,
                orbsUsed: 1,
                totalOrbs: 5
            );

            Assert.Greater(scoreFewOrbs, scoreManyOrbs,
                "Using fewer orbs should produce a higher score");
        }

        [Test]
        public void ZeroOrbsUsed_StillCalculatesScore()
        {
            // Edge case: zero orbs used (e.g., chain-reaction level)
            int score = _scoreManager.CalculateScore(
                destructionPercent: 1.0f,
                orbsUsed: 0,
                totalOrbs: 5
            );

            Assert.Greater(score, 0, "Score should still be positive even with zero orbs used");
        }
    }

    /// <summary>
    /// Stub ScoreManager for test compilation.
    /// Replace with actual game class reference once production code is available.
    /// </summary>
    public class ScoreManager
    {
        private const int BaseScore = 1000;
        private const float OrbBonusWeight = 0.3f;

        public int CalculateStars(float destructionPercent, int orbsUsed, int totalOrbs)
        {
            float orbBonus = totalOrbs > 0 ? (float)(totalOrbs - orbsUsed) / totalOrbs : 0f;
            float combined = destructionPercent * 0.7f + orbBonus * 0.3f;

            if (combined >= 0.85f) return 3;
            if (combined >= 0.55f) return 2;
            return 1;
        }

        public int CalculateScore(float destructionPercent, int orbsUsed, int totalOrbs)
        {
            float orbBonus = totalOrbs > 0 ? (float)(totalOrbs - orbsUsed) / totalOrbs : 1f;
            return (int)(BaseScore * destructionPercent + BaseScore * OrbBonusWeight * orbBonus);
        }
    }
}
