using UnityEngine;
using UnityEditor;
using ElementalSiege.Orbs;
using ElementalSiege.Elements;

namespace ElementalSiege.Editor
{
    /// <summary>
    /// Custom inspector for OrbBase-derived MonoBehaviours.
    /// Shows element info with a coloured header, ability parameters,
    /// launch trajectory preview in scene view, and a play-mode test button.
    /// </summary>
    [CustomEditor(typeof(OrbBase), true)]
    public class OrbBaseEditor : UnityEditor.Editor
    {
        private static readonly Color[] CategoryColors =
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

        private SerializedProperty _elementTypeProp;
        private SerializedProperty _maxLifetimeProp;
        private SerializedProperty _settleVelocityThresholdProp;
        private SerializedProperty _settleTimeRequiredProp;
        private SerializedProperty _trailRendererProp;

        private bool _showTrajectory = true;

        private void OnEnable()
        {
            _elementTypeProp = serializedObject.FindProperty("elementType");
            _maxLifetimeProp = serializedObject.FindProperty("maxLifetime");
            _settleVelocityThresholdProp = serializedObject.FindProperty("settleVelocityThreshold");
            _settleTimeRequiredProp = serializedObject.FindProperty("settleTimeRequired");
            _trailRendererProp = serializedObject.FindProperty("trailRenderer");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var orb = (OrbBase)target;

            // ── Coloured header ──────────────────────────────────────
            Color headerColor = Color.white;
            string headerLabel = "Orb";

            var elementType = orb.ElementType;
            if (elementType != null)
            {
                headerColor = elementType.PrimaryColor;
                headerLabel = elementType.ElementName + " Orb";
            }

            Rect headerRect = EditorGUILayout.GetControlRect(false, 32);
            EditorGUI.DrawRect(headerRect, headerColor);

            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            EditorGUI.LabelField(headerRect, headerLabel, headerStyle);

            EditorGUILayout.Space(4);

            // ── Element Type reference ────────────────────────────────
            EditorGUILayout.LabelField("Element Configuration", EditorStyles.boldLabel);
            if (_elementTypeProp != null)
                EditorGUILayout.PropertyField(_elementTypeProp, new GUIContent("Element Type"));

            // Show element details if assigned
            if (elementType != null)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Category", elementType.Category.ToString());
                EditorGUILayout.LabelField("Base Damage", elementType.BaseDamage.ToString("F1"));
                EditorGUILayout.LabelField("Ability Radius", elementType.AbilityRadius.ToString("F1"));

                if (elementType.Icon != null)
                {
                    Rect iconRect = EditorGUILayout.GetControlRect(false, 48);
                    iconRect.width = 48;
                    Texture2D tex = AssetPreview.GetAssetPreview(elementType.Icon);
                    if (tex != null)
                        GUI.DrawTexture(iconRect, tex, ScaleMode.ScaleToFit);
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);

            // ── Lifecycle parameters ──────────────────────────────────
            EditorGUILayout.LabelField("Lifecycle", EditorStyles.boldLabel);
            if (_maxLifetimeProp != null)
                EditorGUILayout.PropertyField(_maxLifetimeProp, new GUIContent("Max Lifetime"));
            if (_settleVelocityThresholdProp != null)
                EditorGUILayout.PropertyField(_settleVelocityThresholdProp,
                    new GUIContent("Settle Velocity Threshold"));
            if (_settleTimeRequiredProp != null)
                EditorGUILayout.PropertyField(_settleTimeRequiredProp,
                    new GUIContent("Settle Time Required"));

            EditorGUILayout.Space(4);

            // ── Trail ─────────────────────────────────────────────────
            EditorGUILayout.LabelField("Trail", EditorStyles.boldLabel);
            if (_trailRendererProp != null)
                EditorGUILayout.PropertyField(_trailRendererProp, new GUIContent("Trail Renderer"));

            EditorGUILayout.Space(4);

            // ── Runtime state (read-only) ─────────────────────────────
            EditorGUILayout.LabelField("Runtime State", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Current State", orb.CurrentState.ToString());

            EditorGUILayout.Space(4);

            // ── Trajectory preview toggle ─────────────────────────────
            _showTrajectory = EditorGUILayout.Toggle("Show Trajectory Preview", _showTrajectory);

            EditorGUILayout.Space(4);

            // ── Test ability button (play mode only) ──────────────────
            GUI.enabled = Application.isPlaying;
            if (GUILayout.Button("Test Ability", GUILayout.Height(28)))
            {
                orb.TryActivateAbility();
                Debug.Log("[OrbBaseEditor] Triggered TryActivateAbility on " + orb.name);
            }
            GUI.enabled = true;

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Enter Play Mode to test the ability.", MessageType.Info);
            }

            serializedObject.ApplyModifiedProperties();
        }

        // ── Scene view trajectory ────────────────────────────────────
        private void OnSceneGUI()
        {
            if (!_showTrajectory)
                return;

            var orb = (OrbBase)target;
            if (orb == null) return;

            var elementType = orb.ElementType;
            float radius = elementType != null ? elementType.AbilityRadius : 1f;
            int categoryIndex = elementType != null ? (int)elementType.Category : 0;

            Vector3 origin = orb.transform.position;

            Handles.color = GetCategoryColor(categoryIndex);

            // Draw ability radius at origin
            Handles.color = new Color(Handles.color.r, Handles.color.g,
                Handles.color.b, 0.3f);
            Handles.DrawWireDisc(origin, Vector3.forward, radius);
        }

        private Color GetCategoryColor(int index)
        {
            if (index >= 0 && index < CategoryColors.Length)
                return CategoryColors[index];
            return Color.white;
        }
    }
}
