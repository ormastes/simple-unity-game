using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ElementalSiege.Core;

namespace ElementalSiege.VFX
{
    /// <summary>
    /// Spawns special visual effects when two elements combine (e.g., Fire+Ice = Steam).
    /// Manages pooled combo particles, combo text popups, and camera shake integration.
    /// </summary>
    public class ElementComboVFX : MonoBehaviour
    {
        #region Nested Types

        /// <summary>
        /// Defines the visual properties of a specific element combination.
        /// </summary>
        [Serializable]
        public class ComboDefinition
        {
            /// <summary>First element in the combination.</summary>
            public ElementType elementA;

            /// <summary>Second element in the combination.</summary>
            public ElementType elementB;

            /// <summary>Display name (e.g., "STEAM BLAST!").</summary>
            public string comboName;

            /// <summary>Particle effect prefab for this combo.</summary>
            public ParticleSystem effectPrefab;

            /// <summary>Primary tint color for particles.</summary>
            public Color primaryColor = Color.white;

            /// <summary>Secondary tint color for particles.</summary>
            public Color secondaryColor = Color.gray;

            /// <summary>Camera shake intensity (0 = none).</summary>
            [Range(0f, 2f)]
            public float cameraShakeIntensity = 0.3f;

            /// <summary>Camera shake duration.</summary>
            public float cameraShakeDuration = 0.3f;

            /// <summary>Audio clip to play on combo.</summary>
            public AudioClip comboSound;

            /// <summary>Scale of the effect.</summary>
            public float effectScale = 1f;
        }

        #endregion

        #region Serialized Fields

        [Header("Combo Definitions")]
        [SerializeField] private List<ComboDefinition> _comboDefinitions = new List<ComboDefinition>();

        [Header("Text Popup")]
        [SerializeField] private Canvas _worldCanvas;
        [SerializeField] private GameObject _comboTextPrefab;
        [SerializeField] private float _textRiseSpeed = 2f;
        [SerializeField] private float _textDuration = 1.5f;
        [SerializeField] private float _textScaleIn = 0.3f;
        [SerializeField] private float _textScaleOvershoot = 1.3f;
        [SerializeField] private Vector3 _textOffset = new Vector3(0f, 1.5f, 0f);

        [Header("Camera Shake")]
        [SerializeField] private Camera _mainCamera;
        [SerializeField] private float _defaultShakeIntensity = 0.2f;
        [SerializeField] private float _defaultShakeDuration = 0.25f;

        [Header("Pool Settings")]
        [SerializeField] private int _poolSizePerCombo = 3;

        #endregion

        #region Events

        /// <summary>Raised when a combo effect is triggered. Passes the combo name and position.</summary>
        public event Action<string, Vector3> OnComboTriggered;

        #endregion

        #region Private State

        private readonly Dictionary<string, Queue<ParticleSystem>> _effectPools =
            new Dictionary<string, Queue<ParticleSystem>>();
        private readonly Dictionary<string, ComboDefinition> _comboLookup =
            new Dictionary<string, ComboDefinition>();
        private Transform _poolContainer;
        private Coroutine _shakeCoroutine;
        private Vector3 _cameraOriginalPosition;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _poolContainer = new GameObject("ComboVFX_Pool").transform;
            _poolContainer.SetParent(transform);

            if (_mainCamera == null)
                _mainCamera = Camera.main;

            BuildLookupTable();
            InitializePools();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Triggers the combo VFX for two colliding elements at the given position.
        /// </summary>
        /// <param name="elementA">First element.</param>
        /// <param name="elementB">Second element.</param>
        /// <param name="position">World-space position for the effect.</param>
        /// <returns>True if a combo definition was found and triggered.</returns>
        public bool TriggerCombo(ElementType elementA, ElementType elementB, Vector3 position)
        {
            string key = GetComboKey(elementA, elementB);

            if (!_comboLookup.TryGetValue(key, out ComboDefinition combo))
            {
                // Try reversed
                key = GetComboKey(elementB, elementA);
                if (!_comboLookup.TryGetValue(key, out combo))
                    return false;
            }

            // Spawn particle effect
            SpawnComboEffect(combo, position);

            // Spawn text popup
            SpawnComboText(combo.comboName, position, combo.primaryColor);

            // Camera shake
            if (combo.cameraShakeIntensity > 0f)
                TriggerCameraShake(combo.cameraShakeIntensity, combo.cameraShakeDuration);

            // Play sound
            if (combo.comboSound != null)
                AudioSource.PlayClipAtPoint(combo.comboSound, position);

            OnComboTriggered?.Invoke(combo.comboName, position);
            return true;
        }

        /// <summary>
        /// Triggers camera shake independently of combo effects.
        /// </summary>
        public void TriggerCameraShake(float intensity, float duration)
        {
            if (_mainCamera == null) return;

            if (_shakeCoroutine != null)
                StopCoroutine(_shakeCoroutine);

            _shakeCoroutine = StartCoroutine(CameraShakeRoutine(intensity, duration));
        }

        /// <summary>
        /// Returns the combo name for two elements, or null if no combo exists.
        /// </summary>
        public string GetComboName(ElementType elementA, ElementType elementB)
        {
            string key = GetComboKey(elementA, elementB);
            if (_comboLookup.TryGetValue(key, out ComboDefinition combo))
                return combo.comboName;

            key = GetComboKey(elementB, elementA);
            if (_comboLookup.TryGetValue(key, out combo))
                return combo.comboName;

            return null;
        }

        #endregion

        #region Effect Spawning

