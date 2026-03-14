using UnityEngine;

namespace ElementalSiege.Core
{
    /// <summary>
    /// Enumerates the elemental types available in Elemental Siege.
    /// </summary>
    public enum ElementType
    {
        Fire,
        Water,
        Earth,
        Air,
        Ice,
        Lightning
    }

    /// <summary>
    /// Difficulty rating for a level.
    /// </summary>
    public enum Difficulty
    {
        Easy,
        Medium,
        Hard
    }

    /// <summary>
    /// ScriptableObject defining all static data for a single level.
    /// Create via Assets > Create > Elemental Siege > Level Data.
    /// </summary>
    [CreateAssetMenu(fileName = "NewLevelData", menuName = "Elemental Siege/Level Data", order = 0)]
    public class LevelData : ScriptableObject
    {
        #region Identity

        /// <summary>Unique string identifier for this level (e.g., "w1_l3").</summary>
        [Header("Identity")]
        [SerializeField] private string _levelId;

        /// <summary>Display name shown in the UI.</summary>
        [SerializeField] private string _levelName;

        /// <summary>Zero-based world index (0–7).</summary>
        [SerializeField] [Range(0, 7)] private int _worldIndex;

        /// <summary>Zero-based level index within the world.</summary>
        [SerializeField] [Range(0, 20)] private int _levelIndex;

        #endregion

        #region Gameplay

        /// <summary>Element types available as orbs in this level.</summary>
        [Header("Gameplay")]
        [SerializeField] private ElementType[] _availableOrbs;

        /// <summary>Maximum number of stars achievable (always 3).</summary>
        [SerializeField] private int _maxStars = 3;

        /// <summary>
        /// Score thresholds for earning 1, 2, and 3 stars respectively.
        /// Array length must be 3.
        /// </summary>
        [SerializeField] private int[] _starThresholds = new int[3];

        /// <summary>Difficulty rating of the level.</summary>
        [SerializeField] private Difficulty _difficulty = Difficulty.Medium;

        #endregion

        #region Layout

        /// <summary>Bounds of the playable area for this level.</summary>
        [Header("Layout")]
        [SerializeField] private Vector2 _levelBounds = new Vector2(20f, 12f);

        #endregion

        #region Tutorial

        /// <summary>
        /// Optional tutorial ID. If non-empty, the tutorial system will trigger
        /// the corresponding tutorial on first play.
        /// </summary>
        [Header("Tutorial")]
        [SerializeField] private string _tutorialId;

        #endregion

        #region Public Accessors

        /// <summary>Unique string identifier for this level.</summary>
        public string LevelId => _levelId;

        /// <summary>Display name shown in the UI.</summary>
        public string LevelName => _levelName;

        /// <summary>Zero-based world index (0–7).</summary>
        public int WorldIndex => _worldIndex;

        /// <summary>Zero-based level index within the world.</summary>
        public int LevelIndex => _levelIndex;

        /// <summary>Element types available as orbs in this level.</summary>
        public ElementType[] AvailableOrbs => _availableOrbs;

        /// <summary>Maximum number of stars (always 3).</summary>
        public int MaxStars => _maxStars;

        /// <summary>Score thresholds for 1, 2, and 3 stars.</summary>
        public int[] StarThresholds => _starThresholds;

        /// <summary>Difficulty rating.</summary>
        public Difficulty LevelDifficulty => _difficulty;

        /// <summary>Bounds of the playable area.</summary>
        public Vector2 LevelBounds => _levelBounds;

        /// <summary>Optional tutorial ID (empty string if none).</summary>
        public string TutorialId => _tutorialId;

        /// <summary>Returns true if this level has an associated tutorial.</summary>
        public bool HasTutorial => !string.IsNullOrEmpty(_tutorialId);

        #endregion

        #region Utility

        /// <summary>
        /// Returns the number of stars earned for the given score.
        /// </summary>
        /// <param name="score">The player's score.</param>
        /// <returns>0–3 stars.</returns>
        public int CalculateStars(int score)
        {
            if (_starThresholds == null || _starThresholds.Length < 3) return 0;

            int stars = 0;
            for (int i = 0; i < _starThresholds.Length; i++)
            {
                if (score >= _starThresholds[i])
                    stars = i + 1;
            }
            return Mathf.Clamp(stars, 0, _maxStars);
        }

        /// <summary>
        /// Returns whether the given element type is available in this level.
        /// </summary>
        public bool HasElement(ElementType element)
        {
            if (_availableOrbs == null) return false;
            foreach (var orb in _availableOrbs)
            {
                if (orb == element) return true;
            }
            return false;
        }

        #endregion
    }
}
