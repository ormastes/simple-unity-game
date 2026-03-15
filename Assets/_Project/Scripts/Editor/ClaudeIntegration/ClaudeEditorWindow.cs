using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace ElementalSiege.Editor.ClaudeIntegration
{
    /// <summary>
    /// Main EditorWindow for chatting with Claude AI from within the Unity Editor.
    /// Provides a full chat interface with context-aware interactions.
    /// </summary>
    public class ClaudeEditorWindow : EditorWindow
    {
        // ─────────────────────────────────────────────
        // Constants
        // ─────────────────────────────────────────────

        private const string ChatHistoryPref = "ClaudeIntegration_ChatHistory";
        private const string WindowTitle = "Claude Assistant";

        // ─────────────────────────────────────────────
        // Serialized state
        // ─────────────────────────────────────────────

        [Serializable]
        private class ChatHistory
        {
            public List<ChatMessage> messages = new List<ChatMessage>();
        }

        [Serializable]
        private class ChatMessage
        {
            public string role;     // "user" or "assistant"
            public string content;
            public string timestamp;
        }

        // ─────────────────────────────────────────────
        // Fields
        // ─────────────────────────────────────────────

        private ChatHistory _chatHistory = new ChatHistory();
        private string _inputText = "";
        private Vector2 _chatScrollPosition;
        private Vector2 _inputScrollPosition;
        private bool _showSettings;
        private bool _scrollToBottom;
        private string _statusMessage = "Ready";
        private string _pendingContext = "";
        private string _streamingText = "";
        private int _estimatedTokens;

        private ClaudeAPIClient _apiClient;
        private ClaudeSettings _settings;

        // Styles (initialized in OnGUI to avoid null references)
        private GUIStyle _userBubbleStyle;
        private GUIStyle _assistantBubbleStyle;
        private GUIStyle _codeBlockStyle;
        private GUIStyle _timestampStyle;
        private bool _stylesInitialized;

        // Code applicator state
        private Vector2 _diffScrollPosition;
        private List<CodeApplicator.CodeBlock> _detectedCodeBlocks;

        // ─────────────────────────────────────────────
        // Menu + Window lifecycle
        // ─────────────────────────────────────────────

        [MenuItem("Elemental Siege/Claude Assistant %#c")]  // Ctrl+Shift+C
        public static ClaudeEditorWindow ShowWindow()
        {
            var window = GetWindow<ClaudeEditorWindow>(WindowTitle);
            window.minSize = new Vector2(400, 500);
            return window;
        }

        private void OnEnable()
        {
            _apiClient = new ClaudeAPIClient();
            _settings = ClaudeSettings.Instance;
            LoadChatHistory();
            UnityContextCollector.EnsureLogCallbackRegistered();
        }

        private void OnDisable()
        {
            SaveChatHistory();
        }

        /// <summary>
        /// Called by QuickActions to set a pending message with context.
        /// </summary>
        public void SetPendingMessage(string question, string context)
        {
            _inputText = question;
            _pendingContext = context;
            _showSettings = false;
            Repaint();
        }

        // ─────────────────────────────────────────────
        // Styles initialization
        // ─────────────────────────────────────────────

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            _userBubbleStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 8, 8),
                margin = new RectOffset(50, 5, 3, 3),
                wordWrap = true,
                richText = true,
                fontSize = 12
            };

            _assistantBubbleStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 8, 8),
                margin = new RectOffset(5, 50, 3, 3),
                wordWrap = true,
                richText = true,
                fontSize = 12
            };

            _codeBlockStyle = new GUIStyle(EditorStyles.textArea)
            {
                padding = new RectOffset(8, 8, 6, 6),
                margin = new RectOffset(10, 10, 4, 4),
                wordWrap = false,
                richText = false,
                fontSize = 11,
                font = Font.CreateDynamicFontFromOSFont("Consolas", 11)
            };

            _timestampStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
                fontSize = 9
            };

            _stylesInitialized = true;
        }

        // ─────────────────────────────────────────────
        // Main GUI
        // ─────────────────────────────────────────────

        private void OnGUI()
        {
            InitializeStyles();

            if (_showSettings)
            {
                DrawSettingsPanel();
                return;
            }

            DrawToolbar();
            DrawChatArea();
            DrawContextButtons();
            DrawInputArea();
            DrawStatusBar();

            // Auto-scroll after adding messages
            if (_scrollToBottom)
            {
                _chatScrollPosition.y = float.MaxValue;
                _scrollToBottom = false;
                Repaint();
            }
        }

        // ─────────────────────────────────────────────
        // Toolbar
        // ─────────────────────────────────────────────

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // Model selector
            EditorGUILayout.LabelField("Model:", GUILayout.Width(45));
            int newModel = EditorGUILayout.Popup(
                _settings.ModelIndex,
                ClaudeSettings.ModelDisplayNames,
                EditorStyles.toolbarPopup,
                GUILayout.Width(120));
            if (newModel != _settings.ModelIndex)
                _settings.ModelIndex = newModel;

            GUILayout.FlexibleSpace();

            // Clear chat button
            if (GUILayout.Button("Clear Chat", EditorStyles.toolbarButton, GUILayout.Width(75)))
            {
                if (EditorUtility.DisplayDialog("Clear Chat",
                    "Are you sure you want to clear the chat history?", "Clear", "Cancel"))
                {
                    _chatHistory.messages.Clear();
                    SaveChatHistory();
                    _streamingText = "";
                    _detectedCodeBlocks = null;
                }
            }

            // Settings button
            if (GUILayout.Button("Settings", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                _showSettings = true;
            }

            EditorGUILayout.EndHorizontal();
        }

        // ─────────────────────────────────────────────
        // Settings panel
        // ─────────────────────────────────────────────

        private void DrawSettingsPanel()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Back to Chat", EditorStyles.toolbarButton, GUILayout.Width(90)))
            {
                _showSettings = false;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);
            _settings.DrawSettingsGUI();
        }

        // ─────────────────────────────────────────────
        // Chat area
        // ─────────────────────────────────────────────

        private void DrawChatArea()
        {
            _chatScrollPosition = EditorGUILayout.BeginScrollView(
                _chatScrollPosition,
                GUILayout.ExpandHeight(true));

            if (_chatHistory.messages.Count == 0 && string.IsNullOrEmpty(_streamingText))
            {
                EditorGUILayout.Space(20);
                GUIStyle centeredStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 14,
                    wordWrap = true
                };
                EditorGUILayout.LabelField(
                    "Welcome to Claude Assistant!\n\nAsk questions about your Unity project,\nget code suggestions, or debug issues.",
                    centeredStyle, GUILayout.Height(80));
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField(
                    "Tip: Use the context buttons below to include\nselection, script, scene, or console info.",
                    new GUIStyle(centeredStyle) { fontSize = 11 }, GUILayout.Height(40));
            }

            // Draw messages
            foreach (var message in _chatHistory.messages)
            {
                DrawMessageBubble(message);
            }

            // Draw streaming response if in progress
            if (!string.IsNullOrEmpty(_streamingText))
            {
                var streamMsg = new ChatMessage
                {
                    role = "assistant",
                    content = _streamingText + " ...",
                    timestamp = ""
                };
                DrawMessageBubble(streamMsg);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawMessageBubble(ChatMessage message)
        {
            bool isUser = message.role == "user";
            var style = isUser ? _userBubbleStyle : _assistantBubbleStyle;

            // Set background color
            var prevBgColor = GUI.backgroundColor;
            GUI.backgroundColor = isUser
                ? new Color(0.3f, 0.5f, 0.8f, 0.5f)
                : new Color(0.5f, 0.5f, 0.5f, 0.3f);

            EditorGUILayout.BeginVertical(style);

            // Role label
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                isUser ? "You" : "Claude",
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 10 },
                GUILayout.Width(50));
            GUILayout.FlexibleSpace();
            if (!string.IsNullOrEmpty(message.timestamp))
            {
                EditorGUILayout.LabelField(message.timestamp, _timestampStyle, GUILayout.Width(70));
            }
            EditorGUILayout.EndHorizontal();

            // Content - check for code blocks
            if (!isUser)
            {
                DrawAssistantContent(message.content);
            }
            else
            {
                EditorGUILayout.LabelField(message.content,
                    new GUIStyle(EditorStyles.wordWrappedLabel) { richText = true });
            }

            EditorGUILayout.EndVertical();
            GUI.backgroundColor = prevBgColor;
        }

        private void DrawAssistantContent(string content)
        {
            var codeBlocks = CodeApplicator.DetectCodeBlocks(content);

            if (codeBlocks.Count == 0)
            {
                EditorGUILayout.LabelField(content,
                    new GUIStyle(EditorStyles.wordWrappedLabel) { richText = true });
                return;
            }

            // Split content around code blocks and render each part
            int lastEnd = 0;
            foreach (var block in codeBlocks)
            {
                // Text before code block
                if (block.startIndex > lastEnd)
                {
                    string textBefore = content.Substring(lastEnd, block.startIndex - lastEnd).Trim();
                    if (!string.IsNullOrEmpty(textBefore))
                    {
                        EditorGUILayout.LabelField(textBefore,
                            new GUIStyle(EditorStyles.wordWrappedLabel) { richText = true });
                    }
                }

                // Code block
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(
                    $"Code ({block.language})" +
                    (block.DetectedClassName != null ? $" - {block.DetectedClassName}" : ""),
                    EditorStyles.miniLabel);

                // Display code with scroll
                float codeHeight = Mathf.Min(
                    block.code.Split('\n').Length * 15f + 20f, 200f);
                EditorGUILayout.TextArea(block.code, _codeBlockStyle,
                    GUILayout.Height(codeHeight));

                // Action buttons
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Copy", EditorStyles.miniButton, GUILayout.Width(50)))
                {
                    EditorGUIUtility.systemCopyBuffer = block.code;
                    _statusMessage = "Code copied to clipboard.";
                }

                if (block.language == "csharp" || block.language == "cs")
                {
                    string className = block.DetectedClassName;
                    if (!string.IsNullOrEmpty(className))
                    {
                        string existingPath = CodeApplicator.FindExistingScript(className);
                        if (existingPath != null)
                        {
                            if (GUILayout.Button($"Update {className}", EditorStyles.miniButton))
                            {
                                // For simplicity, replace entire file content
                                string fullPath = System.IO.Path.GetFullPath(existingPath);
                                if (System.IO.File.Exists(fullPath))
                                {
                                    string oldContent = System.IO.File.ReadAllText(fullPath);
                                    if (EditorUtility.DisplayDialog("Apply Code",
                                        $"Replace content of '{existingPath}'?", "Apply", "Cancel"))
                                    {
                                        // Backup for undo
                                        EditorPrefs.SetString("ClaudeIntegration_LastBackup",
                                            fullPath + ".claude-backup");
                                        EditorPrefs.SetString("ClaudeIntegration_LastModified",
                                            fullPath);
                                        System.IO.File.WriteAllText(fullPath + ".claude-backup",
                                            oldContent);
                                        System.IO.File.WriteAllText(fullPath, block.code);
                                        AssetDatabase.Refresh();
                                        _statusMessage = $"Updated {className}.cs";
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (GUILayout.Button($"Create {className}.cs", EditorStyles.miniButton))
                            {
                                if (CodeApplicator.CreateScript(className, block.code))
                                {
                                    _statusMessage = $"Created {className}.cs";
                                }
                            }
                        }
                    }
                }

                GUILayout.FlexibleSpace();

                // Undo button
                if (!string.IsNullOrEmpty(
                    EditorPrefs.GetString("ClaudeIntegration_LastBackup", "")))
                {
                    if (GUILayout.Button("Undo Last", EditorStyles.miniButton, GUILayout.Width(70)))
                    {
                        CodeApplicator.UndoLastModification();
                        _statusMessage = "Undo successful.";
                    }
                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();

                lastEnd = block.endIndex;
            }

            // Text after last code block
            if (lastEnd < content.Length)
            {
                string textAfter = content.Substring(lastEnd).Trim();
                if (!string.IsNullOrEmpty(textAfter))
                {
                    EditorGUILayout.LabelField(textAfter,
                        new GUIStyle(EditorStyles.wordWrappedLabel) { richText = true });
                }
            }
        }

        // ─────────────────────────────────────────────
        // Context buttons
        // ─────────────────────────────────────────────

        private void DrawContextButtons()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Add Context:", GUILayout.Width(80));

            if (GUILayout.Button("Selection", EditorStyles.miniButton))
            {
                string info = UnityContextCollector.GetSelectedObjectInfo();
                _pendingContext += "\n" + info;
                _statusMessage = "Added selection context.";
            }

            if (GUILayout.Button("Script", EditorStyles.miniButton))
            {
                string info = UnityContextCollector.GetOpenScriptContent();
                _pendingContext += "\n" + info;
                _statusMessage = "Added script context.";
            }

            if (GUILayout.Button("Scene", EditorStyles.miniButton))
            {
                string info = UnityContextCollector.GetSceneHierarchy();
                _pendingContext += "\n" + info;
                _statusMessage = "Added scene context.";
            }

            if (GUILayout.Button("Console", EditorStyles.miniButton))
            {
                string info = UnityContextCollector.GetRecentErrors();
                _pendingContext += "\n" + info;
                _statusMessage = "Added console errors context.";
            }

            GUILayout.FlexibleSpace();

            if (!string.IsNullOrEmpty(_pendingContext))
            {
                GUI.backgroundColor = new Color(0.3f, 0.7f, 0.3f, 0.5f);
                if (GUILayout.Button("Clear Context", EditorStyles.miniButton, GUILayout.Width(90)))
                {
                    _pendingContext = "";
                    _statusMessage = "Context cleared.";
                }
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.EndHorizontal();
        }

        // ─────────────────────────────────────────────
        // Input area
        // ─────────────────────────────────────────────

        private void DrawInputArea()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Pending context indicator
            if (!string.IsNullOrEmpty(_pendingContext))
            {
                var contextStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    fontStyle = FontStyle.Italic
                };
                int contextLines = _pendingContext.Split('\n').Length;
                EditorGUILayout.LabelField(
                    $"Context attached ({contextLines} lines)",
                    contextStyle);
            }

            // Text input
            EditorGUILayout.BeginHorizontal();

            // Handle Enter key
            Event e = Event.current;
            bool sendOnEnter = e.type == EventType.KeyDown &&
                               e.keyCode == KeyCode.Return &&
                               !e.shift &&
                               GUI.GetNameOfFocusedControl() == "ChatInput";

            GUI.SetNextControlName("ChatInput");
            _inputScrollPosition = EditorGUILayout.BeginScrollView(
                _inputScrollPosition, GUILayout.Height(60));
            _inputText = EditorGUILayout.TextArea(_inputText,
                new GUIStyle(EditorStyles.textArea) { wordWrap = true },
                GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            // Send button
            EditorGUI.BeginDisabledGroup(
                _apiClient.IsRequestInProgress || string.IsNullOrWhiteSpace(_inputText));
            if (GUILayout.Button("Send", GUILayout.Width(60), GUILayout.Height(60)) || sendOnEnter)
            {
                SendCurrentMessage();
            }
            EditorGUI.EndDisabledGroup();

            // Cancel button (shown during request)
            if (_apiClient.IsRequestInProgress)
            {
                if (GUILayout.Button("Stop", GUILayout.Width(45), GUILayout.Height(60)))
                {
                    _apiClient.CancelRequest();
                    _statusMessage = "Request cancelled.";
                    if (!string.IsNullOrEmpty(_streamingText))
                    {
                        AddMessage("assistant", _streamingText + "\n\n[Cancelled]");
                        _streamingText = "";
                    }
                }
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            // Consume the Enter key event to prevent it from adding a newline
            if (sendOnEnter)
            {
                e.Use();
            }
        }

        // ─────────────────────────────────────────────
        // Status bar
        // ─────────────────────────────────────────────

        private void DrawStatusBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // Token estimate
            _estimatedTokens = ClaudeAPIClient.EstimateTokenCount(
                _inputText + _pendingContext);
            EditorGUILayout.LabelField(
                $"Tokens: ~{_estimatedTokens}",
                EditorStyles.miniLabel, GUILayout.Width(80));

            // Model
            EditorGUILayout.LabelField(
                _settings.ModelDisplayName,
                EditorStyles.miniLabel, GUILayout.Width(100));

            GUILayout.FlexibleSpace();

            // Connection status
            string statusIcon = _apiClient.IsRequestInProgress ? "[Sending...]" :
                string.IsNullOrEmpty(_settings.ApiKey) ? "[No API Key]" : "[Connected]";
            EditorGUILayout.LabelField(
                $"{statusIcon} {_statusMessage}",
                EditorStyles.miniLabel);

            EditorGUILayout.EndHorizontal();
        }

        // ─────────────────────────────────────────────
        // Message handling
        // ─────────────────────────────────────────────

        private void SendCurrentMessage()
        {
            if (string.IsNullOrWhiteSpace(_inputText)) return;

            string message = _inputText.Trim();

            // Build auto-context
            string autoContext = "";
            if (_settings.AutoIncludeSelection)
                autoContext += UnityContextCollector.GetSelectedObjectInfo() + "\n";
            if (_settings.AutoIncludeScene)
                autoContext += UnityContextCollector.GetSceneHierarchy() + "\n";
            if (_settings.AutoIncludeConsole)
                autoContext += UnityContextCollector.GetRecentErrors() + "\n";

            // Combine pending + auto context
            string fullContext = (_pendingContext + "\n" + autoContext).Trim();
            string fullMessage = message;
            if (!string.IsNullOrEmpty(fullContext))
            {
                fullMessage = fullContext + "\n\n" + message;
            }

            // Add user message to chat (show only the question, not the full context)
            AddMessage("user", message);
            _inputText = "";
            _pendingContext = "";
            _statusMessage = "Sending...";
            _streamingText = "";
            _scrollToBottom = true;

            // Build conversation history for API
            var apiHistory = new List<ClaudeAPIClient.Message>();
            // Include recent history (limited by settings)
            int startIdx = Mathf.Max(0,
                _chatHistory.messages.Count - _settings.ChatHistoryMax - 1);
            for (int i = startIdx; i < _chatHistory.messages.Count - 1; i++) // -1 to exclude the message we just added
            {
                var msg = _chatHistory.messages[i];
                apiHistory.Add(new ClaudeAPIClient.Message(msg.role, msg.content));
            }

            // Send with streaming
            _apiClient.SendMessageStreaming(
                fullMessage,
                _settings.SystemPrompt,
                apiHistory,
                onStreamChunk: (chunk) =>
                {
                    _streamingText += chunk;
                    Repaint();
                },
                onComplete: (response) =>
                {
                    _streamingText = "";
                    AddMessage("assistant", response);
                    _statusMessage = "Ready";
                    _scrollToBottom = true;
                    Repaint();
                },
                onError: (error) =>
                {
                    _streamingText = "";
                    _statusMessage = $"Error: {error}";
                    AddMessage("assistant", $"[Error: {error}]");
                    _scrollToBottom = true;
                    Repaint();
                });

            Repaint();
        }

        private void AddMessage(string role, string content)
        {
            _chatHistory.messages.Add(new ChatMessage
            {
                role = role,
                content = content,
                timestamp = DateTime.Now.ToString("HH:mm")
            });

            // Enforce max history
            while (_chatHistory.messages.Count > _settings.ChatHistoryMax)
            {
                _chatHistory.messages.RemoveAt(0);
            }

            SaveChatHistory();
        }

        // ─────────────────────────────────────────────
        // Persistence
        // ─────────────────────────────────────────────

        private void SaveChatHistory()
        {
            try
            {
                string json = JsonUtility.ToJson(_chatHistory);
                EditorPrefs.SetString(ChatHistoryPref, json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ClaudeIntegration] Failed to save chat history: {e.Message}");
            }
        }

        private void LoadChatHistory()
        {
            try
            {
                string json = EditorPrefs.GetString(ChatHistoryPref, "");
                if (!string.IsNullOrEmpty(json))
                {
                    _chatHistory = JsonUtility.FromJson<ChatHistory>(json);
                    if (_chatHistory == null)
                        _chatHistory = new ChatHistory();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ClaudeIntegration] Failed to load chat history: {e.Message}");
                _chatHistory = new ChatHistory();
            }
        }
    }
}
