using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using ElementalSiege.Core;

namespace ElementalSiege.Editor
{
    /// <summary>
    /// Custom inspector for SaveManager MonoBehaviour.
    /// Displays visual progress bars per world based on WorldUnlockRequirement
    /// entries, total star calculations, balance analysis, and impossibility warnings.
    /// </summary>
    [CustomEditor(typeof(SaveManager))]
    public class WorldProgressionEditor : UnityEditor.Editor
    {
        private Vector2 _scrollPos;
        private SerializedProperty _worldRequirementsProp;

        private void OnEnable()
        {
            _worldRequirementsProp = serializedObject.FindProperty("_worldRequirements");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("World Progression", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (_worldRequirementsProp == null || _worldRequirementsProp.arraySize == 0)
            {
                EditorGUILayout.HelpBox(
                    "No worlds defined. Add world requirements to configure progression.",
                    MessageType.Info);
                DrawDefaultInspector();
                serializedObject.ApplyModifiedProperties();
                return;
            }

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            int totalStarsAvailable = 0;
            int totalStarsRequired = 0;

            for (int i = 0; i < _worldRequirementsProp.arraySize; i++)
            {
                var worldProp = _worldRequirementsProp.GetArrayElementAtIndex(i);
                DrawWorldEntry(worldProp, i, ref totalStarsAvailable, ref totalStarsRequired);
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
            for (int i = 0; i < _worldRequirementsProp.arraySize; i++)
            {
                var worldProp = _worldRequirementsProp.GetArrayElementAtIndex(i);
                var levelIdsProp = worldProp.FindPropertyRelative("LevelIds");
                int levelsInWorld = (levelIdsProp != null) ? levelIdsProp.arraySize : 0;
                cumulativeAvailable += levelsInWorld * 3; // max 3 stars per level

                if (i + 1 < _worldRequirementsProp.arraySize)
                {
                    var nextWorldProp = _worldRequirementsProp.GetArrayElementAtIndex(i + 1);
                    var nextWorldNameProp = nextWorldProp.FindPropertyRelative("WorldName");
                    var nextStarsReqProp = nextWorldProp.FindPropertyRelative("StarsRequired");

                    string nextWorldName = nextWorldNameProp != null
                        ? nextWorldNameProp.stringValue : $"World {i + 2}";
                    int nextRequired = nextStarsReqProp != null
                        ? nextStarsReqProp.intValue : 0;

                    if (cumulativeAvailable < nextRequired)
                    {
                        EditorGUILayout.HelpBox(
                            $"World '{nextWorldName}' requires " +
                            $"{nextRequired} stars but only {cumulativeAvailable} are " +
                            "available from previous worlds.",
                            MessageType.Error);
                    }
                }
            }

            // ── Worlds list (for adding/removing) ────────────────────
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Edit World Requirements", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_worldRequirementsProp,
                new GUIContent("World Requirements"), true);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawWorldEntry(SerializedProperty worldProp, int index,
            ref int totalAvailable, ref int totalRequired)
        {
            var worldNameProp = worldProp.FindPropertyRelative("WorldName");
            var starsRequiredProp = worldProp.FindPropertyRelative("StarsRequired");
            var levelIdsProp = worldProp.FindPropertyRelative("LevelIds");

            string worldName = worldNameProp != null ? worldNameProp.stringValue : $"World {index + 1}";
            int starsToUnlock = starsRequiredProp != null ? starsRequiredProp.intValue : 0;
            int levelsInWorld = (levelIdsProp != null) ? levelIdsProp.arraySize : 0;

            int starsAvailableInWorld = levelsInWorld * 3;
            totalAvailable += starsAvailableInWorld;
            totalRequired += starsToUnlock;

            // World header
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                $"World {index + 1}: {worldName}", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            // Stars requirement bar
            float ratio = starsAvailableInWorld > 0
                ? Mathf.Clamp01((float)starsToUnlock / starsAvailableInWorld)
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
                $"  Requires {starsToUnlock} stars to unlock",
                new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.white } });

            EditorGUILayout.LabelField(
                $"Levels: {levelsInWorld}  |  Stars available: {starsAvailableInWorld}");

            EditorGUILayout.EndVertical();
        }
    }
}
