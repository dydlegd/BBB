using System.Collections.Generic;

namespace GameFramework.Common
{
    /// <summary>
    /// 活跃实体的只读视图，遍历时自动跳过「已标记待移除」与「已失活」的实体。
    /// <para>为什么需要它：<see cref="EntityManager.Despawn"/> 是延迟回收——实体被标记后仍留在追踪列表里，
    /// 直到帧末 FlushPendingRemove 才真正移除。这中间若是遍历活跃列表做碰撞检测，就会打到一条
    /// 本帧已经死了的实体，表现为「子弹穿透」「重复扣血」「AOE 打死尸体」等偶发问题。
    /// 以前这个过滤要调用方自己写 <c>IsPendingRemove</c> 判断，忘了写不报错，只在特定时序下出错。</para>
    /// <para>struct 实现，<c>foreach</c> 不产生 GC 分配，可安全用于每帧的碰撞遍历。</para>
    /// <para>注意：本类型取代了原先直接返回内部 <see cref="List{T}"/> 的做法，因此不再提供
    /// 索引器与 <c>Count</c>；需要数量请用 <see cref="EntityManager.TotalActiveCount"/>
    /// 或 <see cref="EntityManager.GetActiveCount{TConfig}"/>。</para>
    /// </summary>
    public readonly struct ActiveEntityEnumerable
    {
        /// <summary>空集合，用于无匹配时的返回值，遍历不产生任何迭代开销。</summary>
        public static readonly ActiveEntityEnumerable Empty = default;

        private readonly List<EntityBase> _source;
        private readonly HashSet<EntityBase> _pendingRemove;

        internal ActiveEntityEnumerable(List<EntityBase> source, HashSet<EntityBase> pendingRemove)
        {
            _source = source;
            _pendingRemove = pendingRemove;
        }

        /// <summary>获取遍历器。struct 返回，配合 <c>foreach</c> 编译为纯索引循环，无装箱无分配。</summary>
        public Enumerator GetEnumerator()
        {
            return new Enumerator(_source, _pendingRemove);
        }

        /// <summary>活跃实体遍历器。</summary>
        public struct Enumerator
        {
            private readonly List<EntityBase> _source;
            private readonly HashSet<EntityBase> _pendingRemove;
            private int _index;
            private EntityBase _current;

            internal Enumerator(List<EntityBase> source, HashSet<EntityBase> pendingRemove)
            {
                _source = source;
                _pendingRemove = pendingRemove;
                _index = -1;
                _current = null;
            }

            /// <summary>当前实体。</summary>
            public EntityBase Current => _current;

            /// <summary>前进到下一个仍然存活且未被标记回收的实体。</summary>
            /// <returns>存在下一个元素返回 true</returns>
            public bool MoveNext()
            {
                if (_source == null) return false;

                while (++_index < _source.Count)
                {
                    var entity = _source[_index];

                    // 列表理论上不会存 null，但 Flush 期间存在瞬态，这里一并挡掉，
                    // 保证调用方拿到的每个元素都可直接解引用。
                    if (entity == null) continue;
                    if (!entity.IsActive) continue;

                    // 绝大多数帧里没有待移除实体，先判 Count 再查表，把常见路径的哈希开销省掉
                    if (_pendingRemove != null && _pendingRemove.Count > 0 && _pendingRemove.Contains(entity)) continue;

                    _current = entity;
                    return true;
                }

                _current = null;
                return false;
            }
        }
    }
}
