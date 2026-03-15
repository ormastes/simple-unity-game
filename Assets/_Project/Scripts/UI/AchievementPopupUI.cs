using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ElementalSiege.Core;

namespace ElementalSiege.UI
{
    /// <summary>
    /// Toast-style popup notification for unlocked achievements.
    /// Slides in from the top of the screen, auto-dismisses, and queues multiple unlocks.
    /// Does not block gameplay input.
    /// </summary>
    public class AchievementPopupUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField, Tooltip("Root RectTransform of the popup panel")]
        private RectTransform _popupPanel;

        [SerializeField, Tooltip("Achievement icon display")]
        private Image _iconImage;

        [SerializeField, Tooltip("Achievement name text")]
        private Text _nameText;

        [SerializeField, Tooltip("Achievement description text")]
        private Text _descriptionText;

        [SerializeField, Tooltip("Canvas group for controlling popup opacity")]
        private CanvasGroup _canvasGroup;

        [Header("Animation Settings")]
        [SerializeField, Tooltip("Time in seconds for the slide-in animation")]
        private float _slideInDuration = 0.4f;

        [SerializeField, Tooltip("Time in seconds the popup stays visible")]
        private float _displayDuration = 3.0f;

        [SerializeField, Tooltip("Time in seconds for the slide-out animation")]
        private float _slideOutDuration = 0.3f;

        [SerializeField, Tooltip("Y offset above screen where popup starts (in pixels)")]
        private float _hiddenYOffset = 150f;

        [SerializeField, Tooltip("Y position when popup is visible (in pixels from top)")]
        private float _visibleYPosition = -20f;

        [SerializeField, Tooltip("Default icon used when achievement has no custom icon")]
        private Sprite _defaultIcon;

        private readonly Queue<AchievementData> _popupQueue = new Queue<AchievementData>();
        private bool _isShowingPopup;
        private Vector2 _hiddenPosition;
        private Vector2 _visiblePosition;

        private void Awake()
        {
            if (_canvasGroup == null)
            {
                _canvasGroup = _popupPanel != null
                    ? _popupPanel.GetComponent<CanvasGroup>()
                    : GetComponent<CanvasGroup>();

                if (_canvasGroup == null && _popupPanel != null)
                {
                    _canvasGroup = _popupPanel.gameObject.AddComponent<CanvasGroup>();
                }
            }

            // Ensure it does not block raycasts (no gameplay interruption)
            if (_canvasGroup != null)
            {
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable = false;
                _canvasGroup.alpha = 0f;
            }

            CalculatePositions();
            HideImmediate();
        }

        private void OnEnable()
        {
            if (AchievementManager.Instance != null)
            {
                AchievementManager.Instance.OnAchievementUnlocked += EnqueueAchievement;
            }
        }

        private void OnDisable()
        {
            if (AchievementManager.Instance != null)
            {
                AchievementManager.Instance.OnAchievementUnlocked -= EnqueueAchievement;
            }
        }

        /// <summary>
        /// Calculates the hidden and visible anchor positions based on configuration.
        /// </summary>
        private void CalculatePositions()
        {
            if (_popupPanel == null) return;

            _visiblePosition = new Vector2(_popupPanel.anchoredPosition.x, _visibleYPosition);
            _hiddenPosition = new Vector2(_popupPanel.anchoredPosition.x, _hiddenYOffset);
        }

        /// <summary>
        /// Immediately hides the popup without animation.
        /// </summary>
        private void HideImmediate()
        {
            if (_popupPanel != null)
            {
                _popupPanel.anchoredPosition = _hiddenPosition;
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
            }
        }

        /// <summary>
        /// Adds an achievement to the display queue and starts processing if idle.
        /// </summary>
        /// <param name="achievement">The achievement that was just unlocked.</param>
        public void EnqueueAchievement(AchievementData achievement)
        {
            if (achievement == null) return;

            _popupQueue.Enqueue(achievement);

            if (!_isShowingPopup)
            {
                StartCoroutine(ProcessQueue());
            }
        }

        /// <summary>
        /// Processes the popup queue, showing each achievement one at a time.
        /// </summary>
        private IEnumerator ProcessQueue()
        {
            _isShowingPopup = true;

            while (_popupQueue.Count > 0)
            {
                var achievement = _popupQueue.Dequeue();
                yield return StartCoroutine(ShowPopup(achievement));
            }

            _isShowingPopup = false;
        }

        /// <summary>
        /// Shows a single achievement popup with slide-in, hold, and slide-out animations.
        /// </summary>
        /// <param name="achievement">The achievement to display.</param>
        private IEnumerator ShowPopup(AchievementData achievement)
        {
            // Populate UI
            if (_nameText != null)
            {
                _nameText.text = achievement.Name;
            }

            if (_descriptionText != null)
            {
                _descriptionText.text = achievement.Description;
            }

            if (_iconImage != null)
            {
                _iconImage.sprite = achievement.Icon != null ? achievement.Icon : _defaultIcon;
                _iconImage.enabled = _iconImage.sprite != null;
            }

            // Slide in from top
            yield return StartCoroutine(SlideIn());

            // Hold for display duration
            yield return new WaitForSecondsRealtime(_displayDuration);

            // Slide out to top
            yield return StartCoroutine(SlideOut());
        }

        /// <summary>
        /// Animates the popup sliding down from the hidden position to the visible position.
        /// </summary>
        private IEnumerator SlideIn()
        {
            if (_popupPanel == null) yield break;

            float elapsed = 0f;
            _popupPanel.anchoredPosition = _hiddenPosition;

            while (elapsed < _slideInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = EaseOutBack(Mathf.Clamp01(elapsed / _slideInDuration));

                _popupPanel.anchoredPosition = Vector2.Lerp(_hiddenPosition, _visiblePosition, t);

                if (_canvasGroup != null)
                {
                    _canvasGroup.alpha = Mathf.Clamp01(elapsed / (_slideInDuration * 0.5f));
                }

                yield return null;
            }

            _popupPanel.anchoredPosition = _visiblePosition;
            if (_canvasGroup != null) _canvasGroup.alpha = 1f;
        }

        /// <summary>
        /// Animates the popup sliding up from the visible position back to the hidden position.
        /// </summary>
        private IEnumerator SlideOut()
        {
            if (_popupPanel == null) yield break;

            float elapsed = 0f;

            while (elapsed < _slideOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / _slideOutDuration);

                _popupPanel.anchoredPosition = Vector2.Lerp(_visiblePosition, _hiddenPosition, t);

                if (_canvasGroup != null)
                {
                    _canvasGroup.alpha = 1f - t;
                }

                yield return null;
            }

            _popupPanel.anchoredPosition = _hiddenPosition;
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
        }

        /// <summary>
        /// Ease-out-back easing function for a bouncy slide-in effect.
        /// </summary>
        /// <param name="t">Normalized time (0-1).</param>
        /// <returns>Eased value.</returns>
        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
    }
}
