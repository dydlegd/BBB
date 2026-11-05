using System.Collections.Generic;
using UnityEngine;

namespace GameFish
{
    /// <summary>
    /// 多玩家炮台管理器（MonoBehaviour Singleton）。
    /// <para>管理所有 ICannon 实例，负责输入分发和玩家间协调。</para>
    /// </summary>
    public class CannonManager : SingletonMono<CannonManager>
    {
        private readonly List<ICannon> _cannons = new List<ICannon>();

        public IReadOnlyList<ICannon> Cannons => _cannons;

        private void Start()
        {
            AutoRegisterSceneCannons();
        }

        /// <summary>
        /// 自动搜索场景中所有 Cannon 组件并注册，按 Hierarchy 顺序分配 PlayerId。
        /// </summary>
        private void AutoRegisterSceneCannons()
        {
            var sceneCannons = Object.FindObjectsOfType<Cannon>();
            for (int i = 0; i < sceneCannons.Length; i++)
            {
                sceneCannons[i].Initialize(i);
                Register(sceneCannons[i]);
            }
            Debug.Log($"[CannonManager] 自动注册 {_cannons.Count} 个炮台");
        }

        private void Update()
        {
            if (_cannons.Count == 0) return;

            float rotation = 0f;
            if (Input.GetKey(KeyCode.LeftArrow)) rotation += 1f;   // 左箭头左转
            if (Input.GetKey(KeyCode.RightArrow)) rotation -= 1f;   // 右箭头右转

            if (rotation != 0f)
            {
                foreach (var cannon in _cannons)
                {
                    cannon.Rotate(rotation);
                }
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                foreach (var cannon in _cannons)
                {
                    cannon.Fire();
                }
            }
        }

        /// <summary>
        /// 注册炮台。
        /// </summary>
        public void Register(ICannon cannon)
        {
            if (!_cannons.Contains(cannon))
            {
                _cannons.Add(cannon);
            }
        }

        /// <summary>
        /// 注销炮台。
        /// </summary>
        public void Unregister(ICannon cannon)
        {
            _cannons.Remove(cannon);
        }

        /// <summary>
        /// 指定玩家开火。
        /// </summary>
        public void PlayerFire(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= _cannons.Count) return;
            _cannons[playerIndex].Fire();
        }

        /// <summary>
        /// 所有玩家同时开火。
        /// </summary>
        public void FireAll()
        {
            foreach (var cannon in _cannons)
            {
                cannon.Fire();
            }
        }
    }
}