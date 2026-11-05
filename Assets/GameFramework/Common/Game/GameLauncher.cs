using System;
using System.Collections;
using UnityEngine;

namespace GameFramework.Common
{
    /// <summary>
    /// 游戏启动状态枚举。
    /// </summary>
    public enum GameLaunchState
    {
        /// <summary>未启动</summary>
        None,

        /// <summary>预初始化阶段（加载配置、注册工厂等基础准备）</summary>
        PreInit,

        /// <summary>核心初始化阶段（创建管理器、预加载资源等）</summary>
        Initializing,

        /// <summary>初始化收尾阶段（绑定事件、开启UI等）</summary>
        PostInit,

        /// <summary>启动完成，游戏正常运行中</summary>
        Ready,

        /// <summary>启动失败</summary>
        Failed,
    }

    /// <summary>
    /// 通用游戏启动器基类，提供分阶段启动流程控制。
    /// <para>子类通过重写各阶段方法实现具体的启动逻辑。</para>
    /// </summary>
    /// <example>
    /// 子类实现示例：
    /// <code>
    /// public class FishGameLauncher : GameLauncher
    /// {
    ///     // EntityManager 由基类自动创建，子类只需关注游戏特有逻辑
    ///
    ///     protected override IEnumerator OnPreInit()
    ///     {
    ///         // 实体创建由 Config.CreateEntity() 委托，无需手动注册工厂
    ///         yield break;
    ///     }
    ///
    ///     protected override IEnumerator OnInit()
    ///     {
    ///         // 预加载
    ///         EntityManager.Instance.PrewarmAll();
    ///         yield break;
    ///     }
    ///
    ///     protected override void OnReady()
    ///     {
    ///         Debug.Log("FishGame Ready!");
    ///     }
    /// }
    /// </code>
    /// </example>
    public abstract class GameLauncher : MonoBehaviour
    {
        /// <summary>全局启动器单例</summary>
        public static GameLauncher Instance { get; private set; }

        /// <summary>当前启动状态</summary>
        public GameLaunchState State { get; private set; }

        /// <summary>启动过程中发生的异常（如有）</summary>
        public Exception LaunchException { get; private set; }

        /// <summary>启动是否已完成（Ready 或 Failed 状态）</summary>
        public bool IsLaunchComplete => State == GameLaunchState.Ready || State == GameLaunchState.Failed;

        /// <summary>启动是否成功</summary>
        public bool IsLaunchSuccess => State == GameLaunchState.Ready;

