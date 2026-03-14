using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using ElementalSiege.Utilities;

namespace ElementalSiege.Core
{
    /// <summary>
    /// Singleton audio system with AudioMixer integration.
    /// Handles one-shot SFX via an object pool and music with crossfade support.
    /// Volume settings are persisted to PlayerPrefs.
    /// </summary>
    public class AudioManager : Singleton<AudioManager>
    {
        // ──────────────────────────────────────────────
        //  Constants
        // ──────────────────────────────────────────────

        private const string PrefKeyMaster = "Volume_Master";
        private const string PrefKeyMusic  = "Volume_Music";
        private const string PrefKeySfx    = "Volume_SFX";

        private const float MinDb = -80f;
        private const float MaxDb = 0f;

        // ──────────────────────────────────────────────
        //  Inspector
        // ──────────────────────────────────────────────

        [Header("Audio Mixer")]
        [SerializeField] private AudioMixer _audioMixer;

        [Tooltip("Exposed parameter name for master volume.")]
        [SerializeField] private string _masterVolumeParam = "MasterVolume";

        [Tooltip("Exposed parameter name for music volume.")]
        [SerializeField] private string _musicVolumeParam = "MusicVolume";

        [Tooltip("Exposed parameter name for SFX volume.")]
        [SerializeField] private string _sfxVolumeParam = "SFXVolume";

        [Header("Music")]
        [SerializeField] private AudioSource _musicSourceA;
        [SerializeField] private AudioSource _musicSourceB;
        [SerializeField] private float _crossfadeDuration = 1.5f;

        [Header("SFX Pool")]
        [SerializeField] private GameObject _sfxSourcePrefab;
        [SerializeField] private int _sfxPoolSize = 16;

        // ──────────────────────────────────────────────
        //  State
        // ──────────────────────────────────────────────

        private ObjectPool _sfxPool;
        private AudioSource _activeMusicSource;
        private Coroutine _crossfadeCoroutine;

        // ──────────────────────────────────────────────
        //  Lifecycle
        // ──────────────────────────────────────────────

        protected override void OnSingletonAwake()
        {
            InitialiseMusicSources();
            InitialiseSfxPool();
            LoadVolumeSettings();
        }

        // ──────────────────────────────────────────────
        //  SFX
        // ──────────────────────────────────────────────

        /// <summary>
        /// Plays a one-shot sound effect at the given world position.
        /// Uses object pooling for efficient AudioSource reuse.
        /// </summary>
        /// <param name="clip">The AudioClip to play.</param>
        /// <param name="position">World position for 3D spatialisation.</param>
        /// <param name="volume">Volume multiplier (0-1). Default is 1.</param>
        public void PlaySFX(AudioClip clip, Vector3 position, float volume = 1f)
        {
            if (clip == null)
            {
                Debug.LogWarning("[AudioManager] PlaySFX called with null clip.");
                return;
            }

            var obj = _sfxPool.Get(position, Quaternion.identity);
            if (obj == null) return;

            var source = obj.GetComponent<AudioSource>();
            if (source == null)
            {
                source = obj.AddComponent<AudioSource>();
            }

            source.clip = clip;
            source.volume = volume;
            source.spatialBlend = 1f;
            source.Play();

            _sfxPool.Return(obj, clip.length + 0.1f);
        }

        /// <summary>
        /// Plays a one-shot 2D sound effect (no spatialisation).
        /// </summary>
        /// <param name="clip">The AudioClip to play.</param>
        /// <param name="volume">Volume multiplier (0-1). Default is 1.</param>
        public void PlaySFX2D(AudioClip clip, float volume = 1f)
        {
            if (clip == null) return;

            var obj = _sfxPool.Get(Vector3.zero, Quaternion.identity);
            if (obj == null) return;

            var source = obj.GetComponent<AudioSource>();
            if (source == null)
            {
                source = obj.AddComponent<AudioSource>();
            }

            source.clip = clip;
            source.volume = volume;
            source.spatialBlend = 0f;
            source.Play();

            _sfxPool.Return(obj, clip.length + 0.1f);
        }

        // ──────────────────────────────────────────────
        //  Music
        // ──────────────────────────────────────────────

        /// <summary>
        /// Plays a music track, crossfading from the current track if one is playing.
        /// </summary>
        /// <param name="clip">The music AudioClip.</param>
        /// <param name="loop">Whether the track should loop. Default is true.</param>
        public void PlayMusic(AudioClip clip, bool loop = true)
        {
            if (clip == null)
            {
                Debug.LogWarning("[AudioManager] PlayMusic called with null clip.");
                return;
            }

            // Don't restart the same track
            if (_activeMusicSource != null && _activeMusicSource.clip == clip && _activeMusicSource.isPlaying)
                return;

            var incoming = _activeMusicSource == _musicSourceA ? _musicSourceB : _musicSourceA;
            incoming.clip = clip;
            incoming.loop = loop;
            incoming.volume = 0f;
            incoming.Play();

            if (_crossfadeCoroutine != null)
                StopCoroutine(_crossfadeCoroutine);

            _crossfadeCoroutine = StartCoroutine(CrossfadeMusic(_activeMusicSource, incoming));
            _activeMusicSource = incoming;
        }

