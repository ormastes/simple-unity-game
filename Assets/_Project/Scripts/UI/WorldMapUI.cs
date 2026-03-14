using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ElementalSiege.Core;

namespace ElementalSiege.UI
{
    /// <summary>
    /// World/level selection screen with 8 world nodes arranged in a scrollable path.
    /// Each world can expand to show individual levels. Reads progression from SaveManager.
    /// </summary>
    public class WorldMapUI : MonoBehaviour
    {
        #region Nested Types

        /// <summary>
        /// Runtime data for a single world node in the map.
        /// </summary>
        [Serializable]
        public class WorldNode
        {
            /// <summary>Display name of the world.</summary>
            public string worldName;

            /// <summary>World index (0-7).</summary>
            public int worldIndex;

            /// <summary>Root transform for this node in the scroll content.</summary>
            public RectTransform nodeTransform;

            /// <summary>Button to select/expand this world.</summary>
            public Button selectButton;

            /// <summary>Lock icon overlay shown when the world is locked.</summary>
            public GameObject lockIcon;

            /// <summary>Container that holds individual level buttons (hidden until expanded).</summary>
            public RectTransform levelContainer;

            /// <summary>Star count text (e.g., "12/15").</summary>
            public TextMeshProUGUI starCountText;

            /// <summary>World name label.</summary>
            public TextMeshProUGUI nameLabel;

            /// <summary>Background image for tinting.</summary>
            public Image backgroundImage;

            /// <summary>Level data references for this world.</summary>
            public List<LevelData> levels;
        }

        /// <summary>
        /// Runtime representation of a single level button inside an expanded world.
        /// </summary>
        [Serializable]
        public class LevelNodeUI
        {
            public Button button;
            public TextMeshProUGUI levelNumberText;
            public Image[] starImages;
            public GameObject lockOverlay;
        }

        #endregion

        #region Serialized Fields

        [Header("Map Layout")]
        [SerializeField] private ScrollRect _scrollRect;
        [SerializeField] private RectTransform _mapContent;
        [SerializeField] private List<WorldNode> _worldNodes = new List<WorldNode>();

        [Header("Level Buttons")]
        [SerializeField] private GameObject _levelButtonPrefab;
        [SerializeField] private float _levelButtonSpacing = 80f;

        [Header("Visuals")]
        [SerializeField] private Color _unlockedColor = Color.white;
        [SerializeField] private Color _lockedColor = new Color(0.4f, 0.4f, 0.4f, 0.8f);
        [SerializeField] private Sprite _starFilledSprite;
        [SerializeField] private Sprite _starEmptySprite;

        [Header("Navigation")]
        [SerializeField] private Button _backButton;
        [SerializeField] private float _scrollSnapSpeed = 10f;

        [Header("Total Stars")]
        [SerializeField] private TextMeshProUGUI _totalStarsText;

        #endregion

        #region Events

        /// <summary>Raised when a level is selected. Passes the LevelData to load.</summary>
        public event Action<LevelData> OnLevelSelected;

        /// <summary>Raised when the back button is pressed.</summary>
        public event Action OnBackPressed;

        #endregion

        #region Private State

        private int _expandedWorldIndex = -1;
        private readonly List<GameObject> _spawnedLevelButtons = new List<GameObject>();
        private Vector2 _snapTarget;
        private bool _isSnapping;

        // Stub reference — in production, wire via inspector or service locator.
        private ISaveManager _saveManager;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (_backButton != null)
                _backButton.onClick.AddListener(() => OnBackPressed?.Invoke());
        }

        private void OnEnable()
        {
            RefreshMap();
        }

        private void OnDestroy()
        {
            if (_backButton != null)
                _backButton.onClick.RemoveAllListeners();
        }

