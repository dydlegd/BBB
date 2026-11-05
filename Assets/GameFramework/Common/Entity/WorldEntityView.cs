using UnityEngine;

namespace GameFramework.Common
{
    /// <summary>
    /// 3D 世界实体视图，使用 Transform.position 定位。
    /// <para>坐标使用世界3D坐标，用于鱼、3D物体等。屏幕坐标由 <see cref="EntitySpace"/> 统一转换。</para>
    /// </summary>
    public class WorldEntityView : EntityView
    {
        /// <summary>显示时优先使用 CustomParent，否则保持当前父节点不变。</summary>
        public override void Show()
        {
            if (CustomParent != null)
            {
                transform.SetParent(CustomParent, false);
                CustomParent = null;
            }
            gameObject.SetActive(true);
        }

        /// <summary>使用世界坐标设置位置。</summary>
        public override void SetPosition(Vector3 pos)
        {
            transform.position = pos;
        }

        /// <summary>将实体的世界3D坐标转换为屏幕像素坐标（左下角原点）。</summary>
        public override Vector2 GetScreenPosition()
        {
            return EntitySpace.WorldToScreen(Entity.Position);
        }
    }
}
