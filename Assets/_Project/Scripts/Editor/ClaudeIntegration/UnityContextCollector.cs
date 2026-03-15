using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace ElementalSiege.Editor.ClaudeIntegration
{
    /// <summary>
    /// Gathers Unity Editor context information to send to Claude as part of the prompt.
    /// </summary>
    public static class UnityContextCollector
    {
        private static readonly List<LogEntry> _recentLogs = new List<LogEntry>();
        private static bool _logCallbackRegistered = false;
        private const int MaxLogEntries = 50;

        private struct LogEntry
        {
            public string message;
            public string stackTrace;
            public LogType type;
        }

        /// <summary>
        /// Ensures the log callback is registered to capture console messages.
        /// </summary>
        public static void EnsureLogCallbackRegistered()
        {
            if (!_logCallbackRegistered)
            {
                Application.logMessageReceived += OnLogMessageReceived;
                _logCallbackRegistered = true;
            }
        }

        private static void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            _recentLogs.Add(new LogEntry
            {
                message = condition,
                stackTrace = stackTrace,
                type = type
            });

            while (_recentLogs.Count > MaxLogEntries)
                _recentLogs.RemoveAt(0);
        }

        /// <summary>
        /// Returns information about the currently selected GameObject(s).
        /// </summary>
        public static string GetSelectedObjectInfo()
        {
            var selection = Selection.gameObjects;
            if (selection == null || selection.Length == 0)
                return "[No GameObject selected]";

            var sb = new StringBuilder();
            sb.AppendLine("=== Selected GameObjects ===");

            foreach (var go in selection)
            {
                sb.AppendLine($"\nGameObject: {go.name}");
                sb.AppendLine($"  Active: {go.activeSelf}");
                sb.AppendLine($"  Layer: {LayerMask.LayerToName(go.layer)}");
                sb.AppendLine($"  Tag: {go.tag}");
                sb.AppendLine($"  Static: {go.isStatic}");

                // Transform
                var t = go.transform;
                sb.AppendLine($"  Position: {t.position}");
                sb.AppendLine($"  Rotation: {t.eulerAngles}");
                sb.AppendLine($"  Scale: {t.localScale}");

                // Components
                sb.AppendLine("  Components:");
                var components = go.GetComponents<Component>();
                foreach (var comp in components)
                {
                    if (comp == null) continue;
                    sb.AppendLine($"    - {comp.GetType().Name}");
                }

                // Children
                if (t.childCount > 0)
                {
                    sb.AppendLine($"  Children ({t.childCount}):");
                    for (int i = 0; i < Mathf.Min(t.childCount, 20); i++)
                    {
                        sb.AppendLine($"    - {t.GetChild(i).name}");
                    }
                    if (t.childCount > 20)
                        sb.AppendLine($"    ... and {t.childCount - 20} more");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Returns the scene hierarchy as a formatted tree string.
        /// </summary>
        public static string GetSceneHierarchy(int maxDepth = 5)
        {
            var sb = new StringBuilder();
            var scene = SceneManager.GetActiveScene();
            sb.AppendLine($"=== Scene: {scene.name} ===");
            sb.AppendLine($"Path: {scene.path}");
            sb.AppendLine($"Root objects: {scene.rootCount}");
            sb.AppendLine();

            var rootObjects = scene.GetRootGameObjects();
            foreach (var root in rootObjects)
            {
                AppendHierarchy(sb, root.transform, 0, maxDepth);
            }

            return sb.ToString();
        }

        private static void AppendHierarchy(StringBuilder sb, Transform transform, int depth, int maxDepth)
        {
            if (depth > maxDepth)
            {
                sb.AppendLine(new string(' ', depth * 2) + "...");
                return;
            }

            string prefix = new string(' ', depth * 2);
            string activeMarker = transform.gameObject.activeSelf ? "" : " [inactive]";
            sb.AppendLine($"{prefix}- {transform.name}{activeMarker}");

            for (int i = 0; i < transform.childCount; i++)
            {
                AppendHierarchy(sb, transform.GetChild(i), depth + 1, maxDepth);
            }
        }

        /// <summary>
        /// Returns the last N console errors and warnings.
        /// </summary>
        public static string GetRecentErrors(int count = 10)
        {
            EnsureLogCallbackRegistered();

            var sb = new StringBuilder();
            sb.AppendLine("=== Recent Console Errors/Warnings ===");

            var errorLogs = new List<LogEntry>();
            for (int i = _recentLogs.Count - 1; i >= 0 && errorLogs.Count < count; i--)
            {
                if (_recentLogs[i].type == LogType.Error ||
                    _recentLogs[i].type == LogType.Exception ||
                    _recentLogs[i].type == LogType.Warning)
                {
                    errorLogs.Add(_recentLogs[i]);
                }
            }

            if (errorLogs.Count == 0)
            {
                sb.AppendLine("No recent errors or warnings.");
                return sb.ToString();
            }

            errorLogs.Reverse();
            foreach (var log in errorLogs)
            {
                string typeStr = log.type.ToString().ToUpper();
                sb.AppendLine($"[{typeStr}] {log.message}");
                if (!string.IsNullOrEmpty(log.stackTrace))
                {
                    // Only include first 3 lines of stack trace
                    string[] lines = log.stackTrace.Split('\n');
                    for (int i = 0; i < Mathf.Min(lines.Length, 3); i++)
                    {
                        if (!string.IsNullOrWhiteSpace(lines[i]))
                            sb.AppendLine($"  {lines[i].Trim()}");
                    }
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Gets the content of the currently selected script asset.
        /// </summary>
        public static string GetOpenScriptContent()
        {
            var selected = Selection.activeObject;
            if (selected == null)
                return "[No script selected]";

            string path = AssetDatabase.GetAssetPath(selected);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".cs"))
                return "[Selected asset is not a C# script]";

            string fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
                return $"[File not found: {path}]";

            var sb = new StringBuilder();
            sb.AppendLine($"=== Script: {Path.GetFileName(path)} ===");
            sb.AppendLine($"Path: {path}");
            sb.AppendLine();

            string content = File.ReadAllText(fullPath);
            // Limit content size to avoid huge prompts
            if (content.Length > 10000)
            {
                sb.AppendLine(content.Substring(0, 10000));
                sb.AppendLine($"\n... [Truncated, {content.Length - 10000} chars remaining]");
            }
            else
            {
                sb.AppendLine(content);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Lists all components on a GameObject with key properties.
        /// </summary>
        public static string GetComponentSummary(GameObject go)
        {
            if (go == null) return "[No GameObject provided]";

            var sb = new StringBuilder();
            sb.AppendLine($"=== Component Summary: {go.name} ===");

            var components = go.GetComponents<Component>();
            foreach (var comp in components)
            {
                if (comp == null)
                {
                    sb.AppendLine("  - [Missing Script]");
                    continue;
                }

                sb.AppendLine($"\n  {comp.GetType().Name}:");

                // Special handling for common component types
                if (comp is Renderer renderer)
                {
                    sb.AppendLine($"    Material: {(renderer.sharedMaterial != null ? renderer.sharedMaterial.name : "none")}");
                    sb.AppendLine($"    Enabled: {renderer.enabled}");
                    sb.AppendLine($"    Bounds: {renderer.bounds}");
                }
                else if (comp is Collider collider)
                {
                    sb.AppendLine($"    IsTrigger: {collider.isTrigger}");
                    sb.AppendLine($"    Enabled: {collider.enabled}");
                }
                else if (comp is Rigidbody rb)
                {
                    sb.AppendLine($"    Mass: {rb.mass}");
                    sb.AppendLine($"    UseGravity: {rb.useGravity}");
                    sb.AppendLine($"    IsKinematic: {rb.isKinematic}");
                }
                else if (comp is MonoBehaviour mb)
                {
                    sb.AppendLine($"    Enabled: {mb.enabled}");
                    sb.AppendLine($"    Script: {MonoScript.FromMonoBehaviour(mb)?.name ?? "unknown"}");
                }

                // List serialized fields via SerializedObject
                try
                {
                    var so = new SerializedObject(comp);
                    var prop = so.GetIterator();
                    int fieldCount = 0;
                    if (prop.NextVisible(true))
                    {
                        do
                        {
                            if (prop.name == "m_Script") continue;
                            if (fieldCount >= 10) // Limit fields shown
                            {
                                sb.AppendLine("    ... (more fields)");
                                break;
                            }
                            sb.AppendLine($"    {prop.name}: {GetSerializedPropertyValue(prop)}");
                            fieldCount++;
                        } while (prop.NextVisible(false));
                    }
                    so.Dispose();
                }
                catch
                {
                    // Silently skip if we can't read serialized properties
                }
            }

            return sb.ToString();
        }

        private static string GetSerializedPropertyValue(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer: return prop.intValue.ToString();
                case SerializedPropertyType.Boolean: return prop.boolValue.ToString();
                case SerializedPropertyType.Float: return prop.floatValue.ToString("F3");
                case SerializedPropertyType.String: return $"\"{prop.stringValue}\"";
                case SerializedPropertyType.Enum: return prop.enumDisplayNames != null && prop.enumValueIndex >= 0 && prop.enumValueIndex < prop.enumDisplayNames.Length
                    ? prop.enumDisplayNames[prop.enumValueIndex] : prop.enumValueIndex.ToString();
                case SerializedPropertyType.ObjectReference:
                    return prop.objectReferenceValue != null ? prop.objectReferenceValue.name : "null";
                case SerializedPropertyType.Vector2: return prop.vector2Value.ToString();
                case SerializedPropertyType.Vector3: return prop.vector3Value.ToString();
                case SerializedPropertyType.Color: return prop.colorValue.ToString();
                default: return $"({prop.propertyType})";
            }
        }

        /// <summary>
        /// Returns the folder tree of Assets/_Project/.
        /// </summary>
        public static string GetProjectStructure(int maxDepth = 3)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Project Structure ===");

            string projectPath = Path.Combine(Application.dataPath, "_Project");
            if (!Directory.Exists(projectPath))
            {
                sb.AppendLine("[Assets/_Project/ not found]");
                return sb.ToString();
            }

            AppendDirectoryTree(sb, projectPath, 0, maxDepth);
            return sb.ToString();
        }

        private static void AppendDirectoryTree(StringBuilder sb, string path, int depth, int maxDepth)
        {
            if (depth > maxDepth) return;

            string prefix = new string(' ', depth * 2);
            string dirName = Path.GetFileName(path);
            sb.AppendLine($"{prefix}{dirName}/");

            try
            {
                // Directories first
                foreach (var dir in Directory.GetDirectories(path))
                {
                    if (Path.GetFileName(dir).StartsWith(".")) continue;
                    AppendDirectoryTree(sb, dir, depth + 1, maxDepth);
                }

                // Then files (limited)
                var files = Directory.GetFiles(path);
                int shown = 0;
                foreach (var file in files)
                {
                    string fileName = Path.GetFileName(file);
                    if (fileName.EndsWith(".meta")) continue;
                    if (fileName.StartsWith(".")) continue;
                    if (shown >= 15)
                    {
                        sb.AppendLine($"{prefix}  ... and {files.Length - shown} more files");
                        break;
                    }
                    sb.AppendLine($"{prefix}  {fileName}");
                    shown++;
                }
            }
            catch
            {
                // Skip directories we can't access
            }
        }

        /// <summary>
        /// Combines multiple context strings into a clean context block.
        /// </summary>
        public static string FormatAsContext(params string[] contexts)
        {
            if (contexts == null || contexts.Length == 0)
                return "";

            var sb = new StringBuilder();
            sb.AppendLine("--- Unity Editor Context ---");
            sb.AppendLine();

            foreach (var context in contexts)
            {
                if (string.IsNullOrWhiteSpace(context)) continue;
                sb.AppendLine(context);
                sb.AppendLine();
            }

            sb.AppendLine("--- End Context ---");
            return sb.ToString();
        }
    }
}
