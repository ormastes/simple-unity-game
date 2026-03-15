using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace ElementalSiege.Editor
{
    /// <summary>
    /// Custom inspector for WorldProgression ScriptableObject.
    /// Displays visual progress bars per world, total star calculations,
    /// balance analysis, and impossibility warnings.
    /// </summary>
    [CustomEditor(typeof(WorldProgression))]
    public class WorldProgressionEditor : UnityEditor.Editor
    {
        private Vector2 _scrollPos;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var progression = (WorldProgression)target;

            EditorGUILayout.LabelField("World Progression", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (progression.worlds == null || progression.worlds.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No worlds defined. Add worlds to configure progression.",
                    MessageType.Info);
                DrawDefaultInspector();
                return;
            }

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            int totalStarsAvailable = 0;
            int totalStarsRequired = 0;

            for (int i = 0; i < progression.worlds.Count; i++)
            {
                var world = progression.worlds[i];
                DrawWorldEntry(world, i, ref totalStarsAvailable, ref totalStarsRequired);
                EditorGUILayout.Space(4);
            }

            EditorGUILayout.EndScrollView();

            // ── Summary ──────────────────────────────────────────────
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);

            EditorGUILayout.LabelField("Total Stars Available", totalStarsAvailable.ToString());
            EditorGUILayout.LabelField("Total Stars Required", totalStarsRequired.ToString());

            float overallRatio = totalStarsRequired > 0
                ? (float)totalStarsAvailable / totalStarsRequired
                : 1f;

            Rect summaryBar = EditorGUILayout.GetControlRect(false, 22);
            EditorGUI.ProgressBar(summaryBar,
                Mathf.Clamp01(overallRatio),
                $"Available / Required: {totalStarsAvailable} / {totalStarsRequired}");

            // ── Balance analysis ─────────────────────────────────────
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Balance Analysis", EditorStyles.boldLabel);

            if (overallRatio < 1f)
            {
                EditorGUILayout.HelpBox(
                    "IMPOSSIBLE PROGRESSION: Not enough stars available " +
                    $"({totalStarsAvailable}) to meet total requirements " +
                    $"({totalStarsRequired}). Players cannot complete the game!",
                    MessageType.Error);
            }
            else if (overallRatio < 1.2f)
            {
                EditorGUILayout.HelpBox(
                    "Tight balance: Players must earn most available stars to progress. " +
                    "Consider adding more levels or reducing requirements.",
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Progression balance looks healthy. Players have enough room for " +
                    "non-perfect runs.",
                    MessageType.Info);
            }

            // ── Per-world balance check ──────────────────────────────
            int cumulativeAvailable = 0;
            for (int i = 0; i < progression.worlds.Count; i++)
            {
                var world = progression.worlds[i];
                cumulativeAvailable += world.levelsInWorld * 3; // max 3 stars per level

                if (i + 1 < progression.worlds.Count)
                {
                    int nextRequired = progression.worlds[i + 1].starsToUnlock;
                    if (cumulativeAvailable < nextRequired)
                    {
                        EditorGUILayout.HelpBox(
                            $"World '{progression.worlds[i + 1].worldName}' requires " +
                            $"{nextRequired} stars but only {cumulativeAvailable} are " +
                            "available from previous worlds.",
                            MessageType.Error);
                    }
                }
            }

            // ── Worlds list (for adding/removing) ────────────────────
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Edit Worlds", EditorStyles.boldLabel);

            SerializedProperty worldsProp = serializedObject.FindProperty("worlds");
            if (worldsProp != null)
            {
                EditorGUILayout.PropertyField(worldsProp, new GUIContent("Worlds"), true);
            }
            else
            {
                DrawDefaultInspector();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawWorldEntry(WorldEntry world, int index,
            ref int totalAvailable, ref int totalRequired)
        {
            int starsAvailableInWorld = world.levelsInWorld * 3;
            totalAvailable += starsAvailableInWorld;
            totalRequired += world.starsToUnlock;

            // World header
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                $"World {index + 1}: {world.worldName}", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            // Stars requirement bar
            float ratio = starsAvailableInWorld > 0
                ? Mathf.Clamp01((float)world.starsToUnlock / starsAvailableInWorld)
                : 0f;

            Color barColor = ratio > 0.9f ? Color.red
                : ratio > 0.6f ? Color.yellow
                : Color.green;

            Rect barRect = EditorGUILayout.GetControlRect(false, 18);
            EditorGUI.DrawRect(barRect, new Color(0.15f, 0.15f, 0.15f));
            Rect fillRect = new Rect(barRect.x, barRect.y,
                barRect.width * Mathf.Clamp01(ratio), barRect.height);
            EditorGUI.DrawRect(fillRect, new Color(barColor.r, barColor.g, barColor.b, 0.7f));
            EditorGUI.LabelField(barRect,
                $"  Requires {world.starsToUnlock} stars to unlock",
                new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.white } });

            EditorGUILayout.LabelField(
                $"Levels: {world.levelsInWorld}  |  Stars available: {starsAvailableInWorld}");

            EditorGUILayout.EndVertical();
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // Stub runtime types — replace with actual game assembly references
    // ══════════════════════════════════════════════════════════════════

    [System.Serializable]
    public class WorldEntry
    {
        public string worldName = "World";
        public int starsToUnlock;
        public int levelsInWorld = 5;
    }

    public class WorldProgression : ScriptableObject
    {
        public List<WorldEntry> worlds = new List<WorldEntry>();
    }
}
