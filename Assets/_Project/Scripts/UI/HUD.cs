using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ElementalSiege.Core;

namespace ElementalSiege.UI
{
    /// <summary>
    /// In-game heads-up display showing orb count, score, element indicator,
    /// pause button, and destruction percentage.
    /// Subscribes to LevelManager and ScoreManager events for live updates.
    /// </summary>
    public class HUD : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Orb Display")]
        [SerializeField] private Transform _orbIconContainer;
        [SerializeField] private GameObject _orbIconPrefab;
        [SerializeField] private int _maxOrbIcons = 10;

        [Header("Score")]
        [SerializeField] private TextMeshProUGUI _scoreText;
        [SerializeField] private string _scoreFormat = "Score: {0:N0}";

        [Header("Element Indicator")]
        [SerializeField] private Image _currentElementIcon;
        [SerializeField] private Image _nextElementIcon;
        [SerializeField] private TextMeshProUGUI _currentElementLabel;
        [SerializeField] private Image _currentElementBackground;
        [SerializeField] private Image _nextElementBackground;

        [Header("Pause")]
        [SerializeField] private Button _pauseButton;

        [Header("Destruction Bar")]
        [SerializeField] private Slider _destructionBar;
        [SerializeField] private TextMeshProUGUI _destructionPercentText;
        [SerializeField] private Image _destructionFill;
        [SerializeField] private Gradient _destructionGradient;

        [Header("Animation")]
        [SerializeField] private float _scoreLerpSpeed = 8f;
        [SerializeField] private float _barLerpSpeed = 5f;

        #endregion

        #region Events

        /// <summary>Raised when the pause button is pressed.</summary>
        public event Action OnPauseRequested;

        #endregion

        #region Private State

        private readonly List<GameObject> _orbIcons = new List<GameObject>();
        private int _displayedScore;
        private int _targetScore;
        private float _displayedDestruction;
        private float _targetDestruction;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (_pauseButton != null)
                _pauseButton.onClick.AddListener(HandlePausePressed);

            if (_destructionBar != null)
            {
                _destructionBar.minValue = 0f;
                _destructionBar.maxValue = 1f;
                _destructionBar.value = 0f;
            }
        }

        private void OnDestroy()
        {
            if (_pauseButton != null)
                _pauseButton.onClick.RemoveListener(HandlePausePressed);
        }

        private void Update()
        {
            AnimateScore();
            AnimateDestructionBar();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Initializes the HUD with the starting orb count and element lineup.
        /// Call once when the level begins.
        /// </summary>
        /// <param name="totalOrbs">Total number of orbs available.</param>
        /// <param name="currentElement">The first element to launch.</param>
        /// <param name="nextElement">The second element in the queue.</param>
        public void Initialize(int totalOrbs, ElementType currentElement, ElementType nextElement)
        {
            _targetScore = 0;
            _displayedScore = 0;
            _targetDestruction = 0f;
            _displayedDestruction = 0f;

            SetOrbCount(totalOrbs);
            SetCurrentElement(currentElement);
            SetNextElement(nextElement);
            UpdateScoreImmediate(0);
            UpdateDestructionImmediate(0f);
        }

        /// <summary>
        /// Updates the remaining orb icon count.
        /// </summary>
        /// <param name="remaining">Number of orbs remaining.</param>
        public void SetOrbCount(int remaining)
        {
            // Clear existing icons
            foreach (var icon in _orbIcons)
            {
                if (icon != null)
                    Destroy(icon);
            }
            _orbIcons.Clear();

            if (_orbIconContainer == null || _orbIconPrefab == null) return;

            int count = Mathf.Min(remaining, _maxOrbIcons);
            for (int i = 0; i < count; i++)
            {
                GameObject icon = Instantiate(_orbIconPrefab, _orbIconContainer);
                _orbIcons.Add(icon);
            }
        }

        /// <summary>
        /// Sets the current element indicator icon and color.
        /// </summary>
        public void SetCurrentElement(ElementType element)
        {
            if (_currentElementLabel != null)
                _currentElementLabel.text = element.ToString();

            Color elementColor = GetElementColor(element);
            if (_currentElementIcon != null)
                _currentElementIcon.color = elementColor;
            if (_currentElementBackground != null)
                _currentElementBackground.color = elementColor * 0.5f;
        }

        /// <summary>
        /// Sets the next element indicator icon and color.
        /// </summary>
        public void SetNextElement(ElementType element)
        {
            Color elementColor = GetElementColor(element);
            if (_nextElementIcon != null)
                _nextElementIcon.color = elementColor;
            if (_nextElementBackground != null)
                _nextElementBackground.color = elementColor * 0.4f;
        }

        /// <summary>
        /// Smoothly animates the score toward the target value.
        /// </summary>
        public void UpdateScore(int newScore)
        {
            _targetScore = newScore;
        }

        /// <summary>
        /// Immediately sets the score without animation.
        /// </summary>
        public void UpdateScoreImmediate(int score)
        {
            _targetScore = score;
            _displayedScore = score;
            RefreshScoreText();
        }

        /// <summary>
        /// Smoothly animates the destruction bar to the target percentage.
        /// </summary>
        /// <param name="percent">0–1 destruction ratio.</param>
        public void UpdateDestruction(float percent)
        {
            _targetDestruction = Mathf.Clamp01(percent);
        }

        /// <summary>
        /// Immediately sets the destruction bar without animation.
        /// </summary>
        public void UpdateDestructionImmediate(float percent)
        {
            float clamped = Mathf.Clamp01(percent);
            _targetDestruction = clamped;
            _displayedDestruction = clamped;
            RefreshDestructionBar();
        }

        #endregion

        #region Private Helpers

        private void AnimateScore()
        {
            if (_displayedScore == _targetScore) return;

            _displayedScore = (int)Mathf.MoveTowards(
                _displayedScore, _targetScore, _scoreLerpSpeed * Time.deltaTime * 1000f);
            RefreshScoreText();
        }

        private void AnimateDestructionBar()
        {
            if (Mathf.Approximately(_displayedDestruction, _targetDestruction)) return;

            _displayedDestruction = Mathf.MoveTowards(
                _displayedDestruction, _targetDestruction, _barLerpSpeed * Time.deltaTime);
            RefreshDestructionBar();
        }

        private void RefreshScoreText()
        {
            if (_scoreText != null)
                _scoreText.text = string.Format(_scoreFormat, _displayedScore);
        }

        private void RefreshDestructionBar()
        {
            if (_destructionBar != null)
                _destructionBar.value = _displayedDestruction;

            if (_destructionPercentText != null)
                _destructionPercentText.text = $"{Mathf.RoundToInt(_displayedDestruction * 100f)}%";

            if (_destructionFill != null && _destructionGradient != null)
                _destructionFill.color = _destructionGradient.Evaluate(_displayedDestruction);
        }

        private void HandlePausePressed()
        {
            OnPauseRequested?.Invoke();
        }

        /// <summary>
        /// Returns a representative color for the given element type.
        /// </summary>
        private Color GetElementColor(ElementType element)
        {
            return element switch
            {
                ElementType.Fire      => new Color(1f, 0.35f, 0.1f),
                ElementType.Water     => new Color(0.2f, 0.5f, 1f),
                ElementType.Earth     => new Color(0.55f, 0.4f, 0.2f),
                ElementType.Air       => new Color(0.75f, 0.95f, 1f),
                ElementType.Ice       => new Color(0.6f, 0.9f, 1f),
                ElementType.Lightning => new Color(1f, 0.95f, 0.3f),
                _                     => Color.white,
            };
        }

        #endregion
    }
}
