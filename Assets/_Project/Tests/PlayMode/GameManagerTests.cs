using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ElementalSiege.Tests.PlayMode
{
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
            _gameManager.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            // Restore time scale in case a test left it paused
            Time.timeScale = 1f;

            if (_managerObject != null)
            {
                Object.Destroy(_managerObject);
            }
        }

        [Test]
        public void GameManager_StartsInBootState()
        {
            Assert.AreEqual(GameState.Boot, _gameManager.CurrentState,
                "GameManager should start in the Boot state after initialization");
        }

        [UnityTest]
        public IEnumerator GameManager_TransitionsToMainMenu()
        {
            _gameManager.TransitionTo(GameState.MainMenu);

            yield return null;

            Assert.AreEqual(GameState.MainMenu, _gameManager.CurrentState,
                "GameManager should transition to MainMenu state");
        }

        [UnityTest]
        public IEnumerator GameManager_PauseSetsTimeScaleZero()
        {
            _gameManager.TransitionTo(GameState.Playing);
            yield return null;

            _gameManager.Pause();
            yield return null;

            Assert.AreEqual(0f, Time.timeScale, 0.001f,
                "Pausing the game should set Time.timeScale to 0");
            Assert.AreEqual(GameState.Paused, _gameManager.CurrentState,
                "GameManager should be in Paused state after calling Pause()");
        }

        [UnityTest]
        public IEnumerator GameManager_ResumeSetsTimeScaleOne()
        {
            _gameManager.TransitionTo(GameState.Playing);
            yield return null;

            _gameManager.Pause();
            yield return null;

            _gameManager.Resume();
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

            _gameManager.OnStateChanged += (oldState, newState) =>
            {
                eventFired = true;
                receivedOldState = oldState;
                receivedNewState = newState;
            };

            _gameManager.TransitionTo(GameState.MainMenu);
            yield return null;

            Assert.IsTrue(eventFired, "OnStateChanged event should fire on state transition");
            Assert.AreEqual(GameState.Boot, receivedOldState,
                "Event should report the previous state correctly");
            Assert.AreEqual(GameState.MainMenu, receivedNewState,
                "Event should report the new state correctly");
        }
    }

    /// <summary>
    /// Game state enumeration.
    /// </summary>
    public enum GameState
    {
        Boot,
        MainMenu,
        LevelSelect,
        Loading,
        Playing,
        Paused,
        LevelComplete,
        LevelFailed,
        GameOver
    }

    /// <summary>
    /// Stub GameManager for test compilation.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public GameState CurrentState { get; private set; }

        public delegate void StateChangeHandler(GameState oldState, GameState newState);
        public event StateChangeHandler OnStateChanged;

        private GameState _stateBeforePause;

        public void Initialize()
        {
            CurrentState = GameState.Boot;
        }

        public void TransitionTo(GameState newState)
        {
            GameState oldState = CurrentState;
            CurrentState = newState;
            OnStateChanged?.Invoke(oldState, newState);
        }

        public void Pause()
        {
            if (CurrentState == GameState.Paused) return;

            _stateBeforePause = CurrentState;
            Time.timeScale = 0f;
            TransitionTo(GameState.Paused);
        }

        public void Resume()
        {
            if (CurrentState != GameState.Paused) return;

            Time.timeScale = 1f;
            TransitionTo(_stateBeforePause);
        }
    }
}
