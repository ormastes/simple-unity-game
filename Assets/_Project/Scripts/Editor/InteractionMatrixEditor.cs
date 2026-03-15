using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

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
                    EditorGUI.LabelField(labelRect, kvp.Key);
                }
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(4);
            }

            // ── Grid ─────────────────────────────────────────────────
            if (matrix.elements == null || matrix.elements.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "Assign element names in the 'elements' array to populate the grid.",
                    MessageType.Info);
                DrawDefaultInspector();
                return;
            }

            string[] elements = matrix.elements;
            int count = elements.Length;
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
                GUILayout.Label(elements[col], EditorStyles.miniLabel,
                    GUILayout.Width(cellWidth));
            }
            EditorGUILayout.EndHorizontal();

            // Data rows
            for (int row = 0; row < count; row++)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(elements[row], EditorStyles.miniLabel,
                    GUILayout.Width(headerWidth));

                for (int col = 0; col < count; col++)
                {
                    var interaction = matrix.GetInteraction(row, col);
                    DrawCell(interaction, row, col, cellWidth, cellHeight, matrix);
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Add Interaction", GUILayout.Height(28)))
            {
                AddInteraction(matrix);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawCell(InteractionEntry entry, int row, int col,
            float width, float height, InteractionMatrix matrix)
        {
            Rect cellRect = EditorGUILayout.GetControlRect(false,
                height, GUILayout.Width(width));

            // Background colour
            Color bgColor = (entry != null)
                ? GetInteractionColor(entry.damageMultiplier)
                : new Color(0.2f, 0.2f, 0.2f, 0.3f);

            EditorGUI.DrawRect(cellRect, bgColor);

            if (entry != null)
            {
                // Combo name
                Rect nameRect = new Rect(cellRect.x + 2, cellRect.y + 2,
                    cellRect.width - 4, 14);
                GUI.Label(nameRect, entry.comboName, EditorStyles.miniLabel);

                // Multiplier
                Rect multRect = new Rect(cellRect.x + 2, cellRect.y + 18,
                    cellRect.width - 4, 14);
                GUI.Label(multRect, "x" + entry.damageMultiplier.ToString("F1"),
                    EditorStyles.miniBoldLabel);
            }
            else
            {
                Rect emptyRect = new Rect(cellRect.x + 2, cellRect.y + 10,
                    cellRect.width - 4, 14);
                GUI.Label(emptyRect, "---", EditorStyles.miniLabel);
            }

            // Click to edit
            if (Event.current.type == EventType.MouseDown &&
                cellRect.Contains(Event.current.mousePosition))
            {
                EditInteraction(matrix, row, col, entry);
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

        private void EditInteraction(InteractionMatrix matrix, int row, int col,
            InteractionEntry entry)
        {
            InteractionEditPopup.Show(matrix, row, col, entry);
        }

        private void AddInteraction(InteractionMatrix matrix)
        {
            Undo.RecordObject(matrix, "Add Interaction");

            if (matrix.interactions == null)
                matrix.interactions = new List<InteractionEntry>();

            matrix.interactions.Add(new InteractionEntry
            {
                elementA = 0,
                elementB = 0,
                comboName = "New Combo",
                damageMultiplier = 1f
            });

            EditorUtility.SetDirty(matrix);
        }

        // ── Element category colours ─────────────────────────────────
        private static readonly Dictionary<string, Color> ElementCategoryColors =
            new Dictionary<string, Color>
            {
                { "Fire",      new Color(1f, 0.3f, 0.1f) },
                { "Ice",       new Color(0.4f, 0.8f, 1f) },
                { "Lightning", new Color(1f, 1f, 0.3f) },
                { "Earth",     new Color(0.6f, 0.4f, 0.2f) },
                { "Wind",      new Color(0.7f, 1f, 0.7f) },
                { "Water",     new Color(0.2f, 0.4f, 1f) },
                { "Crystal",   new Color(0.9f, 0.5f, 0.9f) },
                { "Void",      new Color(0.3f, 0.1f, 0.4f) },
            };
    }

    // ── Popup window for editing a single cell ──────────────────────
    public class InteractionEditPopup : EditorWindow
    {
        private InteractionMatrix _matrix;
        private int _row;
        private int _col;
        private string _comboName = "";
        private float _damageMultiplier = 1f;

        public static void Show(InteractionMatrix matrix, int row, int col,
            InteractionEntry existing)
        {
            var popup = CreateInstance<InteractionEditPopup>();
            popup.titleContent = new GUIContent("Edit Interaction");
            popup._matrix = matrix;
            popup._row = row;
            popup._col = col;

            if (existing != null)
            {
                popup._comboName = existing.comboName;
                popup._damageMultiplier = existing.damageMultiplier;
            }

            popup.ShowUtility();
            popup.minSize = new Vector2(260, 120);
            popup.maxSize = new Vector2(260, 120);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Interaction", EditorStyles.boldLabel);
            _comboName = EditorGUILayout.TextField("Combo Name", _comboName);
            _damageMultiplier = EditorGUILayout.FloatField("Damage Multiplier", _damageMultiplier);

            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply"))
            {
                Undo.RecordObject(_matrix, "Edit Interaction");
                _matrix.SetInteraction(_row, _col, _comboName, _damageMultiplier);
                EditorUtility.SetDirty(_matrix);
                Close();
            }
            if (GUILayout.Button("Cancel"))
                Close();
            EditorGUILayout.EndHorizontal();
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // Stub runtime types (replace with actual game assembly references)
    // ══════════════════════════════════════════════════════════════════

    /// <summary>Placeholder — replace with the real InteractionMatrix in your game assembly.</summary>
    // If your project already defines InteractionMatrix, delete this stub.
    public class InteractionMatrix : ScriptableObject
    {
        public string[] elements = { "Fire", "Ice", "Lightning", "Earth", "Wind", "Water" };
        public List<InteractionEntry> interactions = new List<InteractionEntry>();

        public InteractionEntry GetInteraction(int a, int b)
        {
            if (interactions == null) return null;
            return interactions.Find(i => i.elementA == a && i.elementB == b);
        }

        public void SetInteraction(int a, int b, string combo, float multiplier)
        {
            if (interactions == null)
                interactions = new List<InteractionEntry>();

            var existing = interactions.Find(i => i.elementA == a && i.elementB == b);
            if (existing != null)
            {
                existing.comboName = combo;
                existing.damageMultiplier = multiplier;
            }
            else
            {
                interactions.Add(new InteractionEntry
                {
                    elementA = a,
                    elementB = b,
                    comboName = combo,
                    damageMultiplier = multiplier
                });
            }
        }
    }

    [System.Serializable]
    public class InteractionEntry
    {
        public int elementA;
        public int elementB;
        public string comboName;
        public float damageMultiplier;
    }
}
