using UnityEngine;

namespace GameFramework.Common
{
    /// <summary>
    /// 实体视图抽象接口，解耦 IEntity 与具体 MonoBehaviour。
    /// </summary>
    public interface IEntityView
    {
        /// <summary>将逻辑坐标位置同步到视图 Transform。</summary>
        void SetPosition(Vector3 pos);

        /// <summary>将旋转（欧拉角）同步到视图 Transform。</summary>
        void SetRotation(Vector3 eulerAngles);

        /// <summary>显示视图。</summary>
        void Show();

        /// <summary>隐藏视图。</summary>
        void Hide();

        /// <summary>
        /// 获取实体的屏幕像素坐标（左下角原点）。
        /// <para>2D实体直接返回逻辑坐标XY，3D实体通过 <see cref="EntitySpace"/> 投影。</para>
        /// </summary>
        /// <returns>屏幕像素坐标</returns>
        Vector2 GetScreenPosition();
    }
}