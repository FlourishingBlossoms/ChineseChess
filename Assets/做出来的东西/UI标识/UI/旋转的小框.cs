using UnityEngine;
using UnityEngine.UI;

public class 平滑周期摆动 : MonoBehaviour
{
    private Image image;
    private float r, g, b;
    private bool stage1, stage2, stage3, stage4, stage5, stage6;
    public float 摆动幅度 = 15f;

    public float 摆动速度 = 2f;


    public float 基础角度 = 0f;

    private void Start()
    {
        r = 255; g = 92; b = 100;
        stage1 = false;
        stage2 = false;
        stage3 = false;
        stage4 = false;
        stage5 = false;
        stage6 = false;
        image = GetComponent<Image>();
    }

    private void Update()
    {
        // 1. 计算当前的正弦波值 (-1 到 1 之间循环变化)
        // Time.time * 摆动速度：随着时间不断增加，控制波动的快慢
        float 正弦波 = Mathf.Sin(Time.time * 摆动速度);

        // 2. 计算当前的旋转角度
        // 基础角度 + (正弦波 * 幅度)
        // 结果会一直在 (基础角度 - 15) 到 (基础角度 + 15) 之间平滑变化
        float 当前Z角 = 基础角度 + 正弦波 * 摆动幅度;

        // 3. 应用旋转
        // 这里的 Vector3(0, 0, ...) 表示只绕 Z 轴旋转（2D游戏常用）
        // 如果是 3D 物体要左右摇头，可以改成 Vector3(0, 当前Z角, 0)
        transform.rotation = Quaternion.Euler(0, 0, 当前Z角);
        // --- 变色逻辑 (彻底修复版) ---
        image.color = new Color(r / 255f, g / 255f, b / 255f, 150 / 255f);

        // Stage 1: G 上升 (92 -> 255)
        // 关键点：增加了 !stage1 判断。一旦 stage1 变成 true，就再也不进来了，不会卡住。
        if (!stage1 && g <= 255)
        {
            if (g >= 255)
            {
                stage1 = true;
            }
            else
            {
                g++;
            }
        }
        // Stage 2: R 下降 (255 -> 92)
        // 关键点：增加了 !stage2 判断。
        else if (stage1 && !stage2 && r >= 92)
        {
            if (r <= 92)
            {
                stage2 = true;
            }
            else
            {
                r--;
            }
        }
        // Stage 3: B 上升 (100 -> 255)
        else if (stage1 && stage2 && !stage3 && b <= 255)
        {
            if (b >= 255)
            {
                stage3 = true;
            }
            else
            {
                b++;
            }
        }
        // Stage 4: G 下降 (255 -> 92)
        else if (stage1 && stage2 && stage3 && !stage4 && g >= 92)
        {
            if (g <= 92)
            {
                stage4 = true;
            }
            else
            {
                g--;
            }
        }
        // Stage 5: R 上升 (92 -> 255)
        else if (stage1 && stage2 && stage3 && stage4 && !stage5 && r <= 255)
        {
            if (r >= 255)
            {
                stage5 = true;
            }
            else
            {
                r++;
            }
        }
        // Stage 6: B 下降 (255 -> 92) -> 循环重置
        else if (stage1 && stage2 && stage3 && stage4 && stage5 && b >= 92)
        {
            if (b <= 92)
            {
                // 重置所有状态，开始下一轮循环
                stage1 = false;
                stage2 = false;
                stage3 = false;
                stage4 = false;
                stage5 = false;
            }
            else
            {
                b--;
            }
        }
    }
}