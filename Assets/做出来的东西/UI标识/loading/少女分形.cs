using UnityEngine;
using UnityEngine.UI;

public class 少女分形 : MonoBehaviour
{
    public GameObject 少女祈祷中;
    public GameObject 月亮1;
    public GameObject 月亮2;

    private float 计时器;
    private float 透明度;
    private bool 透明;
    private Graphic 少女祈祷中Graphic;

    private void Start()
    {
        计时器 = 0;
        透明度 = 1.0f;
        透明 = false;

        if (少女祈祷中 != null)
        {
            // ★修复：UI 模式下用 Graphic 代替 Renderer
            // Image / Text / RawImage 都继承自 Graphic
            // Graphic.color 的 a 分量直接控制 UI 元素的透明度
            少女祈祷中Graphic = 少女祈祷中.GetComponent<Graphic>();
        }
    }

    private void Update()
    {
        计时器 += Time.unscaledDeltaTime;

        // ===== 呼吸效果（透明度来回变化）=====
        if (透明)
            透明度 += 1.0f * Time.unscaledDeltaTime;
        else
            透明度 -= 1.0f * Time.unscaledDeltaTime;

        if (透明度 < 40f / 255f)
        {
            透明 = true;
            透明度 = 40f / 255f;
        }
        else if (透明度 > 1.0f)
        {
            透明 = false;
            透明度 = 1.0f;
        }

        // ★修复：UI 模式用 Graphic.color 控制透明度
        if (少女祈祷中Graphic != null)
        {
            Color color = 少女祈祷中Graphic.color;
            color.a = 透明度;
            少女祈祷中Graphic.color = color;
        }

        // ===== 月亮下落（保留）=====
        if (计时器 >= 1.8f)
        {
            if (月亮1 != null)
                月亮1.transform.position += Vector3.down * 5 * Time.unscaledDeltaTime;
            if (月亮2 != null)
                月亮2.transform.position += Vector3.down * 5 * Time.unscaledDeltaTime;
        }

        // ★已删除：少女祈祷中 → 位置3 的移动
        // ★已删除：Loading → 位置4 的移动
    }
}
