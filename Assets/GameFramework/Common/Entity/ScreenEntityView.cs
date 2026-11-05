using UnityEngine;

namespace GameFramework.Common
{
    /// <summary>
    /// UI 实体视图，挂载在 Canvas 下通过 RectTransform 渲染。
    /// <para>坐标使用 position（世界空间坐标）。用于子弹、UI特效等。</para>
    /// </summary>
    public class ScreenEntityView : EntityView
    {
        /// <summary>UI Canvas 引用，由 GameLauncher 在初始化时设置</summary>
        public static Canvas ActiveCanvas { get; set; }

        /// <summary>
        /// 使用屏幕像素坐标设置 UI 位置。
        /// <para>Screen Space - Overlay 下画布世界坐标即屏幕像素，可直接赋值；
        /// Screen Space - Camera 下需先转换为画布本地坐标。</para>
        /// </summary>
        public override void SetPosition(Vector3 pos)
        {
            if (RectTransform == null) return;

            Canvas canvas = ActiveCanvas;
            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                var canvasRect = canvas.transform as RectTransform;
                if (canvasRect != null &&
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        canvasRect, new Vector2(pos.x, pos.y), canvas.worldCamera, out Vector2 localPoint))
                {
                    RectTransform.anchoredPosition = localPoint;
                    return;
                }
            }

            RectTransform.position = pos;
        }

        public void SetAnchoredPosition(Vector3 pos)
        {
            if (RectTransform != null)
            {
                RectTransform.anchoredPosition = pos;
            }
        }

        /// <summary>显示时优先使用 CustomParent，否则挂载到 ActiveCanvas。</summary>
        public override void Show()
        {
            if (CustomParent != null)
            {
                transform.SetParent(CustomParent, false);
                CustomParent = null;
            }
            else if (RectTransform != null && ActiveCanvas != null)
            {
                transform.SetParent(ActiveCanvas.transform, false);
            }
            gameObject.SetActive(true);
        }

        /// <summary>返回实体屏幕像素坐标（左下角原点）。</summary>
        public override Vector2 GetScreenPosition()
        {
            return new Vector2(Entity.Position.x, Entity.Position.y);
        }
    }
}