using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ElementalSiege.UI
{
    /// <summary>
    /// Full-screen loading screen with progress bar, random tips, and fade transitions.
    /// Designed to work alongside <see cref="Core.SceneTransitionManager"/>.
    /// </summary>
    public class LoadingScreenUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField, Tooltip("Root canvas group for fade in/out")]
        private CanvasGroup _canvasGroup;

        [SerializeField, Tooltip("The loading progress bar fill image (Image.fillAmount)")]
        private Image _progressBarFill;

        [SerializeField, Tooltip("Text element for displaying loading tips")]
        private Text _tipText;

        [SerializeField, Tooltip("Spinning element icon for the loading animation")]
        private RectTransform _spinnerTransform;

        [SerializeField, Tooltip("Optional percentage text display")]
        private Text _percentageText;

        [Header("Settings")]
        [SerializeField, Tooltip("Minimum time the loading screen is displayed to avoid flashing")]
        private float _minimumDisplayTime = 1.0f;

        [SerializeField, Tooltip("Duration of the fade in transition")]
        private float _fadeInDuration = 0.3f;

        [SerializeField, Tooltip("Duration of the fade out transition")]
        private float _fadeOutDuration = 0.3f;

        [SerializeField, Tooltip("Rotation speed of the spinner in degrees per second")]
        private float _spinnerSpeed = 180f;

        [SerializeField, Tooltip("Speed at which the progress bar smoothly fills")]
        private float _progressLerpSpeed = 3f;

        [Header("Tips")]
        [SerializeField, Tooltip("List of gameplay tips shown randomly during loading")]
        private List<string> _tips = new List<string>
        {
            "Combine Fire and Water orbs to create a devastating Steam explosion!",
            "Earth orbs are extra effective against stone structures.",
            "Try launching orbs at different angles to find hidden weak points.",
            "Lightning chains between wet surfaces — use Water first!",
            "Some guardians are immune to certain elements. Experiment!",
            "Aim for bonus objectives to earn extra stars.",
            "Ice orbs freeze water surfaces, creating new paths for other orbs.",
            "Shadow orbs pass through thin walls — use them to reach hidden guardians.",
            "Complete levels with fewer orbs for higher scores.",
            "Nature orbs grow vines that can topple structures over time.",
            "Slow-motion activates automatically during crucial moments.",
            "Replays of your best runs are saved automatically.",
            "Check the achievement gallery for special challenges.",
            "Air orbs can redirect other orbs mid-flight!",
            "Void orbs are rare but devastatingly powerful against bosses."
        };

        private float _targetProgress;
        private float _displayedProgress;
        private bool _isVisible;
        private float _showTime;

        private void Awake()
        {
            if (_canvasGroup == null)
            {
                _canvasGroup = GetComponent<CanvasGroup>();
                if (_canvasGroup == null)
                {
                    _canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            // Start hidden
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!_isVisible) return;

            // Smoothly interpolate the progress bar
            _displayedProgress = Mathf.Lerp(_displayedProgress, _targetProgress, Time.unscaledDeltaTime * _progressLerpSpeed);

            if (_progressBarFill != null)
            {
                _progressBarFill.fillAmount = _displayedProgress;
            }

            if (_percentageText != null)
            {
                _percentageText.text = $"{Mathf.RoundToInt(_displayedProgress * 100)}%";
            }

            // Rotate the spinner
            if (_spinnerTransform != null)
            {
                _spinnerTransform.Rotate(0f, 0f, -_spinnerSpeed * Time.unscaledDeltaTime);
            }
        }

        /// <summary>
        /// Shows the loading screen with a fade-in transition and displays a random tip.
        /// </summary>
        public void Show()
        {
            gameObject.SetActive(true);
            _targetProgress = 0f;
            _displayedProgress = 0f;
            _showTime = Time.unscaledTime;
            _isVisible = true;

            DisplayRandomTip();
            StartCoroutine(FadeIn());
        }

        /// <summary>
        /// Hides the loading screen with a fade-out transition.
        /// Respects the minimum display time to prevent visual flashing.
        /// </summary>
        public void Hide()
        {
            StartCoroutine(HideAfterMinimumTime());
        }

        /// <summary>
        /// Updates the loading progress value (0-1).
        /// The bar will smoothly animate toward this target.
        /// </summary>
        /// <param name="progress">Progress value between 0 and 1.</param>
        public void SetProgress(float progress)
        {
            _targetProgress = Mathf.Clamp01(progress);
        }

        /// <summary>
        /// Displays a random tip from the tip list.
        /// </summary>
        public void DisplayRandomTip()
        {
            if (_tipText == null || _tips.Count == 0) return;

            int index = Random.Range(0, _tips.Count);
            _tipText.text = _tips[index];
        }

        /// <summary>
        /// Adds a custom tip to the tip pool at runtime.
        /// </summary>
        /// <param name="tip">The tip text to add.</param>
        public void AddTip(string tip)
        {
            if (!string.IsNullOrEmpty(tip) && !_tips.Contains(tip))
            {
                _tips.Add(tip);
            }
        }

        /// <summary>
        /// Coroutine that fades in the loading screen canvas group.
        /// </summary>
        private IEnumerator FadeIn()
        {
            _canvasGroup.blocksRaycasts = true;
            float elapsed = 0f;

            while (elapsed < _fadeInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Clamp01(elapsed / _fadeInDuration);
                yield return null;
            }

            _canvasGroup.alpha = 1f;
        }

        /// <summary>
        /// Coroutine that waits for the minimum display time, then fades out the loading screen.
        /// </summary>
        private IEnumerator HideAfterMinimumTime()
        {
            // Ensure the progress bar reaches 100%
            _targetProgress = 1f;

            // Wait for minimum display time
            float elapsed = Time.unscaledTime - _showTime;
            if (elapsed < _minimumDisplayTime)
            {
                yield return new WaitForSecondsRealtime(_minimumDisplayTime - elapsed);
            }

            // Wait for progress bar to visually catch up
            while (_displayedProgress < 0.98f)
            {
                yield return null;
            }

            // Fade out
            float fadeElapsed = 0f;
            while (fadeElapsed < _fadeOutDuration)
            {
                fadeElapsed += Time.unscaledDeltaTime;
                _canvasGroup.alpha = 1f - Mathf.Clamp01(fadeElapsed / _fadeOutDuration);
                yield return null;
            }

            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
            _isVisible = false;
            gameObject.SetActive(false);
        }
    }
}