        /// <summary>
        /// Stops the currently playing music with a fade-out.
        /// </summary>
        public void StopMusic()
        {
            if (_crossfadeCoroutine != null)
                StopCoroutine(_crossfadeCoroutine);

            _crossfadeCoroutine = StartCoroutine(FadeOutMusic(_activeMusicSource));
        }

        // ──────────────────────────────────────────────
        //  Volume
        // ──────────────────────────────────────────────

        /// <summary>
        /// Sets the volume for a given channel and persists it.
        /// </summary>
        /// <param name="channel">Channel name: "Master", "Music", or "SFX".</param>
        /// <param name="value">Linear volume (0-1).</param>
        public void SetVolume(string channel, float value)
        {
            value = Mathf.Clamp01(value);
            float db = LinearToDecibel(value);

            string paramName;
            string prefKey;

            switch (channel)
            {
                case "Master":
                    paramName = _masterVolumeParam;
                    prefKey = PrefKeyMaster;
                    break;
                case "Music":
                    paramName = _musicVolumeParam;
                    prefKey = PrefKeyMusic;
                    break;
                case "SFX":
                    paramName = _sfxVolumeParam;
                    prefKey = PrefKeySfx;
                    break;
                default:
                    Debug.LogWarning($"[AudioManager] Unknown volume channel: {channel}");
                    return;
            }

            if (_audioMixer != null)
            {
                _audioMixer.SetFloat(paramName, db);
            }

            PlayerPrefs.SetFloat(prefKey, value);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Gets the current linear volume (0-1) for a channel.
        /// </summary>
        /// <param name="channel">Channel name: "Master", "Music", or "SFX".</param>
        /// <returns>Linear volume between 0 and 1.</returns>
        public float GetVolume(string channel)
        {
            string prefKey = channel switch
            {
                "Master" => PrefKeyMaster,
                "Music"  => PrefKeyMusic,
                "SFX"    => PrefKeySfx,
                _        => null
            };

            if (prefKey == null)
            {
                Debug.LogWarning($"[AudioManager] Unknown volume channel: {channel}");
                return 1f;
            }

            return PlayerPrefs.GetFloat(prefKey, 1f);
        }

        // ──────────────────────────────────────────────
        //  Initialisation
        // ──────────────────────────────────────────────

        private void InitialiseMusicSources()
        {
            if (_musicSourceA == null)
            {
                var goA = new GameObject("MusicSource_A");
                goA.transform.SetParent(transform);
                _musicSourceA = goA.AddComponent<AudioSource>();
                _musicSourceA.playOnAwake = false;
                _musicSourceA.spatialBlend = 0f;
                _musicSourceA.loop = true;
            }

            if (_musicSourceB == null)
            {
                var goB = new GameObject("MusicSource_B");
                goB.transform.SetParent(transform);
                _musicSourceB = goB.AddComponent<AudioSource>();
                _musicSourceB.playOnAwake = false;
                _musicSourceB.spatialBlend = 0f;
                _musicSourceB.loop = true;
            }

            _activeMusicSource = _musicSourceA;
        }

        private void InitialiseSfxPool()
        {
            if (_sfxSourcePrefab == null)
            {
                // Create a minimal prefab stand-in at runtime
                _sfxSourcePrefab = new GameObject("SFX_Source_Template");
                _sfxSourcePrefab.AddComponent<AudioSource>();
                _sfxSourcePrefab.SetActive(false);
                _sfxSourcePrefab.transform.SetParent(transform);
            }

            _sfxPool = ObjectPool.Create(_sfxSourcePrefab, _sfxPoolSize, true);
            _sfxPool.transform.SetParent(transform);
        }

        private void LoadVolumeSettings()
        {
            SetVolume("Master", PlayerPrefs.GetFloat(PrefKeyMaster, 1f));
            SetVolume("Music",  PlayerPrefs.GetFloat(PrefKeyMusic, 0.8f));
            SetVolume("SFX",    PlayerPrefs.GetFloat(PrefKeySfx, 1f));
        }

        // ──────────────────────────────────────────────
        //  Crossfade coroutines
        // ──────────────────────────────────────────────

        private IEnumerator CrossfadeMusic(AudioSource outgoing, AudioSource incoming)
        {
            float elapsed = 0f;
            float outStartVolume = outgoing != null ? outgoing.volume : 0f;

            while (elapsed < _crossfadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / _crossfadeDuration;

                if (outgoing != null)
                    outgoing.volume = Mathf.Lerp(outStartVolume, 0f, t);

                incoming.volume = Mathf.Lerp(0f, 1f, t);

                yield return null;
            }

            if (outgoing != null)
            {
                outgoing.Stop();
                outgoing.volume = 0f;
            }

            incoming.volume = 1f;
            _crossfadeCoroutine = null;
        }

        private IEnumerator FadeOutMusic(AudioSource source)
        {
            if (source == null || !source.isPlaying)
                yield break;

            float startVolume = source.volume;
            float elapsed = 0f;

            while (elapsed < _crossfadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(startVolume, 0f, elapsed / _crossfadeDuration);
                yield return null;
            }

            source.Stop();
            source.volume = 0f;
            _crossfadeCoroutine = null;
        }

        // ──────────────────────────────────────────────
        //  Utility
        // ──────────────────────────────────────────────

        private static float LinearToDecibel(float linear)
        {
            if (linear <= 0.0001f) return MinDb;
            return Mathf.Log10(linear) * 20f;
        }
    }
}
