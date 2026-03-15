using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEngine.Networking;

namespace ElementalSiege.Editor.ClaudeIntegration
{
    /// <summary>
    /// HTTP client for communicating with the Claude API.
    /// Uses UnityWebRequest with EditorApplication.update for async operations.
    /// </summary>
    public class ClaudeAPIClient
    {
        private const string ApiUrl = "https://api.anthropic.com/v1/messages";
        private const string ApiVersion = "2023-06-01";

        private bool _isRequestInProgress;
        private UnityWebRequest _currentRequest;
        private Action<string> _onComplete;
        private Action<string> _onError;
        private Action<string> _onStreamChunk;
        private StringBuilder _streamBuffer;

        public bool IsRequestInProgress => _isRequestInProgress;

        [Serializable]
        public class Message
        {
            public string role;
            public string content;

            public Message() { }

            public Message(string role, string content)
            {
                this.role = role;
                this.content = content;
            }
        }

        [Serializable]
        private class ApiRequest
        {
            public string model;
            public int max_tokens;
            public float temperature;
            public string system;
            public ApiMessage[] messages;
            public bool stream;
        }

        [Serializable]
        private class ApiMessage
        {
            public string role;
            public string content;
        }

        [Serializable]
        private class ApiResponse
        {
            public ApiContent[] content;
            public ApiUsage usage;
            public string stop_reason;
        }

        [Serializable]
        private class ApiContent
        {
            public string type;
            public string text;
        }

        [Serializable]
        private class ApiUsage
        {
            public int input_tokens;
            public int output_tokens;
        }

        [Serializable]
        private class ApiError
        {
            public ApiErrorDetail error;
        }

        [Serializable]
        private class ApiErrorDetail
        {
            public string type;
            public string message;
        }

        // SSE event parsing classes
        [Serializable]
        private class StreamEventContentBlockDelta
        {
            public StreamDelta delta;
        }

        [Serializable]
        private class StreamDelta
        {
            public string type;
            public string text;
        }

