using UnityEngine;

namespace GameFramework.Common
{
    /// <summary>
    /// 实体配置基类（ScriptableObject），所有实体配置均继承此类。
    /// <para>包含所有实体通用的基础字段，子类添加类型特有参数。</para>
    /// <para>子类必须实现 <see cref="CreateEntity"/> 返回对应 EntityBase 子类实例，支持同一种 Config 生成不同实体类型。</para>
    /// </summary>
    public abstract class EntityConfig : ScriptableObject
    {
        /// <summary>配置唯一ID。</summary>
        [Header("基础属性")]
        public int ConfigId;

        /// <summary>实体名称。</summary>
        public string EntityName;

        /// <summary>
        /// 实体预制体。
        /// <para>为 null 时该配置生成纯数据实体：不创建 View，只参与逻辑更新与查询。</para>
        /// </summary>
        [Header("表现")]
        public GameObject Prefab;

        /// <summary>对象池预加载数量。</summary>
        [Header("池设置")]
        public int PreloadCount;

        /// <summary>
        /// 根据配置参数创建对应的实体实例。
        /// <para>子类根据自身字段决定返回哪个 EntityBase 子类，实现"一种 Config → 多种实体"。</para>
        /// </summary>
        /// <returns>创建的实体实例</returns>
        public abstract EntityBase CreateEntity();
    }
}