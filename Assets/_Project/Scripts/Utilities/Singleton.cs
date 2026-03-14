using UnityEngine;

namespace ElementalSiege.Utilities
{
    /// <summary>
    /// Thread-safe generic singleton base class for MonoBehaviours.
    /// Persists across scene loads via DontDestroyOnLoad.
    /// </summary>
    /// <typeparam name="T">The MonoBehaviour subclass type.</typeparam>
    public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;
        private static readonly object _lock = new object();
        private static bool _applicationIsQuitting;

        /// <summary>
        /// Returns the singleton instance. Creates one if it does not exist.
        /// </summary>
        public static T Instance
        {
            get
            {
                if (_applicationIsQuitting)
                {
                    Debug.LogWarning(
                        $"[Singleton] Instance of {typeof(T)} requested after application quit. Returning null.");
                    return null;
                }

                lock (_lock)
                {
                    if (_instance != null)
                        return _instance;

                    var instances = FindObjectsByType<T>(FindObjectsSortMode.None);

                    if (instances.Length > 1)
                    {
                        Debug.LogError(
                            $"[Singleton] Multiple instances of {typeof(T)} found. Keeping the first one.");
                        for (int i = 1; i < instances.Length; i++)
                            Destroy(instances[i].gameObject);

                        _instance = instances[0];
                    }
                    else if (instances.Length == 1)
                    {
                        _instance = instances[0];
                    }
                    else
                    {
                        var singletonObject = new GameObject($"[Singleton] {typeof(T).Name}");
                        _instance = singletonObject.AddComponent<T>();
                    }

                    DontDestroyOnLoad(_instance.gameObject);
                    return _instance;
                }
            }
        }

        /// <summary>
        /// Whether a valid instance currently exists (without creating one).
        /// </summary>
        public static bool HasInstance => _instance != null && !_applicationIsQuitting;

        protected virtual void Awake()
        {
            lock (_lock)
            {
                if (_instance == null)
                {
                    _instance = this as T;
                    DontDestroyOnLoad(gameObject);
                    OnSingletonAwake();
                }
                else if (_instance != this)
                {
                    Debug.LogWarning(
                        $"[Singleton] Duplicate {typeof(T).Name} detected on '{gameObject.name}'. Destroying.");
                    Destroy(gameObject);
                }
            }
        }

        /// <summary>
        /// Called once when the singleton instance is first initialised.
        /// Override instead of Awake to avoid lifecycle conflicts.
        /// </summary>
        protected virtual void OnSingletonAwake() { }

        protected virtual void OnApplicationQuit()
        {
            _applicationIsQuitting = true;
        }

        protected virtual void OnDestroy()
        {
            lock (_lock)
            {
                if (_instance == this)
                {
                    _instance = null;
                }
            }
        }
    }
}
