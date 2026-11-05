using System;
using UnityEngine;

/// <summary>
/// 通用 GameObject 工具类。
/// 提供组件获取/添加、层级设置、激活状态、查找创建等常用操作。
/// </summary>
/// <example>组件操作
/// <code>
/// // 获取或添加组件
/// Rigidbody rb = go.GetOrAddComponent<Rigidbody>();
///
/// // 获取自身或父节点的组件
/// Canvas canvas = go.GetComponentInParent<Canvas>();
///
/// // 安全获取组件
/// Button btn = go.TryGetComponent<Button>();
/// </code>
/// </example>
/// <example>层级操作
/// <code>
/// go.SetLayerRecursive(LayerMask.NameToLayer("UI"));
/// go.SetLayerToAllChildren(LayerMask.NameToLayer("Default"));
/// </code>
/// </example>
/// <example>激活状态
/// <code>
/// go.SetActive(true);
/// go.ToggleActive();
/// go.SetActiveAllChildren(false);
/// </code>
/// </example>
/// <example>查找与创建
/// <code>
/// GameObject child = go.FindChild("Panel");
/// GameObject newGo = go.FindOrCreateChild("Content/Item");
/// GameObject newEmpty = Utility_GameObject.CreateGameObject("Empty", parent);
/// </code>
/// </example>
public static class Utility_GameObject
{
    #region 组件操作

    /// <summary>
    /// 获取组件，如果不存在则添加。
    /// </summary>
    public static T GetOrAddComponent<T>(this GameObject go) where T : Component
    {
        if (go == null) return null;
        T component = go.GetComponent<T>();
        return component != null ? component : go.AddComponent<T>();
    }

    /// <summary>
    /// 获取组件，如果不存在则添加（Transform 版本）。
    /// </summary>
    public static T GetOrAddComponent<T>(this Component component) where T : Component
    {
        if (component == null) return null;
        return component.gameObject.GetOrAddComponent<T>();
    }

    /// <summary>
    /// 安全地获取组件，不存在返回 null。
    /// </summary>
    public static T TryGetComponent<T>(this GameObject go) where T : Component
    {
        if (go == null) return null;
        return go.GetComponent<T>();
    }

    /// <summary>
    /// 安全地获取组件，不存在返回 null（Transform 版本）。
    /// </summary>
    public static T TryGetComponent<T>(this Component component) where T : Component
    {
        if (component == null) return null;
        return component.GetComponent<T>();
    }

    /// <summary>
    /// 从自身及所有父节点中查找组件。
    /// </summary>
    public static T GetComponentInParent<T>(this GameObject go, bool includeInactive = false) where T : Component
    {
        if (go == null) return null;
        return go.GetComponentInParent<T>(includeInactive);
    }

    /// <summary>
    /// 从自身及所有子节点中查找组件。
    /// </summary>
    public static T GetComponentInChildren<T>(this GameObject go, bool includeInactive = false) where T : Component
    {
        if (go == null) return null;
        return go.GetComponentInChildren<T>(includeInactive);
    }

    /// <summary>
    /// 从自身及所有子节点中查找所有组件。
    /// </summary>
    public static T[] GetComponentsInChildren<T>(this GameObject go, bool includeInactive = false) where T : Component
    {
        if (go == null) return Array.Empty<T>();
        return go.GetComponentsInChildren<T>(includeInactive);
    }

    /// <summary>
    /// 尝试从自身及父节点获取组件。
    /// </summary>
    public static T TryGetComponentInParent<T>(this GameObject go, bool includeInactive = false) where T : Component
    {
        if (go == null) return null;
        return go.GetComponentInParent<T>(includeInactive);
    }

    /// <summary>
    /// 确保目标并不存在指定类型的组件，若已存在则销毁后再添加。
    /// </summary>
    public static T ReplaceComponent<T>(this GameObject go) where T : Component
    {
        if (go == null) return null;

        T existing = go.GetComponent<T>();
        if (existing != null)
        {
            UnityEngine.Object.Destroy(existing);
        }

        return go.AddComponent<T>();
    }

    #endregion

    #region 层级操作

    /// <summary>
    /// 设置自身及所有子节点的 Layer。
    /// </summary>
    public static void SetLayerRecursive(this GameObject go, int layer)
    {
        if (go == null) return;
        go.layer = layer;
        for (int i = 0; i < go.transform.childCount; i++)
        {
            go.transform.GetChild(i).gameObject.SetLayerRecursive(layer);
        }
    }

    /// <summary>
    /// 仅设置所有子节点的 Layer（不含自身）。
    /// </summary>
    public static void SetLayerToAllChildren(this GameObject go, int layer)
    {
        if (go == null) return;
        for (int i = 0; i < go.transform.childCount; i++)
        {
            go.transform.GetChild(i).gameObject.SetLayerRecursive(layer);
        }
    }