        /// <summary>
        /// Sends a message to the Claude API (non-streaming).
        /// </summary>
        public void SendMessage(
            string userMessage,
            string systemPrompt,
            List<Message> history,
            Action<string> onComplete,
            Action<string> onError)
        {
            if (_isRequestInProgress)
            {
                onError?.Invoke("A request is already in progress.");
                return;
            }

            var settings = ClaudeSettings.Instance;
            string apiKey = settings.ApiKey;

            if (string.IsNullOrEmpty(apiKey))
            {
                onError?.Invoke("API key is not set. Please configure it in Settings.");
                return;
            }

            _onComplete = onComplete;
            _onError = onError;
            _isRequestInProgress = true;

            // Build messages array
            var messages = new List<ApiMessage>();
            if (history != null)
            {
                foreach (var msg in history)
                {
                    messages.Add(new ApiMessage { role = msg.role, content = msg.content });
                }
            }
            messages.Add(new ApiMessage { role = "user", content = userMessage });

            var requestBody = new ApiRequest
            {
                model = settings.ModelApiId,
                max_tokens = settings.MaxTokens,
                temperature = settings.Temperature,
                system = systemPrompt ?? settings.SystemPrompt,
                messages = messages.ToArray(),
                stream = false
            };

            string jsonBody = JsonUtility.ToJson(requestBody);

            _currentRequest = new UnityWebRequest(ApiUrl, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            _currentRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            _currentRequest.downloadHandler = new DownloadHandlerBuffer();
            _currentRequest.SetRequestHeader("Content-Type", "application/json");
            _currentRequest.SetRequestHeader("x-api-key", apiKey);
            _currentRequest.SetRequestHeader("anthropic-version", ApiVersion);
            _currentRequest.timeout = 120;

            _currentRequest.SendWebRequest();
            EditorApplication.update += PollNonStreamingRequest;
        }

        /// <summary>
        /// Sends a message to the Claude API with streaming (SSE).
        /// </summary>
        public void SendMessageStreaming(
            string userMessage,
            string systemPrompt,
            List<Message> history,
            Action<string> onStreamChunk,
            Action<string> onComplete,
            Action<string> onError)
        {
            if (_isRequestInProgress)
            {
                onError?.Invoke("A request is already in progress.");
                return;
            }

            var settings = ClaudeSettings.Instance;
            string apiKey = settings.ApiKey;

            if (string.IsNullOrEmpty(apiKey))
            {
                onError?.Invoke("API key is not set. Please configure it in Settings.");
                return;
            }

            _onStreamChunk = onStreamChunk;
            _onComplete = onComplete;
            _onError = onError;
            _isRequestInProgress = true;
            _streamBuffer = new StringBuilder();

            // Build messages array
            var messages = new List<ApiMessage>();
            if (history != null)
            {
                foreach (var msg in history)
                {
                    messages.Add(new ApiMessage { role = msg.role, content = msg.content });
                }
            }
            messages.Add(new ApiMessage { role = "user", content = userMessage });

            var requestBody = new ApiRequest
            {
                model = settings.ModelApiId,
                max_tokens = settings.MaxTokens,
                temperature = settings.Temperature,
                system = systemPrompt ?? settings.SystemPrompt,
                messages = messages.ToArray(),
                stream = true
            };

            string jsonBody = JsonUtility.ToJson(requestBody);

            _currentRequest = new UnityWebRequest(ApiUrl, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            _currentRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            _currentRequest.downloadHandler = new DownloadHandlerBuffer();
            _currentRequest.SetRequestHeader("Content-Type", "application/json");
            _currentRequest.SetRequestHeader("x-api-key", apiKey);
            _currentRequest.SetRequestHeader("anthropic-version", ApiVersion);
            _currentRequest.timeout = 120;

            _currentRequest.SendWebRequest();
            EditorApplication.update += PollStreamingRequest;
        }

        private void PollNonStreamingRequest()
        {
            if (_currentRequest == null || !_currentRequest.isDone) return;

            EditorApplication.update -= PollNonStreamingRequest;
            _isRequestInProgress = false;

            if (_currentRequest.result == UnityWebRequest.Result.ConnectionError ||
                _currentRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                string errorMessage = ParseErrorMessage(_currentRequest);
                _onError?.Invoke(errorMessage);
            }
            else
            {
                string responseText = _currentRequest.downloadHandler.text;
                try
                {
                    var response = JsonUtility.FromJson<ApiResponse>(responseText);
                    if (response.content != null && response.content.Length > 0)
                    {
                        _onComplete?.Invoke(response.content[0].text);
                    }
                    else
                    {
                        _onError?.Invoke("Empty response from API.");
                    }
                }
                catch (Exception e)
                {
                    _onError?.Invoke($"Failed to parse response: {e.Message}");
                }
            }

            _currentRequest.Dispose();
            _currentRequest = null;
        }

        private int _lastProcessedLength = 0;

        private void PollStreamingRequest()
        {
            if (_currentRequest == null) return;

            // Check for errors
            if (_currentRequest.result == UnityWebRequest.Result.ConnectionError ||
                _currentRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                if (_currentRequest.isDone)
                {
                    EditorApplication.update -= PollStreamingRequest;
                    _isRequestInProgress = false;
                    string errorMessage = ParseErrorMessage(_currentRequest);
                    _onError?.Invoke(errorMessage);
                    _currentRequest.Dispose();
                    _currentRequest = null;
                    _lastProcessedLength = 0;
                }
                return;
            }

            // Process any new data
            string currentData = _currentRequest.downloadHandler?.text ?? "";
            if (currentData.Length > _lastProcessedLength)
            {
                string newData = currentData.Substring(_lastProcessedLength);
                _lastProcessedLength = currentData.Length;
                ProcessSSEData(newData);
            }

            // Check if complete
            if (_currentRequest.isDone)
            {
                EditorApplication.update -= PollStreamingRequest;
                _isRequestInProgress = false;
                _onComplete?.Invoke(_streamBuffer.ToString());
                _currentRequest.Dispose();
                _currentRequest = null;
                _lastProcessedLength = 0;
            }
        }

        private void ProcessSSEData(string data)
        {
            string[] lines = data.Split('\n');
            foreach (string line in lines)
            {
                if (!line.StartsWith("data: ")) continue;
                string jsonData = line.Substring(6).Trim();

                if (jsonData == "[DONE]") continue;

                try
                {
                    // Check if this is a content_block_delta event
                    if (jsonData.Contains("\"content_block_delta\"") || jsonData.Contains("\"text_delta\""))
                    {
                        // Extract the text from the delta
                        var delta = JsonUtility.FromJson<StreamEventContentBlockDelta>(jsonData);
                        if (delta?.delta?.text != null)
                        {
                            _streamBuffer.Append(delta.delta.text);
                            _onStreamChunk?.Invoke(delta.delta.text);
                        }
                    }
                }
                catch
                {
                    // Skip malformed SSE lines
                }
            }
        }

        private string ParseErrorMessage(UnityWebRequest request)
        {
            string responseBody = request.downloadHandler?.text ?? "";

            // Check for rate limiting
            if (request.responseCode == 429)
            {
                return "Rate limited by Claude API. Please wait a moment and try again.";
            }

            // Check for auth errors
            if (request.responseCode == 401)
            {
                return "Authentication failed. Please check your API key in Settings.";
            }

            // Try to parse API error response
            try
            {
                var apiError = JsonUtility.FromJson<ApiError>(responseBody);
                if (apiError?.error != null)
                {
                    return $"API Error ({apiError.error.type}): {apiError.error.message}";
                }
            }
            catch
            {
                // Ignore parse errors
            }

            return $"Request failed ({request.responseCode}): {request.error}";
        }

        /// <summary>
        /// Cancels the current in-progress request.
        /// </summary>
        public void CancelRequest()
        {
            if (!_isRequestInProgress) return;

            EditorApplication.update -= PollNonStreamingRequest;
            EditorApplication.update -= PollStreamingRequest;

            if (_currentRequest != null)
            {
                _currentRequest.Abort();
                _currentRequest.Dispose();
                _currentRequest = null;
            }

            _isRequestInProgress = false;
            _lastProcessedLength = 0;

            // If streaming, return what we have so far
            if (_streamBuffer != null && _streamBuffer.Length > 0)
            {
                _onComplete?.Invoke(_streamBuffer.ToString() + "\n\n[Response cancelled]");
            }
        }

        /// <summary>
        /// Estimates token count for a string (rough approximation: ~4 chars per token).
        /// </summary>
        public static int EstimateTokenCount(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return Mathf.CeilToInt(text.Length / 4f);
        }
    }
}
