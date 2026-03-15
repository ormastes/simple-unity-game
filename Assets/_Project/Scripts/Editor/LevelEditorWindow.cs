using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace ElementalSiege.Editor
{
    /// <summary>
    /// Custom EditorWindow for building levels visually.
    /// Provides structure/environment palettes, guardian placement,
    /// level settings, and utility actions.
    /// </summary>
    public class LevelEditorWindow : EditorWindow
    {
        // ── Palette definitions ──────────────────────────────────────
        private enum PaletteCategory { Structures, Environment, Guardians }

        private static readonly string[] StructureTypes =
        {
            "Wood", "Stone", "Metal", "Glass", "Ice", "Crystal", "WoodPlank", "MetalBeam"
        };

        private static readonly string[] EnvironmentTypes =
        {
            "Water", "Wind", "Rope", "CrystalSurface", "GravityZone"
        };

        // ── State ────────────────────────────────────────────────────
        private PaletteCategory _activeCategory = PaletteCategory.Structures;
        private int _selectedIndex = -1;
        private string _selectedPrefabName;

        private Vector2 _leftScroll;
        private Vector2 _rightScroll;

        // Grid / rotation
        private bool _snapToGrid = true;
        private float _gridSize = 1f;
        private int _rotationIndex; // 0 = 0, 1 = 90, 2 = 180, 3 = 270
        private static readonly float[] Rotations = { 0f, 90f, 180f, 270f };

        // Level settings
        private Object _levelDataRef;
        private string[] _orbNames = { "Fire", "Ice", "Lightning", "Earth", "Wind", "Water" };
        private bool[] _orbSelection = new bool[6];
        private int _star1Threshold = 1000;
        private int _star2Threshold = 3000;
        private int _star3Threshold = 5000;

        // ── Menu entry ───────────────────────────────────────────────
        [MenuItem("Elemental Siege/Level Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<LevelEditorWindow>("Level Editor");
            window.minSize = new Vector2(600, 400);
        }

        // ── Lifecycle ────────────────────────────────────────────────
        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        // ── Main GUI ─────────────────────────────────────────────────
        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();

            // ── Left panel (palette) ─────────────────────────────────
            EditorGUILayout.BeginVertical(GUILayout.Width(220));
            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);

            EditorGUILayout.LabelField("Palette", EditorStyles.boldLabel);
            _activeCategory = (PaletteCategory)GUILayout.Toolbar(
                (int)_activeCategory,
                new[] { "Structures", "Environment", "Guardians" });

            EditorGUILayout.Space(4);

            switch (_activeCategory)
            {
                case PaletteCategory.Structures:
                    DrawPaletteButtons(StructureTypes);
                    break;
                case PaletteCategory.Environment:
                    DrawPaletteButtons(EnvironmentTypes);
                    break;
                case PaletteCategory.Guardians:
                    DrawGuardianPalette();
                    break;
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Placement", EditorStyles.boldLabel);

            _snapToGrid = EditorGUILayout.Toggle("Snap to Grid", _snapToGrid);
            if (_snapToGrid)
                _gridSize = EditorGUILayout.FloatField("Grid Size", _gridSize);

            EditorGUILayout.LabelField("Rotation");
            _rotationIndex = GUILayout.Toolbar(_rotationIndex, new[] { "0", "90", "180", "270" });

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            // ── Right panel (level settings) ─────────────────────────
            EditorGUILayout.BeginVertical();
            _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);

            EditorGUILayout.LabelField("Level Settings", EditorStyles.boldLabel);

            _levelDataRef = EditorGUILayout.ObjectField(
                "Level Data", _levelDataRef, typeof(ScriptableObject), false);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Orb Selection", EditorStyles.boldLabel);
            for (int i = 0; i < _orbNames.Length; i++)
                _orbSelection[i] = EditorGUILayout.Toggle(_orbNames[i], _orbSelection[i]);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Star Thresholds", EditorStyles.boldLabel);
            _star1Threshold = EditorGUILayout.IntField("1 Star", _star1Threshold);
            _star2Threshold = EditorGUILayout.IntField("2 Stars", _star2Threshold);
            _star3Threshold = EditorGUILayout.IntField("3 Stars", _star3Threshold);

            if (_star1Threshold >= _star2Threshold || _star2Threshold >= _star3Threshold)
                EditorGUILayout.HelpBox("Star thresholds must be in ascending order.", MessageType.Warning);

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            // ── Bottom utility bar ───────────────────────────────────
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Clear Level", EditorStyles.toolbarButton))
                ClearLevel();
            if (GUILayout.Button("Save Layout", EditorStyles.toolbarButton))
                SaveLayout();
            if (GUILayout.Button("Load Layout", EditorStyles.toolbarButton))
                LoadLayout();
            if (GUILayout.Button("Test Play", EditorStyles.toolbarButton))
                TestPlay();

            EditorGUILayout.EndHorizontal();
        }

        // ── Palette helpers ──────────────────────────────────────────
        private void DrawPaletteButtons(string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                bool isSelected = (_selectedPrefabName == names[i]);
                GUI.color = isSelected ? Color.cyan : Color.white;
                if (GUILayout.Button(names[i], GUILayout.Height(28)))
                {
                    _selectedIndex = i;
                    _selectedPrefabName = names[i];
                }
            }
            GUI.color = Color.white;
        }

        private void DrawGuardianPalette()
        {
            EditorGUILayout.LabelField("Guardian Types");
            string[] guardians = { "FireGuardian", "IceGuardian", "StoneGuardian" };
            for (int i = 0; i < guardians.Length; i++)
            {
                if (GUILayout.Button(guardians[i], GUILayout.Height(28)))
                {
                    _selectedPrefabName = guardians[i];
                }
            }
        }

        // ── Scene view interaction ───────────────────────────────────
        private void OnSceneGUI(SceneView sceneView)
        {
            if (string.IsNullOrEmpty(_selectedPrefabName))
                return;

            Event e = Event.current;
            if (e == null)
                return;

            // Show label at cursor
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

            if (groundPlane.Raycast(ray, out float distance))
            {
                Vector3 worldPos = ray.GetPoint(distance);

                if (_snapToGrid && _gridSize > 0f)
                {
                    worldPos.x = Mathf.Round(worldPos.x / _gridSize) * _gridSize;
                    worldPos.y = 0f;
                    worldPos.z = Mathf.Round(worldPos.z / _gridSize) * _gridSize;
                }

                Handles.color = new Color(0f, 1f, 1f, 0.4f);
                Handles.DrawWireCube(worldPos, Vector3.one * 0.5f);
                Handles.Label(worldPos + Vector3.up, _selectedPrefabName);

                // Place on click
                if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
                {
                    SpawnPrefabAt(worldPos);
                    e.Use();
                }
            }

            sceneView.Repaint();
        }

        private void SpawnPrefabAt(Vector3 position)
        {
            // Try to find prefab by name in the project
            string[] guids = AssetDatabase.FindAssets(_selectedPrefabName + " t:Prefab");
            GameObject prefab = null;

            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            GameObject instance;
            if (prefab != null)
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            }
            else
            {
                // Fallback: create a primitive placeholder
                instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                instance.name = _selectedPrefabName;
            }

            instance.transform.position = position;
            instance.transform.rotation = Quaternion.Euler(0f, Rotations[_rotationIndex], 0f);

            Undo.RegisterCreatedObjectUndo(instance, "Place " + _selectedPrefabName);
            Selection.activeGameObject = instance;
        }

        // ── Utility actions ──────────────────────────────────────────
        private void ClearLevel()
        {
            if (!EditorUtility.DisplayDialog("Clear Level",
                "Remove all placed objects?", "Yes", "Cancel"))
                return;

            var root = GameObject.Find("LevelObjects");
            if (root != null)
            {
                Undo.DestroyObjectImmediate(root);
            }
        }

        private void SaveLayout()
        {
            string path = EditorUtility.SaveFilePanel("Save Layout", "Assets", "layout", "json");
            if (string.IsNullOrEmpty(path))
                return;

            var root = GameObject.Find("LevelObjects");
            if (root == null)
            {
                Debug.LogWarning("[LevelEditor] No 'LevelObjects' root found to save.");
                return;
            }

            var data = new LevelLayoutData();
            foreach (Transform child in root.transform)
            {
                data.entries.Add(new LevelLayoutEntry
                {
                    name = child.name,
                    position = child.position,
                    rotation = child.eulerAngles
                });
            }

            string json = JsonUtility.ToJson(data, true);
            System.IO.File.WriteAllText(path, json);
            AssetDatabase.Refresh();
            Debug.Log("[LevelEditor] Layout saved to " + path);
        }

        private void LoadLayout()
        {
            string path = EditorUtility.OpenFilePanel("Load Layout", "Assets", "json");
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
                return;

            string json = System.IO.File.ReadAllText(path);
            var data = JsonUtility.FromJson<LevelLayoutData>(json);
            if (data == null || data.entries == null)
                return;

            // Create root if missing
            var root = GameObject.Find("LevelObjects");
            if (root == null)
                root = new GameObject("LevelObjects");

            Undo.RegisterFullObjectHierarchyUndo(root, "Load Layout");

            foreach (var entry in data.entries)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = entry.name;
                go.transform.SetParent(root.transform);
                go.transform.position = entry.position;
                go.transform.eulerAngles = entry.rotation;
                Undo.RegisterCreatedObjectUndo(go, "Load Layout Object");
            }

            Debug.Log("[LevelEditor] Layout loaded from " + path);
        }

        private void TestPlay()
        {
            EditorApplication.isPlaying = true;
        }

        // ── Serializable layout data ─────────────────────────────────
        [System.Serializable]
        private class LevelLayoutData
        {
            public List<LevelLayoutEntry> entries = new List<LevelLayoutEntry>();
        }

        [System.Serializable]
        private class LevelLayoutEntry
        {
            public string name;
            public Vector3 position;
            public Vector3 rotation;
        }
    }
}
