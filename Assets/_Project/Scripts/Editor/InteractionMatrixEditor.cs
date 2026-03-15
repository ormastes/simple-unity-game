using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using ElementalSiege.Elements;

namespace ElementalSiege.Editor
{
    /// <summary>
    /// Custom editor for InteractionMatrix ScriptableObject.
    /// Renders a visual grid where rows = Element A, columns = Element B,
    /// and each cell shows combo name, damage multiplier, and a colour code.
    /// </summary>
    [CustomEditor(typeof(InteractionMatrix))]
    public class InteractionMatrixEditor : UnityEditor.Editor
    {
        private Vector2 _scrollPos;
        private bool _showLegend = true;

        /// <summary>All element category names for grid headers.</summary>
        private static readonly string[] CategoryNames = System.Enum.GetNames(typeof(ElementCategory));

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var matrix = (InteractionMatrix)target;

            EditorGUILayout.LabelField("Interaction Matrix", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // ── Legend ───────────────────────────────────────────────
            _showLegend = EditorGUILayout.Foldout(_showLegend, "Element Category Legend");
            if (_showLegend)
            {
                EditorGUI.indentLevel++;
                foreach (var kvp in ElementCategoryColors)
                {
                    Rect r = EditorGUILayout.GetControlRect(false, 18);
                    Rect colorRect = new Rect(r.x, r.y + 2, 14, 14);
                    EditorGUI.DrawRect(colorRect, kvp.Value);
                    Rect labelRect = new Rect(r.x + 20, r.y, r.width - 20, r.height);
                    EditorGUI.LabelField(labelRect, kvp.Key.ToString());
                }
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(4);
            }

            // ── Grid ─────────────────────────────────────────────────
            int count = CategoryNames.Length;
            float cellWidth = 90f;
            float cellHeight = 40f;
            float headerWidth = 80f;

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos,
                GUILayout.Height(Mathf.Min((count + 1) * cellHeight + 30, 500)));

            // Header row
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(headerWidth);
            for (int col = 0; col < count; col++)
            {
                GUILayout.Label(CategoryNames[col], EditorStyles.miniLabel,
                    GUILayout.Width(cellWidth));
            }
            EditorGUILayout.EndHorizontal();

            // Data rows
            for (int row = 0; row < count; row++)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(CategoryNames[row], EditorStyles.miniLabel,
                    GUILayout.Width(headerWidth));

                for (int col = 0; col < count; col++)
                {
                    var catA = (ElementCategory)row;
                    var catB = (ElementCategory)col;
                    var interaction = matrix.GetInteraction(catA, catB);
                    DrawCell(interaction, catA, catB, cellWidth, cellHeight);
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);

            // ── Default inspector for the interactions list ───────────
            EditorGUILayout.LabelField("Interactions List", EditorStyles.boldLabel);
            var interactionsProp = serializedObject.FindProperty("interactions");
            if (interactionsProp != null)
            {
                EditorGUILayout.PropertyField(interactionsProp, true);
            }

            if (GUILayout.Button("Rebuild Cache", GUILayout.Height(28)))
            {
                matrix.RebuildCache();
                Debug.Log("[InteractionMatrixEditor] Cache rebuilt.");
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawCell(ElementInteraction interaction, ElementCategory catA,
            ElementCategory catB, float width, float height)
        {
            Rect cellRect = EditorGUILayout.GetControlRect(false,
                height, GUILayout.Width(width));

            // Background colour
            Color bgColor = (interaction != null)
                ? GetInteractionColor(interaction.DamageMultiplier)
                : new Color(0.2f, 0.2f, 0.2f, 0.3f);

            EditorGUI.DrawRect(cellRect, bgColor);

            if (interaction != null)
            {
                // Combo name
                Rect nameRect = new Rect(cellRect.x + 2, cellRect.y + 2,
                    cellRect.width - 4, 14);
                GUI.Label(nameRect, interaction.ComboName, EditorStyles.miniLabel);

                // Multiplier
                Rect multRect = new Rect(cellRect.x + 2, cellRect.y + 18,
                    cellRect.width - 4, 14);
                GUI.Label(multRect, "x" + interaction.DamageMultiplier.ToString("F1"),
                    EditorStyles.miniBoldLabel);
            }
            else
            {
                Rect emptyRect = new Rect(cellRect.x + 2, cellRect.y + 10,
                    cellRect.width - 4, 14);
                GUI.Label(emptyRect, "---", EditorStyles.miniLabel);
            }

            // Click to select the interaction asset
            if (interaction != null &&
                Event.current.type == EventType.MouseDown &&
                cellRect.Contains(Event.current.mousePosition))
            {
                EditorGUIUtility.PingObject(interaction);
                Selection.activeObject = interaction;
                Event.current.Use();
            }
        }

        private Color GetInteractionColor(float multiplier)
        {
            if (multiplier >= 2f) return new Color(0.9f, 0.2f, 0.2f, 0.5f);
            if (multiplier >= 1.5f) return new Color(0.9f, 0.6f, 0.1f, 0.5f);
            if (multiplier >= 1f) return new Color(0.2f, 0.8f, 0.2f, 0.5f);
            if (multiplier > 0f) return new Color(0.2f, 0.5f, 0.8f, 0.5f);
            return new Color(0.4f, 0.4f, 0.4f, 0.3f);
        }

        // ── Element category colours ─────────────────────────────────
        private static readonly Dictionary<ElementCategory, Color> ElementCategoryColors =
            new Dictionary<ElementCategory, Color>
            {
                { ElementCategory.Stone,     new Color(0.6f, 0.4f, 0.2f) },
                { ElementCategory.Fire,      new Color(1f, 0.3f, 0.1f) },
                { ElementCategory.Ice,       new Color(0.4f, 0.8f, 1f) },
                { ElementCategory.Lightning, new Color(1f, 1f, 0.3f) },
                { ElementCategory.Wind,      new Color(0.7f, 1f, 0.7f) },
                { ElementCategory.Crystal,   new Color(0.9f, 0.5f, 0.9f) },
                { ElementCategory.Gravity,   new Color(0.5f, 0.3f, 0.7f) },
                { ElementCategory.Void,      new Color(0.3f, 0.1f, 0.4f) },
            };
    }
}
