/* ════════════════════════════════════════════════════════════════════
 *  CSSLoaderUI.cs  —  用 Unity UI 实现 CSS 加载动画
 * ════════════════════════════════════════════════════════════════════
 *
 *  【实现原理】
 *      CSS 的 @keyframes + animation-delay 在 Unity 里用协程模拟：
 *      - 每根竖条一个协程，用 Mathf.Sin/Cos 做插值
 *      - scaleY 用 Mathf.Abs(Mathf.Cos(πt)) 实现 1→0→1
 *
 *  【两种加载动画】
 *      Loader1:  10根竖条，scaleY 往复，延迟递增 → "音频均衡器"波浪
 *      Loader17: 十字旋转 + 4个彩色点做轨迹运动
 *
 *  【Inspector 配置】
 *      在 Canvas 下创建空物体，挂此脚本：
 *      - enableLoader1 = true → 自动生成10根竖条
 *      - enableLoader17 = true → 自动生成十字旋转
 *      两个可以同时开，也可以只开一个
 *
 *  【用在重连遮罩面板上】
 *      把此脚本挂在 reconnectPanel 的子物体上，
 *      断线时面板显示 → 动画自动播放（OnEnable）
 *      重连成功面板隐藏 → 动画自动停止（OnDisable）
 * ════════════════════════════════════════════════════════════════════ */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CSSLoaderUI : MonoBehaviour
{
    // ════════════════════════════════════════════════════════════════
    //  Loader1 配置
    // ════════════════════════════════════════════════════════════════

    [Header("Loader1 配置（竖条波浪）")]
    public bool enableLoader1 = true;

    [Tooltip("竖条数量，对应CSS的 child1~child10")]
    public int barCount = 10;

    [Tooltip("竖条颜色，对应CSS的 background-color: #FFF")]
    public Color barColor = Color.white;

    [Tooltip("竖条宽（像素），对应CSS的 width: 8px")]
    public float barWidth = 8f;

    [Tooltip("竖条高（像素），对应CSS的 height: 50px")]
    public float barHeight = 50f;

    [Tooltip("竖条间距，对应CSS的 margin-right: 3px")]
    public float barSpacing = 3f;

    [Tooltip("动画周期（秒），对应CSS的 animation: animate1 1s")]
    public float barAnimDuration = 1f;

    [Tooltip("延迟递增（秒），对应CSS的 animation-delay: 0.1s, 0.2s...")]
    public float barDelayStep = 0.1f;

    // ════════════════════════════════════════════════════════════════
    //  Loader17 配置
    // ════════════════════════════════════════════════════════════════

    [Header("Loader17 配置（十字旋转）")]
    public bool enableLoader17 = false;

    [Tooltip("基础尺寸（像素），对应CSS的 1em")]
    public float baseSize = 20f;

    [Tooltip("旋转角度，对应CSS的 transform: rotate(165deg)")]
    public float rotationAngle = 165f;

    [Tooltip("动画周期（秒），对应CSS的 animation 2s")]
    public float loader17Duration = 2f;

    // ════════════════════════════════════════════════════════════════
    //  内部状态
    // ════════════════════════════════════════════════════════════════

    private List<RectTransform> bars = new List<RectTransform>();
    private List<Coroutine> barCoroutines = new List<Coroutine>();
    private Coroutine loader17Coroutine;

    // Loader17 子物体
    private RectTransform loader17Root;
    private RectTransform beforeBar;
    private RectTransform afterBar;
    private List<RectTransform> shadowDots = new List<RectTransform>();

    // ════════════════════════════════════════════════════════════════
    //  Unity 生命周期
    // ════════════════════════════════════════════════════════════════

    void Start()
    {
        if (enableLoader1)
            InitLoader1();

        if (enableLoader17)
            InitLoader17();
    }

    void OnEnable()
    {
        // 面板重新激活时恢复动画
        if (enableLoader1 && bars.Count > 0 && barCoroutines.Count == 0)
            StartLoader1Animations();

        if (enableLoader17 && loader17Root != null && loader17Coroutine == null)
            loader17Coroutine = StartCoroutine(Loader17Animation());
    }

    void OnDisable()
    {
        StopAllLoaderAnimations();
    }

    // ════════════════════════════════════════════════════════════════
    //  Loader1: 10根竖条 scaleY 波浪
    // ════════════════════════════════════════════════════════════════

    private void InitLoader1()
    {
        // 创建容器
        GameObject container = new GameObject("Loader1_Container");
        container.transform.SetParent(transform, false);
        RectTransform containerRT = container.AddComponent<RectTransform>();
        containerRT.anchorMin = containerRT.anchorMax = new Vector2(0.5f, 0.5f);
        containerRT.anchoredPosition = Vector2.zero;

        // 计算总宽度，居中排列
        float totalWidth = barCount * barWidth + (barCount - 1) * barSpacing;
        float startX = -totalWidth / 2f + barWidth / 2f;

        for (int i = 0; i < barCount; i++)
        {
            // 自动创建竖条
            GameObject barObj = new GameObject($"Bar_{i}", typeof(Image));
            RectTransform bar = barObj.GetComponent<RectTransform>();
            bar.SetParent(containerRT, false);
            bar.sizeDelta = new Vector2(barWidth, barHeight);
            bar.GetComponent<Image>().color = barColor;

            // 定位
            bar.anchoredPosition = new Vector2(startX + i * (barWidth + barSpacing), 0);
            // 锚点设为中心，scaleY 从中心收缩
            bar.pivot = new Vector2(0.5f, 0.5f);

            bars.Add(bar);
        }

        StartLoader1Animations();
    }

    private void StartLoader1Animations()
    {
        for (int i = 0; i < bars.Count; i++)
        {
            float delay = i * barDelayStep;
            var co = StartCoroutine(BarAnimation(bars[i], delay));
            barCoroutines.Add(co);
        }
    }

    /// <summary>
    /// 单根竖条动画：scaleY 1→0→1 循环
    /// 对应 CSS @keyframes animate1 { 50% { transform: scaleY(0) } }
    ///
    /// 用 |cos(πt)| 实现：
    ///   t=0   → cos(0)=1    → scaleY=1（满高）
    ///   t=0.5 → cos(π/2)=0  → scaleY=0（压扁）
    ///   t=1   → cos(π)=-1  → |cos|=1（满高）
    /// </summary>
    private IEnumerator BarAnimation(RectTransform bar, float delay)
    {
        // 等待延迟（对应 animation-delay）
        yield return new WaitForSecondsRealtime(delay);

        float elapsed = 0f;

        while (true)
        {
            elapsed += Time.unscaledDeltaTime;
            // 周期归一化到 0~1
            float t = (elapsed % barAnimDuration) / barAnimDuration;

            // scaleY: 1→0→1，用 |cos(πt)|
            float scaleY = Mathf.Abs(Mathf.Cos(Mathf.PI * t));

            bar.localScale = new Vector3(1f, scaleY, 1f);

            yield return null;
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  Loader17: 十字旋转 + 彩色点轨迹
    // ════════════════════════════════════════════════════════════════

    private void InitLoader17()
    {
        // 根容器（带旋转）
        GameObject root = new GameObject("Loader17_Root");
        root.transform.SetParent(transform, false);
        loader17Root = root.AddComponent<RectTransform>();
        loader17Root.anchorMin = loader17Root.anchorMax = new Vector2(0.5f, 0.5f);
        loader17Root.anchoredPosition = Vector2.zero;
        loader17Root.sizeDelta = new Vector2(baseSize * 2.5f, baseSize * 2.5f);
        loader17Root.localRotation = Quaternion.Euler(0, 0, rotationAngle);

        // :before — 横条（宽度动画 0.5em → 2.5em → 0.5em）
        beforeBar = CreateBar("Before", loader17Root, new Color(1, 1, 1, 0.9f));

        // :after — 竖条（高度动画）
        afterBar = CreateBar("After", loader17Root, new Color(1, 1, 1, 0.9f));

        // 4个彩色点（模拟 box-shadow 的4个颜色）
        // 粉色 rgba(225,20,98)    — 挂在 before 上
        // 蓝色 rgba(111,202,220)  — 挂在 before 上
        // 绿色 rgba(61,184,143)   — 挂在 after 上
        // 橙色 rgba(233,169,32)   — 挂在 after 上
        shadowDots.Add(CreateDot("Dot_Pink",   new Color(225f/255f, 20f/255f, 98f/255f, 0.75f), beforeBar));
        shadowDots.Add(CreateDot("Dot_Blue",   new Color(111f/255f, 202f/255f, 220f/255f, 0.75f), beforeBar));
        shadowDots.Add(CreateDot("Dot_Green",  new Color(61f/255f, 184f/255f, 143f/255f, 0.75f), afterBar));
        shadowDots.Add(CreateDot("Dot_Orange", new Color(233f/255f, 169f/255f, 32f/255f, 0.75f), afterBar));

        loader17Coroutine = StartCoroutine(Loader17Animation());
    }

    private RectTransform CreateBar(string name, Transform parent, Color color)
    {
        GameObject obj = new GameObject(name, typeof(Image));
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(baseSize * 0.5f, baseSize * 0.5f);
        obj.GetComponent<Image>().color = color;
        return rt;
    }

    private RectTransform CreateDot(string name, Color color, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(Image));
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(baseSize * 0.5f, baseSize * 0.5f);
        obj.GetComponent<Image>().color = color;
        return rt;
    }

    /// <summary>
    /// Loader17 动画协程
    ///
    /// CSS @keyframes before（宽度 + 粉蓝点水平移动）:
    ///   0%:   width=0.5em, 粉(1,-0.5) 蓝(-1,0.5)
    ///   35%:  width=2.5em, 粉(0,-0.5)  蓝(0,0.5)
    ///   70%:  width=0.5em, 粉(-1,-0.5) 蓝(1,0.5)
    ///   100%: 回到0%
    ///
    /// CSS @keyframes after（高度 + 绿橙点垂直移动）:
    ///   0%:   height=0.5em, 绿(0.5,1)  橙(-0.5,-1)
    ///   35%:  height=2.5em, 绿(0.5,0)  橙(-0.5,0)
    ///   70%:  height=0.5em, 绿(0.5,-1) 橙(-0.5,1)
    ///   100%: 回到0%
    /// </summary>
    private IEnumerator Loader17Animation()
    {
        float elapsed = 0f;
        float half = baseSize * 0.5f;
        float full = baseSize * 2.5f;

        while (true)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = (elapsed % loader17Duration) / loader17Duration;  // 0~1

            // === before 宽度 + 粉蓝点 ===
            float beforeWidth;
            Vector2 pinkPos, bluePos;

            if (t < 0.35f)
            {
                float s = t / 0.35f;
                beforeWidth = Mathf.Lerp(half, full, s);
                pinkPos  = Vector2.Lerp(new Vector2(baseSize, -half), new Vector2(0, -half), s);
                bluePos  = Vector2.Lerp(new Vector2(-baseSize, half), new Vector2(0, half), s);
            }
            else if (t < 0.7f)
            {
                float s = (t - 0.35f) / 0.35f;
                beforeWidth = Mathf.Lerp(full, half, s);
                pinkPos  = Vector2.Lerp(new Vector2(0, -half), new Vector2(-baseSize, -half), s);
                bluePos  = Vector2.Lerp(new Vector2(0, half), new Vector2(baseSize, half), s);
            }
            else
            {
                float s = (t - 0.7f) / 0.3f;
                beforeWidth = half;
                pinkPos  = Vector2.Lerp(new Vector2(-baseSize, -half), new Vector2(baseSize, -half), s);
                bluePos  = Vector2.Lerp(new Vector2(baseSize, half), new Vector2(-baseSize, half), s);
            }

            // === after 高度 + 绿橙点 ===
            float afterHeight;
            Vector2 greenPos, orangePos;

            if (t < 0.35f)
            {
                float s = t / 0.35f;
                afterHeight = Mathf.Lerp(half, full, s);
                greenPos  = Vector2.Lerp(new Vector2(half, baseSize), new Vector2(half, 0), s);
                orangePos = Vector2.Lerp(new Vector2(-half, -baseSize), new Vector2(-half, 0), s);
            }
            else if (t < 0.7f)
            {
                float s = (t - 0.35f) / 0.35f;
                afterHeight = Mathf.Lerp(full, half, s);
                greenPos  = Vector2.Lerp(new Vector2(half, 0), new Vector2(half, -baseSize), s);
                orangePos = Vector2.Lerp(new Vector2(-half, 0), new Vector2(-half, baseSize), s);
            }
            else
            {
                float s = (t - 0.7f) / 0.3f;
                afterHeight = half;
                greenPos  = Vector2.Lerp(new Vector2(half, -baseSize), new Vector2(half, baseSize), s);
                orangePos = Vector2.Lerp(new Vector2(-half, baseSize), new Vector2(-half, -baseSize), s);
            }

            // 应用尺寸
            beforeBar.sizeDelta = new Vector2(beforeWidth, half);
            afterBar.sizeDelta = new Vector2(half, afterHeight);

            // 应用点的位置（相对各自的bar）
            if (shadowDots.Count >= 4)
            {
                shadowDots[0].anchoredPosition = pinkPos;
                shadowDots[1].anchoredPosition = bluePos;
                shadowDots[2].anchoredPosition = greenPos;
                shadowDots[3].anchoredPosition = orangePos;
            }

            yield return null;
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  停止/清理
    // ════════════════════════════════════════════════════════════════

    private void StopAllLoaderAnimations()
    {
        foreach (var co in barCoroutines)
        {
            if (co != null) StopCoroutine(co);
        }
        barCoroutines.Clear();

        if (loader17Coroutine != null)
        {
            StopCoroutine(loader17Coroutine);
            loader17Coroutine = null;
        }
    }

    /// <summary>外部调用：显示加载动画</summary>
    public void Show()
    {
        gameObject.SetActive(true);
    }

    /// <summary>外部调用：隐藏加载动画</summary>
    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
