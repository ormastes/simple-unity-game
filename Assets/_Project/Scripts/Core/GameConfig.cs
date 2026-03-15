using UnityEngine;

namespace ElementalSiege.Core
{
    /// <summary>
    /// ScriptableObject holding global game configuration values.
    /// Create instances via Assets > Create > ElementalSiege > Game Config.
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "ElementalSiege/Game Config")]
    public class GameConfig : ScriptableObject
    {
        // ─────────────────────────────────────────────
        // Physics
        // ─────────────────────────────────────────────

        [Header("Physics")]
        [SerializeField, Tooltip("Custom gravity magnitude applied to orbs")]
        private float _gravity = 9.81f;

        /// <summary>Custom gravity magnitude applied to orbs.</summary>
        public float Gravity => _gravity;

        [SerializeField, Tooltip("Maximum velocity an orb can reach")]
        private float _maxVelocity = 50f;

        /// <summary>Maximum velocity an orb can reach before clamping.</summary>
        public float MaxVelocity => _maxVelocity;

        [SerializeField, Tooltip("Velocity threshold below which an orb is considered settled")]
        private float _settleThreshold = 0.1f;

        /// <summary>Velocity threshold below which an orb is considered settled.</summary>
        public float SettleThreshold => _settleThreshold;

        [SerializeField, Tooltip("Time in seconds an orb must remain below settle threshold to count as settled")]
        private float _settleTime = 1.0f;

        /// <summary>Minimum time an orb must stay below the settle threshold to be considered at rest.</summary>
        public float SettleTime => _settleTime;

        // ─────────────────────────────────────────────
        // Scoring
        // ─────────────────────────────────────────────

        [Header("Scoring")]
        [SerializeField, Tooltip("Base multiplier applied to all score calculations")]
        private float _baseScoreMultiplier = 1.0f;

        /// <summary>Base multiplier applied to all score calculations.</summary>
        public float BaseScoreMultiplier => _baseScoreMultiplier;

        [SerializeField, Tooltip("Bonus points per unused orb at level completion")]
        private int _orbBonus = 500;

        /// <summary>Bonus points per unused orb at level completion.</summary>
        public int OrbBonus => _orbBonus;

        [SerializeField, Tooltip("Points awarded per structure or guardian destroyed")]
        private int _destructionBonus = 100;

        /// <summary>Points awarded per structure or guardian destroyed.</summary>
        public int DestructionBonus => _destructionBonus;

        [SerializeField, Tooltip("Weight of the time-based bonus in final score (0-1)")]
        private float _timeBonusWeight = 0.2f;

        /// <summary>Weight of the time-based bonus in the final score formula.</summary>
        public float TimeBonusWeight => _timeBonusWeight;

        // ─────────────────────────────────────────────
        // Progression
        // ─────────────────────────────────────────────

        [Header("Progression")]
        [SerializeField, Tooltip("Stars required to unlock each world (index = world number - 1)")]
        private int[] _starsToUnlockWorld = { 0, 3, 9, 18, 30, 45, 60, 80, 100 };

        /// <summary>Array of star thresholds required to unlock each world.</summary>
        public int[] StarsToUnlockWorld => _starsToUnlockWorld;

        // ─────────────────────────────────────────────
        // Camera
        // ─────────────────────────────────────────────

        [Header("Camera")]
        [SerializeField, Tooltip("Default orthographic camera size")]
        private float _defaultOrthoSize = 10f;

        /// <summary>Default orthographic camera size.</summary>
        public float DefaultOrthoSize => _defaultOrthoSize;

        [SerializeField, Tooltip("Speed at which the camera follows the active orb")]
        private float _cameraFollowSpeed = 5f;

        /// <summary>Speed at which the camera follows the active orb.</summary>
        public float CameraFollowSpeed => _cameraFollowSpeed;

        [SerializeField, Tooltip("Orthographic size to zoom to on impact")]
        private float _impactZoomSize = 6f;

        /// <summary>Orthographic size the camera zooms to during impact moments.</summary>
        public float ImpactZoomSize => _impactZoomSize;

        // ─────────────────────────────────────────────
        // Audio
        // ─────────────────────────────────────────────

        [Header("Audio")]
        [SerializeField, Range(0f, 1f), Tooltip("Default master volume")]
        private float _defaultMasterVolume = 1.0f;

        /// <summary>Default master audio volume (0-1).</summary>
        public float DefaultMasterVolume => _defaultMasterVolume;

        [SerializeField, Range(0f, 1f), Tooltip("Default music volume")]
        private float _defaultMusicVolume = 0.7f;

        /// <summary>Default music volume (0-1).</summary>
        public float DefaultMusicVolume => _defaultMusicVolume;

        [SerializeField, Range(0f, 1f), Tooltip("Default sound effects volume")]
        private float _defaultSfxVolume = 0.8f;

        /// <summary>Default sound effects volume (0-1).</summary>
        public float DefaultSfxVolume => _defaultSfxVolume;

        // ─────────────────────────────────────────────
        // Debug
        // ─────────────────────────────────────────────

        [Header("Debug")]
        [SerializeField, Tooltip("Skip all tutorial sequences")]
        private bool _skipTutorials;

        /// <summary>Whether to skip all tutorial sequences (debug only).</summary>
        public bool SkipTutorials => _skipTutorials;

        [SerializeField, Tooltip("Unlock all levels and worlds")]
        private bool _unlockAll;

        /// <summary>Whether all levels and worlds should be unlocked (debug only).</summary>
        public bool UnlockAll => _unlockAll;

        [SerializeField, Tooltip("Make orbs invincible (they never shatter)")]
        private bool _invincibleOrbs;

        /// <summary>Whether orbs are invincible and never shatter (debug only).</summary>
        public bool InvincibleOrbs => _invincibleOrbs;

        // ─────────────────────────────────────────────
        // Validation
        // ─────────────────────────────────────────────

        private void OnValidate()
        {
            _gravity = Mathf.Max(0f, _gravity);
            _maxVelocity = Mathf.Max(1f, _maxVelocity);
            _settleThreshold = Mathf.Max(0.01f, _settleThreshold);
            _settleTime = Mathf.Max(0.1f, _settleTime);
            _baseScoreMultiplier = Mathf.Max(0.1f, _baseScoreMultiplier);
            _orbBonus = Mathf.Max(0, _orbBonus);
            _destructionBonus = Mathf.Max(0, _destructionBonus);
            _timeBonusWeight = Mathf.Clamp01(_timeBonusWeight);
            _defaultOrthoSize = Mathf.Max(1f, _defaultOrthoSize);
            _cameraFollowSpeed = Mathf.Max(0.1f, _cameraFollowSpeed);
            _impactZoomSize = Mathf.Max(1f, _impactZoomSize);
        }
    }
}
