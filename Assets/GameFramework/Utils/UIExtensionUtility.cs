using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 通用UI扩展工具类。
/// 提供 RectTransform、CanvasGroup、Image、Button、ScrollRect 等常用UI组件的扩展方法，简化UI操作。
/// </summary>
/// <example>RectTransform 锚点与布局
/// <code>
/// RectTransform rt = panel.GetComponent<RectTransform>();
///
/// // 填充父节点
/// rt.SetAnchor(AnchorPreset.StretchAll);
/// rt.SetOffset(0, 0, 0, 0); // 等价于 rt.FillParent();
///
/// // 设置到父节点左上角
/// rt.SetAnchor(AnchorPreset.TopLeft);
/// rt.SetPivot(0, 1);
///
/// // 设置宽度
/// rt.SetWidth(200);
/// rt.SetHeight(100);
/// rt.SetSize(300, 150);
/// </code>
/// </example>
/// <example>CanvasGroup 淡入淡出
/// <code>
/// CanvasGroup cg = panel.GetComponent<CanvasGroup>();
///
/// // 显示/隐藏
/// cg.Show();
/// cg.Hide();
///
/// // 设置透明度
/// cg.SetAlpha(0.5f);
///
/// // 淡入淡出协程
/// StartCoroutine(cg.FadeIn(0.5f));
/// StartCoroutine(cg.FadeOut(0.3f));
/// </code>
/// </example>
/// <example>Image 操作
/// <code>
/// Image img = GetComponent<Image>();
/// img.SetAlpha(0.5f);
/// img.SetColor(new Color(1, 0, 0, 1));
/// img.SetSprite(someSprite);
/// img.SetNativeSize();
/// </code>
/// </example>
/// <example>Button 操作
/// <code>
/// Button btn = GetComponent<Button>();
/// btn.SetInteractable(true);
/// btn.SetClickListener(() => Debug.Log("Clicked!"));
/// btn.RemoveAllListeners();
/// </code>
/// </example>
/// <example>ScrollRect 操作
/// <code>
/// ScrollRect scroll = GetComponent<ScrollRect>();
/// scroll.ScrollToTop();
/// scroll.ScrollToBottom();
/// scroll.SetNormalizedPosition(0.5f, 0f);
/// </code>
/// </example>
/// <example>Transform 查找子节点
/// <code>
/// Transform child = transform.FindRecursive("TargetName");
/// Transform[] allChildren = transform.GetAllChildren();
/// Transform go = transform.FindOrCreate("Panel/Content/Item");
/// </code>
/// </example>
public static class UIExtensionUtility
{
    #region RectTransform 扩展

    /// <summary>
    /// 设置 RectTransform 的宽度。
    /// </summary>
    public static void SetWidth(this RectTransform rectTransform, float width)
    {
        if (rectTransform == null) return;
        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
    }

    /// <summary>
    /// 设置 RectTransform 的高度。
    /// </summary>
    public static void SetHeight(this RectTransform rectTransform, float height)
    {
        if (rectTransform == null) return;
        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
    }

    /// <summary>
    /// 设置 RectTransform 的尺寸（宽、高）。
    /// </summary>
    public static void SetSize(this RectTransform rectTransform, float width, float height)
    {
        if (rectTransform == null) return;
        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
    }

    /// <summary>
    /// 获取 RectTransform 的宽度。
    /// </summary>
    public static float GetWidth(this RectTransform rectTransform)
    {
        if (rectTransform == null) return 0f;
        return rectTransform.rect.width;
    }

    /// <summary>
    /// 获取 RectTransform 的高度。
    /// </summary>
    public static float GetHeight(this RectTransform rectTransform)
    {
        if (rectTransform == null) return 0f;
        return rectTransform.rect.height;
    }

    /// <summary>
    /// 设置左边距（offsetMin.x）。
    /// </summary>
    public static void SetLeft(this RectTransform rectTransform, float left)
    {
        if (rectTransform == null) return;
        rectTransform.offsetMin = new Vector2(left, rectTransform.offsetMin.y);
    }

