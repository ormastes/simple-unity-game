using UnityEngine;
using UnityEditor;

namespace ElementalSiege.Editor
{
    /// <summary>
    /// Debug menu items and utilities for development.
    /// All items live under "Elemental Siege/Debug/".
    /// </summary>
    public static class DebugTools
    {
        // ── Pref keys ────────────────────────────────────────────────
        private const string PrefAllLevelsUnlocked = "ElementalSiege_Debug_AllLevelsUnlocked";
        private const string PrefInvincibleOrbs = "ElementalSiege_Debug_InvincibleOrbs";
        private const string PrefShowColliders = "ElementalSiege_Debug_ShowColliders";

        // ══════════════════════════════════════════════════════════════
        // Unlock All Levels
        // ══════════════════════════════════════════════════════════════

        [MenuItem("Elemental Siege/Debug/Unlock All Levels")]
        public static void UnlockAllLevels()
        {
            PlayerPrefs.SetInt(PrefAllLevelsUnlocked, 1);
            PlayerPrefs.Save();
            Debug.Log("[DebugTools] All levels unlocked.");
        }

        [MenuItem("Elemental Siege/Debug/Unlock All Levels", true)]
        private static bool UnlockAllLevels_Validate()
        {
            return Application.isEditor || Debug.isDebugBuild;
        }

        // ══════════════════════════════════════════════════════════════
        // Reset Save Data
        // ══════════════════════════════════════════════════════════════

        [MenuItem("Elemental Siege/Debug/Reset Save Data")]
        public static void ResetSaveData()
        {
            if (!EditorUtility.DisplayDialog("Reset Save Data",
                "This will DELETE all player progress. Are you sure?",
                "Yes, Reset", "Cancel"))
                return;

            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();

            // Also clear any persistent data files
            string persistentPath = Application.persistentDataPath;
            string saveFile = System.IO.Path.Combine(persistentPath, "save.json");
            if (System.IO.File.Exists(saveFile))
            {
                System.IO.File.Delete(saveFile);
                Debug.Log("[DebugTools] Deleted save file: " + saveFile);
            }

            Debug.Log("[DebugTools] All save data reset.");
        }

        [MenuItem("Elemental Siege/Debug/Reset Save Data", true)]
        private static bool ResetSaveData_Validate()
        {
            return Application.isEditor || Debug.isDebugBuild;
        }

        // ══════════════════════════════════════════════════════════════
        // Give All Stars
        // ══════════════════════════════════════════════════════════════

        [MenuItem("Elemental Siege/Debug/Give All Stars")]
        public static void GiveAllStars()
        {
            // Set a high star count — actual game code should read this
            int maxStars = 999;
            PlayerPrefs.SetInt("ElementalSiege_TotalStars", maxStars);

            // Mark all levels as 3-star completed
            for (int world = 1; world <= 10; world++)
            {
                for (int level = 1; level <= 20; level++)
                {
                    string key = $"ElementalSiege_Stars_W{world}_L{level}";
                    PlayerPrefs.SetInt(key, 3);
                }
            }

            PlayerPrefs.Save();
            Debug.Log($"[DebugTools] Granted {maxStars} stars and 3-starred all levels.");
        }

        [MenuItem("Elemental Siege/Debug/Give All Stars", true)]
        private static bool GiveAllStars_Validate()
        {
            return Application.isEditor || Debug.isDebugBuild;
        }

        // ══════════════════════════════════════════════════════════════
        // Skip to Level
        // ══════════════════════════════════════════════════════════════

        [MenuItem("Elemental Siege/Debug/Skip to Level...")]
        public static void SkipToLevel()
        {
            SkipToLevelWindow.ShowWindow();
        }

        [MenuItem("Elemental Siege/Debug/Skip to Level...", true)]
        private static bool SkipToLevel_Validate()
        {
            return Application.isEditor || Debug.isDebugBuild;
        }

        // ══════════════════════════════════════════════════════════════
        // Toggle Invincible Orbs
        // ══════════════════════════════════════════════════════════════

