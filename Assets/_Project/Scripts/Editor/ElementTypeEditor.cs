using UnityEngine;
using UnityEditor;

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
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var element = (ElementType)target;

            // ── Header with element name ─────────────────────────────
            EditorGUILayout.LabelField("Element Type", EditorStyles.boldLabel);
            element.elementName = EditorGUILayout.TextField("Name", element.elementName);

            EditorGUILayout.Space(4);

            // ── Large colour swatch ──────────────────────────────────
            EditorGUILayout.LabelField("Colours", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            {
                // Primary colour
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField("Primary", GUILayout.Width(70));
                Rect primaryRect = EditorGUILayout.GetControlRect(false, 48, GUILayout.Width(80));
                EditorGUI.DrawRect(primaryRect, element.primaryColor);
                element.primaryColor = EditorGUILayout.ColorField(element.primaryColor,
                    GUILayout.Width(80));
                EditorGUILayout.EndVertical();

                GUILayout.Space(8);

                // Secondary colour
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField("Secondary", GUILayout.Width(70));
                Rect secondaryRect = EditorGUILayout.GetControlRect(false, 48, GUILayout.Width(80));
                EditorGUI.DrawRect(secondaryRect, element.secondaryColor);
                element.secondaryColor = EditorGUILayout.ColorField(element.secondaryColor,
                    GUILayout.Width(80));
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // ── Icon preview ─────────────────────────────────────────
            EditorGUILayout.LabelField("Icon", EditorStyles.boldLabel);
            element.icon = (Sprite)EditorGUILayout.ObjectField(
                "Icon Sprite", element.icon, typeof(Sprite), false);

            if (element.icon != null)
            {
                Rect iconRect = EditorGUILayout.GetControlRect(false, 64);
                iconRect.width = 64;
                Texture2D tex = AssetPreview.GetAssetPreview(element.icon);
                if (tex != null)
                    GUI.DrawTexture(iconRect, tex, ScaleMode.ScaleToFit);
            }

            EditorGUILayout.Space(4);

            // ── Stats overview ───────────────────────────────────────
            EditorGUILayout.LabelField("Stats", EditorStyles.boldLabel);
            element.baseDamage = EditorGUILayout.FloatField("Base Damage", element.baseDamage);
            element.effectRadius = EditorGUILayout.FloatField("Effect Radius", element.effectRadius);

            // Visual stat bars
            DrawStatBar("Damage", element.baseDamage, 100f, Color.red);
            DrawStatBar("Radius", element.effectRadius, 20f, Color.cyan);

            EditorGUILayout.Space(4);

            // ── Associated prefab ────────────────────────────────────
            EditorGUILayout.LabelField("Prefab", EditorStyles.boldLabel);
            element.orbPrefab = (GameObject)EditorGUILayout.ObjectField(
                "Orb Prefab", element.orbPrefab, typeof(GameObject), false);

            if (element.orbPrefab != null)
            {
                if (GUILayout.Button("Select Prefab in Project", GUILayout.Height(22)))
                {
                    EditorGUIUtility.PingObject(element.orbPrefab);
                    Selection.activeObject = element.orbPrefab;
                }
            }

            EditorGUILayout.Space(8);

            // ── Preview in Scene ─────────────────────────────────────
            if (GUILayout.Button("Preview in Scene", GUILayout.Height(28)))
            {
                SpawnTestOrb(element);
            }

            serializedObject.ApplyModifiedProperties();

            if (GUI.changed)
                EditorUtility.SetDirty(target);
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

            if (element.orbPrefab != null)
            {
                testOrb = (GameObject)PrefabUtility.InstantiatePrefab(element.orbPrefab);
            }
            else
            {
                testOrb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                var renderer = testOrb.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    mat.color = element.primaryColor;
                    renderer.sharedMaterial = mat;
                }
            }

            testOrb.name = "[Preview] " + element.elementName + " Orb";

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

            Debug.Log($"[ElementTypeEditor] Spawned preview orb for '{element.elementName}'. " +
                      "Delete it when done.");
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // Stub runtime type — replace with actual game assembly reference
    // ══════════════════════════════════════════════════════════════════

    public class ElementType : ScriptableObject
    {
        public string elementName = "Fire";
        public Color primaryColor = new Color(1f, 0.3f, 0.1f);
        public Color secondaryColor = new Color(1f, 0.6f, 0.2f);
        public Sprite icon;
        public float baseDamage = 25f;
        public float effectRadius = 3f;
        public GameObject orbPrefab;
    }
}