    /// <summary>
    /// 设置右边距（offsetMax.x）。
    /// </summary>
    public static void SetRight(this RectTransform rectTransform, float right)
    {
        if (rectTransform == null) return;
        rectTransform.offsetMax = new Vector2(-right, rectTransform.offsetMax.y);
    }

    /// <summary>
    /// 设置上边距（offsetMax.y）。
    /// </summary>
    public static void SetTop(this RectTransform rectTransform, float top)
    {
        if (rectTransform == null) return;
        rectTransform.offsetMax = new Vector2(rectTransform.offsetMax.x, -top);
    }

    /// <summary>
    /// 设置下边距（offsetMin.y）。
    /// </summary>
    public static void SetBottom(this RectTransform rectTransform, float bottom)
    {
        if (rectTransform == null) return;
        rectTransform.offsetMin = new Vector2(rectTransform.offsetMin.x, bottom);
    }

    /// <summary>
    /// 设置四边偏移（left, right, top, bottom），正值表示向内收缩。
    /// </summary>
    public static void SetOffset(this RectTransform rectTransform, float left, float right, float top, float bottom)
    {
        if (rectTransform == null) return;
        rectTransform.offsetMin = new Vector2(left, bottom);
        rectTransform.offsetMax = new Vector2(-right, -top);
    }

    /// <summary>
    /// 填充父节点：设置锚点为拉伸并设置四边偏移为0。
    /// </summary>
    public static void FillParent(this RectTransform rectTransform)
    {
        if (rectTransform == null) return;
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// 设置锚点预设。
    /// </summary>
    public static void SetAnchor(this RectTransform rectTransform, AnchorPreset preset)
    {
        if (rectTransform == null) return;
        rectTransform.SetAnchor(GetAnchorMin(preset), GetAnchorMax(preset));
    }

    /// <summary>
    /// 设置锚点范围。
    /// </summary>
    public static void SetAnchor(this RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax)
    {
        if (rectTransform == null) return;
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
    }

    /// <summary>
    /// 设置轴心点（Pivot）。
    /// </summary>
    public static void SetPivot(this RectTransform rectTransform, float x, float y)
    {
        if (rectTransform == null) return;
        rectTransform.pivot = new Vector2(x, y);
    }

    /// <summary>
    /// 设置UI位置（anchoredPosition）。
    /// </summary>
    public static void SetPosition(this RectTransform rectTransform, float x, float y)
    {
        if (rectTransform == null) return;
        rectTransform.anchoredPosition = new Vector2(x, y);
    }

    /// <summary>
    /// 设置UI位置的X分量。
    /// </summary>
    public static void SetPositionX(this RectTransform rectTransform, float x)
    {
        if (rectTransform == null) return;
        rectTransform.anchoredPosition = new Vector2(x, rectTransform.anchoredPosition.y);
    }

    /// <summary>
    /// 设置UI位置的Y分量。
    /// </summary>
    public static void SetPositionY(this RectTransform rectTransform, float y)
    {
        if (rectTransform == null) return;
        rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, y);
    }

    /// <summary>
    /// 设置世界坐标位置。
    /// </summary>
    public static void SetWorldPosition(this RectTransform rectTransform, float x, float y, float z)
    {
        if (rectTransform == null) return;
        rectTransform.position = new Vector3(x, y, z);
    }

    /// <summary>
    /// 设置局部缩放。
    /// </summary>
    public static void SetLocalScale(this RectTransform rectTransform, float x, float y, float z)
    {
        if (rectTransform == null) return;
        rectTransform.localScale = new Vector3(x, y, z);
    }

    /// <summary>
    /// 设置局部缩放（统一缩放）。
    /// </summary>
    public static void SetLocalScale(this RectTransform rectTransform, float scale)
    {
        if (rectTransform == null) return;
        rectTransform.localScale = Vector3.one * scale;
    }

