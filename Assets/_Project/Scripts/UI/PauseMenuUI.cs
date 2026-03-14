using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ElementalSiege.UI
{
    /// <summary>
    /// Pause overlay with resume, restart, settings, and quit buttons.
    /// Pauses gameplay via Time.timeScale and provides an audio settings sub-panel.
    /// </summary>
    public class PauseMenuUI : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Overlay")]
        [SerializeField] private GameObject _pausePanel;
        [SerializeField] private CanvasGroup _overlayCanvasGroup;
        [SerializeField] private Image _backgroundDimmer;
        [SerializeField] private Color _dimColor = new Color(0f, 0f, 0f, 0.6f);

        [Header("Main Buttons")]
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _restartButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _quitToMapButton;

        [Header("Settings Sub-Panel")]
        [SerializeField] private GameObject _settingsSubPanel;
        [SerializeField] private Slider _masterVolumeSlider;
        [SerializeField] private Slider _musicVolumeSlider;
        [SerializeField] private Slider _sfxVolumeSlider;
        [SerializeField] private TextMeshProUGUI _masterVolumeLabel;
        [SerializeField] private TextMeshProUGUI _musicVolumeLabel;
        [SerializeField] private TextMeshProUGUI _sfxVolumeLabel;
        [SerializeField] private Button _settingsBackButton;

        [Header("Labels")]
        [SerializeField] private TextMeshProUGUI _pauseTitleText;

        #endregion

        #region Events

        /// <summary>Raised when the player taps Resume.</summary>
        public event Action OnResumePressed;

        /// <summary>Raised when the player taps Restart.</summary>
        public event Action OnRestartPressed;

        /// <summary>Raised when the player taps Quit to Map.</summary>
        public event Action OnQuitToMapPressed;

        #endregion

        #region Private State

        private float _previousTimeScale = 1f;
        private bool _isPaused;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Main buttons
            if (_resumeButton != null) _resumeButton.onClick.AddListener(Resume);
            if (_restartButton != null) _restartButton.onClick.AddListener(HandleRestart);
            if (_settingsButton != null) _settingsButton.onClick.AddListener(ShowSettings);
            if (_quitToMapButton != null) _quitToMapButton.onClick.AddListener(HandleQuitToMap);

            // Settings sub-panel
            if (_settingsBackButton != null) _settingsBackButton.onClick.AddListener(HideSettings);
            if (_masterVolumeSlider != null) _masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            if (_musicVolumeSlider != null) _musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
            if (_sfxVolumeSlider != null) _sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);

            // Load saved volumes
            LoadAudioSettings();

            // Start hidden
            if (_pausePanel != null)
                _pausePanel.SetActive(false);
            if (_settingsSubPanel != null)
                _settingsSubPanel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_resumeButton != null) _resumeButton.onClick.RemoveAllListeners();
            if (_restartButton != null) _restartButton.onClick.RemoveAllListeners();
            if (_settingsButton != null) _settingsButton.onClick.RemoveAllListeners();
            if (_quitToMapButton != null) _quitToMapButton.onClick.RemoveAllListeners();
            if (_settingsBackButton != null) _settingsBackButton.onClick.RemoveAllListeners();
            if (_masterVolumeSlider != null) _masterVolumeSlider.onValueChanged.RemoveAllListeners();
            if (_musicVolumeSlider != null) _musicVolumeSlider.onValueChanged.RemoveAllListeners();
            if (_sfxVolumeSlider != null) _sfxVolumeSlider.onValueChanged.RemoveAllListeners();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Opens the pause menu and freezes gameplay.
        /// </summary>
        public void Pause()
        {
            if (_isPaused) return;
            _isPaused = true;

            _previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;

            if (_pausePanel != null)
                _pausePanel.SetActive(true);

            if (_settingsSubPanel != null)
                _settingsSubPanel.SetActive(false);

            if (_backgroundDimmer != null)
                _backgroundDimmer.color = _dimColor;

            if (_overlayCanvasGroup != null)
            {
                _overlayCanvasGroup.alpha = 1f;
                _overlayCanvasGroup.interactable = true;
                _overlayCanvasGroup.blocksRaycasts = true;
            }

            if (_pauseTitleText != null)
                _pauseTitleText.text = "PAUSED";
        }

        /// <summary>
        /// Closes the pause menu and resumes gameplay.
        /// </summary>
        public void Resume()
        {
            if (!_isPaused) return;
            _isPaused = false;

            Time.timeScale = _previousTimeScale;

            if (_pausePanel != null)
                _pausePanel.SetActive(false);

            if (_overlayCanvasGroup != null)
            {
                _overlayCanvasGroup.alpha = 0f;
                _overlayCanvasGroup.interactable = false;
                _overlayCanvasGroup.blocksRaycasts = false;
            }

            OnResumePressed?.Invoke();
        }

        /// <summary>
        /// Returns whether the game is currently paused via this menu.
        /// </summary>
        public bool IsPaused => _isPaused;

        #endregion

        #region Settings Sub-Panel

        private void ShowSettings()
        {
            if (_settingsSubPanel != null)
                _settingsSubPanel.SetActive(true);
        }

        private void HideSettings()
        {
            if (_settingsSubPanel != null)
                _settingsSubPanel.SetActive(false);

            SaveAudioSettings();
        }

        private void OnMasterVolumeChanged(float value)
        {
            AudioListener.volume = value;
            if (_masterVolumeLabel != null)
                _masterVolumeLabel.text = $"Master: {Mathf.RoundToInt(value * 100)}%";
        }

        private void OnMusicVolumeChanged(float value)
        {
            if (_musicVolumeLabel != null)
                _musicVolumeLabel.text = $"Music: {Mathf.RoundToInt(value * 100)}%";
            // In production, route to AudioManager to set music mixer group volume.
        }

        private void OnSFXVolumeChanged(float value)
        {
            if (_sfxVolumeLabel != null)
                _sfxVolumeLabel.text = $"SFX: {Mathf.RoundToInt(value * 100)}%";
            // In production, route to AudioManager to set SFX mixer group volume.
        }

        private void LoadAudioSettings()
        {
            float master = PlayerPrefs.GetFloat("Audio_Master", 1f);
            float music = PlayerPrefs.GetFloat("Audio_Music", 0.8f);
            float sfx = PlayerPrefs.GetFloat("Audio_SFX", 1f);

            if (_masterVolumeSlider != null) _masterVolumeSlider.SetValueWithoutNotify(master);
            if (_musicVolumeSlider != null) _musicVolumeSlider.SetValueWithoutNotify(music);
            if (_sfxVolumeSlider != null) _sfxVolumeSlider.SetValueWithoutNotify(sfx);

            AudioListener.volume = master;

            OnMasterVolumeChanged(master);
            OnMusicVolumeChanged(music);
            OnSFXVolumeChanged(sfx);
        }

        private void SaveAudioSettings()
        {
            if (_masterVolumeSlider != null) PlayerPrefs.SetFloat("Audio_Master", _masterVolumeSlider.value);
            if (_musicVolumeSlider != null) PlayerPrefs.SetFloat("Audio_Music", _musicVolumeSlider.value);
            if (_sfxVolumeSlider != null) PlayerPrefs.SetFloat("Audio_SFX", _sfxVolumeSlider.value);
            PlayerPrefs.Save();
        }

        #endregion

        #region Button Handlers

        private void HandleRestart()
        {
            Time.timeScale = _previousTimeScale;
            _isPaused = false;
            OnRestartPressed?.Invoke();
        }

        private void HandleQuitToMap()
        {
            Time.timeScale = _previousTimeScale;
            _isPaused = false;
            OnQuitToMapPressed?.Invoke();
        }

        #endregion
    }
}
