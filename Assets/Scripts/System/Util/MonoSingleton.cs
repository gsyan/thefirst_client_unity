//------------------------------------------------------------------------------
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T>
{
    private static T _instance;
    private static bool _isShuttingDown = false;
    private static readonly object _lock = new object();
    private bool _isInitialized = false;

    protected virtual bool ShouldDontDestroyOnLoad => true;

    public static T Instance
    {
        get
        {
            if (_isShuttingDown) return null;

            lock (_lock)
            {
                if (_instance == null)
                {
#if UNITY_EDITOR
                    _instance = FindFirstObjectByType<T>(FindObjectsInactive.Include);
#else
                    _instance = FindFirstObjectByType<T>();
#endif

                    if (_instance == null)
                    {                        
                        GameObject singletonObject = new GameObject(typeof(T).Name);
                        _instance = singletonObject.AddComponent<T>();
                        //Debug.Log($"[MonoSingleton] Created new instance of {typeof(T).Name}");
                    }

                    _instance.InitializeIfNeeded();
                }

                return _instance;
            }
        }
    }

    protected virtual void Awake()
    {
        if (_instance == null)
        {
            _instance = (T)this;
            InitializeIfNeeded();
        }
        else if (_instance != this)
        {
            Debug.LogWarning($"[MonoSingleton] Duplicate instance of {typeof(T).Name} detected, destroying: {name}");
            Destroy(gameObject);
        }
    }

    protected virtual void OnApplicationQuit()
    {
        _isShuttingDown = true;
    }

    protected virtual void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
            // DontDestroyOnLoad 싱글톤만 shutdown 처리
            if (ShouldDontDestroyOnLoad)
                _isShuttingDown = true;
        }
            
    }

    private void InitializeIfNeeded()
    {
        if (_isInitialized == true) return;
        if (ShouldDontDestroyOnLoad == true)
            DontDestroyOnLoad(this.gameObject);
        _isInitialized = true;
        OnInitialize();
    }

    protected virtual void OnInitialize() { }
}
