using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;

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
        private SerializedProperty _orbsProp;
        private SerializedProperty _star1Prop;
        private SerializedProperty _star2Prop;
        private SerializedProperty _star3Prop;
        private SerializedProperty _sceneNameProp;
        private SerializedProperty _levelNameProp;
        private Texture2D _thumbnailCache;

        private void OnEnable()
        {
            _orbsProp = serializedObject.FindProperty("orbs");
            _star1Prop = serializedObject.FindProperty("starThreshold1");
            _star2Prop = serializedObject.FindProperty("starThreshold2");
            _star3Prop = serializedObject.FindProperty("starThreshold3");
            _sceneNameProp = serializedObject.FindProperty("sceneName");
            _levelNameProp = serializedObject.FindProperty("levelName");

            if (_orbsProp != null)
            {
                _orbList = new ReorderableList(serializedObject, _orbsProp,
                    true, true, true, true);
                _orbList.drawHeaderCallback = rect =>
                    EditorGUI.LabelField(rect, "Orbs (drag to reorder)");
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

            // ── Preview section ──────────────────────────────────────
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            if (_levelNameProp != null)
                EditorGUILayout.PropertyField(_levelNameProp);
            if (_sceneNameProp != null)
                EditorGUILayout.PropertyField(_sceneNameProp);

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

            // ── Orb list ─────────────────────────────────────────────
            if (_orbList != null)
            {
                _orbList.DoLayoutList();
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Add an 'orbs' list field to CoreLevelData to enable the reorderable list.",
                    MessageType.Info);
            }

            // Validation: empty orb list
            if (_orbsProp != null && _orbsProp.arraySize == 0)
            {
                EditorGUILayout.HelpBox(
                    "Orb list is empty! The player will have nothing to launch.",
                    MessageType.Warning);
            }

            EditorGUILayout.Space(4);

            // ── Star thresholds ──────────────────────────────────────
            EditorGUILayout.LabelField("Star Thresholds", EditorStyles.boldLabel);

            if (_star1Prop != null) EditorGUILayout.PropertyField(_star1Prop, new GUIContent("1 Star"));
            if (_star2Prop != null) EditorGUILayout.PropertyField(_star2Prop, new GUIContent("2 Stars"));
            if (_star3Prop != null) EditorGUILayout.PropertyField(_star3Prop, new GUIContent("3 Stars"));

            // Validation: out of order
            if (_star1Prop != null && _star2Prop != null && _star3Prop != null)
            {
                int s1 = _star1Prop.intValue;
                int s2 = _star2Prop.intValue;
                int s3 = _star3Prop.intValue;

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
            if (_orbsProp == null || index >= _orbsProp.arraySize)
                return;

            var element = _orbsProp.GetArrayElementAtIndex(index);
            rect.y += 2;
            rect.height = EditorGUIUtility.singleLineHeight;

            // Element icon placeholder (colour dot)
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
                new Color(1f, 0.3f, 0.1f),   // Fire
                new Color(0.4f, 0.8f, 1f),    // Ice
                new Color(1f, 1f, 0.3f),      // Lightning
                new Color(0.6f, 0.4f, 0.2f),  // Earth
                new Color(0.7f, 1f, 0.7f),    // Wind
                new Color(0.2f, 0.4f, 1f),    // Water
            };
            return palette[index % palette.Length];
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // Stub runtime type — replace with the real CoreLevelData
    // ══════════════════════════════════════════════════════════════════

    public class CoreLevelData : ScriptableObject
    {
        public string levelName = "Untitled Level";
        public string sceneName;
        public List<Object> orbs = new List<Object>();
        public int starThreshold1 = 1000;
        public int starThreshold2 = 3000;
        public int starThreshold3 = 5000;
    }
}