    /// <summary>
    /// 设置所有子节点的 Tag。
    /// </summary>
    public static void SetTagToAllChildren(this GameObject go, string tag)
    {
        if (go == null) return;
        for (int i = 0; i < go.transform.childCount; i++)
        {
            go.transform.GetChild(i).gameObject.tag = tag;
            go.transform.GetChild(i).gameObject.SetTagToAllChildren(tag);
        }
    }

    #endregion

    #region 激活状态

    /// <summary>
    /// 切换激活状态。
    /// </summary>
    public static void ToggleActive(this GameObject go)
    {
        if (go == null) return;
        go.SetActive(!go.activeSelf);
    }

    /// <summary>
    /// 设置所有直接子节点的激活状态。
    /// </summary>
    public static void SetActiveAllChildren(this GameObject go, bool active)
    {
        if (go == null) return;
        for (int i = 0; i < go.transform.childCount; i++)
        {
            go.transform.GetChild(i).gameObject.SetActive(active);
        }
    }

    /// <summary>
    /// 递归设置所有子节点的激活状态。
    /// </summary>
    public static void SetActiveRecursive(this GameObject go, bool active)
    {
        if (go == null) return;
        go.SetActive(active);
        for (int i = 0; i < go.transform.childCount; i++)
        {
            go.transform.GetChild(i).gameObject.SetActiveRecursive(active);
        }
    }

    #endregion

    #region 查找与创建

    /// <summary>
    /// 查找直接子节点（非递归）。
    /// </summary>
    public static GameObject FindChild(this GameObject go, string name)
    {
        if (go == null || string.IsNullOrEmpty(name)) return null;
        Transform child = go.transform.Find(name);
        return child != null ? child.gameObject : null;
    }

    /// <summary>
    /// 递归查找子节点。
    /// </summary>
    public static GameObject FindChildRecursive(this GameObject go, string name)
    {
        if (go == null) return null;
        Transform result = go.transform.FindRecursive(name);
        return result != null ? result.gameObject : null;
    }

    /// <summary>
    /// 按路径查找或创建子节点。
    /// 路径格式：如 "Panel/Content/Item"，自动创建不存在的中间节点。
    /// </summary>
    public static GameObject FindOrCreateChild(this GameObject go, string path)
    {
        if (go == null || string.IsNullOrEmpty(path)) return go;

        string[] parts = path.Split('/');
        Transform current = go.transform;

        foreach (string part in parts)
        {
            if (string.IsNullOrEmpty(part)) continue;

            Transform child = current.Find(part);
            if (child == null)
            {
                GameObject childGo = new GameObject(part);
                childGo.transform.SetParent(current, false);
                child = childGo.transform;
            }
            current = child;
        }

        return current.gameObject;
    }

    /// <summary>
    /// 查找或创建指定类型的子节点组件。
    /// 按路径查找子节点，若不存在则创建并添加该组件。
    /// </summary>
    public static T FindOrCreateChild<T>(this GameObject go, string path) where T : Component
    {
        if (go == null) return null;

        GameObject child = go.FindChild(path);
        if (child == null)
        {
            child = go.FindOrCreateChild(path);
        }

        return child.GetOrAddComponent<T>();
    }

    /// <summary>
    /// 检测 GameObject 是否为某父节点的子节点或自身。
    /// </summary>
    public static bool IsChildOf(this GameObject child, GameObject parent)
    {
        if (child == null || parent == null) return false;
        if (child == parent) return true;

        Transform current = child.transform.parent;
        while (current != null)
        {
            if (current.gameObject == parent) return true;
            current = current.parent;
        }
        return false;
    }

    #endregion

    #region 创建与销毁

    /// <summary>
    /// 创建一个新的 GameObject。
    /// </summary>
    public static GameObject CreateGameObject(string name, Transform parent = null)
    {
        GameObject go = new GameObject(name);
        if (parent != null)
        {
            go.transform.SetParent(parent, false);
        }
        return go;
    }

    /// <summary>
    /// 创建带指定组件的 GameObject。
    /// </summary>
    public static GameObject CreateGameObject<T>(string name, Transform parent = null) where T : Component
    {
        GameObject go = CreateGameObject(name, parent);
        go.AddComponent<T>();
        return go;
    }

    /// <summary>
    /// 安全销毁（检查 null 后再销毁）。
    /// </summary>
    public static void DestroySafe(GameObject go)
    {
        if (go != null)
            UnityEngine.Object.Destroy(go);
    }

    /// <summary>
    /// 安全立即销毁（检查 null 后再销毁，谨慎使用）。
    /// </summary>
    public static void DestroySafeImmediate(GameObject go)
    {
        if (go != null)
            UnityEngine.Object.DestroyImmediate(go);
    }

    #endregion
}