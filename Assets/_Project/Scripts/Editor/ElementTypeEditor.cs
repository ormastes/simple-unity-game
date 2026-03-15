using UnityEngine;
using UnityEditor;
using ElementalSiege.Elements;

namespace ElementalSiege.Editor
{
    /// <summary>
    /// Custom inspector for ElementType ScriptableObject.
    /// Displays a large colour swatch, icon preview, stats overview,
    /// scene-preview button, and associated prefab quick-access.
    /// </summary>
    [CustomEditor(typeof(ElementType))]
    public class ElementTypeEditor : UnityEditor.Editor
    {
        private SerializedProperty _elementNameProp;
        private SerializedProperty _categoryProp;
        private SerializedProperty _descriptionProp;
        private SerializedProperty _primaryColorProp;
        private SerializedProperty _secondaryColorProp;
        private SerializedProperty _iconProp;
        private SerializedProperty _orbPrefabProp;
        private SerializedProperty _impactEffectPrefabProp;
        private SerializedProperty _launchSoundProp;
        private SerializedProperty _impactSoundProp;
        private SerializedProperty _abilitySoundProp;
        private SerializedProperty _baseDamageProp;
        private SerializedProperty _abilityRadiusProp;

        private void OnEnable()
        {
            _elementNameProp = serializedObject.FindProperty("elementName");
            _categoryProp = serializedObject.FindProperty("category");
            _descriptionProp = serializedObject.FindProperty("description");
            _primaryColorProp = serializedObject.FindProperty("primaryColor");
            _secondaryColorProp = serializedObject.FindProperty("secondaryColor");
            _iconProp = serializedObject.FindProperty("icon");
            _orbPrefabProp = serializedObject.FindProperty("orbPrefab");
            _impactEffectPrefabProp = serializedObject.FindProperty("impactEffectPrefab");
            _launchSoundProp = serializedObject.FindProperty("launchSound");
            _impactSoundProp = serializedObject.FindProperty("impactSound");
            _abilitySoundProp = serializedObject.FindProperty("abilitySound");
            _baseDamageProp = serializedObject.FindProperty("baseDamage");
            _abilityRadiusProp = serializedObject.FindProperty("abilityRadius");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var element = (ElementType)target;

            // ── Header with element name ─────────────────────────────
            EditorGUILayout.LabelField("Element Type", EditorStyles.boldLabel);
            if (_elementNameProp != null)
                EditorGUILayout.PropertyField(_elementNameProp, new GUIContent("Name"));
            if (_categoryProp != null)
                EditorGUILayout.PropertyField(_categoryProp, new GUIContent("Category"));
            if (_descriptionProp != null)
                EditorGUILayout.PropertyField(_descriptionProp, new GUIContent("Description"));

            EditorGUILayout.Space(4);

            // ── Large colour swatch ──────────────────────────────────
            EditorGUILayout.LabelField("Colours", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            {
                // Primary colour
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField("Primary", GUILayout.Width(70));
                Rect primaryRect = EditorGUILayout.GetControlRect(false, 48, GUILayout.Width(80));
                EditorGUI.DrawRect(primaryRect, element.PrimaryColor);
                if (_primaryColorProp != null)
                    EditorGUILayout.PropertyField(_primaryColorProp, GUIContent.none,
                        GUILayout.Width(80));
                EditorGUILayout.EndVertical();

                GUILayout.Space(8);

                // Secondary colour
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField("Secondary", GUILayout.Width(70));
                Rect secondaryRect = EditorGUILayout.GetControlRect(false, 48, GUILayout.Width(80));
                EditorGUI.DrawRect(secondaryRect, element.SecondaryColor);
                if (_secondaryColorProp != null)
                    EditorGUILayout.PropertyField(_secondaryColorProp, GUIContent.none,
                        GUILayout.Width(80));
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // ── Icon preview ─────────────────────────────────────────
            EditorGUILayout.LabelField("Icon", EditorStyles.boldLabel);
            if (_iconProp != null)
                EditorGUILayout.PropertyField(_iconProp, new GUIContent("Icon Sprite"));

            if (element.Icon != null)
            {
                Rect iconRect = EditorGUILayout.GetControlRect(false, 64);
                iconRect.width = 64;
                Texture2D tex = AssetPreview.GetAssetPreview(element.Icon);
                if (tex != null)
                    GUI.DrawTexture(iconRect, tex, ScaleMode.ScaleToFit);
            }

            EditorGUILayout.Space(4);

            // ── Stats overview ───────────────────────────────────────
            EditorGUILayout.LabelField("Stats", EditorStyles.boldLabel);
            if (_baseDamageProp != null)
                EditorGUILayout.PropertyField(_baseDamageProp, new GUIContent("Base Damage"));
            if (_abilityRadiusProp != null)
                EditorGUILayout.PropertyField(_abilityRadiusProp, new GUIContent("Ability Radius"));

            // Visual stat bars
            DrawStatBar("Damage", element.BaseDamage, 100f, Color.red);
            DrawStatBar("Radius", element.AbilityRadius, 20f, Color.cyan);

            EditorGUILayout.Space(4);

            // ── Prefabs ──────────────────────────────────────────────
            EditorGUILayout.LabelField("Prefabs", EditorStyles.boldLabel);
            if (_orbPrefabProp != null)
                EditorGUILayout.PropertyField(_orbPrefabProp, new GUIContent("Orb Prefab"));
            if (_impactEffectPrefabProp != null)
                EditorGUILayout.PropertyField(_impactEffectPrefabProp,
                    new GUIContent("Impact Effect Prefab"));

            if (element.OrbPrefab != null)
            {
                if (GUILayout.Button("Select Prefab in Project", GUILayout.Height(22)))
                {
                    EditorGUIUtility.PingObject(element.OrbPrefab);
                    Selection.activeObject = element.OrbPrefab;
                }
            }

            EditorGUILayout.Space(4);

            // ── Audio ────────────────────────────────────────────────
            EditorGUILayout.LabelField("Audio", EditorStyles.boldLabel);
            if (_launchSoundProp != null)
                EditorGUILayout.PropertyField(_launchSoundProp, new GUIContent("Launch Sound"));
            if (_impactSoundProp != null)
                EditorGUILayout.PropertyField(_impactSoundProp, new GUIContent("Impact Sound"));
            if (_abilitySoundProp != null)
                EditorGUILayout.PropertyField(_abilitySoundProp, new GUIContent("Ability Sound"));

            EditorGUILayout.Space(8);

            // ── Preview in Scene ─────────────────────────────────────
            if (GUILayout.Button("Preview in Scene", GUILayout.Height(28)))
            {
                SpawnTestOrb(element);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawStatBar(string label, float value, float maxValue, Color color)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 16);
            float ratio = Mathf.Clamp01(value / maxValue);

            // Background
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f));

            // Filled portion
            Rect fillRect = new Rect(rect.x, rect.y, rect.width * ratio, rect.height);
            EditorGUI.DrawRect(fillRect, new Color(color.r, color.g, color.b, 0.6f));

            // Label
            EditorGUI.LabelField(rect, $"  {label}: {value:F1}",
                new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.white } });
        }

        private void SpawnTestOrb(ElementType element)
        {
            GameObject testOrb;

            if (element.OrbPrefab != null)
            {
                testOrb = (GameObject)PrefabUtility.InstantiatePrefab(element.OrbPrefab);
            }
            else
            {
                testOrb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                var renderer = testOrb.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    mat.color = element.PrimaryColor;
                    renderer.sharedMaterial = mat;
                }
            }

            testOrb.name = "[Preview] " + element.ElementName + " Orb";

            // Place in front of scene camera
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                testOrb.transform.position = sceneView.camera.transform.position +
                    sceneView.camera.transform.forward * 5f;
            }
            else
            {
                testOrb.transform.position = Vector3.up * 2f;
            }

            Undo.RegisterCreatedObjectUndo(testOrb, "Preview Element Orb");
            Selection.activeGameObject = testOrb;

            Debug.Log($"[ElementTypeEditor] Spawned preview orb for '{element.ElementName}'. " +
                      "Delete it when done.");
        }
    }
}
