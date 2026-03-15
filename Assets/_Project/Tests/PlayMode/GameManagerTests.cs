using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ElementalSiege.Core;
using GameState = ElementalSiege.Core.GameManager.GameState;

namespace ElementalSiege.Tests.PlayMode
{
    /// <summary>
    /// Tests for GameManager using the real ElementalSiege.Core assembly.
    /// The real GameManager extends Singleton&lt;GameManager&gt; and uses:
    /// - SetState(GameState) instead of TransitionTo(GameState)
    /// - PauseGame() / ResumeGame() instead of Pause() / Resume()
    /// - Static event OnGameStateChanged instead of instance OnStateChanged
    /// - GameState is a nested enum: GameManager.GameState
    /// </summary>
    [TestFixture]
    public class GameManagerTests
    {
        private GameObject _managerObject;
        private GameManager _gameManager;

        [SetUp]
        public void SetUp()
        {
            _managerObject = new GameObject("TestGameManager");
            _gameManager = _managerObject.AddComponent<GameManager>();
            // Singleton pattern Awake runs automatically.
            // The Start method transitions to MainMenu, but Start runs after SetUp in test.
        }

        [TearDown]
        public void TearDown()
        {
            // Restore time scale in case a test left it paused
            Time.timeScale = 1f;

            // Unsubscribe static event to prevent leaks between tests
            GameManager.OnGameStateChanged = null;

            if (_managerObject != null)
            {
                Object.DestroyImmediate(_managerObject);
            }
        }

        [Test]
        public void GameManager_CurrentState_IsAccessible()
        {
            // After Awake but before Start, state should be Boot (the initial field value)
            Assert.IsNotNull(_gameManager,
                "GameManager should be a valid component");
            // CurrentState returns the _currentState field, initially Boot
            Assert.AreEqual(GameState.Boot, _gameManager.CurrentState,
                "GameManager should start in Boot state before Start() runs");
        }

        [UnityTest]
        public IEnumerator GameManager_TransitionsToState()
        {
            _gameManager.SetState(GameState.Playing);

            yield return null;

            Assert.AreEqual(GameState.Playing, _gameManager.CurrentState,
                "GameManager should transition to Playing state");
        }

        [UnityTest]
        public IEnumerator GameManager_PauseSetsTimeScaleZero()
        {
            _gameManager.SetState(GameState.Playing);
            yield return null;

            _gameManager.PauseGame();
            yield return null;

            Assert.AreEqual(0f, Time.timeScale, 0.001f,
                "Pausing the game should set Time.timeScale to 0");
            Assert.AreEqual(GameState.Paused, _gameManager.CurrentState,
                "GameManager should be in Paused state after calling PauseGame()");
        }

        [UnityTest]
        public IEnumerator GameManager_ResumeSetsTimeScaleOne()
        {
            _gameManager.SetState(GameState.Playing);
            yield return null;

            _gameManager.PauseGame();
            yield return null;

            _gameManager.ResumeGame();
            yield return null;

            Assert.AreEqual(1f, Time.timeScale, 0.001f,
                "Resuming the game should restore Time.timeScale to 1");
            Assert.AreEqual(GameState.Playing, _gameManager.CurrentState,
                "GameManager should return to Playing state after resuming");
        }

        [UnityTest]
        public IEnumerator GameManager_FiresStateChangeEvent()
        {
            GameState receivedOldState = GameState.Boot;
            GameState receivedNewState = GameState.Boot;
            bool eventFired = false;

            GameManager.OnGameStateChanged += (oldState, newState) =>
            {
                eventFired = true;
                receivedOldState = oldState;
                receivedNewState = newState;
            };

            _gameManager.SetState(GameState.Playing);
            yield return null;

            Assert.IsTrue(eventFired, "OnGameStateChanged event should fire on state transition");
            Assert.AreEqual(GameState.Boot, receivedOldState,
                "Event should report the previous state correctly");
            Assert.AreEqual(GameState.Playing, receivedNewState,
                "Event should report the new state correctly");
        }

        [Test]
        public void GameManager_IsPaused_ReturnsFalse_WhenNotPaused()
        {
            Assert.IsFalse(_gameManager.IsPaused,
                "IsPaused should be false when not in Paused state");
        }

        [UnityTest]
        public IEnumerator GameManager_CompleteLevel_SetsState()
        {
            _gameManager.SetState(GameState.Playing);
            yield return null;

            _gameManager.CompleteLevel();
            yield return null;

            Assert.AreEqual(GameState.LevelComplete, _gameManager.CurrentState,
                "CompleteLevel should transition to LevelComplete state");
        }

        [UnityTest]
        public IEnumerator GameManager_FailLevel_SetsState()
        {
            _gameManager.SetState(GameState.Playing);
            yield return null;

            _gameManager.FailLevel();
            yield return null;

            Assert.AreEqual(GameState.LevelFailed, _gameManager.CurrentState,
                "FailLevel should transition to LevelFailed state");
        }
    }
}
