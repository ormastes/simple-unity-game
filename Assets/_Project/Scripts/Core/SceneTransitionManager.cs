using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ElementalSiege.Core
{
    /// <summary>
    /// Singleton manager that handles scene transitions with fade-to-black effects.
    /// Persists across scenes via DontDestroyOnLoad.
    /// </summary>
    public class SceneTransitionManager : MonoBehaviour
    {
        /// <summary>Fired when a scene transition begins.</summary>
        public event Action OnTransitionStarted;

        /// <summary>Fired when a scene transition completes.</summary>
        public event Action OnTransitionCompleted;

        /// <summary>Optional callback that receives async loading progress (0-1).</summary>
        public event Action<float> OnLoadProgress;

        private static SceneTransitionManager _instance;

        /// <summary>Global singleton accessor.</summary>
        public static SceneTransitionManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[SceneTransitionManager]");
                    _instance = go.AddComponent<SceneTransitionManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private Canvas _fadeCanvas;
        private Image _fadeImage;
        private bool _isTransitioning;

        /// <summary>Whether a transition is currently in progress.</summary>
        public bool IsTransitioning => _isTransitioning;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            CreateFadeCanvas();
        }

        /// <summary>
        /// Creates the full-screen canvas and black overlay image used for fade effects.
        /// </summary>
        private void CreateFadeCanvas()
        {
            var canvasGo = new GameObject("FadeCanvas");
            canvasGo.transform.SetParent(transform);

            _fadeCanvas = canvasGo.AddComponent<Canvas>();
            _fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _fadeCanvas.sortingOrder = 9999;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasGo.AddComponent<GraphicRaycaster>();

            var imageGo = new GameObject("FadeImage");
            imageGo.transform.SetParent(canvasGo.transform, false);

            _fadeImage = imageGo.AddComponent<Image>();
            _fadeImage.color = new Color(0f, 0f, 0f, 0f);
            _fadeImage.raycastTarget = true;

            var rect = _fadeImage.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Loads a scene with a fade-out / async-load / fade-in transition.
        /// </summary>
        /// <param name="sceneName">Name of the scene to load.</param>
        /// <param name="fadeDuration">Duration in seconds for each fade (in and out).</param>
        public void LoadSceneWithTransition(string sceneName, float fadeDuration = 0.5f)
        {
            if (_isTransitioning)
            {
                Debug.LogWarning("[SceneTransitionManager] Transition already in progress.");
                return;
            }

            StartCoroutine(TransitionCoroutine(sceneName, fadeDuration));
        }

        /// <summary>
        /// Core transition coroutine: fade out, async load, fade in.
        /// </summary>
        private IEnumerator TransitionCoroutine(string sceneName, float fadeDuration)
        {
            _isTransitioning = true;
            OnTransitionStarted?.Invoke();

            yield return StartCoroutine(FadeToBlack(fadeDuration));

            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
            if (asyncLoad != null)
            {
                asyncLoad.allowSceneActivation = false;

                while (asyncLoad.progress < 0.9f)
                {
                    OnLoadProgress?.Invoke(asyncLoad.progress / 0.9f);
                    yield return null;
                }

                OnLoadProgress?.Invoke(1f);
                asyncLoad.allowSceneActivation = true;

                while (!asyncLoad.isDone)
                {
                    yield return null;
                }
            }

            yield return StartCoroutine(FadeFromBlack(fadeDuration));

            _isTransitioning = false;
            OnTransitionCompleted?.Invoke();
        }

        /// <summary>
        /// Coroutine that fades the overlay from transparent to fully opaque black.
        /// </summary>
        /// <param name="duration">Fade duration in seconds.</param>
        public IEnumerator FadeToBlack(float duration)
        {
            float elapsed = 0f;
            Color color = _fadeImage.color;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float alpha = Mathf.Clamp01(elapsed / duration);
                _fadeImage.color = new Color(color.r, color.g, color.b, alpha);
                yield return null;
            }

            _fadeImage.color = new Color(0f, 0f, 0f, 1f);
        }

        /// <summary>
        /// Coroutine that fades the overlay from fully opaque black to transparent.
        /// </summary>
        /// <param name="duration">Fade duration in seconds.</param>
        public IEnumerator FadeFromBlack(float duration)
        {
            float elapsed = 0f;
            Color color = _fadeImage.color;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float alpha = 1f - Mathf.Clamp01(elapsed / duration);
                _fadeImage.color = new Color(color.r, color.g, color.b, alpha);
                yield return null;
            }

            _fadeImage.color = new Color(0f, 0f, 0f, 0f);
        }
    }
}