    /// <summary>
    /// 复制另一个 RectTransform 的锚点、偏移、轴心和尺寸设置。
    /// </summary>
    public static void CopyFrom(this RectTransform rectTransform, RectTransform source)
    {
        if (rectTransform == null || source == null) return;
        rectTransform.anchorMin = source.anchorMin;
        rectTransform.anchorMax = source.anchorMax;
        rectTransform.offsetMin = source.offsetMin;
        rectTransform.offsetMax = source.offsetMax;
        rectTransform.pivot = source.pivot;
        rectTransform.anchoredPosition = source.anchoredPosition;
        rectTransform.sizeDelta = source.sizeDelta;
    }

    /// <summary>
    /// 获取 RectTransform 在世界空间中的矩形区域。
    /// </summary>
    public static Rect GetWorldRect(this RectTransform rectTransform)
    {
        if (rectTransform == null) return Rect.zero;

        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);

        Vector2 min = corners[0];
        Vector2 max = corners[2];
        return new Rect(min, max - min);
    }

    #endregion

    #region CanvasGroup 扩展

    /// <summary>
    /// 显示 CanvasGroup（设置 alpha=1, interactable=true, blocksRaycasts=true）。
    /// </summary>
    public static void Show(this CanvasGroup canvasGroup)
    {
        if (canvasGroup == null) return;
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }

    /// <summary>
    /// 隐藏 CanvasGroup（设置 alpha=0, interactable=false, blocksRaycasts=false）。
    /// </summary>
    public static void Hide(this CanvasGroup canvasGroup)
    {
        if (canvasGroup == null) return;
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    /// <summary>
    /// 设置透明度。
    /// </summary>
    public static void SetAlpha(this CanvasGroup canvasGroup, float alpha)
    {
        if (canvasGroup == null) return;
        canvasGroup.alpha = Mathf.Clamp01(alpha);
    }

    /// <summary>
    /// 淡入协程。
    /// </summary>
    /// <param name="canvasGroup">CanvasGroup 组件</param>
    /// <param name="duration">持续时间（秒）</param>
    /// <param name="onComplete">完成回调</param>
    public static IEnumerator FadeIn(this CanvasGroup canvasGroup, float duration, Action onComplete = null)
    {
        if (canvasGroup == null) yield break;

        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        float startAlpha = canvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, elapsed / duration);
            yield return null;
        }

        canvasGroup.alpha = 1f;
        onComplete?.Invoke();
    }

    /// <summary>
    /// 淡出协程。
    /// </summary>
    /// <param name="canvasGroup">CanvasGroup 组件</param>
    /// <param name="duration">持续时间（秒）</param>
    /// <param name="onComplete">完成回调</param>
    public static IEnumerator FadeOut(this CanvasGroup canvasGroup, float duration, Action onComplete = null)
    {
        if (canvasGroup == null) yield break;

        float startAlpha = canvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / duration);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        onComplete?.Invoke();
    }

    #endregion

    #region Image / RawImage 扩展

    /// <summary>
    /// 设置 Image 的透明度。
    /// </summary>
    public static void SetAlpha(this Image image, float alpha)
    {
        if (image == null) return;
        Color color = image.color;
        color.a = Mathf.Clamp01(alpha);
        image.color = color;
    }

    /// <summary>
    /// 设置 RawImage 的透明度。
    /// </summary>
    public static void SetAlpha(this RawImage rawImage, float alpha)
    {
        if (rawImage == null) return;
        Color color = rawImage.color;
        color.a = Mathf.Clamp01(alpha);
        rawImage.color = color;
    }

    /// <summary>
    /// 设置 Image 的颜色（保留原透明度）。
    /// </summary>
    public static void SetColor(this Image image, Color color)
    {
        if (image == null) return;
        color.a = image.color.a;
        image.color = color;
    }

    /// <summary>
    /// 设置 Image 的颜色（含透明度）。
    /// </summary>
    public static void SetColorWithAlpha(this Image image, Color color)
    {
        if (image == null) return;
        image.color = color;
    }

    /// <summary>
    /// 设置 RawImage 的颜色（保留原透明度）。
    /// </summary>
    public static void SetColor(this RawImage rawImage, Color color)
    {
        if (rawImage == null) return;
        color.a = rawImage.color.a;
        rawImage.color = color;
    }

    /// <summary>
    /// 设置 RawImage 的颜色（含透明度）。
    /// </summary>
    public static void SetColorWithAlpha(this RawImage rawImage, Color color)
    {
        if (rawImage == null) return;
        rawImage.color = color;
    }

    /// <summary>
    /// 设置 Image 的 Sprite 并可选调用 SetNativeSize。
    /// </summary>
    public static void SetSprite(this Image image, Sprite sprite, bool setNativeSize = false)
    {
        if (image == null) return;
        image.sprite = sprite;
        if (setNativeSize)
        {
            image.SetNativeSize();
        }
    }

    /// <summary>
    /// 设置 RawImage 的 Texture。
    /// </summary>
    public static void SetTexture(this RawImage rawImage, Texture texture)
    {
        if (rawImage == null) return;
        rawImage.texture = texture;
    }

    #endregion

    #region Button 扩展

    /// <summary>
    /// 设置按钮是否可交互。
    /// </summary>
    public static void SetInteractable(this Button button, bool interactable)
    {
        if (button == null) return;
        button.interactable = interactable;
    }

    /// <summary>
    /// 为按钮添加点击监听（会清除已有监听）。
    /// </summary>
    public static void SetClickListener(this Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    /// <summary>
    /// 为按钮追加点击监听（保留已有监听）。
    /// </summary>
    public static void AddClickListener(this Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;
        button.onClick.AddListener(action);
    }

    /// <summary>
    /// 清除按钮的所有点击监听。
    /// </summary>
    public static void RemoveAllListeners(this Button button)
    {
        if (button == null) return;
        button.onClick.RemoveAllListeners();
    }

    /// <summary>
    /// 通过代码触发按钮点击。
    /// </summary>
    public static void SimulateClick(this Button button)
    {
        if (button == null || !button.interactable) return;
        button.onClick.Invoke();
    }

    #endregion

    #region Toggle 扩展

    /// <summary>
    /// 设置 Toggle 的开关状态（不触发回调）。
    /// </summary>
    public static void SetValueSilent(this Toggle toggle, bool value)
    {
        if (toggle == null) return;
        toggle.SetIsOnWithoutNotify(value);
    }

    /// <summary>
    /// 设置 Toggle 的开关状态（触发回调）。
    /// </summary>
    public static void SetValue(this Toggle toggle, bool value)
    {
        if (toggle == null) return;
        toggle.isOn = value;
    }

    /// <summary>
    /// 为 Toggle 添加值变化监听（会清除已有监听）。
    /// </summary>
    public static void SetValueChangedListener(this Toggle toggle, UnityEngine.Events.UnityAction<bool> action)
    {
        if (toggle == null) return;
        toggle.onValueChanged.RemoveAllListeners();
        toggle.onValueChanged.AddListener(action);
    }

    #endregion

    #region Slider 扩展

    /// <summary>
    /// 设置 Slider 的值（不触发回调）。
    /// </summary>
    public static void SetValueSilent(this Slider slider, float value)
    {
        if (slider == null) return;
        slider.SetValueWithoutNotify(value);
    }

    /// <summary>
    /// 设置 Slider 的值（触发回调）。
    /// </summary>
    public static void SetValue(this Slider slider, float value)
    {
        if (slider == null) return;
        slider.value = Mathf.Clamp(value, slider.minValue, slider.maxValue);
    }

    /// <summary>
    /// 获取 Slider 的归一化值（0-1）。
    /// </summary>
    public static float GetNormalizedValue(this Slider slider)
    {
        if (slider == null) return 0f;
        return Mathf.InverseLerp(slider.minValue, slider.maxValue, slider.value);
    }

    #endregion

    #region ScrollRect 扩展

    /// <summary>
    /// 设置滚动视图的归一化位置。
    /// </summary>
    public static void SetNormalizedPosition(this ScrollRect scrollRect, float horizontal, float vertical)
    {
        if (scrollRect == null) return;
        scrollRect.normalizedPosition = new Vector2(
            Mathf.Clamp01(horizontal),
            Mathf.Clamp01(vertical)
        );
    }

    /// <summary>
    /// 滚动到顶部。
    /// </summary>
    public static void ScrollToTop(this ScrollRect scrollRect)
    {
        if (scrollRect == null) return;
        scrollRect.verticalNormalizedPosition = 1f;
    }

    /// <summary>
    /// 滚动到底部。
    /// </summary>
    public static void ScrollToBottom(this ScrollRect scrollRect)
    {
        if (scrollRect == null) return;
        scrollRect.verticalNormalizedPosition = 0f;
    }

    /// <summary>
    /// 滚动到最左边。
    /// </summary>
    public static void ScrollToLeft(this ScrollRect scrollRect)
    {
        if (scrollRect == null) return;
        scrollRect.horizontalNormalizedPosition = 0f;
    }

    /// <summary>
    /// 滚动到最右边。
    /// </summary>
    public static void ScrollToRight(this ScrollRect scrollRect)
    {
        if (scrollRect == null) return;
        scrollRect.horizontalNormalizedPosition = 1f;
    }

    /// <summary>
    /// 启用/禁用滚动。
    /// </summary>
    public static void SetScrollEnabled(this ScrollRect scrollRect, bool horizontal, bool vertical)
    {
        if (scrollRect == null) return;
        scrollRect.horizontal = horizontal;
        scrollRect.vertical = vertical;
    }

    #endregion

    #region InputField 扩展

    /// <summary>
    /// 设置输入框文本（不触发回调）。
    /// </summary>
    public static void SetTextSilent(this InputField inputField, string text)
    {
        if (inputField == null) return;
        inputField.SetTextWithoutNotify(text ?? string.Empty);
    }

    /// <summary>
    /// 设置输入框文本（触发回调）。
    /// </summary>
    public static void SetText(this InputField inputField, string text)
    {
        if (inputField == null) return;
        inputField.text = text ?? string.Empty;
    }

    /// <summary>
    /// 获取输入框文本。
    /// </summary>
    public static string GetText(this InputField inputField)
    {
        if (inputField == null) return string.Empty;
        return inputField.text;
    }

    /// <summary>
    /// 清空输入框。
    /// </summary>
    public static void Clear(this InputField inputField)
    {
        if (inputField == null) return;
        inputField.text = string.Empty;
    }

    #endregion

    #region Text 扩展

    /// <summary>
    /// 设置 Text 内容。
    /// </summary>
    public static void SetText(this Text text, string content)
    {
        if (text == null) return;
        text.text = content ?? string.Empty;
    }

    /// <summary>
    /// 设置 Text 内容（格式化）。
    /// </summary>
    public static void SetTextFormat(this Text text, string format, params object[] args)
    {
        if (text == null) return;
        text.text = string.Format(format, args);
    }

    /// <summary>
    /// 设置 Text 字体大小。
    /// </summary>
    public static void SetFontSize(this Text text, int size)
    {
        if (text == null) return;
        text.fontSize = size;
    }

    /// <summary>
    /// 设置 Text 颜色。
    /// </summary>
    public static void SetColor(this Text text, Color color)
    {
        if (text == null) return;
        text.color = color;
    }

    /// <summary>
    /// 设置 Text 透明度。
    /// </summary>
    public static void SetAlpha(this Text text, float alpha)
    {
        if (text == null) return;
        Color color = text.color;
        color.a = Mathf.Clamp01(alpha);
        text.color = color;
    }

    #endregion

    #region Transform 扩展（UI节点查找）
    /// <summary>
    /// 获取所有直接子节点。
    /// </summary>
    public static Transform[] GetAllChildren(this Transform parent)
    {
        if (parent == null) return Array.Empty<Transform>();

        Transform[] children = new Transform[parent.childCount];
        for (int i = 0; i < parent.childCount; i++)
        {
            children[i] = parent.GetChild(i);
        }
        return children;
    }

    /// <summary>
    /// 按路径查找或创建节点。
    /// 路径格式：如 "Panel/Content/Item"。
    /// </summary>
    /// <param name="parent">父节点</param>
    /// <param name="path">路径（用 / 分隔）</param>
    /// <returns>路径末端节点</returns>
    public static Transform FindOrCreate(this Transform parent, string path)
    {
        if (parent == null || string.IsNullOrEmpty(path)) return parent;

        string[] parts = path.Split('/');
        Transform current = parent;

        foreach (string part in parts)
        {
            if (string.IsNullOrEmpty(part)) continue;

            Transform child = current.Find(part);
            if (child == null)
            {
                GameObject go = new GameObject(part);
                go.transform.SetParent(current, false);
                child = go.transform;
            }
            current = child;
        }

        return current;
    }

    /// <summary>
    /// 销毁所有子节点。
    /// </summary>
    public static void DestroyAllChildren(this Transform parent)
    {
        if (parent == null) return;

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            UnityEngine.Object.Destroy(parent.GetChild(i).gameObject);
        }
    }

    /// <summary>
    /// 立即销毁所有子节点（编辑器模式下也可用）。
    /// </summary>
    public static void DestroyAllChildrenImmediate(this Transform parent)
    {
        if (parent == null) return;

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            UnityEngine.Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }
    }

    /// <summary>
    /// 设置父节点（保持世界坐标不变）。
    /// </summary>
    public static void SetParentWorld(this Transform child, Transform parent)
    {
        if (child == null) return;
        child.SetParent(parent, true);
    }

    /// <summary>
    /// 设置父节点（保持局部坐标不变，用于UI）。
    /// </summary>
    public static void SetParentLocal(this Transform child, Transform parent)
    {
        if (child == null) return;
        child.SetParent(parent, false);
    }

    /// <summary>
    /// 将自身设置为同级的第一个（最上层渲染）。
    /// </summary>
    public static void SetAsFirstSibling(this Transform transform)
    {
        if (transform == null) return;
        transform.SetAsFirstSibling();
    }

    /// <summary>
    /// 将自身设置为同级的最后一个（最下层渲染）。
    /// </summary>
    public static void SetAsLastSibling(this Transform transform)
    {
        if (transform == null) return;
        transform.SetAsLastSibling();
    }

    /// <summary>
    /// 获取或添加指定组件（Transform版本）。
    /// </summary>
    public static T GetOrAddComponent<T>(this Transform transform) where T : Component
    {
        if (transform == null) return null;
        return transform.gameObject.GetOrAddComponent<T>();
    }

    #endregion

    #region Selectable 扩展

    /// <summary>
    /// 启用/禁用 Selectable 组件。
    /// </summary>
    public static void SetEnabled(this Selectable selectable, bool enabled)
    {
        if (selectable == null) return;
        selectable.interactable = enabled;
    }

    #endregion

    #region 布局辅助

    /// <summary>
    /// 强制立即重建布局（LayoutGroup / ContentSizeFitter）。
    /// </summary>
    public static void ForceRebuildLayout(this RectTransform rectTransform)
    {
        if (rectTransform == null) return;
        LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
    }

    /// <summary>
    /// 标记布局需要重建（下一帧生效）。
    /// </summary>
    public static void MarkLayoutForRebuild(this RectTransform rectTransform)
    {
        if (rectTransform == null) return;
        LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
    }

    #endregion

    #region 事件系统辅助

    /// <summary>
    /// 检查当前是否悬停在UI上。
    /// </summary>
    public static bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;

        if (Application.isMobilePlatform)
        {
            return EventSystem.current.IsPointerOverGameObject(0);
        }
        else
        {
            return EventSystem.current.IsPointerOverGameObject();
        }
    }

    /// <summary>
    /// 检查指定触摸点是否在UI上。
    /// </summary>
    public static bool IsPointerOverUI(int fingerId)
    {
        if (EventSystem.current == null) return false;
        return EventSystem.current.IsPointerOverGameObject(fingerId);
    }

    #endregion

    #region 辅助类型

    private static Vector2 GetAnchorMin(AnchorPreset preset)
    {
        switch (preset)
        {
            case AnchorPreset.TopLeft: return new Vector2(0f, 1f);
            case AnchorPreset.TopCenter: return new Vector2(0.5f, 1f);
            case AnchorPreset.TopRight: return new Vector2(1f, 1f);
            case AnchorPreset.MiddleLeft: return new Vector2(0f, 0.5f);
            case AnchorPreset.MiddleCenter: return new Vector2(0.5f, 0.5f);
            case AnchorPreset.MiddleRight: return new Vector2(1f, 0.5f);
            case AnchorPreset.BottomLeft: return new Vector2(0f, 0f);
            case AnchorPreset.BottomCenter: return new Vector2(0.5f, 0f);
            case AnchorPreset.BottomRight: return new Vector2(1f, 0f);
            case AnchorPreset.StretchTop: return new Vector2(0f, 1f);
            case AnchorPreset.StretchMiddle: return new Vector2(0f, 0.5f);
            case AnchorPreset.StretchBottom: return new Vector2(0f, 0f);
            case AnchorPreset.StretchLeft: return new Vector2(0f, 0f);
            case AnchorPreset.StretchCenter: return new Vector2(0.5f, 0f);
            case AnchorPreset.StretchRight: return new Vector2(1f, 0f);
            case AnchorPreset.StretchAll: return Vector2.zero;
            default: return new Vector2(0.5f, 0.5f);
        }
    }

    private static Vector2 GetAnchorMax(AnchorPreset preset)
    {
        switch (preset)
        {
            case AnchorPreset.TopLeft: return new Vector2(0f, 1f);
            case AnchorPreset.TopCenter: return new Vector2(0.5f, 1f);
            case AnchorPreset.TopRight: return new Vector2(1f, 1f);
            case AnchorPreset.MiddleLeft: return new Vector2(0f, 0.5f);
            case AnchorPreset.MiddleCenter: return new Vector2(0.5f, 0.5f);
            case AnchorPreset.MiddleRight: return new Vector2(1f, 0.5f);
            case AnchorPreset.BottomLeft: return new Vector2(0f, 0f);
            case AnchorPreset.BottomCenter: return new Vector2(0.5f, 0f);
            case AnchorPreset.BottomRight: return new Vector2(1f, 0f);
            case AnchorPreset.StretchTop: return new Vector2(1f, 1f);
            case AnchorPreset.StretchMiddle: return new Vector2(1f, 0.5f);
            case AnchorPreset.StretchBottom: return new Vector2(1f, 0f);
            case AnchorPreset.StretchLeft: return new Vector2(0f, 1f);
            case AnchorPreset.StretchCenter: return new Vector2(0.5f, 1f);
            case AnchorPreset.StretchRight: return new Vector2(1f, 1f);
            case AnchorPreset.StretchAll: return Vector2.one;
            default: return new Vector2(0.5f, 0.5f);
        }
    }

    #endregion
}

/// <summary>
/// 锚点预设枚举。
/// </summary>
public enum AnchorPreset
{
    TopLeft,
    TopCenter,
    TopRight,
    MiddleLeft,
    MiddleCenter,
    MiddleRight,
    BottomLeft,
    BottomCenter,
    BottomRight,
    StretchTop,
    StretchMiddle,
    StretchBottom,
    StretchLeft,
    StretchCenter,
    StretchRight,
    StretchAll,
}