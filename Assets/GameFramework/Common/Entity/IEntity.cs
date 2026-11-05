using UnityEngine;

namespace GameFramework.Common
{
    /// <summary>
    /// 通用实体接口，定义所有实体的核心数据与生命周期。
    /// <para>Position/Rotation 是逻辑数据的唯一源头，View 层从 Entity 同步。</para>
    /// <para>坐标语义由实体空间决定：3D 实体使用世界坐标；标记了 <see cref="IScreenSpaceEntity"/> 的 2D 实体使用屏幕像素坐标（左下角原点）。</para>
    /// </summary>
    public interface IEntity
    {
        /// <summary>实体唯一标识ID。</summary>
        int EntityId { get; }

        /// <summary>实体类型标识，用于 EntitySystem 筛选。</summary>
        string EntityType { get; }

        /// <summary>实体是否处于活跃（已生成）状态。</summary>
        bool IsActive { get; }

        /// <summary>实体视图。纯数据实体可为 null，此时不参与渲染，仅做逻辑更新。</summary>
        IEntityView View { get; }

        /// <summary>
        /// 逻辑位置（逻辑层数据源头）。
        /// <para>3D 实体为世界坐标；标记 <see cref="IScreenSpaceEntity"/> 的 2D 实体为屏幕像素坐标。</para>
        /// </summary>
        Vector3 Position { get; set; }

        /// <summary>旋转-欧拉角（逻辑层数据源头）。屏幕空间实体仅使用 Z 分量。</summary>
        Vector3 Rotation { get; set; }

        /// <summary>实体生成/从对象池取出时调用。</summary>
        void OnSpawn();

        /// <summary>实体回收/回到对象池时调用。</summary>
        void OnDespawn();

        /// <summary>每帧更新。</summary>
        /// <param name="deltaTime">帧间隔时间（秒）</param>
        void OnUpdate(float deltaTime);
    }

    /// <summary>
    /// 屏幕空间实体标记接口。
    /// <para>实现该接口的实体，其 <see cref="IEntity.Position"/> 表示屏幕像素坐标（左下角原点），而非世界坐标。</para>
    /// </summary>
    public interface IScreenSpaceEntity : IEntity
    {
    }
}