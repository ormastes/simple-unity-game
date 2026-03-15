using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ElementalSiege.Core
{
    /// <summary>
    /// Manages time manipulation effects including slow-motion and freeze frames.
    /// Supports stacking multiple time effects and properly restores Time.timeScale
    /// and Time.fixedDeltaTime. Tracks unscaled time for UI animations during slow-mo.
    /// </summary>
    public class TimeManager : MonoBehaviour
    {
        /// <summary>Fired whenever the effective time scale changes.</summary>
        public event Action<float> OnTimeScaleChanged;

        private static TimeManager _instance;

        /// <summary>Global singleton accessor.</summary>
        public static TimeManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[TimeManager]");
                    _instance = go.AddComponent<TimeManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        /// <summary>
        /// Represents a single time-scale effect on the stack.
        /// </summary>
        private class TimeEffect
        {
            /// <summary>Unique identifier for this effect.</summary>
            public int id;

            /// <summary>Target time scale for this effect.</summary>
            public float targetScale;

            /// <summary>Remaining duration in real-time seconds, or -1 for indefinite.</summary>
            public float remainingDuration;

            /// <summary>Whether this effect is indefinite (must be removed manually).</summary>
            public bool isIndefinite;
        }

        [SerializeField, Tooltip("Default fixed delta time at normal speed")]
        private float _defaultFixedDeltaTime = 0.02f;

        private readonly List<TimeEffect> _effectStack = new List<TimeEffect>();
        private int _nextEffectId;
        private float _baseTimeScale = 1f;
        private Coroutine _transitionCoroutine;

        /// <summary>The current effective time scale.</summary>
        public float CurrentTimeScale => Time.timeScale;

        /// <summary>Whether any time effects are currently active.</summary>
        public bool IsTimeModified => _effectStack.Count > 0;

        /// <summary>
        /// Unscaled time elapsed since game start; useful for UI animations during slow-mo.
        /// </summary>
        public float UnscaledTime => Time.unscaledTime;

        /// <summary>
        /// Unscaled delta time for the current frame; useful for UI animations during slow-mo.
        /// </summary>
        public float UnscaledDeltaTime => Time.unscaledDeltaTime;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            _defaultFixedDeltaTime = Time.fixedDeltaTime;
        }

        private void Update()
        {
            UpdateEffectDurations();
        }

        /// <summary>
        /// Smoothly transitions to a target time scale over a specified duration.
        /// </summary>
        /// <param name="scale">Target time scale (e.g. 0.3 for slow-mo, 1 for normal).</param>
        /// <param name="duration">Duration in real-time seconds that this effect lasts.</param>
        /// <returns>An effect ID that can be used to remove the effect early.</returns>
        public int SetTimeScale(float scale, float duration)
        {
            scale = Mathf.Clamp(scale, 0f, 10f);
            int id = AddEffect(scale, duration);
            ApplyEffectiveTimeScale();
            return id;
        }

        /// <summary>
        /// Applies a brief slow-motion effect, commonly used during dramatic moments.
        /// </summary>
        /// <param name="targetScale">Time scale during slow-motion (default 0.3).</param>
        /// <param name="duration">Duration in real-time seconds (default 2).</param>
        /// <returns>An effect ID that can be used to remove the effect early.</returns>
        public int SlowMotion(float targetScale = 0.3f, float duration = 2f)
        {
            targetScale = Mathf.Clamp(targetScale, 0.01f, 1f);
            int id = AddEffect(targetScale, duration);
            ApplyEffectiveTimeScale();

            Debug.Log($"[TimeManager] Slow-motion: {targetScale}x for {duration}s (ID: {id})");
            return id;
        }

        /// <summary>
        /// Creates a very brief freeze frame, typically used on impact.
        /// </summary>
        /// <param name="duration">Duration in real-time seconds (default 0.1).</param>
        /// <returns>An effect ID that can be used to remove the effect early.</returns>
        public int FreezeFrame(float duration = 0.1f)
        {
            int id = AddEffect(0f, duration);
            ApplyEffectiveTimeScale();

            Debug.Log($"[TimeManager] Freeze frame for {duration}s (ID: {id})");
            return id;
        }

        /// <summary>
        /// Smoothly transitions to a target time scale over a transition period.
        /// The effect persists for the given duration after reaching the target.
        /// </summary>
        /// <param name="targetScale">Target time scale.</param>
        /// <param name="transitionDuration">Time (real seconds) to ease into the target scale.</param>
        /// <param name="holdDuration">Time (real seconds) to hold at the target scale.</param>
        /// <returns>An effect ID that can be used to remove the effect early.</returns>
        public int SmoothSlowMotion(float targetScale, float transitionDuration, float holdDuration)
        {
            int id = AddEffect(targetScale, transitionDuration + holdDuration);

            if (_transitionCoroutine != null)
            {
                StopCoroutine(_transitionCoroutine);
            }

            _transitionCoroutine = StartCoroutine(SmoothTransitionCoroutine(targetScale, transitionDuration));
            return id;
        }

        /// <summary>
        /// Removes a specific time effect by its ID.
        /// </summary>
        /// <param name="effectId">The ID returned when the effect was created.</param>
        public void RemoveEffect(int effectId)
        {
            _effectStack.RemoveAll(e => e.id == effectId);
            ApplyEffectiveTimeScale();
        }

        /// <summary>
        /// Removes all active time effects and restores normal time.
        /// </summary>
        public void ClearAllEffects()
        {
            _effectStack.Clear();

            if (_transitionCoroutine != null)
            {
                StopCoroutine(_transitionCoroutine);
                _transitionCoroutine = null;
            }

            ApplyEffectiveTimeScale();
            Debug.Log("[TimeManager] All time effects cleared.");
        }

        /// <summary>
        /// Adds a new time effect to the stack.
        /// </summary>
        private int AddEffect(float targetScale, float duration)
        {
            int id = _nextEffectId++;

            var effect = new TimeEffect
            {
                id = id,
                targetScale = targetScale,
                remainingDuration = duration,
                isIndefinite = duration < 0f
            };

            _effectStack.Add(effect);
            return id;
        }

        /// <summary>
        /// Updates remaining durations and removes expired effects each frame.
        /// </summary>
        private void UpdateEffectDurations()
        {
            if (_effectStack.Count == 0) return;

            bool changed = false;

            for (int i = _effectStack.Count - 1; i >= 0; i--)
            {
                var effect = _effectStack[i];
                if (effect.isIndefinite) continue;

                effect.remainingDuration -= Time.unscaledDeltaTime;

                if (effect.remainingDuration <= 0f)
                {
                    _effectStack.RemoveAt(i);
                    changed = true;
                }
            }

            if (changed)
            {
                ApplyEffectiveTimeScale();
            }
        }

        /// <summary>
        /// Calculates and applies the effective time scale from all stacked effects.
        /// The lowest (slowest) scale wins when multiple effects overlap.
        /// </summary>
        private void ApplyEffectiveTimeScale()
        {
            float effectiveScale;

            if (_effectStack.Count == 0)
            {
                effectiveScale = _baseTimeScale;
            }
            else
            {
                // The most extreme (lowest) scale takes priority
                effectiveScale = _baseTimeScale;
                foreach (var effect in _effectStack)
                {
                    if (effect.targetScale < effectiveScale)
                    {
                        effectiveScale = effect.targetScale;
                    }
                }
            }

            float previousScale = Time.timeScale;
            Time.timeScale = effectiveScale;
            Time.fixedDeltaTime = _defaultFixedDeltaTime * effectiveScale;

            if (!Mathf.Approximately(previousScale, effectiveScale))
            {
                OnTimeScaleChanged?.Invoke(effectiveScale);
            }
        }

        /// <summary>
        /// Coroutine that smoothly eases from the current time scale to the target.
        /// </summary>
        private IEnumerator SmoothTransitionCoroutine(float targetScale, float transitionDuration)
        {
            float startScale = Time.timeScale;
            float elapsed = 0f;

            while (elapsed < transitionDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / transitionDuration);
                float newScale = Mathf.Lerp(startScale, targetScale, t);

                Time.timeScale = newScale;
                Time.fixedDeltaTime = _defaultFixedDeltaTime * newScale;
                OnTimeScaleChanged?.Invoke(newScale);

                yield return null;
            }

            _transitionCoroutine = null;
        }

        private void OnDestroy()
        {
            // Restore normal time when the manager is destroyed
            if (_instance == this)
            {
                Time.timeScale = 1f;
                Time.fixedDeltaTime = _defaultFixedDeltaTime;
                _instance = null;
            }
        }
    }
}
