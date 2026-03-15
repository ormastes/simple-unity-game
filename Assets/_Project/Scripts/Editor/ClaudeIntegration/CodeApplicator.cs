using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;

namespace ElementalSiege.Editor.ClaudeIntegration
{
    /// <summary>
    /// Detects and applies code suggestions from Claude's responses.
    /// Supports creating new scripts, modifying existing ones, and undo.
    /// </summary>
    public static class CodeApplicator
    {
        /// <summary>
        /// Represents a detected code block from Claude's response.
        /// </summary>
        public class CodeBlock
        {
            public string language;
            public string code;
            public string fileName;
            public int startIndex;
            public int endIndex;

            /// <summary>
            /// Attempts to extract the class/struct name from C# code.
            /// </summary>
            public string DetectedClassName
            {
                get
                {
                    if (!string.IsNullOrEmpty(fileName))
                        return Path.GetFileNameWithoutExtension(fileName);

                    // Try to find class/struct name
                    var classMatch = Regex.Match(code, @"(?:public\s+)?(?:class|struct|interface|enum)\s+(\w+)");
                    if (classMatch.Success)
                        return classMatch.Groups[1].Value;

                    return null;
                }
            }
        }

        /// <summary>
        /// Represents a diff between old and new code for preview.
        /// </summary>
        public class DiffResult
        {
            public List<DiffLine> lines = new List<DiffLine>();
        }

        public class DiffLine
        {
            public enum DiffType { Unchanged, Added, Removed }
            public DiffType type;
            public string content;

            public DiffLine(DiffType type, string content)
            {
                this.type = type;
                this.content = content;
            }
        }

        /// <summary>
        /// Finds all code blocks (```language ... ```) in Claude's response.
        /// </summary>
        public static List<CodeBlock> DetectCodeBlocks(string response)
        {
            var blocks = new List<CodeBlock>();
            if (string.IsNullOrEmpty(response)) return blocks;

            // Pattern: ```language\ncode\n``` with optional filename comment
            var pattern = new Regex(
                @"```(\w+)?(?:\s*//\s*(\S+\.cs))?\s*\n(.*?)```",
                RegexOptions.Singleline);

            var matches = pattern.Matches(response);
            foreach (Match match in matches)
            {
                var block = new CodeBlock
                {
                    language = match.Groups[1].Success ? match.Groups[1].Value : "text",
                    fileName = match.Groups[2].Success ? match.Groups[2].Value : null,
                    code = match.Groups[3].Value.TrimEnd(),
                    startIndex = match.Index,
                    endIndex = match.Index + match.Length
                };

                blocks.Add(block);
            }

            return blocks;
        }

