using System.Collections.Generic;
using UnityEngine;

namespace ElementalSiege.VFX
{
    /// <summary>
    /// Manages fire spread visuals between Flammable objects.
    /// Spawns pooled fire particles on burning objects and renders
    /// ember trails between spreading fires.
    /// </summary>
    public class FireSpreadVFX : MonoBehaviour
    {
        #region Nested Types

        /// <summary>
        /// Tracks a single burning object and its associated particle instance.
        /// </summary>
        private class BurningEntry
        {
            public Transform target;
            public ParticleSystem fireParticles;
            public float startTime;
        }

        /// <summary>
        /// Tracks an ember trail between two burning objects.
        /// </summary>
        private class EmberTrail
        {
            public Transform source;
            public Transform destination;
            public ParticleSystem trailParticles;
            public float startTime;
        }

        #endregion

        #region Serialized Fields

        [Header("Fire Particles")]
        [SerializeField] private ParticleSystem _fireParticlePrefab;
        [SerializeField] private int _initialPoolSize = 10;
        [SerializeField] private Vector3 _fireOffset = new Vector3(0f, 0.5f, 0f);

        [Header("Ember Trail")]
        [SerializeField] private ParticleSystem _emberTrailPrefab;
        [SerializeField] private int _emberPoolSize = 5;
        [SerializeField] private float _emberTrailDuration = 1f;

        [Header("Heat Distortion")]
        [SerializeField] private Material _heatDistortionMaterial;
        [SerializeField] private float _distortionIntensity = 0.02f;
        [SerializeField] private float _distortionRadius = 1.5f;

        [Header("Settings")]
        [SerializeField] private float _maxBurnDuration = 5f;
        [SerializeField] private float _fireScaleMin = 0.5f;
        [SerializeField] private float _fireScaleMax = 1.5f;

        #endregion

        #region Private State

        private readonly List<BurningEntry> _activeFires = new List<BurningEntry>();
        private readonly List<EmberTrail> _activeTrails = new List<EmberTrail>();
        private readonly Queue<ParticleSystem> _firePool = new Queue<ParticleSystem>();
        private readonly Queue<ParticleSystem> _emberPool = new Queue<ParticleSystem>();
        private Transform _poolContainer;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _poolContainer = new GameObject("FireVFX_Pool").transform;
            _poolContainer.SetParent(transform);

            InitializePool(_fireParticlePrefab, _firePool, _initialPoolSize);
            InitializePool(_emberTrailPrefab, _emberPool, _emberPoolSize);
        }

