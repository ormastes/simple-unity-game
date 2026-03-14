using UnityEngine;
using UnityEngine.Pool;

namespace ElementalSiege.Elements
{
    /// <summary>
    /// Static factory responsible for spawning element impact and combo effects.
    /// Uses Unity's built-in ObjectPool for efficient memory management on mobile (iOS)
    /// and desktop (Mac) targets. All spawned effects auto-return to their pool after
    /// their particle systems complete.
    /// </summary>
    public static class ElementEffectFactory
    {
        /// <summary>Default pool capacity per effect prefab.</summary>
        private const int DefaultPoolCapacity = 8;

        /// <summary>Maximum pool size before objects are destroyed instead of returned.</summary>
        private const int MaxPoolSize = 32;

        /// <summary>
        /// Registry of object pools keyed by prefab instance ID.
        /// Lazily populated as new prefabs are requested.
        /// </summary>
        private static readonly System.Collections.Generic.Dictionary<int, IObjectPool<GameObject>> _pools
            = new System.Collections.Generic.Dictionary<int, IObjectPool<GameObject>>();

        /// <summary>
        /// Spawns an impact effect for the given element at the specified world position.
        /// The effect is retrieved from an object pool and automatically returned when
        /// its particle system finishes (or after a fallback timeout).
        /// </summary>
        /// <param name="element">The element type whose impact effect prefab will be used.</param>
        /// <param name="position">World position to spawn the effect.</param>
        /// <param name="radius">Scale multiplier based on the ability radius.</param>
        /// <returns>The spawned effect GameObject, or null if no prefab is assigned.</returns>
        public static GameObject CreateImpactEffect(ElementType element, Vector2 position, float radius)
        {
            if (element == null || element.ImpactEffectPrefab == null)
                return null;

            var go = GetFromPool(element.ImpactEffectPrefab);
            go.transform.position = new Vector3(position.x, position.y, 0f);

            // Scale the effect based on the ability radius relative to a 1-unit baseline.
            float scale = Mathf.Max(radius, 0.1f);
            go.transform.localScale = new Vector3(scale, scale, scale);

            go.SetActive(true);
            ScheduleReturn(go, element.ImpactEffectPrefab);
            return go;
        }

        /// <summary>
        /// Spawns a combo effect at the specified world position using the interaction's
        /// combo effect prefab.
        /// </summary>
        /// <param name="interaction">The interaction definition containing the combo prefab.</param>
        /// <param name="position">World position to spawn the combo effect.</param>
        /// <returns>The spawned combo effect GameObject, or null if no prefab is assigned.</returns>
        public static GameObject CreateComboEffect(ElementInteraction interaction, Vector2 position)
        {
            if (interaction == null || interaction.ComboEffectPrefab == null)
                return null;

            var go = GetFromPool(interaction.ComboEffectPrefab);
            go.transform.position = new Vector3(position.x, position.y, 0f);
            go.transform.localScale = Vector3.one;

            go.SetActive(true);
            ScheduleReturn(go, interaction.ComboEffectPrefab);
            return go;
        }

        /// <summary>
        /// Clears all pools and destroys pooled objects. Call this on scene unload
        /// or when transitioning between levels to free memory.
        /// </summary>
        public static void ClearAllPools()
        {
            foreach (var pool in _pools.Values)
            {
                pool.Clear();
            }
            _pools.Clear();
        }

        /// <summary>
        /// Retrieves a GameObject from the pool associated with the given prefab.
        /// Creates a new pool if one does not yet exist for this prefab.
        /// </summary>
        private static GameObject GetFromPool(GameObject prefab)
        {
            var pool = GetOrCreatePool(prefab);
            return pool.Get();
        }

        /// <summary>
        /// Returns a previously spawned effect to its pool. Called automatically
        /// by <see cref="EffectAutoReturn"/> when the effect finishes playing.
        /// </summary>
        internal static void ReturnToPool(GameObject prefab, GameObject instance)
        {
            int id = prefab.GetInstanceID();
            if (_pools.TryGetValue(id, out var pool))
            {
                instance.SetActive(false);
                pool.Release(instance);
            }
            else
            {
                // Pool was cleared (e.g., scene transition). Destroy the orphan.
                Object.Destroy(instance);
            }
        }

        /// <summary>
        /// Gets an existing pool or creates a new one for the specified prefab.
        /// </summary>
        private static IObjectPool<GameObject> GetOrCreatePool(GameObject prefab)
        {
            int id = prefab.GetInstanceID();
            if (_pools.TryGetValue(id, out var existing))
                return existing;

            var pool = new ObjectPool<GameObject>(
                createFunc: () =>
                {
                    var go = Object.Instantiate(prefab);
                    go.SetActive(false);
                    return go;
                },
                actionOnGet: go => { /* Activation handled by caller */ },
                actionOnRelease: go => go.SetActive(false),
                actionOnDestroy: go => Object.Destroy(go),
                collectionCheck: false,
                defaultCapacity: DefaultPoolCapacity,
                maxSize: MaxPoolSize
            );

            _pools[id] = pool;
            return pool;
        }

        /// <summary>
        /// Attaches an auto-return component to the effect so it returns to the pool
        /// after its particle system completes or after a fallback duration.
        /// </summary>
        private static void ScheduleReturn(GameObject instance, GameObject prefab)
        {
            var autoReturn = instance.GetComponent<EffectAutoReturn>();
            if (autoReturn == null)
                autoReturn = instance.AddComponent<EffectAutoReturn>();

            autoReturn.Initialize(prefab);
        }
    }

    /// <summary>
    /// MonoBehaviour attached to pooled effect instances. Monitors the particle system
    /// and returns the object to the pool once all particles have finished, or after
    /// a fallback timeout to prevent leaks.
    /// </summary>
    internal class EffectAutoReturn : MonoBehaviour
    {
        private const float FallbackTimeout = 5f;

        private GameObject _prefab;
        private ParticleSystem _particleSystem;
        private float _timer;

        /// <summary>
        /// Initializes the auto-return with the prefab reference needed to identify
        /// the correct pool.
        /// </summary>
        public void Initialize(GameObject prefab)
        {
            _prefab = prefab;
            _timer = 0f;
            _particleSystem = GetComponent<ParticleSystem>();
        }

        private void Update()
        {
            _timer += Time.deltaTime;

            // Return when particle system is done, or after fallback timeout.
            bool particlesDone = _particleSystem != null && !_particleSystem.IsAlive(true);
            bool timedOut = _timer >= FallbackTimeout;

            if (particlesDone || (_particleSystem == null && timedOut) || timedOut)
            {
                ElementEffectFactory.ReturnToPool(_prefab, gameObject);
            }
        }
    }
}
