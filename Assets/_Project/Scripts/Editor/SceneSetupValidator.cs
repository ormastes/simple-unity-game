using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace ElementalSiege.Editor
{
    /// <summary>
    /// Validates scene setup on play and on scene load.
    /// Checks that required components exist in Gameplay and Boot scenes.
    /// </summary>
    [InitializeOnLoad]
    public static class SceneSetupValidator
    {
        private const string GameplaySceneSubstring = "Gameplay";
        private const string BootSceneSubstring = "Boot";

        static SceneSetupValidator()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        // ── Triggers ─────────────────────────────────────────────────

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                ValidateAllOpenScenes();
            }
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            ValidateScene(scene);
        }

        // ── Main validation ──────────────────────────────────────────

        private static void ValidateAllOpenScenes()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded)
                    ValidateScene(scene);
            }
        }

        private static void ValidateScene(Scene scene)
        {
            string name = scene.name;

            if (name.Contains(GameplaySceneSubstring))
                ValidateGameplayScene(scene);
            else if (name.Contains(BootSceneSubstring))
                ValidateBootScene(scene);
        }

        // ── Gameplay scene checks ────────────────────────────────────

        private static void ValidateGameplayScene(Scene scene)
        {
            string prefix = $"[SceneValidator] Gameplay scene '{scene.name}': ";
            bool allGood = true;

            // LevelManager
            if (!FindComponentInScene<MonoBehaviour>(scene, "LevelManager"))
            {
                Debug.LogWarning(prefix + "Missing 'LevelManager' component.");
                allGood = false;
            }

            // Catapult / Launcher
            if (!FindComponentInScene<MonoBehaviour>(scene, "Catapult") &&
                !FindComponentInScene<MonoBehaviour>(scene, "Launcher"))
            {
                Debug.LogWarning(prefix +
                    "Missing 'Catapult' or 'Launcher' component.");
                allGood = false;
            }

            // Canvas (UI)
            if (!FindComponentInScene<Canvas>(scene))
            {
                Debug.LogWarning(prefix + "Missing Canvas (UI).");
                allGood = false;
            }

            // Camera
            if (!FindComponentInScene<UnityEngine.Camera>(scene))
            {
                Debug.LogWarning(prefix + "Missing Camera.");
                allGood = false;
            }

            // EventSystem
            if (!FindComponentInScene<EventSystem>(scene))
            {
                Debug.LogWarning(prefix + "Missing EventSystem.");
                allGood = false;
            }

            // Validate prefab references on key components
            ValidatePrefabReferences(scene, prefix, ref allGood);

            if (allGood)
                Debug.Log(prefix + "All required components present.");
        }

        // ── Boot scene checks ────────────────────────────────────────

        private static void ValidateBootScene(Scene scene)
        {
            string prefix = $"[SceneValidator] Boot scene '{scene.name}': ";
            bool allGood = true;

            // GameManager
            if (!FindComponentInScene<MonoBehaviour>(scene, "GameManager"))
            {
                Debug.LogWarning(prefix + "Missing 'GameManager' component.");
                allGood = false;
            }

            if (allGood)
                Debug.Log(prefix + "All required components present.");
        }

        // ── Prefab reference validation ──────────────────────────────

        private static void ValidatePrefabReferences(Scene scene, string prefix,
            ref bool allGood)
        {
            var rootObjects = scene.GetRootGameObjects();

            foreach (var root in rootObjects)
            {
                var monos = root.GetComponentsInChildren<MonoBehaviour>(true);
                foreach (var mono in monos)
                {
                    if (mono == null) continue;

                    // Check serialized fields for null prefab references
                    var so = new SerializedObject(mono);
                    var prop = so.GetIterator();

                    while (prop.NextVisible(true))
                    {
                        if (prop.propertyType == SerializedPropertyType.ObjectReference)
                        {
                            // Check fields that look like prefab references
                            string fieldName = prop.name.ToLowerInvariant();
                            bool isPrefabField = fieldName.Contains("prefab") ||
                                                 fieldName.Contains("template");

                            if (isPrefabField && prop.objectReferenceValue == null)
                            {
                                Debug.LogWarning(prefix +
                                    $"Null prefab reference: {mono.GetType().Name}.{prop.name} " +
                                    $"on '{mono.gameObject.name}'.");
                                allGood = false;
                            }
                        }
                    }
                }
            }
        }

        // ── Utility: find component by exact type ────────────────────

        private static bool FindComponentInScene<T>(Scene scene) where T : Component
        {
            var rootObjects = scene.GetRootGameObjects();
            foreach (var root in rootObjects)
            {
                if (root.GetComponentInChildren<T>(true) != null)
                    return true;
            }
            return false;
        }

        // ── Utility: find MonoBehaviour by type name ─────────────────

        private static bool FindComponentInScene<T>(Scene scene, string typeName)
            where T : MonoBehaviour
        {
            var rootObjects = scene.GetRootGameObjects();
            foreach (var root in rootObjects)
            {
                var components = root.GetComponentsInChildren<T>(true);
                foreach (var comp in components)
                {
                    if (comp != null && comp.GetType().Name == typeName)
                        return true;
                }
            }
            return false;
        }
    }
}
