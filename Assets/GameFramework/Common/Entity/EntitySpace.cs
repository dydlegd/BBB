using UnityEngine;

namespace GameFramework.Common
{
    /// <summary>
    /// 全局坐标空间转换服务。
    /// <para>约定：3D 实体的逻辑位置为世界坐标；2D 实体（子弹、UI特效）的逻辑位置为屏幕像素坐标（左下角原点）。</para>
    /// <para>世界坐标与屏幕坐标的转换统一走本类，其他模块不要再各自持有 Camera 做转换。</para>
    /// </summary>
    public static class EntitySpace
    {
        /// <summary>主摄像机，由启动流程注入；未注入时回退 Camera.main</summary>
        public static Camera MainCamera { get; set; }

        /// <summary>屏幕像素边界（左下角原点）</summary>
        public static Rect ScreenBounds => new Rect(0f, 0f, Screen.width, Screen.height);

        /// <summary>世界坐标转屏幕像素坐标，位于相机背后时返回零向量</summary>
        public static Vector2 WorldToScreen(Vector3 worldPos)
        {
            Camera cam = ResolveCamera();
            if (cam == null) return Vector2.zero;

            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
            if (screenPos.z <= 0f) return Vector2.zero;
            return new Vector2(screenPos.x, screenPos.y);
        }

        /// <summary>
        /// 世界空间半径转屏幕像素半径（球形近似）。
        /// <para>在目标位置沿摄像机右方向偏移 worldRadius 后投影，取两点的屏幕像素距离。</para>
        /// </summary>
        public static float WorldRadiusToScreen(Vector3 worldPos, float worldRadius)
        {
            Camera cam = ResolveCamera();
            if (cam == null || worldRadius <= 0f) return 0f;

            Vector3 center = cam.WorldToScreenPoint(worldPos);
            Vector3 offset = cam.WorldToScreenPoint(worldPos + cam.transform.right * worldRadius);
            if (center.z <= 0f || offset.z <= 0f) return 0f;

            return Vector2.Distance(
                new Vector2(center.x, center.y),
                new Vector2(offset.x, offset.y));
        }

        /// <summary>
        /// 将 Canvas 层级中的坐标转换为屏幕像素坐标（左下角原点）。
        /// <para>Canvas 下的 Transform.position 是画布世界坐标而非场景世界坐标，不能直接用
        /// <see cref="WorldToScreen"/> 投影。调用方应先确认目标确实位于 Canvas 下。</para>
        /// </summary>
        /// <param name="worldPos">Canvas 层级中的位置</param>
        /// <param name="canvas">该元素所属 Canvas</param>
        /// <returns>屏幕像素坐标</returns>
        public static Vector2 UiWorldToScreen(Vector3 worldPos, Canvas canvas)
        {
            if (canvas == null) return WorldToScreen(worldPos);

            // Overlay 画布的坐标本身就是屏幕像素，eventCamera 传 null 表示不投影
            Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : (canvas.worldCamera != null ? canvas.worldCamera : ResolveCamera());

            return RectTransformUtility.WorldToScreenPoint(eventCamera, worldPos);
        }

        /// <summary>获取可用摄像机，未注入时尝试 Camera.main</summary>
        private static Camera ResolveCamera()
        {
            if (MainCamera == null)
            {
                MainCamera = Camera.main;
            }
            return MainCamera;
        }
    }
}
