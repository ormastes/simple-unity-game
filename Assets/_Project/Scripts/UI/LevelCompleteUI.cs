using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ElementalSiege.UI
{
    /// <summary>
    /// Post-level completion popup showing star rating, score breakdown,
    /// and navigation buttons. Animates in with scale + fade.
    /// </summary>
    public class LevelCompleteUI : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Panel")]
        [SerializeField] private CanvasGroup _panelCanvasGroup;
        [SerializeField] private RectTransform _panelTransform;
        [SerializeField] private GameObject _overlay;

        [Header("Stars")]
        [SerializeField] private Image[] _starImages = new Image[3];
        [SerializeField] private Sprite _starFilledSprite;
        [SerializeField] private Sprite _starEmptySprite;
        [SerializeField] private Color _starFilledColor = new Color(1f, 0.85f, 0.2f);
        [SerializeField] private Color _starEmptyColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);

        [Header("Stats")]
        [SerializeField] private TextMeshProUGUI _scoreText;
        [SerializeField] private TextMeshProUGUI _orbsUsedText;
        [SerializeField] private TextMeshProUGUI _destructionText;
        [SerializeField] private TextMeshProUGUI _titleText;

        [Header("Buttons")]
        [SerializeField] private Button _retryButton;
        [SerializeField] private Button _nextLevelButton;
        [SerializeField] private Button _worldMapButton;

        [Header("Animation")]
        [SerializeField] private float _animationDuration = 0.5f;
        [SerializeField] private float _starAnimDelay = 0.3f;
        [SerializeField] private float _starAnimDuration = 0.4f;
        [SerializeField] private AnimationCurve _scaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve _starScaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        #endregion

        #region Events

        /// <summary>Raised when the player taps Retry.</summary>
        public event Action OnRetryPressed;

        /// <summary>Raised when the player taps Next Level.</summary>
        public event Action OnNextLevelPressed;

        /// <summary>Raised when the player taps World Map.</summary>
        public event Action OnWorldMapPressed;

        #endregion

        #region Private State

        private Coroutine _animationCoroutine;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (_retryButton != null)
                _retryButton.onClick.AddListener(() => OnRetryPressed?.Invoke());
            if (_nextLevelButton != null)
                _nextLevelButton.onClick.AddListener(() => OnNextLevelPressed?.Invoke());
            if (_worldMapButton != null)
                _worldMapButton.onClick.AddListener(() => OnWorldMapPressed?.Invoke());

            // Start hidden
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_retryButton != null) _retryButton.onClick.RemoveAllListeners();
            if (_nextLevelButton != null) _nextLevelButton.onClick.RemoveAllListeners();
            if (_worldMapButton != null) _worldMapButton.onClick.RemoveAllListeners();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Shows the level complete popup with the given results.
        /// </summary>
        /// <param name="score">Final score achieved.</param>
        /// <param name="orbsUsed">Number of orbs the player used.</param>
        /// <param name="totalOrbs">Total orbs available in the level.</param>
        /// <param name="destructionPercent">0–1 destruction ratio.</param>
        /// <param name="starsEarned">Number of stars earned (1–3).</param>
        /// <param name="hasNextLevel">Whether a next level exists.</param>
        public void Show(int score, int orbsUsed, int totalOrbs, float destructionPercent,
                         int starsEarned, bool hasNextLevel)
        {
            gameObject.SetActive(true);

            if (_overlay != null)
                _overlay.SetActive(true);

            // Populate stats
            if (_titleText != null)
                _titleText.text = "Level Complete!";

            if (_scoreText != null)
                _scoreText.text = $"Score: {score:N0}";

            if (_orbsUsedText != null)
                _orbsUsedText.text = $"Orbs: {orbsUsed} / {totalOrbs}";

            if (_destructionText != null)
                _destructionText.text = $"Destruction: {Mathf.RoundToInt(destructionPercent * 100f)}%";

            // Next level button visibility
            if (_nextLevelButton != null)
                _nextLevelButton.gameObject.SetActive(hasNextLevel);

            // Reset stars to empty
            for (int i = 0; i < _starImages.Length; i++)
            {
                if (_starImages[i] != null)
                {
                    _starImages[i].sprite = _starEmptySprite;
                    _starImages[i].color = _starEmptyColor;
                    _starImages[i].transform.localScale = Vector3.zero;
                }
            }

            // Animate in
            if (_animationCoroutine != null)
                StopCoroutine(_animationCoroutine);

            _animationCoroutine = StartCoroutine(AnimateIn(starsEarned));
        }

        /// <summary>
        /// Hides the level complete popup.
        /// </summary>
        public void Hide()
        {
            if (_animationCoroutine != null)
            {
                StopCoroutine(_animationCoroutine);
                _animationCoroutine = null;
            }

            if (_overlay != null)
                _overlay.SetActive(false);

            gameObject.SetActive(false);
        }

        #endregion

        #region Animation

        private IEnumerator AnimateIn(int starsEarned)
        {
            // Panel scale + fade animation
            float elapsed = 0f;
            if (_panelTransform != null)
                _panelTransform.localScale = Vector3.zero;
            if (_panelCanvasGroup != null)
                _panelCanvasGroup.alpha = 0f;

            while (elapsed < _animationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / _animationDuration);
                float curveValue = _scaleCurve.Evaluate(t);

                if (_panelTransform != null)
                    _panelTransform.localScale = Vector3.one * curveValue;
                if (_panelCanvasGroup != null)
                    _panelCanvasGroup.alpha = t;

                yield return null;
            }

            if (_panelTransform != null)
                _panelTransform.localScale = Vector3.one;
            if (_panelCanvasGroup != null)
                _panelCanvasGroup.alpha = 1f;

            // Animate stars one by one
            for (int i = 0; i < Mathf.Min(starsEarned, _starImages.Length); i++)
            {
                yield return new WaitForSecondsRealtime(_starAnimDelay);
                yield return StartCoroutine(AnimateStar(i));
            }

            _animationCoroutine = null;
        }

        private IEnumerator AnimateStar(int index)
        {
            if (index < 0 || index >= _starImages.Length || _starImages[index] == null)
                yield break;

            Image star = _starImages[index];
            star.sprite = _starFilledSprite;
            star.color = _starFilledColor;

            float elapsed = 0f;
            while (elapsed < _starAnimDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / _starAnimDuration);
                float scale = _starScaleCurve.Evaluate(t);

                // Overshoot effect: scale up to 1.3 then settle to 1.0
                float overshoot = t < 0.6f
                    ? Mathf.Lerp(0f, 1.3f, t / 0.6f)
                    : Mathf.Lerp(1.3f, 1f, (t - 0.6f) / 0.4f);

                star.transform.localScale = Vector3.one * overshoot;
                yield return null;
            }

            star.transform.localScale = Vector3.one;
        }

        #endregion
    }
}
