using UnityEngine;

/// <summary>
/// 通用 Transform 操作工具类。
/// 提供位置、旋转、缩放、父子关系、坐标系转换等常用 Transform 扩展方法。
/// 所有方法均有 null 安全检查。
/// </summary>
/// <example>位置操作
/// <code>
/// transform.SetPositionX(5f);
/// transform.SetPositionY(3f);
/// transform.SetPosition(0, 0, 10f);
/// transform.AddPosition(1f, 0, 0);
///
/// transform.SetLocalPositionX(0f);
/// transform.SetLocalPosition(0, 0, 0);
/// </code>
/// </example>
/// <example>旋转操作
/// <code>
/// transform.SetRotation(0, 90, 0);
/// transform.SetEulerZ(45f);
/// transform.LookAt2D(target.position);
/// transform.RotateAroundPivot(pivot.position, Vector3.up, 90f);
/// </code>
/// </example>
/// <example>父子关系
/// <code>
/// transform.SetParentAndKeepWorld(parent);
/// transform.ResetLocal();
/// transform.DestroyAllChildren();
/// Transform child = transform.FindRecursive("Weapon");
/// </code>
/// </example>
/// <example>距离与方向
/// <code>
/// float dist = transform.DistanceTo(transform2);
/// Vector3 dir = transform.DirectionTo(transform2);
/// transform.FaceTarget(target.position);
/// </code>
/// </example>
public static class TransformUtility
{
    #region 世界坐标位置

    /// <summary>
    /// 设置世界坐标位置。
    /// </summary>
    public static void SetPosition(this Transform transform, float x, float y, float z)
    {
        if (transform == null) return;
        transform.position = new Vector3(x, y, z);
    }

    /// <summary>
    /// 设置世界坐标的 X 分量。
    /// </summary>
    public static void SetPositionX(this Transform transform, float x)
    {
        if (transform == null) return;
        Vector3 pos = transform.position;
        pos.x = x;
        transform.position = pos;
    }

    /// <summary>
    /// 设置世界坐标的 Y 分量。
    /// </summary>
    public static void SetPositionY(this Transform transform, float y)
    {
        if (transform == null) return;
        Vector3 pos = transform.position;
        pos.y = y;
        transform.position = pos;
    }

    /// <summary>
    /// 设置世界坐标的 Z 分量。
    /// </summary>
    public static void SetPositionZ(this Transform transform, float z)
    {
        if (transform == null) return;
        Vector3 pos = transform.position;
        pos.z = z;
        transform.position = pos;
    }

    /// <summary>
    /// 在世界坐标系中平移。
    /// </summary>
    public static void AddPosition(this Transform transform, float x, float y, float z)
    {
        if (transform == null) return;
        transform.position += new Vector3(x, y, z);
    }

    #endregion

    #region 局部坐标位置

    /// <summary>
    /// 设置局部坐标位置。
    /// </summary>
    public static void SetLocalPosition(this Transform transform, float x, float y, float z)
    {
        if (transform == null) return;
        transform.localPosition = new Vector3(x, y, z);
    }

    /// <summary>
    /// 设置局部坐标的 X 分量。
    /// </summary>
    public static void SetLocalPositionX(this Transform transform, float x)
    {
        if (transform == null) return;
        Vector3 pos = transform.localPosition;
        pos.x = x;
        transform.localPosition = pos;
    }

    /// <summary>
    /// 设置局部坐标的 Y 分量。
    /// </summary>
    public static void SetLocalPositionY(this Transform transform, float y)
    {
        if (transform == null) return;
        Vector3 pos = transform.localPosition;
        pos.y = y;
        transform.localPosition = pos;
    }

    /// <summary>
    /// 设置局部坐标的 Z 分量。
    /// </summary>
    public static void SetLocalPositionZ(this Transform transform, float z)
    {
        if (transform == null) return;
        Vector3 pos = transform.localPosition;
        pos.z = z;
        transform.localPosition = pos;
    }

    /// <summary>
    /// 在局部坐标系中平移。
    /// </summary>
    public static void AddLocalPosition(this Transform transform, float x, float y, float z)
    {
        if (transform == null) return;
        transform.localPosition += new Vector3(x, y, z);
    }

    #endregion

    #region 旋转

    /// <summary>
    /// 设置世界旋转（欧拉角）。
    /// </summary>
    public static void SetRotation(this Transform transform, float x, float y, float z)
    {
        if (transform == null) return;
        transform.eulerAngles = new Vector3(x, y, z);
    }

    /// <summary>
    /// 设置世界旋转的 X 分量。
    /// </summary>
    public static void SetEulerX(this Transform transform, float x)
    {
        if (transform == null) return;
        Vector3 euler = transform.eulerAngles;
        euler.x = x;
        transform.eulerAngles = euler;
    }

