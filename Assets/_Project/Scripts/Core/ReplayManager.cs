using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ElementalSiege.Core
{
    /// <summary>
    /// A single frame of recorded replay data capturing one launch action.
    /// </summary>
    [Serializable]
    public struct ReplayFrame
    {
        /// <summary>Time elapsed since recording started, in seconds.</summary>
        public float timestamp;

        /// <summary>Index of the orb used in this frame.</summary>
        public int orbIndex;

        /// <summary>Normalized direction vector the orb was launched toward.</summary>
        public Vector2 launchDirection;

        /// <summary>Force magnitude applied to the orb.</summary>
        public float launchForce;

        /// <summary>Time (relative to timestamp) when the element ability was activated, or -1 if unused.</summary>
        public float abilityActivateTime;
    }

    /// <summary>
    /// Complete replay data for a single level attempt.
    /// </summary>
    [Serializable]
    public class ReplayData
    {
        /// <summary>Identifier of the level this replay belongs to.</summary>
        public string levelId;

        /// <summary>Final score achieved during this attempt.</summary>
        public int score;

        /// <summary>Total duration of the recorded attempt in seconds.</summary>
        public float totalDuration;

        /// <summary>Ordered list of replay frames capturing each launch action.</summary>
        public List<ReplayFrame> frames = new List<ReplayFrame>();
    }

    /// <summary>
    /// Records and replays level attempts, saving the best replay per level to persistent storage.
    /// Supports variable playback speed (0.5x, 1x, 2x).
    /// </summary>
    public class ReplayManager : MonoBehaviour
    {
        /// <summary>Fired when replay playback begins.</summary>
        public event Action OnReplayStarted;

        /// <summary>Fired when replay playback finishes.</summary>
        public event Action OnReplayComplete;

        /// <summary>Fired each time a replay frame is played back, passing the frame data.</summary>
        public event Action<ReplayFrame> OnReplayFramePlayed;

        private static ReplayManager _instance;

        /// <summary>Global singleton accessor.</summary>
        public static ReplayManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[ReplayManager]");
                    _instance = go.AddComponent<ReplayManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        [SerializeField, Tooltip("Available playback speed options")]
        private float[] _speedOptions = { 0.5f, 1.0f, 2.0f };

        private ReplayData _currentRecording;
        private bool _isRecording;
        private bool _isPlaying;
        private float _recordingStartTime;
        private float _currentPlaybackSpeed = 1.0f;
        private int _currentSpeedIndex = 1;
        private Coroutine _playbackCoroutine;
        private string _replayDirectory;

        /// <summary>Whether the manager is currently recording.</summary>
        public bool IsRecording => _isRecording;

        /// <summary>Whether the manager is currently playing back a replay.</summary>
        public bool IsPlaying => _isPlaying;

        /// <summary>Current playback speed multiplier.</summary>
        public float CurrentPlaybackSpeed => _currentPlaybackSpeed;

        /// <summary>Available speed options for playback.</summary>
        public float[] SpeedOptions => _speedOptions;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            _replayDirectory = Path.Combine(Application.persistentDataPath, "Replays");
            if (!Directory.Exists(_replayDirectory))
            {
                Directory.CreateDirectory(_replayDirectory);
            }
        }

        /// <summary>
        /// Begins recording a new replay for the specified level.
        /// </summary>
        /// <param name="levelId">Identifier of the level being played.</param>
        public void StartRecording(string levelId)
        {
            if (_isPlaying)
            {
                Debug.LogWarning("[ReplayManager] Cannot record while playing a replay.");
                return;
            }

            _currentRecording = new ReplayData
            {
                levelId = levelId,
                frames = new List<ReplayFrame>()
            };

            _recordingStartTime = Time.time;
            _isRecording = true;

            Debug.Log($"[ReplayManager] Started recording for level: {levelId}");
        }

        /// <summary>
        /// Records a single frame/action during an active recording session.
        /// </summary>
        /// <param name="orbIndex">Index of the orb launched.</param>
        /// <param name="direction">Launch direction vector.</param>
        /// <param name="force">Launch force magnitude.</param>
        /// <param name="abilityTime">Time of ability activation relative to launch, or -1 if unused.</param>
        public void RecordFrame(int orbIndex, Vector2 direction, float force, float abilityTime = -1f)
        {
            if (!_isRecording)
            {
                Debug.LogWarning("[ReplayManager] Not currently recording.");
                return;
            }

            var frame = new ReplayFrame
            {
                timestamp = Time.time - _recordingStartTime,
                orbIndex = orbIndex,
                launchDirection = direction.normalized,
                launchForce = force,
                abilityActivateTime = abilityTime
            };

            _currentRecording.frames.Add(frame);
        }

        /// <summary>
        /// Stops the current recording and returns the completed replay data.
        /// </summary>
        /// <param name="score">Final score achieved during this attempt.</param>
        /// <returns>The completed ReplayData, or null if not recording.</returns>
        public ReplayData StopRecording(int score = 0)
        {
            if (!_isRecording) return null;

            _isRecording = false;
            _currentRecording.totalDuration = Time.time - _recordingStartTime;
            _currentRecording.score = score;

            var completedRecording = _currentRecording;
            _currentRecording = null;

            Debug.Log($"[ReplayManager] Stopped recording. Frames: {completedRecording.frames.Count}, " +
                      $"Duration: {completedRecording.totalDuration:F2}s");

            // Auto-save if it is the best replay for this level
            SaveBestReplay(completedRecording);

            return completedRecording;
        }

        /// <summary>
        /// Plays back a replay at the current playback speed.
        /// </summary>
        /// <param name="data">The replay data to play.</param>
        public void PlayReplay(ReplayData data)
        {
            if (data == null || data.frames.Count == 0)
            {
                Debug.LogWarning("[ReplayManager] No replay data to play.");
                return;
            }

            if (_isRecording)
            {
                Debug.LogWarning("[ReplayManager] Cannot play replay while recording.");
                return;
            }

            if (_isPlaying)
            {
                StopReplay();
            }

            _playbackCoroutine = StartCoroutine(PlaybackCoroutine(data));
        }

        /// <summary>
        /// Stops the currently playing replay.
        /// </summary>
        public void StopReplay()
        {
            if (_playbackCoroutine != null)
            {
                StopCoroutine(_playbackCoroutine);
                _playbackCoroutine = null;
            }

            _isPlaying = false;
        }

        /// <summary>
        /// Cycles to the next available playback speed.
        /// </summary>
        public void CyclePlaybackSpeed()
        {
            _currentSpeedIndex = (_currentSpeedIndex + 1) % _speedOptions.Length;
            _currentPlaybackSpeed = _speedOptions[_currentSpeedIndex];
            Debug.Log($"[ReplayManager] Playback speed set to {_currentPlaybackSpeed}x");
        }

        /// <summary>
        /// Sets a specific playback speed multiplier.
        /// </summary>
        /// <param name="speed">Speed multiplier (e.g. 0.5, 1.0, 2.0).</param>
        public void SetPlaybackSpeed(float speed)
        {
            _currentPlaybackSpeed = Mathf.Max(0.1f, speed);

            for (int i = 0; i < _speedOptions.Length; i++)
            {
                if (Mathf.Approximately(_speedOptions[i], speed))
                {
                    _currentSpeedIndex = i;
                    break;
                }
            }
        }

        /// <summary>
        /// Coroutine that drives replay playback, firing frame events at the correct timestamps.
        /// </summary>
        private IEnumerator PlaybackCoroutine(ReplayData data)
        {
            _isPlaying = true;
            OnReplayStarted?.Invoke();

            float playbackTime = 0f;
            int frameIndex = 0;

            Debug.Log($"[ReplayManager] Playing replay for level: {data.levelId} " +
                      $"({data.frames.Count} frames, {_currentPlaybackSpeed}x speed)");

            while (frameIndex < data.frames.Count)
            {
                playbackTime += Time.unscaledDeltaTime * _currentPlaybackSpeed;

                while (frameIndex < data.frames.Count &&
                       data.frames[frameIndex].timestamp <= playbackTime)
                {
                    OnReplayFramePlayed?.Invoke(data.frames[frameIndex]);
                    frameIndex++;
                }

                yield return null;
            }

            _isPlaying = false;
            _playbackCoroutine = null;
            OnReplayComplete?.Invoke();

            Debug.Log("[ReplayManager] Replay playback complete.");
        }

        /// <summary>
        /// Saves the replay if it beats the existing best score for this level.
        /// </summary>
        private void SaveBestReplay(ReplayData data)
        {
            string filePath = GetReplayFilePath(data.levelId);

            if (File.Exists(filePath))
            {
                try
                {
                    string existingJson = File.ReadAllText(filePath);
                    var existing = JsonUtility.FromJson<ReplayData>(existingJson);
                    if (existing != null && existing.score >= data.score)
                    {
                        return; // Existing replay is better or equal
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ReplayManager] Failed to read existing replay: {e.Message}");
                }
            }

            try
            {
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(filePath, json);
                Debug.Log($"[ReplayManager] Best replay saved for level: {data.levelId}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ReplayManager] Failed to save replay: {e.Message}");
            }
        }

        /// <summary>
        /// Loads the best saved replay for a specific level.
        /// </summary>
        /// <param name="levelId">The level identifier.</param>
        /// <returns>The saved ReplayData, or null if none exists.</returns>
        public ReplayData LoadBestReplay(string levelId)
        {
            string filePath = GetReplayFilePath(levelId);

            if (!File.Exists(filePath)) return null;

            try
            {
                string json = File.ReadAllText(filePath);
                return JsonUtility.FromJson<ReplayData>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ReplayManager] Failed to load replay: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Returns the file path for a level's replay file.
        /// </summary>
        private string GetReplayFilePath(string levelId)
        {
            string sanitized = levelId.Replace("/", "_").Replace("\\", "_");
            return Path.Combine(_replayDirectory, $"replay_{sanitized}.json");
        }
    }
}
