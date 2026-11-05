using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameFramework.Common
{
    /// <summary>
    /// 通用实体工厂，封装指定类型实体的生成、回收和查询操作。
    /// <para>泛型参数指定实体配置类型和实体类型，内部委托给 <see cref="EntityManager"/> 执行。</para>
    /// <para>每个游戏模块创建自己的工厂实例（如鱼工厂、子弹工厂），无需额外注册或初始化。</para>
    /// <para>所有成员都会先校验 <see cref="EntityManager.Instance"/> 是否存在：框架未初始化时输出错误并返回安全值
    /// （引用类型返回 null、集合返回空、void 方法直接返回），不会抛 NullReferenceException。</para>
    /// </summary>
    /// <typeparam name="TConfig">EntityConfig 子类型（如 FishConfig、BulletConfig）</typeparam>
    /// <typeparam name="TEntity">EntityBase 子类型（如 FishEntity、BulletEntity）</typeparam>
    /// <example>
    /// 创建和持有工厂：
    /// <code>
    /// // 在 FishGameLauncher 中创建工厂实例
    /// public static EntityFactory&lt;FishConfig, FishEntity&gt; Fish { get; private set; }
    /// public static EntityFactory&lt;BulletConfig, BulletEntity&gt; Bullet { get; private set; }
    ///
    /// Fish = new EntityFactory&lt;FishConfig, FishEntity&gt;();
    /// Bullet = new EntityFactory&lt;BulletConfig, BulletEntity&gt;();
    /// </code>
    ///
    /// 运行时使用：
    /// <code>
    /// var fishFactory = FishGameLauncher.Fish;
    ///
    /// FishEntity fish = fishFactory.SpawnById(1001, position, new Vector3(0, 90, 0));
    ///
    /// FishEntity fish2 = fishFactory.SpawnByName("小丑鱼", position, new Vector3(0, 270, 0));
    ///
    /// fishFactory.Recycle(fish);
    ///
    /// fishFactory.RecycleAll();
    ///
    /// foreach (var f in fishFactory.ActiveEntities)
    /// {
    ///     var fishEntity = (FishEntity)f;
    ///     Debug.Log(fishEntity.GetConfig().EntityName);
    /// }
    /// </code>
    /// </example>
    public class EntityFactory<TConfig, TEntity>
        where TConfig : EntityConfig
        where TEntity : EntityBase
    {
        /// <summary>
        /// 创建实体工厂实例。
        /// <para>工厂本身无状态，所有操作委托给 <see cref="EntityManager.Instance"/>。</para>
        /// <para>构造工厂不会访问 <see cref="EntityManager"/>，因此可以在框架初始化之前就创建。</para>
        /// </summary>
        public EntityFactory()
        {
        }

        /// <summary>
        /// 当前可用的 <see cref="EntityManager"/> 单例。
        /// <para>未初始化时输出错误日志并返回 null，由各成员短路处理。</para>
        /// </summary>
        private static EntityManager Manager
        {
            get
            {
                var manager = EntityManager.Instance;
                if (manager == null)
                {
                    Debug.LogError(
                        $"[EntityFactory<{typeof(TConfig).Name}, {typeof(TEntity).Name}>] EntityManager 尚未初始化。" +
                        "请确认 GameLauncher 已完成初始化，或场景中存在激活的 EntityManager。");
                }

                return manager;
            }
        }

        /// <summary>
        /// 获取本工厂对应配置类型的所有活跃实体（零分配，只读）。
        /// <para>遍历时自动跳过已标记待移除的实体。框架未初始化时返回空集合。</para>
        /// </summary>
        public ActiveEntityEnumerable ActiveEntities
        {
            get
            {
                var manager = Manager;
                if (manager == null) return ActiveEntityEnumerable.Empty;

                return manager.GetActive<TConfig>();
            }
        }

        /// <summary>当前活跃实体数量。框架未初始化时返回 0。</summary>
        public int ActiveCount
        {
            get
            {
                var manager = Manager;
                if (manager == null) return 0;

                return manager.GetActiveCount<TConfig>();
            }
        }

        /// <summary>
        /// 根据 EntityConfig 直接生成实体。
        /// </summary>
        /// <param name="config">实体配置资产</param>
        /// <param name="position">出生位置</param>
        /// <param name="rotation">出生旋转（欧拉角）</param>
        /// <param name="parent">自定义父节点，为 null 时使用默认父节点</param>
        /// <returns>已初始化的实体实例；配置为空、生成的实体类型与 TEntity 不符、或框架未初始化时返回 null</returns>
        public TEntity Spawn(TConfig config, Vector3 position, Vector3 rotation, Transform parent = null)
        {
            if (config == null)
            {
                Debug.LogError($"[EntityFactory<{typeof(TConfig).Name}, {typeof(TEntity).Name}>] Spawn failed: config is null");
                return null;
            }

            var manager = Manager;
            if (manager == null) return null;

            // 走 EntityManager 的类型校验路径：泛型 TEntity 只是编译期约束，
            // 运行期实体类型由 config.CreateEntity() 决定，二者不符时由 SpawnAs 输出可读日志并回收。
            return manager.SpawnAs<TConfig, TEntity>(config, position, rotation, parent, $"ConfigId={config.ConfigId}");
        }

        /// <summary>
        /// 根据实体配置ID生成实体。
        /// </summary>
        /// <param name="configId">实体配置的 ConfigId</param>
        /// <param name="position">出生位置</param>
        /// <param name="rotation">出生旋转（欧拉角）</param>
        /// <param name="parent">自定义父节点，为 null 时使用默认父节点</param>
        /// <returns>已初始化的实体实例；配置不存在、类型不符或框架未初始化时返回 null</returns>
        public TEntity SpawnById(int configId, Vector3 position, Vector3 rotation, Transform parent = null)
        {
            var manager = Manager;
            if (manager == null) return null;

            return manager.SpawnById<TConfig, TEntity>(configId, position, rotation, parent);
        }

        /// <summary>
        /// 根据实体名称生成实体。
        /// </summary>
        /// <param name="entityName">实体配置的 EntityName</param>
        /// <param name="position">出生位置</param>
        /// <param name="rotation">出生旋转（欧拉角）</param>
        /// <param name="parent">自定义父节点，为 null 时使用默认父节点</param>
        /// <returns>已初始化的实体实例；配置不存在、类型不符或框架未初始化时返回 null</returns>
        public TEntity SpawnByName(string entityName, Vector3 position, Vector3 rotation, Transform parent = null)
        {
            var manager = Manager;
            if (manager == null) return null;

            return manager.SpawnByName<TConfig, TEntity>(entityName, position, rotation, parent);
        }

        /// <summary>
        /// 回收单个实体到对象池。
        /// <para>实体从活跃列表移除，状态重置后放回池中。</para>
        /// </summary>
        /// <param name="entity">要回收的实体</param>
        public void Recycle(TEntity entity)
        {
            var manager = Manager;
            if (manager == null) return;

            manager.Despawn(entity);
        }

        /// <summary>
        /// 回收当前类型的所有活跃实体。
        /// </summary>
        public void RecycleAll()
        {
            var manager = Manager;
            if (manager == null) return;

            manager.DespawnAll<TConfig>();
        }

        /// <summary>
        /// 按实体配置ID获取活跃实体（零分配，只读）。
        /// <para>遍历时自动跳过已标记待移除的实体。</para>
        /// </summary>
        /// <param name="configId">实体配置的 ConfigId</param>
        /// <returns>活跃实体只读视图，无匹配或框架未初始化时返回空集合</returns>
        public ActiveEntityEnumerable GetActiveById(int configId)
        {
            var manager = Manager;
            if (manager == null) return ActiveEntityEnumerable.Empty;

            return manager.GetActiveById<TConfig>(configId);
        }

        /// <summary>
        /// 按实体配置ID预热对象池。
        /// </summary>
        /// <param name="configId">实体配置的 ConfigId</param>
        /// <param name="count">预加载数量，为 0 时使用 config.PreloadCount</param>
        public void Prewarm(int configId, int count = 0)
        {
            var manager = Manager;
            if (manager == null) return;

            manager.Prewarm<TConfig>(configId, count);
        }

        /// <summary>
        /// 按实体名称预热对象池。
        /// </summary>
        /// <param name="entityName">实体配置的 EntityName</param>
        /// <param name="count">预加载数量，为 0 时使用 config.PreloadCount</param>
        public void Prewarm(string entityName, int count = 0)
        {
            var manager = Manager;
            if (manager == null) return;

            manager.Prewarm<TConfig>(entityName, count);
        }
    }
}
