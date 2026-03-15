using UnityEngine;
using UnityEditor;

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
        private static readonly Color[] ElementColors =
        {
            new Color(1f, 0.3f, 0.1f),   // Fire
            new Color(0.4f, 0.8f, 1f),    // Ice
            new Color(1f, 1f, 0.3f),      // Lightning
            new Color(0.6f, 0.4f, 0.2f),  // Earth
            new Color(0.7f, 1f, 0.7f),    // Wind
            new Color(0.2f, 0.4f, 1f),    // Water
            new Color(0.9f, 0.5f, 0.9f),  // Crystal
        };

        private bool _showTrajectory = true;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var orb = (OrbBase)target;

            // ── Coloured header ──────────────────────────────────────
            Color headerColor = GetElementColor(orb.elementIndex);
            Rect headerRect = EditorGUILayout.GetControlRect(false, 32);
            EditorGUI.DrawRect(headerRect, headerColor);

            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            EditorGUI.LabelField(headerRect, orb.elementName, headerStyle);

            EditorGUILayout.Space(4);

            // ── Element info ─────────────────────────────────────────
            orb.elementName = EditorGUILayout.TextField("Element Name", orb.elementName);
            orb.elementIndex = EditorGUILayout.IntSlider("Element Index", orb.elementIndex, 0, 6);

            EditorGUILayout.Space(4);

            // ── Ability description ──────────────────────────────────
            EditorGUILayout.LabelField("Ability", EditorStyles.boldLabel);
            orb.abilityName = EditorGUILayout.TextField("Name", orb.abilityName);

            EditorGUILayout.LabelField("Description");
            orb.abilityDescription = EditorGUILayout.TextArea(
                orb.abilityDescription, GUILayout.Height(48));

            EditorGUILayout.Space(4);

            // ── Ability parameters ───────────────────────────────────
            EditorGUILayout.LabelField("Parameters", EditorStyles.boldLabel);
            orb.damage = EditorGUILayout.FloatField("Damage", orb.damage);
            orb.radius = EditorGUILayout.FloatField("Effect Radius", orb.radius);
            orb.launchForce = EditorGUILayout.FloatField("Launch Force", orb.launchForce);
            orb.launchAngle = EditorGUILayout.Slider("Launch Angle", orb.launchAngle, 5f, 85f);

            EditorGUILayout.Space(4);

            // ── Trajectory preview toggle ────────────────────────────
            _showTrajectory = EditorGUILayout.Toggle("Show Trajectory Preview", _showTrajectory);

            EditorGUILayout.Space(4);

            // ── Test ability button (play mode only) ─────────────────
            GUI.enabled = Application.isPlaying;
            if (GUILayout.Button("Test Ability", GUILayout.Height(28)))
            {
                orb.SendMessage("ActivateAbility", SendMessageOptions.DontRequireReceiver);
                Debug.Log("[OrbBaseEditor] Triggered ActivateAbility on " + orb.name);
            }
            GUI.enabled = true;

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Enter Play Mode to test the ability.", MessageType.Info);
            }

            serializedObject.ApplyModifiedProperties();

            if (GUI.changed)
                EditorUtility.SetDirty(target);
        }

        // ── Scene view trajectory ────────────────────────────────────
        private void OnSceneGUI()
        {
            if (!_showTrajectory)
                return;

            var orb = (OrbBase)target;
            if (orb == null) return;

            Vector3 origin = orb.transform.position;
            float force = orb.launchForce;
            float angle = orb.launchAngle * Mathf.Deg2Rad;
            float gravity = Physics.gravity.magnitude;

            Vector3 forward = orb.transform.forward;
            Vector3 velocity = forward * Mathf.Cos(angle) * force +
                               Vector3.up * Mathf.Sin(angle) * force;

            int steps = 60;
            float dt = 0.05f;
            Vector3 prev = origin;

            Handles.color = GetElementColor(orb.elementIndex);

            for (int i = 1; i <= steps; i++)
            {
                float t = i * dt;
                Vector3 pos = origin + velocity * t +
                              0.5f * Physics.gravity * t * t;

                Handles.DrawLine(prev, pos);

                // Impact point marker
                if (pos.y < origin.y - 0.1f)
                {
                    Handles.DrawWireDisc(pos, Vector3.up, orb.radius);
                    break;
                }

                prev = pos;
            }

            // Draw radius at origin
            Handles.color = new Color(Handles.color.r, Handles.color.g,
                Handles.color.b, 0.3f);
            Handles.DrawWireDisc(origin, Vector3.up, orb.radius);
        }

        private Color GetElementColor(int index)
        {
            if (index >= 0 && index < ElementColors.Length)
                return ElementColors[index];
            return Color.white;
        }
    }

    // ══════════════════════════════════════════════════════════════════
    // Stub runtime type — replace with actual game assembly reference
    // ══════════════════════════════════════════════════════════════════

    public class OrbBase : MonoBehaviour
    {
        public string elementName = "Fire";
        public int elementIndex;
        public string abilityName = "Fireball";
        public string abilityDescription = "Explodes on impact, dealing fire damage.";
        public float damage = 25f;
        public float radius = 3f;
        public float launchForce = 20f;
        public float launchAngle = 45f;
    }
}
