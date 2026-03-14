using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ElementalSiege.VFX
{
    /// <summary>
    /// Renders a lightning bolt between two points using LineRenderer with
    /// zigzag randomization, brightness flash, and optional branch lightning.
    /// Auto-destroys after the configured duration.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class LightningBoltVFX : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Bolt Shape")]
        [SerializeField] private int _segments = 12;
        [SerializeField] private float _amplitude = 0.5f;
        [SerializeField] private float _jitterSpeed = 20f;
        [SerializeField] private AnimationCurve _amplitudeFalloff = AnimationCurve.Linear(0f, 1f, 1f, 0f);

        [Header("Timing")]
        [SerializeField] private float _duration = 0.3f;
        [SerializeField] private float _flickerInterval = 0.05f;

        [Header("Color & Brightness")]
        [SerializeField] private Color _boltColor = new Color(0.6f, 0.8f, 1f, 1f);
        [SerializeField] private Color _flashColor = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private float _flashDuration = 0.05f;
        [SerializeField] private float _startWidth = 0.15f;
        [SerializeField] private float _endWidth = 0.05f;

        [Header("Branching")]
        [SerializeField] private bool _enableBranches = true;
        [SerializeField] private int _maxBranches = 3;
        [SerializeField] private float _branchProbability = 0.3f;
        [SerializeField] private float _branchLengthRatio = 0.4f;
        [SerializeField] private float _branchAmplitude = 0.3f;
        [SerializeField] private int _branchSegments = 6;
        [SerializeField] private float _branchWidth = 0.06f;
        [SerializeField] private Material _lightningMaterial;

        [Header("Auto Destroy")]
        [SerializeField] private bool _autoDestroy = true;

        #endregion

        #region Private State

        private LineRenderer _lineRenderer;
        private Vector3 _startPoint;
        private Vector3 _endPoint;
        private float _elapsed;
        private float _nextFlickerTime;
        private readonly List<LineRenderer> _branches = new List<LineRenderer>();
        private bool _isFlashing;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _lineRenderer = GetComponent<LineRenderer>();
            ConfigureLineRenderer(_lineRenderer, _startWidth, _endWidth);
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;

            if (_elapsed >= _duration)
            {
                if (_autoDestroy)
                    Destroy(gameObject);
                return;
            }

            // Flicker / re-jitter the bolt at intervals
            if (Time.time >= _nextFlickerTime)
            {
                GenerateBolt(_lineRenderer, _startPoint, _endPoint, _segments, _amplitude);

                if (_enableBranches)
                    RegenerateBranches();

                _nextFlickerTime = Time.time + _flickerInterval;
            }

            // Fade out over duration
            float fade = 1f - (_elapsed / _duration);
            SetBoltAlpha(fade);
        }

        #endregion

        #region Public API

        /// <summary>
        /// Initializes and fires the lightning bolt between two world-space points.
        /// </summary>
        /// <param name="start">World-space start position.</param>
        /// <param name="end">World-space end position.</param>
        public void Fire(Vector3 start, Vector3 end)
        {
            _startPoint = start;
            _endPoint = end;
            _elapsed = 0f;
            _nextFlickerTime = 0f;

            transform.position = start;

            GenerateBolt(_lineRenderer, start, end, _segments, _amplitude);

            if (_enableBranches)
                GenerateInitialBranches();

            // Brightness flash
            StartCoroutine(FlashRoutine());
        }

        /// <summary>
        /// Creates and fires a lightning bolt instance between two points.
        /// </summary>
        /// <param name="prefab">The LightningBoltVFX prefab to instantiate.</param>
        /// <param name="start">Start point.</param>
        /// <param name="end">End point.</param>
        /// <returns>The instantiated LightningBoltVFX.</returns>
        public static LightningBoltVFX Create(LightningBoltVFX prefab, Vector3 start, Vector3 end)
        {
            if (prefab == null) return null;

            LightningBoltVFX instance = Instantiate(prefab, start, Quaternion.identity);
            instance.Fire(start, end);
            return instance;
        }

        #endregion

        #region Bolt Generation

        private void GenerateBolt(LineRenderer lr, Vector3 start, Vector3 end,
                                   int segments, float amp)
        {
            if (lr == null) return;

            lr.positionCount = segments + 1;

            Vector3 direction = end - start;
            Vector3 perpendicular = Vector3.Cross(direction.normalized, Vector3.forward).normalized;

            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                Vector3 basePos = Vector3.Lerp(start, end, t);

                // No displacement at endpoints
                if (i > 0 && i < segments)
                {
                    float falloff = _amplitudeFalloff.Evaluate(t);
                    float displacement = Random.Range(-amp, amp) * falloff;
                    basePos += perpendicular * displacement;
                }

                lr.SetPosition(i, basePos);
            }
        }

        private void ConfigureLineRenderer(LineRenderer lr, float startW, float endW)
        {
            if (lr == null) return;

            lr.startWidth = startW;
            lr.endWidth = endW;
            lr.startColor = _boltColor;
            lr.endColor = _boltColor;
            lr.useWorldSpace = true;
            lr.sortingOrder = 100;

            if (_lightningMaterial != null)
                lr.material = _lightningMaterial;
        }

        #endregion

        #region Branch Lightning

        private void GenerateInitialBranches()
        {
            ClearBranches();

            int branchCount = 0;
            for (int i = 1; i < _segments - 1 && branchCount < _maxBranches; i++)
            {
                if (Random.value > _branchProbability) continue;

                CreateBranch(i);
                branchCount++;
            }
        }

        private void RegenerateBranches()
        {
            // Re-jitter existing branches
            int branchIndex = 0;
            for (int i = 1; i < _segments - 1 && branchIndex < _branches.Count; i++)
            {
                if (branchIndex >= _branches.Count) break;

                Vector3 branchStart = _lineRenderer.GetPosition(i);
                Vector3 direction = _endPoint - _startPoint;
                Vector3 perpendicular = Vector3.Cross(direction.normalized, Vector3.forward).normalized;
                Vector3 branchEnd = branchStart +
                    (perpendicular * Random.Range(-1f, 1f) + direction.normalized * 0.3f).normalized *
                    direction.magnitude * _branchLengthRatio;

                GenerateBolt(_branches[branchIndex], branchStart, branchEnd,
                    _branchSegments, _branchAmplitude);
                branchIndex++;
            }
        }

        private void CreateBranch(int segmentIndex)
        {
            GameObject branchObj = new GameObject($"Branch_{_branches.Count}");
            branchObj.transform.SetParent(transform);

            LineRenderer lr = branchObj.AddComponent<LineRenderer>();
            ConfigureLineRenderer(lr, _branchWidth, _branchWidth * 0.3f);

            Vector3 branchStart = _lineRenderer.GetPosition(segmentIndex);
            Vector3 direction = _endPoint - _startPoint;
            Vector3 perpendicular = Vector3.Cross(direction.normalized, Vector3.forward).normalized;
            float side = Random.value > 0.5f ? 1f : -1f;
            Vector3 branchEnd = branchStart +
                (perpendicular * side + direction.normalized * 0.5f).normalized *
                direction.magnitude * _branchLengthRatio;

            GenerateBolt(lr, branchStart, branchEnd, _branchSegments, _branchAmplitude);

            _branches.Add(lr);
        }

        private void ClearBranches()
        {
            foreach (var branch in _branches)
            {
                if (branch != null)
                    Destroy(branch.gameObject);
            }
            _branches.Clear();
        }

        #endregion

        #region Visual Effects

        private IEnumerator FlashRoutine()
        {
            _isFlashing = true;

            // Set flash color
            if (_lineRenderer != null)
            {
                _lineRenderer.startColor = _flashColor;
                _lineRenderer.endColor = _flashColor;
            }

            foreach (var branch in _branches)
            {
                if (branch != null)
                {
                    branch.startColor = _flashColor;
                    branch.endColor = _flashColor;
                }
            }

            yield return new WaitForSeconds(_flashDuration);

            // Return to normal color
            if (_lineRenderer != null)
            {
                _lineRenderer.startColor = _boltColor;
                _lineRenderer.endColor = _boltColor;
            }

            foreach (var branch in _branches)
            {
                if (branch != null)
                {
                    branch.startColor = _boltColor;
                    branch.endColor = _boltColor;
                }
            }

            _isFlashing = false;
        }

        private void SetBoltAlpha(float alpha)
        {
            if (_isFlashing) return;

            Color c = _boltColor;
            c.a = alpha;

            if (_lineRenderer != null)
            {
                _lineRenderer.startColor = c;
                _lineRenderer.endColor = c;
            }

            foreach (var branch in _branches)
            {
                if (branch != null)
                {
                    branch.startColor = c;
                    branch.endColor = c;
                }
            }
        }

        #endregion

        #region Cleanup

        private void OnDestroy()
        {
            ClearBranches();
        }

        #endregion
    }
}