    /// <summary>
    /// 设置世界旋转的 Y 分量。
    /// </summary>
    public static void SetEulerY(this Transform transform, float y)
    {
        if (transform == null) return;
        Vector3 euler = transform.eulerAngles;
        euler.y = y;
        transform.eulerAngles = euler;
    }

    /// <summary>
    /// 设置世界旋转的 Z 分量。
    /// </summary>
    public static void SetEulerZ(this Transform transform, float z)
    {
        if (transform == null) return;
        Vector3 euler = transform.eulerAngles;
        euler.z = z;
        transform.eulerAngles = euler;
    }

    /// <summary>
    /// 设置局部旋转（欧拉角）。
    /// </summary>
    public static void SetLocalRotation(this Transform transform, float x, float y, float z)
    {
        if (transform == null) return;
        transform.localEulerAngles = new Vector3(x, y, z);
    }

    /// <summary>
    /// 设置局部旋转的 Z 分量（2D旋转常用）。
    /// </summary>
    public static void SetLocalEulerZ(this Transform transform, float z)
    {
        if (transform == null) return;
        Vector3 euler = transform.localEulerAngles;
        euler.z = z;
        transform.localEulerAngles = euler;
    }

    /// <summary>
    /// 围绕指定轴旋转（增量）。
    /// </summary>
    public static void RotateAroundAxis(this Transform transform, Vector3 axis, float angle)
    {
        if (transform == null) return;
        transform.Rotate(axis, angle, Space.World);
    }

    #endregion

    #region 缩放

    /// <summary>
    /// 设置局部缩放（统一值）。
    /// </summary>
    public static void SetLocalScale(this Transform transform, float scale)
    {
        if (transform == null) return;
        transform.localScale = Vector3.one * scale;
    }

    /// <summary>
    /// 设置局部缩放。
    /// </summary>
    public static void SetLocalScale(this Transform transform, float x, float y, float z)
    {
        if (transform == null) return;
        transform.localScale = new Vector3(x, y, z);
    }

    /// <summary>
    /// 设置局部缩放的 X 分量。
    /// </summary>
    public static void SetLocalScaleX(this Transform transform, float x)
    {
        if (transform == null) return;
        Vector3 scale = transform.localScale;
        scale.x = x;
        transform.localScale = scale;
    }

    /// <summary>
    /// 设置局部缩放的 Y 分量。
    /// </summary>
    public static void SetLocalScaleY(this Transform transform, float y)
    {
        if (transform == null) return;
        Vector3 scale = transform.localScale;
        scale.y = y;
        transform.localScale = scale;
    }

    /// <summary>
    /// 设置局部缩放的 Z 分量。
    /// </summary>
    public static void SetLocalScaleZ(this Transform transform, float z)
    {
        if (transform == null) return;
        Vector3 scale = transform.localScale;
        scale.z = z;
        transform.localScale = scale;
    }

    #endregion

    #region 父子关系

    /// <summary>
    /// 设置父节点并保持世界坐标不变。
    /// </summary>
    public static void SetParentAndKeepWorld(this Transform transform, Transform parent)
    {
        if (transform == null) return;
        transform.SetParent(parent, true);
    }

    /// <summary>
    /// 设置父节点并重置局部坐标。
    /// </summary>
    public static void SetParentAndReset(this Transform transform, Transform parent)
    {
        if (transform == null) return;
        transform.SetParent(parent, false);
        transform.ResetLocal();
    }

    /// <summary>
    /// 重置局部坐标：position(0,0,0), rotation(0,0,0), scale(1,1,1)。
    /// </summary>
    public static void ResetLocal(this Transform transform)
    {
        if (transform == null) return;
        transform.localPosition = Vector3.zero;
        transform.localEulerAngles = Vector3.zero;
        transform.localScale = Vector3.one;
    }

    /// <summary>
    /// 重置局部位置和旋转（保留缩放）。
    /// </summary>
    public static void ResetLocalPositionAndRotation(this Transform transform)
    {
        if (transform == null) return;
        transform.localPosition = Vector3.zero;
        transform.localEulerAngles = Vector3.zero;
    }

