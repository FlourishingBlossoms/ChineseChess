using UnityEngine;

public class 一边旋转一边下落 : MonoBehaviour
{
    private float speed = 1.5f;
    public bool 左右;

    [Tooltip("自动销毁时间（秒）。UI模式下 OnBecameInvisible 不生效，用计时器兜底")]
    public float 生命周期 = 5f;

    private float 计时器;

    private void Update()
    {
        计时器 += Time.unscaledDeltaTime;

        if (左右)
        {
            transform.Rotate(0, 0, speed * 360 * Time.unscaledDeltaTime);
        }
        else
        {
            transform.Rotate(0, 0, -speed * 360 * Time.unscaledDeltaTime);
        }

        // ★修复：UI 模式下 OnBecameInvisible 不会被调用（Image 没有 Renderer）
        // 用计时器兜底，到期自动销毁
        if (计时器 >= 生命周期)
        {
            Destroy(gameObject);
        }
    }

    private void OnBecameInvisible()
    {
        // 3D 模式下仍然生效（有 Renderer 时由 Unity 自动调用）
        // UI 模式下不会调用此方法，靠上面的计时器销毁
        Destroy(gameObject);
    }
}