        private void SpawnComboEffect(ComboDefinition combo, Vector3 position)
        {
            if (combo.effectPrefab == null) return;

            string key = GetComboKey(combo.elementA, combo.elementB);
            ParticleSystem ps = GetFromPool(key, combo.effectPrefab);

            if (ps == null) return;

            ps.transform.position = position;
            ps.transform.localScale = Vector3.one * combo.effectScale;

            // Apply colors
            var main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(combo.primaryColor, combo.secondaryColor);

            ps.gameObject.SetActive(true);
            ps.Play();

            // Auto-return to pool after completion
            float totalDuration = main.duration + main.startLifetime.constantMax;
            StartCoroutine(ReturnToPoolAfterDelay(key, ps, totalDuration));
        }

        private void SpawnComboText(string comboName, Vector3 worldPosition, Color color)
        {
            if (_comboTextPrefab == null) return;

            GameObject textObj;
            if (_worldCanvas != null)
            {
                textObj = Instantiate(_comboTextPrefab, _worldCanvas.transform);
            }
            else
            {
                textObj = Instantiate(_comboTextPrefab);
            }

            textObj.transform.position = worldPosition + _textOffset;

            TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
            TextMeshPro tmpWorld = textObj.GetComponent<TextMeshPro>();

            if (tmp != null)
            {
                tmp.text = comboName;
                tmp.color = color;
            }
            else if (tmpWorld != null)
            {
                tmpWorld.text = comboName;
                tmpWorld.color = color;
            }

            StartCoroutine(AnimateComboText(textObj, color));
        }

        #endregion

        #region Text Animation

        private IEnumerator AnimateComboText(GameObject textObj, Color color)
        {
            if (textObj == null) yield break;

            float elapsed = 0f;
            Vector3 startPos = textObj.transform.position;
            Transform textTransform = textObj.transform;

            // Scale in with overshoot
            textTransform.localScale = Vector3.zero;

            while (elapsed < _textDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _textDuration);

                // Rise
                textTransform.position = startPos + Vector3.up * (_textRiseSpeed * elapsed);

                // Scale animation (quick scale up with overshoot, then settle)
                float scaleT = Mathf.Clamp01(elapsed / _textScaleIn);
                float scale;
                if (scaleT < 0.6f)
                    scale = Mathf.Lerp(0f, _textScaleOvershoot, scaleT / 0.6f);
                else
                    scale = Mathf.Lerp(_textScaleOvershoot, 1f, (scaleT - 0.6f) / 0.4f);

                textTransform.localScale = Vector3.one * scale;

                // Fade out in last 40%
                if (t > 0.6f)
                {
                    float fadeT = (t - 0.6f) / 0.4f;
                    Color c = color;
                    c.a = 1f - fadeT;

                    TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
                    TextMeshPro tmpWorld = textObj.GetComponent<TextMeshPro>();
                    if (tmp != null) tmp.color = c;
                    else if (tmpWorld != null) tmpWorld.color = c;
                }

                yield return null;
            }

            Destroy(textObj);
        }

        #endregion

        #region Camera Shake

        private IEnumerator CameraShakeRoutine(float intensity, float duration)
        {
            if (_mainCamera == null) yield break;

            _cameraOriginalPosition = _mainCamera.transform.localPosition;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float decay = 1f - (elapsed / duration);

                float x = UnityEngine.Random.Range(-intensity, intensity) * decay;
                float y = UnityEngine.Random.Range(-intensity, intensity) * decay;

                _mainCamera.transform.localPosition = _cameraOriginalPosition + new Vector3(x, y, 0f);
                yield return null;
            }

            _mainCamera.transform.localPosition = _cameraOriginalPosition;
            _shakeCoroutine = null;
        }

        #endregion

        #region Pool Management

        private void BuildLookupTable()
        {
            _comboLookup.Clear();
            foreach (var combo in _comboDefinitions)
            {
                string key = GetComboKey(combo.elementA, combo.elementB);
                _comboLookup[key] = combo;
            }
        }

        private void InitializePools()
        {
            foreach (var combo in _comboDefinitions)
            {
                if (combo.effectPrefab == null) continue;

                string key = GetComboKey(combo.elementA, combo.elementB);
                if (_effectPools.ContainsKey(key)) continue;

                Queue<ParticleSystem> pool = new Queue<ParticleSystem>();
                for (int i = 0; i < _poolSizePerCombo; i++)
                {
                    ParticleSystem ps = Instantiate(combo.effectPrefab, _poolContainer);
                    ps.gameObject.SetActive(false);
                    pool.Enqueue(ps);
                }
                _effectPools[key] = pool;
            }
        }

        private ParticleSystem GetFromPool(string key, ParticleSystem prefab)
        {
            if (_effectPools.TryGetValue(key, out Queue<ParticleSystem> pool) && pool.Count > 0)
                return pool.Dequeue();

            // Grow pool
            if (prefab != null)
            {
                ParticleSystem ps = Instantiate(prefab, _poolContainer);
                ps.gameObject.SetActive(false);
                return ps;
            }

            return null;
        }

        private IEnumerator ReturnToPoolAfterDelay(string key, ParticleSystem ps, float delay)
        {
            yield return new WaitForSeconds(delay);

            if (ps != null)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.transform.SetParent(_poolContainer);
                ps.gameObject.SetActive(false);

                if (!_effectPools.ContainsKey(key))
                    _effectPools[key] = new Queue<ParticleSystem>();

                _effectPools[key].Enqueue(ps);
            }
        }

        private static string GetComboKey(ElementType a, ElementType b)
        {
            // Normalize so order doesn't matter
            int ia = (int)a;
            int ib = (int)b;
            return ia <= ib ? $"{a}_{b}" : $"{b}_{a}";
        }

        #endregion
    }
}
