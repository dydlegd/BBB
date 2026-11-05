using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameFish
{
    #region ===================== 游动策略接口 =====================

/// <summary>
/// 游动策略接口，接收当前欧拉角旋转，返回计算后的新欧拉角
/// <para>策略实现为单例，内部不存成员变量，所有动态数据放在ISwimContext中</para>
/// </summary>
/// <example>
/// 使用示例：
/// <code>
/// // 切换策略
/// fish.SetSwimStrategy(SineWaveSwim.Instance);
///
/// // MoveLogic 中调用
/// Rotation = strategy.CalculateRotation(swimContext, Rotation, Time.deltaTime);
/// </code>
/// </example>
public interface ISwimStrategy
{
    /// <summary>根据当前欧拉角和上下文计算新欧拉角</summary>
    Vector3 CalculateRotation(ISwimContext context, Vector3 currentRotation, float dt);
}

#endregion

#region ===================== 策略实现（单例） =====================

/// <summary>
/// 直线前进策略
/// <para>保持当前朝向不变</para>
/// </summary>
/// <example>
/// 使用示例：
/// <code>
/// fish.SetSwimStrategy(StraightSwim.Instance);
/// </code>
/// </example>
public class StraightSwim : ISwimStrategy
{
    /// <summary>单例</summary>
    public static readonly StraightSwim Instance = new StraightSwim();
    private StraightSwim() { }

    /// <summary>直线移动，不改变旋转</summary>
    public Vector3 CalculateRotation(ISwimContext context, Vector3 currentRotation, float dt)
    {
        return currentRotation;
    }
}

/// <summary>
/// 正弦波S形游动策略
/// <para>沿当前朝向前进的同时左右摆动，形成S形轨迹</para>
/// </summary>
/// <example>
/// 使用示例：
/// <code>
/// fish.SetSwimStrategy(SineWaveSwim.Instance);
/// // 通过 FishConfig 或直接设置参数
/// swimData.WaveFrequency = 2.2f;
/// swimData.WaveAmplitude = 0.35f;
/// </code>
/// </example>
public class SineWaveSwim : ISwimStrategy
{
    /// <summary>单例</summary>
    public static readonly SineWaveSwim Instance = new SineWaveSwim();
    private SineWaveSwim() { }

    /// <summary>正弦波偏转，修改欧拉角Y分量</summary>
    public Vector3 CalculateRotation(ISwimContext context, Vector3 currentRotation, float dt)
    {
        context.WavePhase += dt * context.WaveFrequency;

        float angleRad = Mathf.Cos(context.WavePhase) * context.WaveFrequency * context.WaveAmplitude * dt;
        currentRotation.y += -angleRad * Mathf.Rad2Deg;
        return currentRotation;
    }
}

/// <summary>
/// 随机偏转游动策略
/// <para>每过一段随机时间，在±TurnAngleRange范围内随机偏转朝向</para>
/// </summary>
/// <example>
/// 使用示例：
/// <code>
/// fish.SetSwimStrategy(RandomTurnSwim.Instance);
/// // 可通过 FishConfig 配置偏转角度和间隔
/// swimData.TurnAngleRange = 60f;
/// swimData.TurnIntervalMin = 2f;
/// swimData.TurnIntervalMax = 4f;
/// </code>
/// </example>
public class RandomTurnSwim : ISwimStrategy
{
    /// <summary>单例</summary>
    public static readonly RandomTurnSwim Instance = new RandomTurnSwim();
    private RandomTurnSwim() { }

    /// <summary>随机偏转，到达倒计时时随机修改欧拉角Y分量</summary>
    public Vector3 CalculateRotation(ISwimContext context, Vector3 currentRotation, float dt)
    {
        context.TurnCountdown -= dt;
        if (context.TurnCountdown <= 0f)
        {
            float angle = Random.Range(-context.TurnAngleRange, context.TurnAngleRange);
            currentRotation.y += angle;
            context.TurnCountdown = Random.Range(context.TurnIntervalMin, context.TurnIntervalMax);
        }
        return currentRotation;
    }
}

/// <summary>
/// 环形绕圈游动策略
/// <para>每帧持续旋转 dt*CircleRotateSpeed 角度，形成圆周运动</para>
/// </summary>
/// <example>
/// 使用示例：
/// <code>
/// fish.SetSwimStrategy(CircleSwim.Instance);
/// // 可通过 FishConfig 配置绕圈速度
/// swimData.CircleRotateSpeed = 1.5f;
/// </code>
/// </example>
public class CircleSwim : ISwimStrategy
{
    /// <summary>单例</summary>
    public static readonly CircleSwim Instance = new CircleSwim();
    private CircleSwim() { }

    /// <summary>圆周旋转，每帧累加欧拉角Y分量</summary>
    public Vector3 CalculateRotation(ISwimContext context, Vector3 currentRotation, float dt)
    {
        float angleDeg = dt * context.CircleRotateSpeed * Mathf.Rad2Deg;
        currentRotation.y += angleDeg;
        return currentRotation;
    }
}

#endregion
}