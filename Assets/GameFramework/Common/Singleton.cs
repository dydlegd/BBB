using System;
using System.Reflection;
using UnityEngine;

#region Singleton<T> — 不继承 MonoBehaviour 的纯 C# 单例

/// <summary>
/// 通用单例基类（纯 C# 版本，不继承 MonoBehaviour），线程安全。
/// <para>使用示例：</para>
/// <code>
/// // ===== 定义一个单例类 =====
/// public class AudioConfig : Singleton<AudioConfig>
/// {
///     // 私有构造函数，防止外部 new
///     private AudioConfig() { }
///
///     public float MasterVolume = 1f;
///     public float MusicVolume = 0.8f;
///     public float SfxVolume = 1f;
/// }
///
/// // ===== 在任何地方使用 =====
/// AudioConfig.Instance.MasterVolume = 0.5f;
/// float vol = AudioConfig.Instance.MusicVolume;
/// </code>
/// </summary>
/// <typeparam name="T">子类类型</typeparam>
public abstract class Singleton<T> where T : class
{
    /// <summary>
    /// 线程安全的懒加载单例实例
    /// </summary>
    private static readonly Lazy<T> _instance = new Lazy<T>(() =>
    {
        ConstructorInfo ctor = typeof(T).GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, Type.EmptyTypes, null);

        if (ctor == null)
        {
            throw new InvalidOperationException(
                $"Singleton<{typeof(T).Name}>: 未找到无参构造函数，请确保子类有无参构造函数。");
        }

        return (T)ctor.Invoke(null);
    });

    /// <summary>
    /// 全局唯一实例（首次访问时自动创建）
    /// </summary>
    public static T Instance => _instance.Value;
}

#endregion

#region SingletonMono<T> — 继承 MonoBehaviour 的单例

/// <summary>
/// 通用单例基类（MonoBehaviour 版本），适用于需要挂载到场景中的组件。
/// 首次访问时若场景中不存在则自动创建 GameObject，并设置 DontDestroyOnLoad。
/// <para>使用示例：</para>
/// <code>
/// // ===== 定义一个单例 MonoBehaviour 类 =====
/// public class UIManager : SingletonMono<UIManager>
/// {
///     public void ShowPanel(string panelName)
///     {
///         Debug.Log($"打开面板: {panelName}");
///     }
/// }
///
/// // ===== 在任何地方使用 =====
/// UIManager.Instance.ShowPanel("MainMenu");
/// </code>
/// </summary>
/// <typeparam name="T">子类类型</typeparam>
public abstract class SingletonMono<T> : MonoBehaviour where T : MonoBehaviour
{
    /// <summary>
    /// 单例实例缓存
    /// </summary>
    private static T _instance;

    /// <summary>
    /// 标记应用程序是否正在退出，防止退出后访问产生新的对象
    /// </summary>
    private static bool _applicationQuitting;

    /// <summary>
    /// 全局唯一实例（首次访问时若不存在则自动创建）
    /// </summary>
    public static T Instance
    {
        get
        {
            if (_applicationQuitting)
            {
                Debug.LogWarning($"[SingletonMono] 应用程序正在退出，{typeof(T).Name}.Instance 返回 null。");
                return null;
            }

            if (_instance == null)
            {
                _instance = FindObjectOfType<T>();

                if (_instance == null)
                {
                    GameObject go = new GameObject($"[SingletonMono] {typeof(T).Name}");
                    _instance = go.AddComponent<T>();
                    DontDestroyOnLoad(go);
                }
            }

            return _instance;
        }
    }

    /// <summary>
    /// 实例是否存活（无日志、无自动创建）。
    /// 用于 Shutdown 等退出路径中静默判空，避免打印冗余警告。
    /// </summary>
    public static bool HasInstance => !_applicationQuitting && _instance != null;

    /// <summary>
    /// Awake 中处理多实例冲突，确保只有一个实例存活
    /// </summary>
    protected virtual void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this as T;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 实例销毁时清理静态引用
    /// </summary>
    protected virtual void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    /// <summary>
    /// 应用程序退出时设置标记，防止退出后访问产生新实例
    /// </summary>
    protected virtual void OnApplicationQuit()
    {
        _applicationQuitting = true;
    }
}

#endregion