        [MenuItem("Elemental Siege/Debug/Toggle Invincible Orbs")]
        public static void ToggleInvincibleOrbs()
        {
            bool current = PlayerPrefs.GetInt(PrefInvincibleOrbs, 0) == 1;
            bool toggled = !current;
            PlayerPrefs.SetInt(PrefInvincibleOrbs, toggled ? 1 : 0);
            PlayerPrefs.Save();
            Debug.Log("[DebugTools] Invincible orbs: " + (toggled ? "ON" : "OFF"));
        }

        [MenuItem("Elemental Siege/Debug/Toggle Invincible Orbs", true)]
        private static bool ToggleInvincibleOrbs_Validate()
        {
            return Application.isEditor || Debug.isDebugBuild;
        }

        // ══════════════════════════════════════════════════════════════
        // Show Colliders (toggle gizmo visibility)
        // ══════════════════════════════════════════════════════════════

        [MenuItem("Elemental Siege/Debug/Show Colliders")]
        public static void ToggleShowColliders()
        {
            bool current = EditorPrefs.GetBool(PrefShowColliders, false);
            bool toggled = !current;
            EditorPrefs.SetBool(PrefShowColliders, toggled);

            // Toggle Physics debug visualization
            if (toggled)
            {
                Physics.queriesHitTriggers = true;
                Debug.Log("[DebugTools] Collider gizmos: ON — " +
                    "enable Gizmos in Scene View to see them.");
            }
            else
            {
                Debug.Log("[DebugTools] Collider gizmos: OFF");
            }
        }

        [MenuItem("Elemental Siege/Debug/Show Colliders", true)]
        private static bool ToggleShowColliders_Validate()
        {
            return Application.isEditor || Debug.isDebugBuild;
        }

        // ══════════════════════════════════════════════════════════════
        // Collider Gizmo drawer
        // ══════════════════════════════════════════════════════════════

        [DrawGizmo(GizmoType.Active | GizmoType.NonSelected)]
        private static void DrawColliderGizmos(Collider collider, GizmoType gizmoType)
        {
            if (!EditorPrefs.GetBool(PrefShowColliders, false))
                return;

            Gizmos.color = collider.isTrigger
                ? new Color(0f, 1f, 0f, 0.3f)
                : new Color(0f, 0.5f, 1f, 0.3f);

            if (collider is BoxCollider box)
            {
                Gizmos.matrix = box.transform.localToWorldMatrix;
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else if (collider is SphereCollider sphere)
            {
                Gizmos.matrix = sphere.transform.localToWorldMatrix;
                Gizmos.DrawWireSphere(sphere.center, sphere.radius);
            }
            else if (collider is CapsuleCollider capsule)
            {
                Gizmos.matrix = capsule.transform.localToWorldMatrix;
                Gizmos.DrawWireSphere(capsule.center, capsule.radius);
            }
        }
    }

    // ── Skip-to-level popup window ───────────────────────────────────
    public class SkipToLevelWindow : EditorWindow
    {
        private int _world = 1;
        private int _level = 1;

        public static void ShowWindow()
        {
            var window = CreateInstance<SkipToLevelWindow>();
            window.titleContent = new GUIContent("Skip to Level");
            window.ShowUtility();
            window.minSize = new Vector2(250, 100);
            window.maxSize = new Vector2(250, 100);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Skip to Level", EditorStyles.boldLabel);
            _world = EditorGUILayout.IntSlider("World", _world, 1, 10);
            _level = EditorGUILayout.IntSlider("Level", _level, 1, 20);

            EditorGUILayout.Space(4);

            if (GUILayout.Button("Go"))
            {
                EditorPrefs.SetInt("ElementalSiege_SkipWorld", _world);
                EditorPrefs.SetInt("ElementalSiege_SkipLevel", _level);
                Debug.Log($"[DebugTools] Set skip target: World {_world}, Level {_level}. " +
                    "Enter Play Mode to jump there.");

                if (!EditorApplication.isPlaying)
                {
                    if (EditorUtility.DisplayDialog("Enter Play Mode?",
                        $"Skip to World {_world} Level {_level}?\n" +
                        "This will enter Play Mode.", "Play", "Just Set"))
                    {
                        EditorApplication.isPlaying = true;
                    }
                }

                Close();
            }
        }
    }
}
