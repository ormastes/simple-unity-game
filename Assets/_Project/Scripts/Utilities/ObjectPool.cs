using System;
using System.Collections.Generic;
using UnityEngine;

namespace ElementalSiege.Utilities
{
    /// <summary>
    /// Generic GameObject object pool using a Queue.
    /// Supports automatic expansion when the pool is exhausted.
    /// </summary>
    public class ObjectPool : MonoBehaviour
    {
        [Header("Pool Settings")]
        [SerializeField] private GameObject _prefab;
        [SerializeField] private int _initialSize = 10;
        [SerializeField] private bool _autoExpand = true;
        [SerializeField] private int _expandAmount = 5;
        [SerializeField] private int _maxSize = 100;

        private Queue<GameObject> _available;
        private List<GameObject> _allObjects;
        private Transform _poolParent;

        /// <summary>Number of objects currently available in the pool.</summary>
        public int AvailableCount => _available.Count;

        /// <summary>Total number of objects managed by this pool (active + inactive).</summary>
        public int TotalCount => _allObjects.Count;

        /// <summary>The prefab this pool instantiates.</summary>
        public GameObject Prefab => _prefab;

        private void Awake()
        {
            Initialise(_prefab, _initialSize);
        }

        /// <summary>
        /// Initialises the pool with the given prefab and pre-warms it.
        /// Safe to call manually if the pool was added at runtime.
        /// </summary>
        /// <param name="prefab">The prefab to pool.</param>
        /// <param name="initialSize">Number of instances to pre-create.</param>
        public void Initialise(GameObject prefab, int initialSize)
        {
            if (prefab == null)
            {
                Debug.LogError("[ObjectPool] Prefab is null. Pool will not function.");
                return;
            }

            _prefab = prefab;
            _initialSize = initialSize;

            _poolParent = new GameObject($"Pool_{_prefab.name}").transform;
            _poolParent.SetParent(transform);

            _available = new Queue<GameObject>(initialSize);
            _allObjects = new List<GameObject>(initialSize);

            Prewarm(initialSize);
        }

        /// <summary>
        /// Creates a pool at runtime for a given prefab.
        /// </summary>
        /// <param name="prefab">The prefab to pool.</param>
        /// <param name="initialSize">Number of instances to pre-create.</param>
        /// <param name="autoExpand">Whether the pool auto-expands when exhausted.</param>
        /// <returns>The configured ObjectPool component.</returns>
        public static ObjectPool Create(GameObject prefab, int initialSize = 10, bool autoExpand = true)
        {
            var poolGo = new GameObject($"ObjectPool_{prefab.name}");
            var pool = poolGo.AddComponent<ObjectPool>();
            pool._autoExpand = autoExpand;
            pool.Initialise(prefab, initialSize);
            return pool;
        }

        /// <summary>
        /// Retrieves an object from the pool, activating and positioning it.
        /// Returns null if the pool is empty and auto-expand is disabled or max size is reached.
        /// </summary>
        /// <param name="position">World position.</param>
        /// <param name="rotation">World rotation.</param>
        /// <param name="parent">Optional parent transform.</param>
        /// <returns>The pooled GameObject, or null if unavailable.</returns>
        public GameObject Get(Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (_available.Count == 0)
            {
                if (!_autoExpand || _allObjects.Count >= _maxSize)
                {
                    Debug.LogWarning(
                        $"[ObjectPool] Pool '{_prefab.name}' exhausted. " +
                        $"AutoExpand={_autoExpand}, Total={_allObjects.Count}, Max={_maxSize}.");
                    return null;
                }

                int expandCount = Mathf.Min(_expandAmount, _maxSize - _allObjects.Count);
                Prewarm(expandCount);
            }

            var obj = _available.Dequeue();
            obj.transform.SetParent(parent);
            obj.transform.SetPositionAndRotation(position, rotation);
            obj.SetActive(true);
            return obj;
        }

        /// <summary>
        /// Retrieves an object from the pool at the origin with identity rotation.
        /// </summary>
        public GameObject Get()
        {
            return Get(Vector3.zero, Quaternion.identity);
        }

        /// <summary>
        /// Returns an object to the pool, deactivating it.
        /// </summary>
        /// <param name="obj">The object to return.</param>
        public void Return(GameObject obj)
        {
            if (obj == null) return;

            obj.SetActive(false);
            obj.transform.SetParent(_poolParent);
            _available.Enqueue(obj);
        }

        /// <summary>
        /// Returns an object to the pool after a delay.
        /// </summary>
        /// <param name="obj">The object to return.</param>
        /// <param name="delay">Delay in seconds.</param>
        public void Return(GameObject obj, float delay)
        {
            if (obj == null) return;
            StartCoroutine(ReturnDelayed(obj, delay));
        }

        /// <summary>
        /// Deactivates and re-queues all managed objects.
        /// </summary>
        public void ReturnAll()
        {
            _available.Clear();
            foreach (var obj in _allObjects)
            {
                if (obj == null) continue;
                obj.SetActive(false);
                obj.transform.SetParent(_poolParent);
                _available.Enqueue(obj);
            }
        }

        private void Prewarm(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var obj = Instantiate(_prefab, _poolParent);
                obj.SetActive(false);
                obj.name = $"{_prefab.name}_{_allObjects.Count}";
                _available.Enqueue(obj);
                _allObjects.Add(obj);
            }
        }

        private System.Collections.IEnumerator ReturnDelayed(GameObject obj, float delay)
        {
            yield return new WaitForSeconds(delay);
            Return(obj);
        }
    }
}
