using System;
using UnityEngine;

namespace ElementalSiege.Core
{
    /// <summary>
    /// Calculates star ratings and scores for completed levels
    /// based on orbs remaining, destruction percentage, and time bonus.
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        // ──────────────────────────────────────────────
        //  Data types
        // ──────────────────────────────────────────────

        /// <summary>
        /// Immutable result of a score calculation.
        /// </summary>
        [Serializable]
        public struct ScoreData
        {
            /// <summary>Star rating (1-3).</summary>
            public int Stars;

            /// <summary>Total numeric score.</summary>
            public int Score;

            /// <summary>Number of orbs consumed during the level.</summary>
            public int OrbsUsed;

            /// <summary>Percentage of destructible structures destroyed (0-1).</summary>
            public float DestructionPercent;

            /// <summary>Time the player took to complete the level in seconds.</summary>
            public float CompletionTime;

            public override string ToString()
            {
                return $"Stars={Stars}, Score={Score}, Orbs={OrbsUsed}, " +
                       $"Destruction={DestructionPercent:P0}, Time={CompletionTime:F1}s";
            }
        }

        // ──────────────────────────────────────────────
        //  Events
        // ──────────────────────────────────────────────

        /// <summary>Fired when a score has been calculated after level completion.</summary>
        public static event Action<ScoreData> OnScoreCalculated;

        // ──────────────────────────────────────────────
        //  Tuning
        // ──────────────────────────────────────────────

        [Header("Score Weights")]
        [Tooltip("Points awarded per unused orb.")]
        [SerializeField] private int _pointsPerOrbRemaining = 5000;

        [Tooltip("Maximum points for 100% destruction.")]
        [SerializeField] private int _maxDestructionScore = 10000;

        [Tooltip("Maximum points for completing under par time.")]
        [SerializeField] private int _maxTimeBonus = 5000;

        [Header("Star Thresholds")]
        [Tooltip("Minimum total score for 2 stars.")]
        [SerializeField] private int _twoStarThreshold = 8000;

        [Tooltip("Minimum total score for 3 stars.")]
        [SerializeField] private int _threeStarThreshold = 15000;

        // ──────────────────────────────────────────────
        //  Lifecycle
        // ──────────────────────────────────────────────

        private void OnEnable()
        {
            LevelManager.OnLevelComplete += HandleLevelComplete;
        }

        private void OnDisable()
        {
            LevelManager.OnLevelComplete -= HandleLevelComplete;
        }

        // ──────────────────────────────────────────────
        //  Public API
        // ──────────────────────────────────────────────

        /// <summary>
        /// Calculates the score and star rating for the current level state.
        /// </summary>
        /// <param name="levelManager">The LevelManager to read state from.</param>
        /// <returns>A ScoreData struct with the computed results.</returns>
        public ScoreData CalculateScore(LevelManager levelManager)
        {
            if (levelManager == null || levelManager.CurrentLevelData == null)
            {
                Debug.LogError("[ScoreManager] Cannot calculate score: LevelManager or LevelData is null.");
                return default;
            }

            int orbsRemaining = levelManager.OrbsRemaining;
            int orbsUsed = levelManager.OrbsUsed;
            float destructionPercent = levelManager.DestructionPercent;
            float elapsed = levelManager.ElapsedTime;
            float parTime = levelManager.CurrentLevelData.ParTime;

            // Orb bonus
            int orbScore = orbsRemaining * _pointsPerOrbRemaining;

            // Destruction score (linear)
            int destructionScore = Mathf.RoundToInt(destructionPercent * _maxDestructionScore);

            // Time bonus (linear falloff: full bonus at 0s, zero bonus at 2x par time)
            float timeRatio = Mathf.Clamp01(1f - (elapsed / (parTime * 2f)));
            int timeBonus = Mathf.RoundToInt(timeRatio * _maxTimeBonus);

            int totalScore = orbScore + destructionScore + timeBonus;

            // Star rating
            int stars;
            if (totalScore >= _threeStarThreshold)
                stars = 3;
            else if (totalScore >= _twoStarThreshold)
                stars = 2;
            else
                stars = 1;

            var data = new ScoreData
            {
                Stars = stars,
                Score = totalScore,
                OrbsUsed = orbsUsed,
                DestructionPercent = destructionPercent,
                CompletionTime = elapsed
            };

            Debug.Log($"[ScoreManager] {data}");
            OnScoreCalculated?.Invoke(data);

            return data;
        }

        // ──────────────────────────────────────────────
        //  Internal
        // ──────────────────────────────────────────────

        private void HandleLevelComplete()
        {
            var levelManager = FindFirstObjectByType<LevelManager>();
            if (levelManager != null)
            {
                CalculateScore(levelManager);
            }
            else
            {
                Debug.LogWarning("[ScoreManager] LevelManager not found when level completed.");
            }
        }
    }
}