        private void Update()
        {
            UpdateActiveFires();
            UpdateActiveTrails();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Starts fire particles on the given target object.
        /// </summary>
        /// <param name="target">The transform of the burning object.</param>
        /// <param name="intensity">Fire intensity (0–1), affects scale.</param>
        public void StartFire(Transform target, float intensity = 1f)
        {
            if (target == null) return;

            // Avoid duplicates
            foreach (var entry in _activeFires)
            {
                if (entry.target == target) return;
            }

            ParticleSystem ps = GetFromPool(_firePool, _fireParticlePrefab);
            if (ps == null) return;

            ps.transform.SetParent(target);
            ps.transform.localPosition = _fireOffset;
            float scale = Mathf.Lerp(_fireScaleMin, _fireScaleMax, intensity);
            ps.transform.localScale = Vector3.one * scale;
            ps.gameObject.SetActive(true);
            ps.Play();

            _activeFires.Add(new BurningEntry
            {
                target = target,
                fireParticles = ps,
                startTime = Time.time
            });

            // Apply heat distortion if material is set
            ApplyHeatDistortion(target, intensity);
        }

        /// <summary>
        /// Stops fire on the given target and returns the particle system to the pool.
        /// </summary>
        public void StopFire(Transform target)
        {
            if (target == null) return;

            for (int i = _activeFires.Count - 1; i >= 0; i--)
            {
                if (_activeFires[i].target == target)
                {
                    ReturnFireToPool(_activeFires[i]);
                    _activeFires.RemoveAt(i);
                    break;
                }
            }
        }

        /// <summary>
        /// Spawns an ember trail particle effect between two objects
        /// to visualize fire spreading.
        /// </summary>
        /// <param name="source">The object already on fire.</param>
        /// <param name="destination">The object catching fire.</param>
        public void SpawnEmberTrail(Transform source, Transform destination)
        {
            if (source == null || destination == null) return;

            ParticleSystem ps = GetFromPool(_emberPool, _emberTrailPrefab);
            if (ps == null) return;

            Vector3 midpoint = (source.position + destination.position) * 0.5f;
            ps.transform.position = midpoint;
            ps.transform.SetParent(null);

            // Orient toward destination
            Vector3 dir = destination.position - source.position;
            if (dir.sqrMagnitude > 0.01f)
                ps.transform.rotation = Quaternion.LookRotation(Vector3.forward, dir);

            // Scale trail length to distance
            float distance = dir.magnitude;
            var shape = ps.shape;
            shape.radius = distance * 0.5f;

            ps.gameObject.SetActive(true);
            ps.Play();

            _activeTrails.Add(new EmberTrail
            {
                source = source,
                destination = destination,
                trailParticles = ps,
                startTime = Time.time
            });
        }

        /// <summary>
        /// Stops all active fires and trails, returning everything to the pool.
        /// </summary>
        public void StopAll()
        {
            for (int i = _activeFires.Count - 1; i >= 0; i--)
            {
                ReturnFireToPool(_activeFires[i]);
            }
            _activeFires.Clear();

            for (int i = _activeTrails.Count - 1; i >= 0; i--)
            {
                ReturnEmberToPool(_activeTrails[i]);
            }
            _activeTrails.Clear();
        }

        #endregion

        #region Update Loops

        private void UpdateActiveFires()
        {
            for (int i = _activeFires.Count - 1; i >= 0; i--)
            {
                BurningEntry entry = _activeFires[i];

                // Remove if target was destroyed
                if (entry.target == null)
                {
                    if (entry.fireParticles != null)
                    {
                        entry.fireParticles.Stop();
                        ReturnToPool(_firePool, entry.fireParticles);
                    }
                    _activeFires.RemoveAt(i);
                    continue;
                }

                // Remove if exceeded max duration
                if (Time.time - entry.startTime > _maxBurnDuration)
                {
                    ReturnFireToPool(entry);
                    _activeFires.RemoveAt(i);
                }
            }
        }

        private void UpdateActiveTrails()
        {
            for (int i = _activeTrails.Count - 1; i >= 0; i--)
            {
                EmberTrail trail = _activeTrails[i];

                if (Time.time - trail.startTime > _emberTrailDuration)
                {
                    ReturnEmberToPool(trail);
                    _activeTrails.RemoveAt(i);
                }
            }
        }

        #endregion

        #region Pool Management

        private void InitializePool(ParticleSystem prefab, Queue<ParticleSystem> pool, int count)
        {
            if (prefab == null) return;

            for (int i = 0; i < count; i++)
            {
                ParticleSystem instance = Instantiate(prefab, _poolContainer);
                instance.gameObject.SetActive(false);
                pool.Enqueue(instance);
            }
        }

        private ParticleSystem GetFromPool(Queue<ParticleSystem> pool, ParticleSystem prefab)
        {
            if (pool.Count > 0)
                return pool.Dequeue();

            // Grow pool if needed
            if (prefab != null)
            {
                ParticleSystem instance = Instantiate(prefab, _poolContainer);
                instance.gameObject.SetActive(false);
                return instance;
            }

            return null;
        }

        private void ReturnToPool(Queue<ParticleSystem> pool, ParticleSystem ps)
        {
            if (ps == null) return;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.transform.SetParent(_poolContainer);
            ps.gameObject.SetActive(false);
            pool.Enqueue(ps);
        }

        private void ReturnFireToPool(BurningEntry entry)
        {
            if (entry.fireParticles != null)
            {
                entry.fireParticles.Stop();
                ReturnToPool(_firePool, entry.fireParticles);
            }
        }

        private void ReturnEmberToPool(EmberTrail trail)
        {
            if (trail.trailParticles != null)
            {
                trail.trailParticles.Stop();
                ReturnToPool(_emberPool, trail.trailParticles);
            }
        }

        #endregion

        #region Heat Distortion

        private void ApplyHeatDistortion(Transform target, float intensity)
        {
            if (_heatDistortionMaterial == null) return;

            // In production, apply distortion via a fullscreen post-process or
            // a sprite overlay with the distortion material.
            SpriteRenderer sr = target.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                // Create a child object for the distortion overlay
                GameObject distortionObj = new GameObject("HeatDistortion");
                distortionObj.transform.SetParent(target);
                distortionObj.transform.localPosition = _fireOffset;
                distortionObj.transform.localScale = Vector3.one * _distortionRadius;

                SpriteRenderer distortionSR = distortionObj.AddComponent<SpriteRenderer>();
                distortionSR.material = new Material(_heatDistortionMaterial);
                distortionSR.material.SetFloat("_Intensity", _distortionIntensity * intensity);
                distortionSR.sortingOrder = sr.sortingOrder + 1;

                // Auto-destroy with fire timeout
                Destroy(distortionObj, _maxBurnDuration);
            }
        }

        #endregion
    }
}
