using UnityEngine;

public class MaterialUVOffsetMover : MonoBehaviour
{
    public Material material;  // 需要操作的材质球
    public Vector2 moveSpeed = new Vector2(0.1f, 0f);  // 控制偏移速度，x 和 y 的偏移速度
    private Vector2 currentOffset = Vector2.zero;  // 当前的偏移量

    void Start()
    {
        // 如果没有指定材质，尝试从物体的 Renderer 获取材质
        if (material == null)
        {
            Renderer renderer = GetComponent<Renderer>();
            if (renderer != null)
            {
                material = renderer.material;
            }
        }
    }

    void Update()
    {
        // 更新当前偏移量，依据时间进行位移
        currentOffset.x += moveSpeed.x * Time.deltaTime;
        currentOffset.y += moveSpeed.y * Time.deltaTime;

        // 保证偏移量在 0 到 1 之间循环
        currentOffset.x = Mathf.Repeat(currentOffset.x, 1f);  // X 轴偏移在 0 到 1 之间循环
        currentOffset.y = Mathf.Repeat(currentOffset.y, 1f);  // Y 轴偏移在 0 到 1 之间循环

        // 将偏移量应用到材质的 mainTextureOffset 属性
        if (material != null)
        {
            material.mainTextureOffset = currentOffset;
        }
    }
}