    /// <summary>
    /// 销毁所有子节点。
    /// </summary>
    public static void DestroyAllChildren(this Transform transform)
    {
        if (transform == null) return;
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Object.Destroy(transform.GetChild(i).gameObject);
        }
    }

    /// <summary>
    /// 立即销毁所有子节点（编辑器模式下也可用，谨慎使用）。
    /// </summary>
    public static void DestroyAllChildrenImmediate(this Transform transform)
    {
        if (transform == null) return;
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(transform.GetChild(i).gameObject);
        }
    }

    /// <summary>
    /// 深度优先递归查找子节点。
    /// </summary>
    public static Transform FindRecursive(this Transform transform, string name)
    {
        if (transform == null || string.IsNullOrEmpty(name)) return null;
        if (transform.name == name) return transform;

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform result = transform.GetChild(i).FindRecursive(name);
            if (result != null) return result;
        }
        return null;
    }

    /// <summary>
    /// 按路径查找子节点（如 "Body/Head/Eye"）。
    /// </summary>
    public static Transform FindByPath(this Transform transform, string path)
    {
        if (transform == null || string.IsNullOrEmpty(path)) return transform;
        return transform.Find(path);
    }

    /// <summary>
    /// 获取所有直接子节点。
    /// </summary>
    public static Transform[] GetChildrenArray(this Transform transform)
    {
        if (transform == null) return new Transform[0];

        Transform[] children = new Transform[transform.childCount];
        for (int i = 0; i < transform.childCount; i++)
        {
            children[i] = transform.GetChild(i);
        }
        return children;
    }

    #endregion

    #region 距离与方向

    /// <summary>
    /// 计算到目标 Transform 的世界空间距离。
    /// </summary>
    public static float DistanceTo(this Transform from, Transform to)
    {
        if (from == null || to == null) return 0f;
        return Vector3.Distance(from.position, to.position);
    }

    /// <summary>
    /// 计算到目标位置的世界空间距离。
    /// </summary>
    public static float DistanceTo(this Transform from, Vector3 to)
    {
        if (from == null) return 0f;
        return Vector3.Distance(from.position, to);
    }

    /// <summary>
    /// 计算到目标的世界空间距离的平方（性能更优，适合比较排序）。
    /// </summary>
    public static float SqrDistanceTo(this Transform from, Vector3 to)
    {
        if (from == null) return 0f;
        return (from.position - to).sqrMagnitude;
    }

    /// <summary>
    /// 获取从当前 Transform 到目标的方向向量。
    /// </summary>
    public static Vector3 DirectionTo(this Transform from, Transform to)
    {
        if (from == null || to == null) return Vector3.forward;
        return (to.position - from.position).normalized;
    }

    /// <summary>
    /// 获取从当前 Transform 到目标位置的方向向量。
    /// </summary>
    public static Vector3 DirectionTo(this Transform from, Vector3 to)
    {
        if (from == null) return Vector3.forward;
        Vector3 dir = to - from.position;
        return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
    }

    /// <summary>
    /// 使 Transform 正方向指向目标位置（Y轴向上）。
    /// </summary>
    public static void LookAtSafe(this Transform transform, Vector3 target, Vector3 worldUp)
    {
        if (transform == null) return;
        Vector3 dir = target - transform.position;
        if (dir.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(dir, worldUp);
        }
    }

    /// <summary>
    /// 使 Transform 正方向指向目标位置（Y轴向上）。
    /// </summary>
    public static void FaceTarget(this Transform transform, Vector3 target)
    {
        if (transform == null) return;
        Vector3 dir = target - transform.position;
        if (dir.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }
    }

    /// <summary>
    /// 2D 朝向：使 Transform 的右方向（X轴）指向目标位置。
    /// </summary>
    public static void LookAt2D(this Transform transform, Vector3 target, float angleOffset = -90f)
    {
        if (transform == null) return;
        Vector3 dir = target - transform.position;
        if (dir.sqrMagnitude < 0.0001f) return;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + angleOffset;
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    /// <summary>
    /// 2D 朝向：平滑旋转到目标方向。
    /// </summary>
    public static void LookAt2DSmooth(this Transform transform, Vector3 target, float rotationSpeed, float angleOffset = -90f)
    {
        if (transform == null) return;
        Vector3 dir = target - transform.position;
        if (dir.sqrMagnitude < 0.0001f) return;

        float targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + angleOffset;
        float currentAngle = transform.eulerAngles.z;
        float angle = Mathf.LerpAngle(currentAngle, targetAngle, rotationSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    #endregion

    #region 坐标系转换

    /// <summary>
    /// 将世界坐标转换为相对于指定父节点的局部坐标。
    /// </summary>
    public static Vector3 WorldToLocal(this Transform parent, Vector3 worldPosition)
    {
        if (parent == null) return worldPosition;
        return parent.InverseTransformPoint(worldPosition);
    }

    /// <summary>
    /// 将局部坐标转换为世界坐标。
    /// </summary>
    public static Vector3 LocalToWorld(this Transform parent, Vector3 localPosition)
    {
        if (parent == null) return localPosition;
        return parent.TransformPoint(localPosition);
    }

    #endregion
}