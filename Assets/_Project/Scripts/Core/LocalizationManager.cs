using System;
using System.Collections.Generic;
using UnityEngine;

namespace ElementalSiege.Core
{
    /// <summary>
    /// Simple string-table localization manager.
    /// Loads language JSON files from Resources/Localization/ and provides keyed string lookups.
    /// </summary>
    public class LocalizationManager : MonoBehaviour
    {
        /// <summary>Fired when the active language changes.</summary>
        public event Action<string> OnLanguageChanged;

        private static LocalizationManager _instance;

        /// <summary>Global singleton accessor.</summary>
        public static LocalizationManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[LocalizationManager]");
                    _instance = go.AddComponent<LocalizationManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        /// <summary>PlayerPrefs key used to persist the selected language.</summary>
        private const string LanguagePrefKey = "SelectedLanguage";

        /// <summary>Default language code used as fallback.</summary>
        private const string DefaultLanguage = "en";

        /// <summary>Supported language codes mapped to their display names.</summary>
        private static readonly Dictionary<string, string> SupportedLanguages = new Dictionary<string, string>
        {
            { "en", "English" },
            { "ko", "Korean" },
            { "ja", "Japanese" },
            { "zh", "Chinese" },
            { "es", "Spanish" }
        };

        /// <summary>Current active language code.</summary>
        [SerializeField, Tooltip("Current language code (en, ko, ja, zh, es)")]
        private string _currentLanguage = DefaultLanguage;

        /// <summary>Current active language code.</summary>
        public string CurrentLanguage => _currentLanguage;

        /// <summary>Loaded string tables per language code.</summary>
        private readonly Dictionary<string, Dictionary<string, string>> _languageTables
            = new Dictionary<string, Dictionary<string, string>>();

        /// <summary>The currently active string table.</summary>
        private Dictionary<string, string> _activeTable = new Dictionary<string, string>();

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            string savedLang = PlayerPrefs.GetString(LanguagePrefKey, DefaultLanguage);
            LoadLanguage(savedLang);
        }

        /// <summary>
        /// Static convenience accessor to get a localized string by key.
        /// Falls back to the key itself if the manager is not initialized.
        /// </summary>
        /// <param name="key">The localization key.</param>
        /// <returns>The localized string, or the key if not found.</returns>
        public static string Get(string key)
        {
            if (_instance == null) return key;
            return _instance.GetString(key);
        }

        /// <summary>
        /// Returns the localized string for the given key in the current language.
        /// Falls back to English if the key is missing, then to the key itself.
        /// </summary>
        /// <param name="key">The localization key to look up.</param>
        /// <returns>The localized string.</returns>
        public string GetString(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;

            // Try current language
            if (_activeTable.TryGetValue(key, out string value))
            {
                return value;
            }

            // Fallback to English
            if (_currentLanguage != DefaultLanguage)
            {
                if (_languageTables.TryGetValue(DefaultLanguage, out var fallbackTable))
                {
                    if (fallbackTable.TryGetValue(key, out string fallbackValue))
                    {
                        Debug.LogWarning($"[Localization] Key '{key}' missing in '{_currentLanguage}', using English fallback.");
                        return fallbackValue;
                    }
                }
            }

            Debug.LogWarning($"[Localization] Key '{key}' not found in any language.");
            return key;
        }

        /// <summary>
        /// Switches the active language and reloads the string table.
        /// </summary>
        /// <param name="langCode">Language code (en, ko, ja, zh, es).</param>
        public void SetLanguage(string langCode)
        {
            if (!SupportedLanguages.ContainsKey(langCode))
            {
                Debug.LogWarning($"[Localization] Unsupported language code: '{langCode}'. Falling back to English.");
                langCode = DefaultLanguage;
            }

            if (_currentLanguage == langCode && _activeTable.Count > 0) return;

            LoadLanguage(langCode);
            PlayerPrefs.SetString(LanguagePrefKey, langCode);
            PlayerPrefs.Save();

            OnLanguageChanged?.Invoke(langCode);
        }

        /// <summary>
        /// Returns all supported language codes and their display names.
        /// </summary>
        public Dictionary<string, string> GetSupportedLanguages()
        {
            return new Dictionary<string, string>(SupportedLanguages);
        }

