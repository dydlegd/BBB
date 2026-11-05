/// <summary>
/// 对象池元素接口，用于池化GameObject的生成/回收回调
/// </summary>
/// <example>
/// 使用示例：
/// <code>
/// public class MyPoolObject : MonoBehaviour, IPoolable
/// {
///     public void OnSpawn()
///     {
///         // 从池中取出，重置状态
///         hp = maxHp;
///         transform.position = spawnPoint;
///     }
///
///     public void OnDespawn()
///     {
///         // 回收到池，清理引用
///         target = null;
///     }
/// }
/// </code>
/// </example>
namespace GameFramework.Common
{
    public interface IPoolable
    {
        /// <summary>从池中取出时调用，做初始化/重置</summary>
        void OnSpawn();

        /// <summary>回收到池时调用，做清理/还原</summary>
        void OnDespawn();
    }
}