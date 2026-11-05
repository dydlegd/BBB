using GameFramework.Common;
using UnityEngine;

namespace GameFish
{
    #region ===================== SwimStrategyType 枚举 =====================

/// <summary>游动策略类型枚举，用于FishConfig中配置默认策略</summary>
public enum SwimStrategyType
{
    /// <summary>直线前进</summary>
    Straight,

    /// <summary>正弦波S形游动</summary>
    SineWave,

    /// <summary>随机偏转游动</summary>
    RandomTurn,

    /// <summary>环形绕圈游动</summary>
    Circle,
}

#endregion

#region ===================== FishConfig ScriptableObject =====================

/// <summary>
/// 鱼配置数据，继承 EntityConfig，添加鱼特有参数。
/// <para>EntityId / EntityName / Prefab / PreloadCount 继承自基类。</para>
/// </summary>
/// <example>
/// 使用示例：
/// <code>
/// // 1. 右键 → Create → 捕鱼 → FishConfig 创建配置资产
/// // 2. 在Inspector中配置各类参数
/// // 3. 代码中使用
/// FishConfig config = Resources.Load<FishConfig>("Configs/Fish_Clown");
/// fish.SetConfig(config);
/// fish.OnSpawn();
/// </code>
/// </example>
[CreateAssetMenu(fileName = "FishConfig", menuName = "捕鱼游戏/FishConfig")]
public class FishConfig : EntityConfig
{
    /// <summary>最大生命值</summary>
    [Header("战斗属性")]
    public int MaxHP = 100;

    /// <summary>击杀得分</summary>
    public int Score;

    /// <summary>基础移动速度</summary>
    [Header("移动")]
    public float BaseMoveSpeed = 3f;

    /// <summary>碰撞半径</summary>
    public float CollideRadius = 0.5f;

    /// <summary>默认游动策略类型</summary>
    [Header("游动策略")]
    public SwimStrategyType DefaultStrategy;

    /// <summary>SineWave：频率</summary>
    [Header("SineWave 参数")]
    public float WaveFrequency = 2.2f;

    /// <summary>SineWave：振幅</summary>
    public float WaveAmplitude = 0.35f;

    /// <summary>RandomTurn：最小转向间隔（秒）</summary>
    [Header("RandomTurn 参数")]
    public float TurnIntervalMin = 2.5f;

    /// <summary>RandomTurn：最大转向间隔（秒）</summary>
    public float TurnIntervalMax = 5f;

    /// <summary>RandomTurn：偏转角度范围（±值，度）</summary>
    public float TurnAngleRange = 60f;

    /// <summary>Circle：绕圈旋转速度</summary>
    [Header("Circle 参数")]
    public float CircleRotateSpeed = 1f;

    /// <summary>游动边界（相对原点的半边长，X/Y 共用，超出后掉头）</summary>
    [Header("空间")]
    public float BoundaryLimit = 30f;

    /// <summary>Z 轴（景深）波动幅度，0 表示固定深度；3D 鱼可设非 0，让前后位置随时间摆动</summary>
    public float DepthAmplitude = 0f;

    /// <summary>Z 轴波动频率（Hz）</summary>
    public float DepthFrequency = 0.2f;

    /// <summary>
    /// 渲染旋转偏移（欧拉角），只影响显示、不影响游动方向。
    /// <para>美术朝向约定不固定时用它补偿：2D 立绘头朝左填 (0,0,180)，3D 模型直立制作可填 (-90,0,0)。</para>
    /// </summary>
    [Header("表现")]
    public Vector3 ViewRotationOffset = Vector3.zero;

    /// <summary>
    /// 根据配置创建对应的鱼实体实例。
    /// <para>如果 Config 中指定了不同的鱼子类型（如 Boss），可在此处通过字段判断后返回不同实体。</para>
    /// </summary>
    /// <returns>FishEntity 或其子类实例</returns>
    public override EntityBase CreateEntity()
    {
        return new FishEntity();
    }
}

#endregion
}