        /// <summary>
        /// Loads a language file from Resources/Localization/{langCode}.json.
        /// </summary>
        private void LoadLanguage(string langCode)
        {
            _currentLanguage = langCode;

            if (_languageTables.TryGetValue(langCode, out var cachedTable))
            {
                _activeTable = cachedTable;
                return;
            }

            string resourcePath = $"Localization/{langCode}";
            TextAsset textAsset = Resources.Load<TextAsset>(resourcePath);

            if (textAsset == null)
            {
                Debug.LogWarning($"[Localization] No localization file found at Resources/{resourcePath}.json");
                _activeTable = new Dictionary<string, string>();

                if (langCode != DefaultLanguage)
                {
                    LoadLanguage(DefaultLanguage);
                }
                return;
            }

            var table = ParseLocalizationJson(textAsset.text);
            _languageTables[langCode] = table;
            _activeTable = table;

            // Always ensure English is loaded as fallback
            if (langCode != DefaultLanguage && !_languageTables.ContainsKey(DefaultLanguage))
            {
                var englishAsset = Resources.Load<TextAsset>($"Localization/{DefaultLanguage}");
                if (englishAsset != null)
                {
                    _languageTables[DefaultLanguage] = ParseLocalizationJson(englishAsset.text);
                }
            }

            Debug.Log($"[Localization] Loaded language: {SupportedLanguages[langCode]} ({table.Count} keys)");
        }

        /// <summary>
        /// Parses a flat JSON object into a string dictionary.
        /// Expected format: { "key1": "value1", "key2": "value2", ... }
        /// </summary>
        private Dictionary<string, string> ParseLocalizationJson(string json)
        {
            var result = new Dictionary<string, string>();

            try
            {
                // Use a wrapper since JsonUtility cannot deserialize Dictionary directly
                var wrapper = JsonUtility.FromJson<LocalizationFileWrapper>(
                    "{\"entries\":" + json + "}");

                // Fallback: manual parse for flat key-value JSON
                if (wrapper == null || wrapper.entries == null)
                {
                    return ParseFlatJson(json);
                }
            }
            catch
            {
                return ParseFlatJson(json);
            }

            return result;
        }

        /// <summary>
        /// Manual parser for flat key-value JSON objects.
        /// Handles the common { "key": "value" } format without requiring a wrapper.
        /// </summary>
        private Dictionary<string, string> ParseFlatJson(string json)
        {
            var result = new Dictionary<string, string>();

            json = json.Trim();
            if (json.StartsWith("{")) json = json.Substring(1);
            if (json.EndsWith("}")) json = json.Substring(0, json.Length - 1);

            bool inKey = false;
            bool inValue = false;
            bool escaped = false;
            string currentKey = "";
            string currentValue = "";
            var buffer = new System.Text.StringBuilder();
            bool isKeyPhase = true;

            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];

                if (escaped)
                {
                    buffer.Append(c);
                    escaped = false;
                    continue;
                }

                if (c == '\\')
                {
                    escaped = true;
                    buffer.Append(c);
                    continue;
                }

                if (c == '"')
                {
                    if (!inKey && !inValue)
                    {
                        if (isKeyPhase)
                        {
                            inKey = true;
                            buffer.Clear();
                        }
                        else
                        {
                            inValue = true;
                            buffer.Clear();
                        }
                    }
                    else if (inKey)
                    {
                        inKey = false;
                        currentKey = buffer.ToString();
                        isKeyPhase = false;
                    }
                    else if (inValue)
                    {
                        inValue = false;
                        currentValue = buffer.ToString();
                        result[currentKey] = currentValue;
                        isKeyPhase = true;
                    }
                    continue;
                }

                if (inKey || inValue)
                {
                    buffer.Append(c);
                }
            }

            return result;
        }

        /// <summary>
        /// Wrapper class for JsonUtility deserialization (not directly used for flat JSON).
        /// </summary>
        [Serializable]
        private class LocalizationFileWrapper
        {
            public List<LocalizationEntry> entries;
        }

        /// <summary>
        /// Single localization entry for structured JSON files.
        /// </summary>
        [Serializable]
        private class LocalizationEntry
        {
            public string key;
            public string value;
        }
    }
}