        /// <summary>启动流程完成时触发（含成功和失败），参数为最终状态</summary>
        public event Action<GameLaunchState> OnLaunchCompleted;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            try
            {
                OnAwake();
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameLauncher] OnAwake error: {e}");
            }
        }

        private void Start()
        {
            StartCoroutine(LaunchSequence());
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// 启动流程主协程，按顺序执行 内置Init → 子类Init → 内置Init → 子类Init → Ready。
        /// <para>每个阶段先执行基类内置初始化，再执行子类重写的逻辑。</para>
        /// <para>任一阶段抛出异常均会终止并进入 Failed 状态。</para>
        /// </summary>
        private IEnumerator LaunchSequence()
        {
            // PreInit
            State = GameLaunchState.PreInit;
            Debug.Log("[GameLauncher] PreInit start");
            yield return StartCoroutine(RunStage(PreInitBase()));
            if (State == GameLaunchState.Failed) { OnLaunchCompleted?.Invoke(State); yield break; }
            yield return StartCoroutine(RunStage(OnPreInit()));
            if (State == GameLaunchState.Failed) { OnLaunchCompleted?.Invoke(State); yield break; }
            Debug.Log("[GameLauncher] PreInit done");

            // Init
            State = GameLaunchState.Initializing;
            Debug.Log("[GameLauncher] Init start");
            yield return StartCoroutine(RunStage(InitBase()));
            if (State == GameLaunchState.Failed) { OnLaunchCompleted?.Invoke(State); yield break; }
            yield return StartCoroutine(RunStage(OnInit()));
            if (State == GameLaunchState.Failed) { OnLaunchCompleted?.Invoke(State); yield break; }
            Debug.Log("[GameLauncher] Init done");

            // PostInit
            State = GameLaunchState.PostInit;
            Debug.Log("[GameLauncher] PostInit start");
            yield return StartCoroutine(RunStage(PostInitBase()));
            if (State == GameLaunchState.Failed) { OnLaunchCompleted?.Invoke(State); yield break; }
            yield return StartCoroutine(RunStage(OnPostInit()));
            if (State == GameLaunchState.Failed) { OnLaunchCompleted?.Invoke(State); yield break; }
            Debug.Log("[GameLauncher] PostInit done");

            // Ready
            State = GameLaunchState.Ready;
            try
            {
                OnReady();
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameLauncher] OnReady error: {e}");
                LaunchException = e;
                State = GameLaunchState.Failed;
                OnLaunchCompleted?.Invoke(State);
                yield break;
            }
            Debug.Log("[GameLauncher] Ready");
            OnLaunchCompleted?.Invoke(State);
        }

        /// <summary>
        /// 执行一个启动阶段，捕获异常并记录。
        /// <para>完成后通过 <see cref="State"/> 判断是否成功（Failed 表示失败）。</para>
        /// </summary>
        /// <param name="stage">阶段协程</param>
        private IEnumerator RunStage(IEnumerator stage)
        {
            while (true)
            {
                bool hasNext;
                try
                {
                    hasNext = stage.MoveNext();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GameLauncher] Stage [{State}] error: {e}");
                    LaunchException = e;
                    State = GameLaunchState.Failed;
                    yield break;
                }

                if (!hasNext)
                {
                    yield break;
                }

                yield return stage.Current;
            }
        }

        #region Built-in Init

        /// <summary>
        /// 基类内置的 PreInit：创建核心服务（EntityManager 等）。
        /// </summary>
        private IEnumerator PreInitBase()
        {
            EnsureEntityManager();
            yield break;
        }

        /// <summary>
        /// 基类内置的 Init：核心服务初始化。
        /// </summary>
        private IEnumerator InitBase()
        {
            EntityManager.Instance.PrewarmAll();
            yield break;
        }

        /// <summary>
        /// 基类内置的 PostInit：启动收尾。
        /// </summary>
        private IEnumerator PostInitBase()
        {
            yield break;
        }

        /// <summary>
        /// 确保 EntityManager 存在，不存在则自动创建。
        /// </summary>
        private static void EnsureEntityManager()
        {
            if (EntityManager.Instance != null) return;

            var go = new GameObject("EntityManager");
            DontDestroyOnLoad(go);
            go.AddComponent<EntityManager>();
        }

        #endregion

        #region Virtual Methods

        /// <summary>
        /// Awake 阶段回调，在单例初始化之后调用。
        /// <para>适合做不依赖其他 MonoBehaviour 的早期初始化。</para>
        /// </summary>
        protected virtual void OnAwake()
        {
        }

        /// <summary>
        /// 预初始化阶段（PreInit），用于加载配置、注册工厂等游戏特有基础准备。
        /// <para>此阶段执行时 EntityManager 等核心服务已就绪。</para>
        /// </summary>
        protected virtual IEnumerator OnPreInit()
        {
            yield break;
        }

        /// <summary>
        /// 核心初始化阶段（Init），用于创建游戏特有管理器、预加载资源等。
        /// </summary>
        protected virtual IEnumerator OnInit()
        {
            yield break;
        }

        /// <summary>
        /// 初始化收尾阶段（PostInit），用于绑定事件、开启 UI 等初始化完成后操作。
        /// </summary>
        protected virtual IEnumerator OnPostInit()
        {
            yield break;
        }

        /// <summary>
        /// 所有初始化完成后的回调，游戏已就绪。
        /// <para>适合启动游戏主循环、播放开场动画等。</para>
        /// </summary>
        protected virtual void OnReady()
        {
        }

        #endregion
    }
}