using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ElementalSiege.UI
{
    /// <summary>
    /// Title screen with animated background, navigation buttons,
    /// credits panel, and version display.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Title")]
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private Image _logoImage;
        [SerializeField] private float _titlePulseSpeed = 1.5f;
        [SerializeField] private float _titlePulseMin = 0.95f;
        [SerializeField] private float _titlePulseMax = 1.05f;

        [Header("Buttons")]
        [SerializeField] private Button _playButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _creditsButton;

        [Header("Credits Panel")]
        [SerializeField] private GameObject _creditsPanel;
        [SerializeField] private Button _creditsBackButton;
        [SerializeField] private ScrollRect _creditsScrollRect;

        [Header("Animated Background")]
        [SerializeField] private ParallaxLayer[] _parallaxLayers;
        [SerializeField] private ParticleSystem _backgroundParticles;
        [SerializeField] private float _parallaxBaseSpeed = 20f;

        [Header("Version")]
        [SerializeField] private TextMeshProUGUI _versionText;

        [Header("Fade In")]
        [SerializeField] private CanvasGroup _mainCanvasGroup;
        [SerializeField] private float _fadeInDuration = 1f;
        [SerializeField] private float _fadeInDelay = 0.5f;

        #endregion

        #region Nested Types

        /// <summary>
        /// Configuration for a single parallax background layer.
        /// </summary>
        [Serializable]
        public class ParallaxLayer
        {
            /// <summary>The RectTransform of the layer image.</summary>
            public RectTransform layerTransform;

            /// <summary>Speed multiplier relative to base speed (farther = slower).</summary>
            [Range(0.1f, 2f)]
            public float speedMultiplier = 1f;

            /// <summary>Direction of movement.</summary>
            public Vector2 direction = Vector2.left;

            /// <summary>Width of the layer for wrapping.</summary>
            public float wrapWidth = 1920f;
        }

        #endregion

        #region Events

        /// <summary>Raised when the Play button is pressed.</summary>
        public event Action OnPlayPressed;

        /// <summary>Raised when the Settings button is pressed.</summary>
        public event Action OnSettingsPressed;

        #endregion

        #region Private State

        private Coroutine _fadeCoroutine;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Button listeners
            if (_playButton != null) _playButton.onClick.AddListener(HandlePlay);
            if (_settingsButton != null) _settingsButton.onClick.AddListener(HandleSettings);
            if (_creditsButton != null) _creditsButton.onClick.AddListener(ShowCredits);
            if (_creditsBackButton != null) _creditsBackButton.onClick.AddListener(HideCredits);

            // Initial state
            if (_creditsPanel != null) _creditsPanel.SetActive(false);
            if (_versionText != null) _versionText.text = $"v{Application.version}";
        }

        private void OnEnable()
        {
            _fadeCoroutine = StartCoroutine(FadeIn());

            if (_backgroundParticles != null && !_backgroundParticles.isPlaying)
                _backgroundParticles.Play();
        }

        private void OnDisable()
        {
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
                _fadeCoroutine = null;
            }
        }

        private void OnDestroy()
        {
            if (_playButton != null) _playButton.onClick.RemoveAllListeners();
            if (_settingsButton != null) _settingsButton.onClick.RemoveAllListeners();
            if (_creditsButton != null) _creditsButton.onClick.RemoveAllListeners();
            if (_creditsBackButton != null) _creditsBackButton.onClick.RemoveAllListeners();
        }

        private void Update()
        {
            UpdateParallax();
            UpdateTitlePulse();
        }

        #endregion

        #region Animated Background

        private void UpdateParallax()
        {
            if (_parallaxLayers == null) return;

            float dt = Time.deltaTime;
            foreach (var layer in _parallaxLayers)
            {
                if (layer.layerTransform == null) continue;

                Vector2 move = layer.direction.normalized * _parallaxBaseSpeed * layer.speedMultiplier * dt;
                layer.layerTransform.anchoredPosition += move;

                // Wrap around when layer has scrolled past its width
                Vector2 pos = layer.layerTransform.anchoredPosition;
                if (Mathf.Abs(pos.x) > layer.wrapWidth)
                    pos.x += layer.wrapWidth * Mathf.Sign(-pos.x);
                if (Mathf.Abs(pos.y) > layer.wrapWidth)
                    pos.y += layer.wrapWidth * Mathf.Sign(-pos.y);
                layer.layerTransform.anchoredPosition = pos;
            }
        }

        private void UpdateTitlePulse()
        {
            if (_logoImage == null && _titleText == null) return;

            float t = (Mathf.Sin(Time.time * _titlePulseSpeed) + 1f) * 0.5f;
            float scale = Mathf.Lerp(_titlePulseMin, _titlePulseMax, t);

            if (_logoImage != null)
                _logoImage.transform.localScale = Vector3.one * scale;
            else if (_titleText != null)
                _titleText.transform.localScale = Vector3.one * scale;
        }

        #endregion

        #region Fade Animation

        private IEnumerator FadeIn()
        {
            if (_mainCanvasGroup != null)
            {
                _mainCanvasGroup.alpha = 0f;
                _mainCanvasGroup.interactable = false;

                yield return new WaitForSeconds(_fadeInDelay);

                float elapsed = 0f;
                while (elapsed < _fadeInDuration)
                {
                    elapsed += Time.deltaTime;
                    _mainCanvasGroup.alpha = Mathf.Clamp01(elapsed / _fadeInDuration);
                    yield return null;
                }

                _mainCanvasGroup.alpha = 1f;
                _mainCanvasGroup.interactable = true;
            }
            _fadeCoroutine = null;
        }

        #endregion

        #region Button Handlers

        private void HandlePlay()
        {
            OnPlayPressed?.Invoke();
        }

        private void HandleSettings()
        {
            OnSettingsPressed?.Invoke();
        }

        private void ShowCredits()
        {
            if (_creditsPanel != null)
                _creditsPanel.SetActive(true);

            if (_creditsScrollRect != null)
                _creditsScrollRect.verticalNormalizedPosition = 1f;
        }

        private void HideCredits()
        {
            if (_creditsPanel != null)
                _creditsPanel.SetActive(false);
        }

        #endregion
    }
}
