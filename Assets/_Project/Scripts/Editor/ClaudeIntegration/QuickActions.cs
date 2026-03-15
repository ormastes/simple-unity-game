using UnityEngine;
using UnityEditor;

namespace ElementalSiege.Editor.ClaudeIntegration
{
    /// <summary>
    /// Context-menu integrations for quick Claude AI interactions.
    /// Provides right-click menu items for GameObjects, Components, Scripts, and Scenes.
    /// </summary>
    public static class QuickActions
    {
        // ─────────────────────────────────────────────
        // GameObject context menu
        // ─────────────────────────────────────────────

        [MenuItem("GameObject/Ask Claude about this object", false, 49)]
        private static void AskClaudeAboutGameObject()
        {
            var go = Selection.activeGameObject;
            if (go == null) return;

            string context = UnityContextCollector.GetSelectedObjectInfo();
            string componentInfo = UnityContextCollector.GetComponentSummary(go);
            string fullContext = UnityContextCollector.FormatAsContext(context, componentInfo);

            OpenWindowWithContext(
                $"Tell me about the GameObject '{go.name}'. What is its purpose based on its components and hierarchy position? Are there any potential issues or improvements?",
                fullContext);
        }

        [MenuItem("GameObject/Ask Claude about this object", true)]
        private static bool AskClaudeAboutGameObjectValidate()
        {
            return Selection.activeGameObject != null;
        }

        // ─────────────────────────────────────────────
        // Component context menu (via CONTEXT)
        // ─────────────────────────────────────────────

        [MenuItem("CONTEXT/Component/Ask Claude to optimize this component")]
        private static void AskClaudeToOptimizeComponent(MenuCommand command)
        {
            var component = command.context as Component;
            if (component == null) return;

            string componentType = component.GetType().Name;
            string goName = component.gameObject.name;
            string context = UnityContextCollector.GetComponentSummary(component.gameObject);
            string fullContext = UnityContextCollector.FormatAsContext(context);

            OpenWindowWithContext(
                $"Please review and suggest optimizations for the '{componentType}' component on '{goName}'. " +
                "Focus on performance, best practices, and potential issues.",
                fullContext);
        }

        [MenuItem("CONTEXT/MonoBehaviour/Ask Claude to review this script")]
        private static void AskClaudeToReviewComponentScript(MenuCommand command)
        {
            var mb = command.context as MonoBehaviour;
            if (mb == null) return;

            var script = MonoScript.FromMonoBehaviour(mb);
            if (script == null) return;

            string scriptPath = AssetDatabase.GetAssetPath(script);
            string scriptContent = "";
            if (!string.IsNullOrEmpty(scriptPath))
            {
                string fullPath = System.IO.Path.GetFullPath(scriptPath);
                if (System.IO.File.Exists(fullPath))
                    scriptContent = System.IO.File.ReadAllText(fullPath);
            }

            string context = UnityContextCollector.FormatAsContext(
                $"=== Script: {script.name} ===\nPath: {scriptPath}\n\n{scriptContent}");

            OpenWindowWithContext(
                $"Please review the script '{script.name}' for code quality, performance, and Unity best practices. " +
                "Suggest improvements with code examples.",
                context);
        }

        // ─────────────────────────────────────────────
        // Script asset context menu (Project window)
        // ─────────────────────────────────────────────

        [MenuItem("Assets/Ask Claude to review this script", false, 1000)]
        private static void AskClaudeToReviewScript()
        {
            var selected = Selection.activeObject;
            if (selected == null) return;

            string path = AssetDatabase.GetAssetPath(selected);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".cs")) return;

            string fullPath = System.IO.Path.GetFullPath(path);
            string content = "";
            if (System.IO.File.Exists(fullPath))
                content = System.IO.File.ReadAllText(fullPath);

            string context = UnityContextCollector.FormatAsContext(
                $"=== Script: {System.IO.Path.GetFileName(path)} ===\nPath: {path}\n\n{content}");

            OpenWindowWithContext(
                $"Please review the script '{System.IO.Path.GetFileName(path)}'. " +
                "Check for bugs, performance issues, Unity best practices violations, and suggest improvements. " +
                "Provide corrected code using ```csharp code blocks.",
                context);
        }

        [MenuItem("Assets/Ask Claude to review this script", true)]
        private static bool AskClaudeToReviewScriptValidate()
        {
            var selected = Selection.activeObject;
            if (selected == null) return false;
            string path = AssetDatabase.GetAssetPath(selected);
            return !string.IsNullOrEmpty(path) && path.EndsWith(".cs");
        }

        // ─────────────────────────────────────────────
        // Scene context menu
        // ─────────────────────────────────────────────

        [MenuItem("Assets/Ask Claude about scene setup", false, 1001)]
        private static void AskClaudeAboutScene()
        {
            var selected = Selection.activeObject;
            if (selected == null) return;

            string path = AssetDatabase.GetAssetPath(selected);
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(path);

            // We can only get hierarchy if the scene is loaded
            string hierarchy = UnityContextCollector.GetSceneHierarchy();
            string context = UnityContextCollector.FormatAsContext(
                $"=== Scene: {sceneName} ===\nPath: {path}",
                hierarchy);

            OpenWindowWithContext(
                $"Please review the scene '{sceneName}' setup. " +
                "Analyze the hierarchy organization, suggest improvements for performance, " +
                "and identify any common setup issues.",
                context);
        }

        [MenuItem("Assets/Ask Claude about scene setup", true)]
        private static bool AskClaudeAboutSceneValidate()
        {
            var selected = Selection.activeObject;
            if (selected == null) return false;
            string path = AssetDatabase.GetAssetPath(selected);
            return !string.IsNullOrEmpty(path) && path.EndsWith(".unity");
        }

        // ─────────────────────────────────────────────
        // Helper
        // ─────────────────────────────────────────────

        /// <summary>
        /// Opens the Claude Editor Window with pre-filled context and question.
        /// </summary>
        private static void OpenWindowWithContext(string question, string context)
        {
            var window = ClaudeEditorWindow.ShowWindow();
            window.SetPendingMessage(question, context);
        }
    }
}
