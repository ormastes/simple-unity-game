using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using ElementalSiege.Core;
using ElementalSiege.Elements;

namespace ElementalSiege.Editor
{
    /// <summary>
    /// Custom inspector for CoreLevelData ScriptableObject.
    /// Shows a layout preview, reorderable orb list, star threshold
    /// calculator, and quick-action buttons.
    /// </summary>
    [CustomEditor(typeof(CoreLevelData))]
    public class CoreLevelDataEditor : UnityEditor.Editor
    {
        private ReorderableList _orbList;
        private SerializedProperty _availableOrbsProp;
        private SerializedProperty _starThresholdsProp;
        private SerializedProperty _levelNameProp;
        private SerializedProperty _levelIdProp;
        private SerializedProperty _worldIndexProp;
        private SerializedProperty _levelIndexProp;
        private SerializedProperty _difficultyProp;
        private SerializedProperty _levelBoundsProp;
        private SerializedProperty _tutorialIdProp;
        private Texture2D _thumbnailCache;

        private void OnEnable()
        {
            _levelIdProp = serializedObject.FindProperty("_levelId");
            _levelNameProp = serializedObject.FindProperty("_levelName");
            _worldIndexProp = serializedObject.FindProperty("_worldIndex");
            _levelIndexProp = serializedObject.FindProperty("_levelIndex");
            _availableOrbsProp = serializedObject.FindProperty("_availableOrbs");
            _starThresholdsProp = serializedObject.FindProperty("_starThresholds");
            _difficultyProp = serializedObject.FindProperty("_difficulty");
            _levelBoundsProp = serializedObject.FindProperty("_levelBounds");
            _tutorialIdProp = serializedObject.FindProperty("_tutorialId");

            if (_availableOrbsProp != null)
            {
                _orbList = new ReorderableList(serializedObject, _availableOrbsProp,
                    true, true, true, true);
                _orbList.drawHeaderCallback = rect =>
                    EditorGUI.LabelField(rect, "Available Orbs (drag to reorder)");
                _orbList.drawElementCallback = DrawOrbElement;
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var levelData = (CoreLevelData)target;

            // ── Header ───────────────────────────────────────────────
            EditorGUILayout.LabelField("Core Level Data", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // ── Identity section ─────────────────────────────────────
            EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
            if (_levelIdProp != null)
                EditorGUILayout.PropertyField(_levelIdProp, new GUIContent("Level ID"));
            if (_levelNameProp != null)
                EditorGUILayout.PropertyField(_levelNameProp, new GUIContent("Level Name"));
            if (_worldIndexProp != null)
                EditorGUILayout.PropertyField(_worldIndexProp, new GUIContent("World Index"));
            if (_levelIndexProp != null)
                EditorGUILayout.PropertyField(_levelIndexProp, new GUIContent("Level Index"));

            EditorGUILayout.Space(4);

            // ── Preview section ──────────────────────────────────────
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

            if (_thumbnailCache != null)
            {
                Rect thumbRect = GUILayoutUtility.GetRect(200, 120);
                GUI.DrawTexture(thumbRect, _thumbnailCache, ScaleMode.ScaleToFit);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "No layout thumbnail available. Open the scene to generate one.",
                    MessageType.None);
            }

            EditorGUILayout.Space(4);

            // ── Gameplay section ─────────────────────────────────────
            EditorGUILayout.LabelField("Gameplay", EditorStyles.boldLabel);

            if (_difficultyProp != null)
                EditorGUILayout.PropertyField(_difficultyProp, new GUIContent("Difficulty"));

            // ── Orb list ─────────────────────────────────────────────
            if (_orbList != null)
            {
                _orbList.DoLayoutList();
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Could not find '_availableOrbs' property on CoreLevelData.",
                    MessageType.Info);
            }

            // Validation: empty orb list
            if (_availableOrbsProp != null && _availableOrbsProp.arraySize == 0)
            {
                EditorGUILayout.HelpBox(
                    "Orb list is empty! The player will have nothing to launch.",
                    MessageType.Warning);
            }

            EditorGUILayout.Space(4);

            // ── Star thresholds ──────────────────────────────────────
            EditorGUILayout.LabelField("Star Thresholds", EditorStyles.boldLabel);

            if (_starThresholdsProp != null)
            {
                // Ensure array has exactly 3 elements
                if (_starThresholdsProp.arraySize != 3)
                {
                    _starThresholdsProp.arraySize = 3;
                }

                var s1Prop = _starThresholdsProp.GetArrayElementAtIndex(0);
                var s2Prop = _starThresholdsProp.GetArrayElementAtIndex(1);
                var s3Prop = _starThresholdsProp.GetArrayElementAtIndex(2);

                EditorGUILayout.PropertyField(s1Prop, new GUIContent("1 Star"));
                EditorGUILayout.PropertyField(s2Prop, new GUIContent("2 Stars"));
                EditorGUILayout.PropertyField(s3Prop, new GUIContent("3 Stars"));

                int s1 = s1Prop.intValue;
                int s2 = s2Prop.intValue;
                int s3 = s3Prop.intValue;

                if (s1 >= s2 || s2 >= s3)
                {
                    EditorGUILayout.HelpBox(
                        "Star thresholds must be in ascending order (1 < 2 < 3).",
                        MessageType.Error);
                }

                // Estimated difficulty
                float avgThreshold = (s1 + s2 + s3) / 3f;
                string difficulty = avgThreshold < 2000 ? "Easy"
                    : avgThreshold < 4000 ? "Medium"
                    : avgThreshold < 6000 ? "Hard"
                    : "Very Hard";

                EditorGUILayout.LabelField("Estimated Difficulty", difficulty);
            }

            EditorGUILayout.Space(4);

            // ── Layout section ───────────────────────────────────────
            EditorGUILayout.LabelField("Layout", EditorStyles.boldLabel);
            if (_levelBoundsProp != null)
                EditorGUILayout.PropertyField(_levelBoundsProp, new GUIContent("Level Bounds"));

            EditorGUILayout.Space(4);

            // ── Tutorial section ─────────────────────────────────────
            EditorGUILayout.LabelField("Tutorial", EditorStyles.boldLabel);
            if (_tutorialIdProp != null)
                EditorGUILayout.PropertyField(_tutorialIdProp, new GUIContent("Tutorial ID"));

            EditorGUILayout.LabelField("Has Tutorial", levelData.HasTutorial.ToString());

            EditorGUILayout.Space(8);

            // ── Action buttons ───────────────────────────────────────
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Open in Level Editor", GUILayout.Height(28)))
            {
                var window = EditorWindow.GetWindow<LevelEditorWindow>("Level Editor");
                window.Show();
            }

            if (GUILayout.Button("Play Test", GUILayout.Height(28)))
            {
                if (EditorApplication.isPlaying)
                {
                    Debug.LogWarning("[CoreLevelData] Already in play mode.");
                }
                else
                {
                    // Set the active level and enter play mode
                    EditorPrefs.SetString("ElementalSiege_TestLevel",
                        AssetDatabase.GetAssetPath(target));
                    EditorApplication.isPlaying = true;
                }
            }

            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawOrbElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            if (_availableOrbsProp == null || index >= _availableOrbsProp.arraySize)
                return;

            var element = _availableOrbsProp.GetArrayElementAtIndex(index);
            rect.y += 2;
            rect.height = EditorGUIUtility.singleLineHeight;

            // Element icon placeholder (colour dot based on ElementCategory)
            Rect iconRect = new Rect(rect.x, rect.y, 16, 16);
            EditorGUI.DrawRect(iconRect, GetOrbColor(index));

            Rect fieldRect = new Rect(rect.x + 22, rect.y,
                rect.width - 22, rect.height);
            EditorGUI.PropertyField(fieldRect, element, GUIContent.none);
        }

        private Color GetOrbColor(int index)
        {
            Color[] palette =
            {
                new Color(0.6f, 0.4f, 0.2f),  // Stone
                new Color(1f, 0.3f, 0.1f),    // Fire
                new Color(0.4f, 0.8f, 1f),    // Ice
                new Color(1f, 1f, 0.3f),      // Lightning
                new Color(0.7f, 1f, 0.7f),    // Wind
                new Color(0.9f, 0.5f, 0.9f),  // Crystal
                new Color(0.5f, 0.3f, 0.7f),  // Gravity
                new Color(0.3f, 0.1f, 0.4f),  // Void
            };
            return palette[index % palette.Length];
        }
    }
}
