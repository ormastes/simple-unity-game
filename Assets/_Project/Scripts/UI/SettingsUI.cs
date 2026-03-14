using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ElementalSiege.UI
{
    /// <summary>
    /// Full settings panel with audio volume sliders, screen shake toggle,
    /// quality presets, and progress reset. Persists to PlayerPrefs.
    /// </summary>
    public class SettingsUI : MonoBehaviour
    {
        #region Nested Types

        /// <summary>Quality preset levels.</summary>
        public enum QualityPreset
        {
            Low = 0,
            Medium = 1,
            High = 2
        }

        #endregion

        #region Serialized Fields

        [Header("Audio")]
        [SerializeField] private Slider _masterVolumeSlider;
        [SerializeField] private Slider _musicVolumeSlider;
        [SerializeField] private Slider _sfxVolumeSlider;
        [SerializeField] private TextMeshProUGUI _masterVolumeLabel;
        [SerializeField] private TextMeshProUGUI _musicVolumeLabel;
        [SerializeField] private TextMeshProUGUI _sfxVolumeLabel;

        [Header("Gameplay")]
        [SerializeField] private Toggle _screenShakeToggle;
        [SerializeField] private TextMeshProUGUI _screenShakeLabel;

        [Header("Graphics")]
        [SerializeField] private TMP_Dropdown _qualityDropdown;

        [Header("Progress Reset")]
        [SerializeField] private Button _resetProgressButton;
        [SerializeField] private GameObject _confirmationDialog;
        [SerializeField] private Button _confirmResetButton;
        [SerializeField] private Button _cancelResetButton;
        [SerializeField] private TextMeshProUGUI _confirmationText;

        [Header("Navigation")]
        [SerializeField] private Button _backButton;

        #endregion

        #region Events

        /// <summary>Raised when the back button is pressed.</summary>
        public event Action OnBackPressed;

        /// <summary>Raised when progress is fully reset.</summary>
        public event Action OnProgressReset;

        /// <summary>Raised when the master volume changes.</summary>
        public event Action<float> OnMasterVolumeChanged;

        /// <summary>Raised when the music volume changes.</summary>
        public event Action<float> OnMusicVolumeChanged;

        /// <summary>Raised when the SFX volume changes.</summary>
        public event Action<float> OnSFXVolumeChanged;

        /// <summary>Raised when screen shake setting changes.</summary>
        public event Action<bool> OnScreenShakeChanged;

        /// <summary>Raised when quality preset changes.</summary>
        public event Action<QualityPreset> OnQualityChanged;

        #endregion

        #region PlayerPrefs Keys

        private const string KeyMasterVolume = "Settings_MasterVolume";
        private const string KeyMusicVolume = "Settings_MusicVolume";
        private const string KeySFXVolume = "Settings_SFXVolume";
        private const string KeyScreenShake = "Settings_ScreenShake";
        private const string KeyQualityPreset = "Settings_Quality";

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Button listeners
            if (_backButton != null) _backButton.onClick.AddListener(HandleBack);
            if (_resetProgressButton != null) _resetProgressButton.onClick.AddListener(ShowConfirmation);
            if (_confirmResetButton != null) _confirmResetButton.onClick.AddListener(ConfirmReset);
            if (_cancelResetButton != null) _cancelResetButton.onClick.AddListener(CancelReset);

            // Slider listeners
            if (_masterVolumeSlider != null) _masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
            if (_musicVolumeSlider != null) _musicVolumeSlider.onValueChanged.AddListener(SetMusicVolume);
            if (_sfxVolumeSlider != null) _sfxVolumeSlider.onValueChanged.AddListener(SetSFXVolume);

            // Toggle listener
            if (_screenShakeToggle != null) _screenShakeToggle.onValueChanged.AddListener(SetScreenShake);

            // Dropdown
            if (_qualityDropdown != null)
            {
                _qualityDropdown.ClearOptions();
                _qualityDropdown.AddOptions(new System.Collections.Generic.List<string>
                    { "Low", "Medium", "High" });
                _qualityDropdown.onValueChanged.AddListener(SetQualityPreset);
            }

            // Confirmation dialog starts hidden
            if (_confirmationDialog != null)
                _confirmationDialog.SetActive(false);

            LoadSettings();
        }

        private void OnDestroy()
        {
            if (_backButton != null) _backButton.onClick.RemoveAllListeners();
            if (_resetProgressButton != null) _resetProgressButton.onClick.RemoveAllListeners();
            if (_confirmResetButton != null) _confirmResetButton.onClick.RemoveAllListeners();
            if (_cancelResetButton != null) _cancelResetButton.onClick.RemoveAllListeners();
            if (_masterVolumeSlider != null) _masterVolumeSlider.onValueChanged.RemoveAllListeners();
            if (_musicVolumeSlider != null) _musicVolumeSlider.onValueChanged.RemoveAllListeners();
            if (_sfxVolumeSlider != null) _sfxVolumeSlider.onValueChanged.RemoveAllListeners();
            if (_screenShakeToggle != null) _screenShakeToggle.onValueChanged.RemoveAllListeners();
            if (_qualityDropdown != null) _qualityDropdown.onValueChanged.RemoveAllListeners();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Returns the current screen shake setting.
        /// </summary>
        public bool ScreenShakeEnabled => PlayerPrefs.GetInt(KeyScreenShake, 1) == 1;

        /// <summary>
        /// Returns the current quality preset.
        /// </summary>
        public QualityPreset CurrentQuality =>
            (QualityPreset)PlayerPrefs.GetInt(KeyQualityPreset, (int)QualityPreset.High);

        /// <summary>
        /// Shows the settings panel.
        /// </summary>
        public void Show()
        {
            gameObject.SetActive(true);
            LoadSettings();
        }

        /// <summary>
        /// Hides the settings panel.
        /// </summary>
        public void Hide()
        {
            SaveSettings();
            gameObject.SetActive(false);
        }

        #endregion

        #region Volume Controls

        private void SetMasterVolume(float value)
        {
            AudioListener.volume = value;
            if (_masterVolumeLabel != null)
                _masterVolumeLabel.text = $"Master: {Mathf.RoundToInt(value * 100)}%";
            OnMasterVolumeChanged?.Invoke(value);
        }

        private void SetMusicVolume(float value)
        {
            if (_musicVolumeLabel != null)
                _musicVolumeLabel.text = $"Music: {Mathf.RoundToInt(value * 100)}%";
            OnMusicVolumeChanged?.Invoke(value);
        }

        private void SetSFXVolume(float value)
        {
            if (_sfxVolumeLabel != null)
                _sfxVolumeLabel.text = $"SFX: {Mathf.RoundToInt(value * 100)}%";
            OnSFXVolumeChanged?.Invoke(value);
        }

        #endregion

        #region Gameplay / Graphics

        private void SetScreenShake(bool enabled)
        {
            if (_screenShakeLabel != null)
                _screenShakeLabel.text = enabled ? "Screen Shake: ON" : "Screen Shake: OFF";
            OnScreenShakeChanged?.Invoke(enabled);
        }

        private void SetQualityPreset(int index)
        {
            QualityPreset preset = (QualityPreset)index;
            QualitySettings.SetQualityLevel(index, true);
            OnQualityChanged?.Invoke(preset);
        }

        #endregion

        #region Progress Reset

        private void ShowConfirmation()
        {
            if (_confirmationDialog != null)
                _confirmationDialog.SetActive(true);

            if (_confirmationText != null)
                _confirmationText.text = "Are you sure you want to reset ALL progress?\nThis cannot be undone!";
        }

        private void CancelReset()
        {
            if (_confirmationDialog != null)
                _confirmationDialog.SetActive(false);
        }

        private void ConfirmReset()
        {
            if (_confirmationDialog != null)
                _confirmationDialog.SetActive(false);

            PlayerPrefs.DeleteAll();
            LoadSettings();
            OnProgressReset?.Invoke();
        }

        #endregion

        #region Persistence

        private void LoadSettings()
        {
            float master = PlayerPrefs.GetFloat(KeyMasterVolume, 1f);
            float music = PlayerPrefs.GetFloat(KeyMusicVolume, 0.8f);
            float sfx = PlayerPrefs.GetFloat(KeySFXVolume, 1f);
            bool shake = PlayerPrefs.GetInt(KeyScreenShake, 1) == 1;
            int quality = PlayerPrefs.GetInt(KeyQualityPreset, (int)QualityPreset.High);

            if (_masterVolumeSlider != null) _masterVolumeSlider.SetValueWithoutNotify(master);
            if (_musicVolumeSlider != null) _musicVolumeSlider.SetValueWithoutNotify(music);
            if (_sfxVolumeSlider != null) _sfxVolumeSlider.SetValueWithoutNotify(sfx);
            if (_screenShakeToggle != null) _screenShakeToggle.SetIsOnWithoutNotify(shake);
            if (_qualityDropdown != null) _qualityDropdown.SetValueWithoutNotify(quality);

            // Apply loaded values
            AudioListener.volume = master;
            QualitySettings.SetQualityLevel(quality, true);

            // Refresh labels
            SetMasterVolume(master);
            SetMusicVolume(music);
            SetSFXVolume(sfx);
            SetScreenShake(shake);
        }

        private void SaveSettings()
        {
            if (_masterVolumeSlider != null) PlayerPrefs.SetFloat(KeyMasterVolume, _masterVolumeSlider.value);
            if (_musicVolumeSlider != null) PlayerPrefs.SetFloat(KeyMusicVolume, _musicVolumeSlider.value);
            if (_sfxVolumeSlider != null) PlayerPrefs.SetFloat(KeySFXVolume, _sfxVolumeSlider.value);
            if (_screenShakeToggle != null) PlayerPrefs.SetInt(KeyScreenShake, _screenShakeToggle.isOn ? 1 : 0);
            if (_qualityDropdown != null) PlayerPrefs.SetInt(KeyQualityPreset, _qualityDropdown.value);
            PlayerPrefs.Save();
        }

        #endregion

        #region Navigation

        private void HandleBack()
        {
            SaveSettings();
            OnBackPressed?.Invoke();
        }

        #endregion
    }
}
