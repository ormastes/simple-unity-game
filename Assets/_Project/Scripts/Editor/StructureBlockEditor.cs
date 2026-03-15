using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

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
                { MaterialType.Wood,      new Color(0.6f, 0.4f, 0.2f) },
                { MaterialType.Stone,     new Color(0.5f, 0.5f, 0.5f) },
                { MaterialType.Metal,     new Color(0.7f, 0.7f, 0.8f) },
                { MaterialType.Glass,     new Color(0.6f, 0.9f, 1f, 0.6f) },
                { MaterialType.Ice,       new Color(0.4f, 0.8f, 1f) },
                { MaterialType.Crystal,   new Color(0.9f, 0.5f, 0.9f) },
                { MaterialType.WoodPlank, new Color(0.7f, 0.5f, 0.3f) },
                { MaterialType.MetalBeam, new Color(0.6f, 0.6f, 0.7f) },
            };

        private static readonly Dictionary<MaterialType, float> DefaultHealth =
            new Dictionary<MaterialType, float>
            {
                { MaterialType.Wood,      50f },
                { MaterialType.Stone,     100f },
                { MaterialType.Metal,     150f },
                { MaterialType.Glass,     25f },
                { MaterialType.Ice,       40f },
                { MaterialType.Crystal,   60f },
                { MaterialType.WoodPlank, 35f },
                { MaterialType.MetalBeam, 120f },
            };

        private static readonly Dictionary<MaterialType, float> DefaultMass =
            new Dictionary<MaterialType, float>
            {
                { MaterialType.Wood,      1f },
                { MaterialType.Stone,     3f },
                { MaterialType.Metal,     5f },
                { MaterialType.Glass,     0.8f },
                { MaterialType.Ice,       1.2f },
                { MaterialType.Crystal,   1.5f },
                { MaterialType.WoodPlank, 0.7f },
                { MaterialType.MetalBeam, 4f },
            };

        /// <summary>
        /// Components that should be attached for each material type.
        /// </summary>
        private static readonly Dictionary<MaterialType, string[]> RequiredComponents =
            new Dictionary<MaterialType, string[]>
            {
                { MaterialType.Wood,      new[] { "Flammable" } },
                { MaterialType.Stone,     new string[0] },
                { MaterialType.Metal,     new[] { "Conductive" } },
                { MaterialType.Glass,     new[] { "Fragile" } },
                { MaterialType.Ice,       new[] { "Meltable" } },
                { MaterialType.Crystal,   new[] { "Refractive", "Fragile" } },
                { MaterialType.WoodPlank, new[] { "Flammable" } },
                { MaterialType.MetalBeam, new[] { "Conductive" } },
            };

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
                if (!MaterialColors.TryGetValue(block.materialType, out swatchColor))
                    swatchColor = Color.gray;

                Rect swatchRect = EditorGUILayout.GetControlRect(false,
                    EditorGUIUtility.singleLineHeight, GUILayout.Width(24));
                EditorGUI.DrawRect(swatchRect, swatchColor);

                // Dropdown
                block.materialType = (MaterialType)EditorGUILayout.EnumPopup(
                    "Material", block.materialType);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // ── Auto-configure button ────────────────────────────────
            if (GUILayout.Button("Auto-Configure (Health, Mass, Components)",
                GUILayout.Height(24)))
            {
                AutoConfigure(block);
            }

            EditorGUILayout.Space(4);

            // ── Health / Mass ────────────────────────────────────────
            block.maxHealth = EditorGUILayout.FloatField("Max Health", block.maxHealth);
            block.currentHealth = EditorGUILayout.FloatField("Current Health", block.currentHealth);
            block.mass = EditorGUILayout.FloatField("Mass", block.mass);

            // Health bar
            if (block.maxHealth > 0f)
            {
                float ratio = Mathf.Clamp01(block.currentHealth / block.maxHealth);
                Rect barRect = EditorGUILayout.GetControlRect(false, 18);
                EditorGUI.ProgressBar(barRect, ratio,
                    $"Health: {block.currentHealth:F0} / {block.maxHealth:F0}");
            }

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
                    "Missing components for " + block.materialType + ": " +
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

        private void AutoConfigure(StructureBlock block)
        {
            Undo.RecordObject(block, "Auto-Configure StructureBlock");

            if (DefaultHealth.TryGetValue(block.materialType, out float health))
            {
                block.maxHealth = health;
                block.currentHealth = health;
            }

            if (DefaultMass.TryGetValue(block.materialType, out float mass))
            {
                block.mass = mass;
                var rb = block.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Undo.RecordObject(rb, "Auto-Configure Mass");
                    rb.mass = mass;
                }
            }

            List<string> missing = GetMissingComponents(block);
            AddMissingComponents(block, missing);

            EditorUtility.SetDirty(block);
        }

        private List<string> GetMissingComponents(StructureBlock block)
        {
            var missing = new List<string>();

            if (!RequiredComponents.TryGetValue(block.materialType, out string[] required))
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
                           asm.GetType("ElementalSiege.Elements." + compName);
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
                        "Create it in your Elements assembly.");
                }
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // Stub runtime types — replace with actual game assembly references
    // ══════════════════════════════════════════════════════════════════

    public enum MaterialType
    {
        Wood, Stone, Metal, Glass, Ice, Crystal, WoodPlank, MetalBeam
    }

    public class StructureBlock : MonoBehaviour
    {
        public MaterialType materialType = MaterialType.Wood;
        public float maxHealth = 50f;
        public float currentHealth = 50f;
        public float mass = 1f;
    }
}