        /// <summary>
        /// Creates a new C# script file at the appropriate project path.
        /// </summary>
        public static bool CreateScript(string className, string code, string subfolder = null)
        {
            if (string.IsNullOrEmpty(className))
            {
                Debug.LogError("[ClaudeIntegration] Cannot create script: class name is empty.");
                return false;
            }

            // Determine path
            string basePath = "Assets/_Project/Scripts";
            if (!string.IsNullOrEmpty(subfolder))
                basePath = Path.Combine(basePath, subfolder);

            // Ensure directory exists
            string fullDirPath = Path.Combine(Application.dataPath, "..", basePath);
            fullDirPath = Path.GetFullPath(fullDirPath);
            if (!Directory.Exists(fullDirPath))
                Directory.CreateDirectory(fullDirPath);

            string filePath = Path.Combine(basePath, $"{className}.cs");
            string fullFilePath = Path.Combine(Application.dataPath, "..", filePath);
            fullFilePath = Path.GetFullPath(fullFilePath);

            // Check if file already exists
            if (File.Exists(fullFilePath))
            {
                if (!EditorUtility.DisplayDialog(
                    "File Exists",
                    $"The file '{filePath}' already exists. Overwrite?",
                    "Overwrite", "Cancel"))
                {
                    return false;
                }
            }

            try
            {
                File.WriteAllText(fullFilePath, code);
                AssetDatabase.Refresh();
                Debug.Log($"[ClaudeIntegration] Created script: {filePath}");

                // Select the newly created asset
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(filePath);
                if (asset != null)
                    Selection.activeObject = asset;

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ClaudeIntegration] Failed to create script: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Modifies an existing script by replacing old code with new code.
        /// Supports undo via file backup.
        /// </summary>
        public static bool ModifyScript(string filePath, string oldCode, string newCode)
        {
            string fullPath = Path.Combine(Application.dataPath, "..", filePath);
            fullPath = Path.GetFullPath(fullPath);

            if (!File.Exists(fullPath))
            {
                Debug.LogError($"[ClaudeIntegration] File not found: {filePath}");
                return false;
            }

            try
            {
                string content = File.ReadAllText(fullPath);

                if (!content.Contains(oldCode))
                {
                    Debug.LogWarning($"[ClaudeIntegration] Could not find the code to replace in {filePath}. " +
                                     "The file may have been modified since the suggestion was made.");
                    return false;
                }

                // Create backup for undo
                string backupPath = fullPath + ".claude-backup";
                File.WriteAllText(backupPath, content);

                // Store backup info in EditorPrefs for undo
                EditorPrefs.SetString("ClaudeIntegration_LastBackup", backupPath);
                EditorPrefs.SetString("ClaudeIntegration_LastModified", fullPath);

                // Apply modification
                string newContent = content.Replace(oldCode, newCode);
                File.WriteAllText(fullPath, newContent);
                AssetDatabase.Refresh();

                Debug.Log($"[ClaudeIntegration] Modified script: {filePath}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ClaudeIntegration] Failed to modify script: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Undoes the last code modification by restoring from backup.
        /// </summary>
        public static bool UndoLastModification()
        {
            string backupPath = EditorPrefs.GetString("ClaudeIntegration_LastBackup", "");
            string modifiedPath = EditorPrefs.GetString("ClaudeIntegration_LastModified", "");

            if (string.IsNullOrEmpty(backupPath) || string.IsNullOrEmpty(modifiedPath))
            {
                Debug.LogWarning("[ClaudeIntegration] No modification to undo.");
                return false;
            }

            if (!File.Exists(backupPath))
            {
                Debug.LogWarning("[ClaudeIntegration] Backup file not found.");
                return false;
            }

            try
            {
                string backupContent = File.ReadAllText(backupPath);
                File.WriteAllText(modifiedPath, backupContent);
                File.Delete(backupPath);

                EditorPrefs.DeleteKey("ClaudeIntegration_LastBackup");
                EditorPrefs.DeleteKey("ClaudeIntegration_LastModified");

                AssetDatabase.Refresh();
                Debug.Log("[ClaudeIntegration] Undo successful.");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ClaudeIntegration] Failed to undo: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Generates a simple diff between old and new content for preview.
        /// </summary>
        public static DiffResult GenerateDiff(string oldContent, string newContent)
        {
            var result = new DiffResult();

            string[] oldLines = oldContent.Split('\n');
            string[] newLines = newContent.Split('\n');

            // Simple line-by-line diff (not a full LCS diff, but good enough for preview)
            int oldIdx = 0, newIdx = 0;

            while (oldIdx < oldLines.Length || newIdx < newLines.Length)
            {
                if (oldIdx >= oldLines.Length)
                {
                    // Remaining new lines are additions
                    result.lines.Add(new DiffLine(DiffLine.DiffType.Added, newLines[newIdx]));
                    newIdx++;
                }
                else if (newIdx >= newLines.Length)
                {
                    // Remaining old lines are removals
                    result.lines.Add(new DiffLine(DiffLine.DiffType.Removed, oldLines[oldIdx]));
                    oldIdx++;
                }
                else if (oldLines[oldIdx].Trim() == newLines[newIdx].Trim())
                {
                    result.lines.Add(new DiffLine(DiffLine.DiffType.Unchanged, oldLines[oldIdx]));
                    oldIdx++;
                    newIdx++;
                }
                else
                {
                    // Check if the old line appears later in new (was something inserted?)
                    bool foundInNew = false;
                    for (int lookAhead = newIdx + 1;
                         lookAhead < Mathf.Min(newIdx + 5, newLines.Length);
                         lookAhead++)
                    {
                        if (oldLines[oldIdx].Trim() == newLines[lookAhead].Trim())
                        {
                            // Lines were inserted before this point
                            while (newIdx < lookAhead)
                            {
                                result.lines.Add(new DiffLine(DiffLine.DiffType.Added, newLines[newIdx]));
                                newIdx++;
                            }
                            foundInNew = true;
                            break;
                        }
                    }

                    if (!foundInNew)
                    {
                        // Check if the new line appears later in old (was something removed?)
                        bool foundInOld = false;
                        for (int lookAhead = oldIdx + 1;
                             lookAhead < Mathf.Min(oldIdx + 5, oldLines.Length);
                             lookAhead++)
                        {
                            if (newLines[newIdx].Trim() == oldLines[lookAhead].Trim())
                            {
                                while (oldIdx < lookAhead)
                                {
                                    result.lines.Add(
                                        new DiffLine(DiffLine.DiffType.Removed, oldLines[oldIdx]));
                                    oldIdx++;
                                }
                                foundInOld = true;
                                break;
                            }
                        }

                        if (!foundInOld)
                        {
                            // Line was changed
                            result.lines.Add(new DiffLine(DiffLine.DiffType.Removed, oldLines[oldIdx]));
                            result.lines.Add(new DiffLine(DiffLine.DiffType.Added, newLines[newIdx]));
                            oldIdx++;
                            newIdx++;
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Draws a diff preview in the editor GUI.
        /// </summary>
        public static void DrawDiffPreview(DiffResult diff, ref Vector2 scrollPosition)
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.MaxHeight(300));

            var defaultColor = GUI.backgroundColor;
            var style = new GUIStyle(EditorStyles.label)
            {
                font = Font.CreateDynamicFontFromOSFont("Consolas", 12),
                richText = false,
                wordWrap = false
            };

            foreach (var line in diff.lines)
            {
                switch (line.type)
                {
                    case DiffLine.DiffType.Added:
                        GUI.backgroundColor = new Color(0.2f, 0.6f, 0.2f, 0.3f);
                        EditorGUILayout.LabelField("+ " + line.content, style);
                        break;
                    case DiffLine.DiffType.Removed:
                        GUI.backgroundColor = new Color(0.6f, 0.2f, 0.2f, 0.3f);
                        EditorGUILayout.LabelField("- " + line.content, style);
                        break;
                    default:
                        GUI.backgroundColor = defaultColor;
                        EditorGUILayout.LabelField("  " + line.content, style);
                        break;
                }
            }

            GUI.backgroundColor = defaultColor;
            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// Finds existing scripts that match a class name.
        /// </summary>
        public static string FindExistingScript(string className)
        {
            string[] guids = AssetDatabase.FindAssets($"t:MonoScript {className}");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == className)
                    return path;
            }
            return null;
        }
    }
}
