using UnityEngine;
using UnityEditor;

namespace ElementalSiege.Editor.ClaudeIntegration
{
    /// <summary>
    /// Settings for the Claude AI integration plugin.
    /// Stored in EditorPrefs as serialized JSON.
    /// </summary>
    public class ClaudeSettings
    {
        private const string PrefsPrefix = "ClaudeIntegration_";
        private const string ApiKeyPref = PrefsPrefix + "ApiKey";
        private const string ModelPref = PrefsPrefix + "Model";
        private const string MaxTokensPref = PrefsPrefix + "MaxTokens";
        private const string TemperaturePref = PrefsPrefix + "Temperature";
        private const string SystemPromptPref = PrefsPrefix + "SystemPrompt";
        private const string AutoIncludeSelectionPref = PrefsPrefix + "AutoIncludeSelection";
        private const string AutoIncludeScenePref = PrefsPrefix + "AutoIncludeScene";
        private const string AutoIncludeConsolePref = PrefsPrefix + "AutoIncludeConsole";
        private const string ChatHistoryMaxPref = PrefsPrefix + "ChatHistoryMax";

        public enum ClaudeModel
        {
            Sonnet,
            Opus,
            Haiku
        }

        public static readonly string[] ModelDisplayNames = new string[]
        {
            "Claude Sonnet",
            "Claude Opus",
            "Claude Haiku"
        };

        public static readonly string[] ModelApiIds = new string[]
        {
            "claude-sonnet-4-20250514",
            "claude-opus-4-20250514",
            "claude-haiku-3-20250514"
        };

        public string ApiKey
        {
            get => EditorPrefs.GetString(ApiKeyPref, "");
            set => EditorPrefs.SetString(ApiKeyPref, value);
        }

        public int ModelIndex
        {
            get => EditorPrefs.GetInt(ModelPref, 0);
            set => EditorPrefs.SetInt(ModelPref, value);
        }

        public string ModelApiId => ModelApiIds[Mathf.Clamp(ModelIndex, 0, ModelApiIds.Length - 1)];
        public string ModelDisplayName => ModelDisplayNames[Mathf.Clamp(ModelIndex, 0, ModelDisplayNames.Length - 1)];

        public int MaxTokens
        {
            get => EditorPrefs.GetInt(MaxTokensPref, 4096);
            set => EditorPrefs.SetInt(MaxTokensPref, value);
        }

        public float Temperature
        {
            get => EditorPrefs.GetFloat(TemperaturePref, 0.7f);
            set => EditorPrefs.SetFloat(TemperaturePref, Mathf.Clamp01(value));
        }

        public string SystemPrompt
        {
            get => EditorPrefs.GetString(SystemPromptPref, GetDefaultSystemPrompt());
            set => EditorPrefs.SetString(SystemPromptPref, value);
        }

        public bool AutoIncludeSelection
        {
            get => EditorPrefs.GetBool(AutoIncludeSelectionPref, false);
            set => EditorPrefs.SetBool(AutoIncludeSelectionPref, value);
        }

        public bool AutoIncludeScene
        {
            get => EditorPrefs.GetBool(AutoIncludeScenePref, false);
            set => EditorPrefs.SetBool(AutoIncludeScenePref, value);
        }

        public bool AutoIncludeConsole
        {
            get => EditorPrefs.GetBool(AutoIncludeConsolePref, false);
            set => EditorPrefs.SetBool(AutoIncludeConsolePref, value);
        }

        public int ChatHistoryMax
        {
            get => EditorPrefs.GetInt(ChatHistoryMaxPref, 50);
            set => EditorPrefs.SetInt(ChatHistoryMaxPref, Mathf.Max(1, value));
        }

        private static ClaudeSettings _instance;
        public static ClaudeSettings Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new ClaudeSettings();
                return _instance;
            }
        }

        public static string GetDefaultSystemPrompt()
        {
            return "You are a Unity game development assistant for the project 'Elemental Siege'. " +
                   $"Unity version: {Application.unityVersion}. " +
                   "Help with C# scripting, shader code, game design, performance optimization, " +
                   "and Unity Editor workflows. Provide practical, working code examples. " +
                   "When suggesting code changes, use ```csharp code blocks.";
        }

        public void DrawSettingsGUI()
        {
            EditorGUILayout.LabelField("Claude AI Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // API Key (masked)
            EditorGUILayout.LabelField("API Key");
            string currentKey = ApiKey;
            string maskedKey = string.IsNullOrEmpty(currentKey)
                ? ""
                : new string('*', Mathf.Max(0, currentKey.Length - 4)) +
                  (currentKey.Length > 4 ? currentKey.Substring(currentKey.Length - 4) : currentKey);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(string.IsNullOrEmpty(maskedKey) ? "(not set)" : maskedKey);
            if (GUILayout.Button("Set API Key", GUILayout.Width(100)))
            {
                string newKey = EditorInputDialog.Show("Set API Key",
                    "Enter your Anthropic API key:", "");
                if (newKey != null)
                    ApiKey = newKey;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Model
            ModelIndex = EditorGUILayout.Popup("Default Model", ModelIndex, ModelDisplayNames);

            // Max Tokens
            MaxTokens = EditorGUILayout.IntSlider("Max Tokens", MaxTokens, 256, 8192);

            // Temperature
            Temperature = EditorGUILayout.Slider("Temperature", Temperature, 0f, 1f);

            EditorGUILayout.Space(5);

            // Auto-include toggles
            EditorGUILayout.LabelField("Auto-Include Context", EditorStyles.boldLabel);
            AutoIncludeSelection = EditorGUILayout.Toggle("Selected Object", AutoIncludeSelection);
            AutoIncludeScene = EditorGUILayout.Toggle("Scene Hierarchy", AutoIncludeScene);
            AutoIncludeConsole = EditorGUILayout.Toggle("Console Errors", AutoIncludeConsole);

            EditorGUILayout.Space(5);

            // Chat history max
            ChatHistoryMax = EditorGUILayout.IntSlider("Chat History Max", ChatHistoryMax, 5, 200);

            EditorGUILayout.Space(5);

            // System prompt
            EditorGUILayout.LabelField("System Prompt", EditorStyles.boldLabel);
            string prompt = SystemPrompt;
            string newPrompt = EditorGUILayout.TextArea(prompt, GUILayout.MinHeight(80));
            if (newPrompt != prompt)
                SystemPrompt = newPrompt;

            if (GUILayout.Button("Reset to Default"))
            {
                SystemPrompt = GetDefaultSystemPrompt();
            }
        }
    }

    /// <summary>
    /// Simple input dialog for the editor.
    /// </summary>
    public class EditorInputDialog : EditorWindow
    {
        private string _message;
        private string _inputText;
        private string _result;
        private bool _confirmed;
        private bool _closed;

        public static string Show(string title, string message, string defaultText)
        {
            var window = CreateInstance<EditorInputDialog>();
            window.titleContent = new GUIContent(title);
            window._message = message;
            window._inputText = defaultText;
            window._result = null;
            window._confirmed = false;
            window._closed = false;
            window.minSize = new Vector2(350, 130);
            window.maxSize = new Vector2(350, 130);
            window.ShowModalUtility();
            return window._result;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField(_message);
            EditorGUILayout.Space(5);
            _inputText = EditorGUILayout.TextField(_inputText);
            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Cancel", GUILayout.Width(80)))
            {
                _result = null;
                Close();
            }
            if (GUILayout.Button("OK", GUILayout.Width(80)))
            {
                _result = _inputText;
                Close();
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