        private void Update()
        {
            if (_isSnapping && _scrollRect != null)
            {
                _scrollRect.content.anchoredPosition = Vector2.Lerp(
                    _scrollRect.content.anchoredPosition,
                    _snapTarget,
                    _scrollSnapSpeed * Time.unscaledDeltaTime);

                if (Vector2.Distance(_scrollRect.content.anchoredPosition, _snapTarget) < 1f)
                {
                    _scrollRect.content.anchoredPosition = _snapTarget;
                    _isSnapping = false;
                }
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Injects the save manager dependency for reading progression data.
        /// </summary>
        public void SetSaveManager(ISaveManager saveManager)
        {
            _saveManager = saveManager;
            RefreshMap();
        }

        /// <summary>
        /// Refreshes all world nodes and level buttons from current save data.
        /// </summary>
        public void RefreshMap()
        {
            int totalStars = 0;

            for (int i = 0; i < _worldNodes.Count; i++)
            {
                WorldNode node = _worldNodes[i];
                bool unlocked = IsWorldUnlocked(node.worldIndex);
                int worldStars = GetWorldStarCount(node.worldIndex);
                int maxWorldStars = node.levels != null ? node.levels.Count * 3 : 0;
                totalStars += worldStars;

                // Visuals
                if (node.backgroundImage != null)
                    node.backgroundImage.color = unlocked ? _unlockedColor : _lockedColor;

                if (node.lockIcon != null)
                    node.lockIcon.SetActive(!unlocked);

                if (node.nameLabel != null)
                    node.nameLabel.text = node.worldName;

                if (node.starCountText != null)
                    node.starCountText.text = $"{worldStars}/{maxWorldStars}";

                // Button
                if (node.selectButton != null)
                {
                    node.selectButton.interactable = unlocked;
                    int capturedIndex = i;
                    node.selectButton.onClick.RemoveAllListeners();
                    if (unlocked)
                        node.selectButton.onClick.AddListener(() => ToggleWorldExpansion(capturedIndex));
                }

                // Collapse all level containers initially
                if (node.levelContainer != null)
                    node.levelContainer.gameObject.SetActive(false);
            }

            if (_totalStarsText != null)
                _totalStarsText.text = $"Total Stars: {totalStars}";

            // Re-expand if one was open
            if (_expandedWorldIndex >= 0 && _expandedWorldIndex < _worldNodes.Count)
                ExpandWorld(_expandedWorldIndex);
        }

        /// <summary>
        /// Scrolls the map to center on the given world node.
        /// </summary>
        public void ScrollToWorld(int worldIndex)
        {
            if (worldIndex < 0 || worldIndex >= _worldNodes.Count) return;

            WorldNode node = _worldNodes[worldIndex];
            if (node.nodeTransform == null || _mapContent == null) return;

            _snapTarget = -node.nodeTransform.anchoredPosition;
            _isSnapping = true;
        }

        #endregion

        #region Private Helpers

        private void ToggleWorldExpansion(int worldIndex)
        {
            if (_expandedWorldIndex == worldIndex)
            {
                CollapseWorld(worldIndex);
                _expandedWorldIndex = -1;
            }
            else
            {
                if (_expandedWorldIndex >= 0)
                    CollapseWorld(_expandedWorldIndex);

                ExpandWorld(worldIndex);
                _expandedWorldIndex = worldIndex;
            }
        }

        private void ExpandWorld(int worldIndex)
        {
            WorldNode node = _worldNodes[worldIndex];
            if (node.levelContainer == null || node.levels == null) return;

            node.levelContainer.gameObject.SetActive(true);
            ClearSpawnedLevelButtons();

            for (int i = 0; i < node.levels.Count; i++)
            {
                LevelData levelData = node.levels[i];
                if (levelData == null || _levelButtonPrefab == null) continue;

                GameObject buttonObj = Instantiate(_levelButtonPrefab, node.levelContainer);
                _spawnedLevelButtons.Add(buttonObj);

                RectTransform rt = buttonObj.GetComponent<RectTransform>();
                if (rt != null)
                    rt.anchoredPosition = new Vector2(0, -i * _levelButtonSpacing);

                LevelNodeUI levelUI = buttonObj.GetComponent<LevelNodeUI>();
                if (levelUI != null)
                {
                    bool levelUnlocked = IsLevelUnlocked(worldIndex, i);
                    int stars = GetLevelStars(worldIndex, i);

                    if (levelUI.levelNumberText != null)
                        levelUI.levelNumberText.text = (i + 1).ToString();

                    if (levelUI.lockOverlay != null)
                        levelUI.lockOverlay.SetActive(!levelUnlocked);

                    // Star display
                    if (levelUI.starImages != null)
                    {
                        for (int s = 0; s < levelUI.starImages.Length; s++)
                        {
                            if (levelUI.starImages[s] != null)
                            {
                                levelUI.starImages[s].sprite =
                                    s < stars ? _starFilledSprite : _starEmptySprite;
                            }
                        }
                    }

                    if (levelUI.button != null)
                    {
                        levelUI.button.interactable = levelUnlocked;
                        LevelData captured = levelData;
                        levelUI.button.onClick.RemoveAllListeners();
                        if (levelUnlocked)
                            levelUI.button.onClick.AddListener(() => OnLevelSelected?.Invoke(captured));
                    }
                }
            }

            ScrollToWorld(worldIndex);
        }

        private void CollapseWorld(int worldIndex)
        {
            if (worldIndex < 0 || worldIndex >= _worldNodes.Count) return;

            WorldNode node = _worldNodes[worldIndex];
            if (node.levelContainer != null)
                node.levelContainer.gameObject.SetActive(false);

            ClearSpawnedLevelButtons();
        }

        private void ClearSpawnedLevelButtons()
        {
            foreach (var btn in _spawnedLevelButtons)
            {
                if (btn != null)
                    Destroy(btn);
            }
            _spawnedLevelButtons.Clear();
        }

        private bool IsWorldUnlocked(int worldIndex)
        {
            if (_saveManager == null) return worldIndex == 0;
            return _saveManager.IsWorldUnlocked(worldIndex);
        }

        private bool IsLevelUnlocked(int worldIndex, int levelIndex)
        {
            if (_saveManager == null) return levelIndex == 0;
            return _saveManager.IsLevelUnlocked(worldIndex, levelIndex);
        }

        private int GetWorldStarCount(int worldIndex)
        {
            if (_saveManager == null) return 0;
            return _saveManager.GetWorldStarCount(worldIndex);
        }

        private int GetLevelStars(int worldIndex, int levelIndex)
        {
            if (_saveManager == null) return 0;
            return _saveManager.GetLevelStars(worldIndex, levelIndex);
        }

        #endregion
    }

    /// <summary>
    /// Interface for save/progression queries used by the world map.
    /// Implement in your SaveManager class.
    /// </summary>
    public interface ISaveManager
    {
        bool IsWorldUnlocked(int worldIndex);
        bool IsLevelUnlocked(int worldIndex, int levelIndex);
        int GetWorldStarCount(int worldIndex);
        int GetLevelStars(int worldIndex, int levelIndex);
        bool HasShownTutorial(string tutorialId);
        void MarkTutorialShown(string tutorialId);
    }
}
