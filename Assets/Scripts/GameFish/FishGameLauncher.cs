using System.Collections;
using GameFramework.Common;
using UnityEngine;

namespace GameFish
{

    /// <summary>
    /// 鱼游戏启动器，继承 GameLauncher，负责鱼游戏特有的初始化流程。
    /// <para>EntityManager 由基类自动创建，本类负责工厂注册和 EntityFactory 实例创建。</para>
    /// </summary>
    public class FishGameLauncher : GameLauncher
    {
        /// <summary>鱼实体工厂</summary>
        public static EntityFactory<FishConfig, FishEntity> FishFactory { get; private set; }

        /// <summary>子弹实体工厂</summary>
        public static EntityFactory<BulletConfig, BulletEntity> BulletFactory { get; private set; }

        protected override IEnumerator OnPreInit()
        {
            FishFactory = new EntityFactory<FishConfig, FishEntity>();
            BulletFactory = new EntityFactory<BulletConfig, BulletEntity>();

            InitUICanvas();

            yield break;
        }

        protected override IEnumerator OnInit()
        {
            EntityManager.Instance.PrewarmAll();

            yield break;
        }

        protected override void OnReady()
        {
            Debug.Log("[FishGameLauncher] 鱼游戏启动完成！");
        }

        /// <summary>
        /// 查找并设置 UI Canvas 和主摄像机引用，供 EntityView 使用。
        /// <para>不能用 FindObjectOfType&lt;Canvas&gt;：炮台预制体里还挂着多个嵌套小 Canvas（ZHAO、ZHAO (1)、UI），
        /// 一旦被先找到，UI 实体就会被挂到炮台特效画布下，继承它的缩放和 Timeline 动画，表现为子弹位置/大小异常。</para>
        /// </summary>
        private static void InitUICanvas()
        {
            if (ScreenEntityView.ActiveCanvas != null) return;

            var canvas = FindMainUICanvas();
            if (canvas == null)
            {
                Debug.LogError("[FishGameLauncher] 场景中找不到 Canvas，UI 实体将无法渲染！");
                return;
            }
            ScreenEntityView.ActiveCanvas = canvas;
            Debug.Log($"[FishGameLauncher] UI Canvas 已绑定: {canvas.name} (mode={canvas.renderMode}, root={canvas.isRootCanvas})");

            EntitySpace.MainCamera = canvas.worldCamera ?? Camera.main;
            if (EntitySpace.MainCamera != null)
            {
                Debug.Log($"[FishGameLauncher] 主摄像机已绑定: {EntitySpace.MainCamera.name}");
            }
            else
            {
                Debug.LogError("[FishGameLauncher] 找不到主摄像机！碰撞检测将无法工作。");
            }
        }

        /// <summary>
        /// 查找承载 UI 实体的主画布。
        /// <para>优先取非 WorldSpace 的根 Canvas（嵌套画布的 renderMode 由根画布决定，不能用它区分）；
        /// 若没有根画布，则退回面积最大的非 WorldSpace 画布，避免误绑炮台特效里的小画布。</para>
        /// </summary>
        private static Canvas FindMainUICanvas()
        {
            Canvas mainCanvas = null;
            float mainArea = -1f;
            Canvas fallbackCanvas = null;
            float fallbackArea = -1f;

            foreach (var canvas in Object.FindObjectsOfType<Canvas>())
            {
                if (canvas.renderMode == RenderMode.WorldSpace) continue;

                var rect = canvas.transform as RectTransform;
                float area = rect != null ? rect.rect.width * rect.rect.height : 0f;

                if (!canvas.isRootCanvas)
                {
                    if (area > fallbackArea)
                    {
                        fallbackCanvas = canvas;
                        fallbackArea = area;
                    }
                    continue;
                }

                if (area > mainArea)
                {
                    mainCanvas = canvas;
                    mainArea = area;
                }
            }

            return mainCanvas != null ? mainCanvas : fallbackCanvas;
        }

        void Update()
        {
            
        }
    }
}