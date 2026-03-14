using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using ElementalSiege.Utilities;

namespace ElementalSiege.Core
{
    /// <summary>
    /// Central game state manager. Handles state transitions, scene loading,
    /// and pause/resume functionality.
    /// </summary>
    public class GameManager : Singleton<GameManager>
    {
        /// <summary>All possible game states.</summary>
        public enum GameState
        {
            Boot,
            MainMenu,
            WorldMap,
            Playing,
            Paused,
            LevelComplete,
            LevelFailed
        }

        /// <summary>
        /// Fired whenever the game state changes.
        /// Parameters: previous state, new state.
        /// </summary>
        public static event Action<GameState, GameState> OnGameStateChanged;

        [Header("Scene Names")]
        [SerializeField] private string _mainMenuScene = "MainMenu";
        [SerializeField] private string _worldMapScene = "WorldMap";

        private GameState _currentState = GameState.Boot;
        private GameState _stateBeforePause;

        /// <summary>The current game state.</summary>
        public GameState CurrentState => _currentState;

        /// <summary>Whether the game is currently paused.</summary>
        public bool IsPaused => _currentState == GameState.Paused;

        protected override void OnSingletonAwake()
        {
            Application.targetFrameRate = 60;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Start()
        {
            SetState(GameState.MainMenu);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        // ──────────────────────────────────────────────
        //  State transitions
        // ──────────────────────────────────────────────

        /// <summary>
        /// Transitions to a new game state. Logs the transition and fires the event.
        /// </summary>
        /// <param name="newState">The state to transition to.</param>
        public void SetState(GameState newState)
        {
            if (_currentState == newState) return;

            GameState previous = _currentState;
            _currentState = newState;

            Debug.Log($"[GameManager] {previous} -> {newState}");
            OnGameStateChanged?.Invoke(previous, newState);
        }

        // ──────────────────────────────────────────────
        //  Scene loading
        // ──────────────────────────────────────────────

        /// <summary>
        /// Loads the main menu scene and transitions to MainMenu state.
        /// </summary>
        public void GoToMainMenu()
        {
            Time.timeScale = 1f;
            LoadScene(_mainMenuScene);
            SetState(GameState.MainMenu);
        }

        /// <summary>
        /// Loads the world map scene and transitions to WorldMap state.
        /// </summary>
        public void GoToWorldMap()
        {
            Time.timeScale = 1f;
            LoadScene(_worldMapScene);
            SetState(GameState.WorldMap);
        }

        /// <summary>
        /// Loads a level scene by name and transitions to Playing state.
        /// </summary>
        /// <param name="sceneName">The scene to load.</param>
        public void LoadLevel(string sceneName)
        {
            Time.timeScale = 1f;
            LoadScene(sceneName);
            SetState(GameState.Playing);
        }

        /// <summary>
        /// Reloads the currently active scene.
        /// </summary>
        public void RestartLevel()
        {
            Time.timeScale = 1f;
            string currentScene = SceneManager.GetActiveScene().name;
            LoadScene(currentScene);
            SetState(GameState.Playing);
        }

        /// <summary>
        /// Loads a scene additively (for UI overlays, etc.).
        /// </summary>
        /// <param name="sceneName">The scene to load additively.</param>
        public void LoadSceneAdditive(string sceneName)
        {
            SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        }

        /// <summary>
        /// Unloads an additive scene.
        /// </summary>
        /// <param name="sceneName">The scene to unload.</param>
        public void UnloadScene(string sceneName)
        {
            SceneManager.UnloadSceneAsync(sceneName);
        }

        private void LoadScene(string sceneName)
        {
            SceneManager.LoadScene(sceneName);
        }

        // ──────────────────────────────────────────────
        //  Pause / Resume
        // ──────────────────────────────────────────────

        /// <summary>
        /// Pauses the game by setting Time.timeScale to 0.
        /// </summary>
        public void PauseGame()
        {
            if (_currentState != GameState.Playing) return;

            _stateBeforePause = _currentState;
            Time.timeScale = 0f;
            SetState(GameState.Paused);
        }

        /// <summary>
        /// Resumes the game by restoring Time.timeScale to 1.
        /// </summary>
        public void ResumeGame()
        {
            if (_currentState != GameState.Paused) return;

            Time.timeScale = 1f;
            SetState(_stateBeforePause);
        }

        /// <summary>
        /// Toggles between Paused and Playing states.
        /// </summary>
        public void TogglePause()
        {
            if (IsPaused)
                ResumeGame();
            else
                PauseGame();
        }

        // ──────────────────────────────────────────────
        //  Level outcome
        // ──────────────────────────────────────────────

        /// <summary>
        /// Call when the player completes a level successfully.
        /// </summary>
        public void CompleteLevel()
        {
            SetState(GameState.LevelComplete);
        }

        /// <summary>
        /// Call when the player fails a level.
        /// </summary>
        public void FailLevel()
        {
            SetState(GameState.LevelFailed);
        }

        // ──────────────────────────────────────────────
        //  Callbacks
        // ──────────────────────────────────────────────

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Debug.Log($"[GameManager] Scene loaded: {scene.name} (mode: {mode})");
        }

        // ──────────────────────────────────────────────
        //  Application lifecycle
        // ──────────────────────────────────────────────

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && _currentState == GameState.Playing)
            {
                PauseGame();
            }
        }
    }
}
