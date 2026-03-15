using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using ElementalSiege.Structures;

namespace ElementalSiege.Editor
{
    /// <summary>
    /// Custom inspector for StructureBlock MonoBehaviour.
    /// Shows material dropdown, auto-configuration, health bar,
    /// attached elemental components, and missing-component helpers.
    /// </summary>
    [CustomEditor(typeof(StructureBlock))]
    public class StructureBlockEditor : UnityEditor.Editor
    {
        private static readonly Dictionary<MaterialType, Color> MaterialColors =
            new Dictionary<MaterialType, Color>
            {
                { MaterialType.Wood,    new Color(0.6f, 0.4f, 0.2f) },
                { MaterialType.Stone,   new Color(0.5f, 0.5f, 0.5f) },
                { MaterialType.Metal,   new Color(0.7f, 0.7f, 0.8f) },
                { MaterialType.Glass,   new Color(0.6f, 0.9f, 1f, 0.6f) },
                { MaterialType.Ice,     new Color(0.4f, 0.8f, 1f) },
                { MaterialType.Crystal, new Color(0.9f, 0.5f, 0.9f) },
            };

        /// <summary>
        /// Components that should be attached for each material type.
        /// </summary>
        private static readonly Dictionary<MaterialType, string[]> RequiredComponents =
            new Dictionary<MaterialType, string[]>
            {
                { MaterialType.Wood,    new[] { "Flammable" } },
                { MaterialType.Stone,   new string[0] },
                { MaterialType.Metal,   new[] { "Conductive" } },
                { MaterialType.Glass,   new[] { "Fragile" } },
                { MaterialType.Ice,     new[] { "Meltable" } },
                { MaterialType.Crystal, new[] { "Refractive", "Fragile" } },
            };

        private SerializedProperty _materialTypeProp;
        private SerializedProperty _damageSpritesProp;
        private SerializedProperty _debrisParticlePrefabProp;
        private SerializedProperty _debrisParticleCountProp;
        private SerializedProperty _scoreOverrideProp;

        private void OnEnable()
        {
            _materialTypeProp = serializedObject.FindProperty("materialType");
            _damageSpritesProp = serializedObject.FindProperty("damageSprites");
            _debrisParticlePrefabProp = serializedObject.FindProperty("debrisParticlePrefab");
            _debrisParticleCountProp = serializedObject.FindProperty("debrisParticleCount");
            _scoreOverrideProp = serializedObject.FindProperty("scoreOverride");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var block = (StructureBlock)target;

            // ── Material type dropdown with colour swatch ────────────
            EditorGUILayout.LabelField("Structure Block", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            {
                // Color swatch
                Color swatchColor;
                if (!MaterialColors.TryGetValue(block.Material, out swatchColor))
                    swatchColor = Color.gray;

                Rect swatchRect = EditorGUILayout.GetControlRect(false,
                    EditorGUIUtility.singleLineHeight, GUILayout.Width(24));
                EditorGUI.DrawRect(swatchRect, swatchColor);

                // Material property via SerializedProperty
                if (_materialTypeProp != null)
                    EditorGUILayout.PropertyField(_materialTypeProp, new GUIContent("Material"));
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // ── Material stats (read-only from real class) ────────────
            EditorGUILayout.LabelField("Material Stats", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Base Health", block.BaseHealth.ToString("F0"));
            EditorGUILayout.LabelField("Density", block.Density.ToString("F1"));
            EditorGUILayout.LabelField("Score Value", block.ScoreValue.ToString());
            EditorGUILayout.LabelField("Is Destroyed", block.IsDestroyed.ToString());

            // Health bar (using BaseHealth as max since current health is managed by StructureHealth)
            float maxHealth = block.BaseHealth;
            if (maxHealth > 0f)
            {
                Rect barRect = EditorGUILayout.GetControlRect(false, 18);
                float ratio = block.IsDestroyed ? 0f : 1f;
                EditorGUI.ProgressBar(barRect, ratio,
                    $"Base Health: {maxHealth:F0}");
            }

            EditorGUILayout.Space(4);

            // ── Damage visuals ───────────────────────────────────────
            EditorGUILayout.LabelField("Damage Visuals", EditorStyles.boldLabel);
            if (_damageSpritesProp != null)
                EditorGUILayout.PropertyField(_damageSpritesProp, true);

            EditorGUILayout.Space(4);

            // ── Destruction effects ──────────────────────────────────
            EditorGUILayout.LabelField("Destruction Effects", EditorStyles.boldLabel);
            if (_debrisParticlePrefabProp != null)
                EditorGUILayout.PropertyField(_debrisParticlePrefabProp,
                    new GUIContent("Debris Particle Prefab"));
            if (_debrisParticleCountProp != null)
                EditorGUILayout.PropertyField(_debrisParticleCountProp,
                    new GUIContent("Debris Particle Count"));

            EditorGUILayout.Space(4);

            // ── Score ────────────────────────────────────────────────
            EditorGUILayout.LabelField("Score", EditorStyles.boldLabel);
            if (_scoreOverrideProp != null)
                EditorGUILayout.PropertyField(_scoreOverrideProp,
                    new GUIContent("Score Override (0 = use default)"));

            EditorGUILayout.Space(4);

            // ── Attached elemental components ────────────────────────
            EditorGUILayout.LabelField("Elemental Components", EditorStyles.boldLabel);

            var components = block.GetComponents<MonoBehaviour>();
            bool anyElemental = false;
            foreach (var comp in components)
            {
                string typeName = comp.GetType().Name;
                if (typeName == "Flammable" || typeName == "Conductive" ||
                    typeName == "Fragile" || typeName == "Meltable" ||
                    typeName == "Refractive")
                {
                    EditorGUILayout.LabelField("  " + typeName);
                    anyElemental = true;
                }
            }
            if (!anyElemental)
                EditorGUILayout.LabelField("  (none)");

            // ── Missing components ───────────────────────────────────
            List<string> missing = GetMissingComponents(block);
            if (missing.Count > 0)
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.HelpBox(
                    "Missing components for " + block.Material + ": " +
                    string.Join(", ", missing),
                    MessageType.Warning);

                if (GUILayout.Button("Add Missing Components", GUILayout.Height(24)))
                {
                    AddMissingComponents(block, missing);
                }
            }

            serializedObject.ApplyModifiedProperties();

            if (GUI.changed)
                EditorUtility.SetDirty(target);
        }

        private List<string> GetMissingComponents(StructureBlock block)
        {
            var missing = new List<string>();

            if (!RequiredComponents.TryGetValue(block.Material, out string[] required))
                return missing;

            foreach (string compName in required)
            {
                bool found = false;
                foreach (var comp in block.GetComponents<MonoBehaviour>())
                {
                    if (comp.GetType().Name == compName)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                    missing.Add(compName);
            }

            return missing;
        }

        private void AddMissingComponents(StructureBlock block, List<string> missing)
        {
            foreach (string compName in missing)
            {
                // Attempt to find the type in all loaded assemblies
                System.Type type = null;
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    type = asm.GetType(compName) ??
                           asm.GetType("ElementalSiege." + compName) ??
                           asm.GetType("ElementalSiege.Elements." + compName) ??
                           asm.GetType("ElementalSiege.Structures." + compName);
                    if (type != null) break;
                }

                if (type != null && typeof(Component).IsAssignableFrom(type))
                {
                    Undo.AddComponent(block.gameObject, type);
                }
                else
                {
                    Debug.LogWarning(
                        $"[StructureBlockEditor] Could not find component type '{compName}'. " +
                        "Create it in your game assembly.");
                }
            }
        }
    }
}
