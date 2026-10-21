/* ════════════════════════════════════════════════════════════════════
 *  SceneLoader.cs  —  全局场景加载器（加载动画 + 异步加载并行 + 平滑淡入淡出 + 全屏锚点修复）
 * ════════════════════════════════════════════════════════════════════
 *
 *  【这个脚本干什么？】
 *      提供全局静态方法 SceneLoader.LoadScene("场景名")，
 *      同时做两件事：
 *        1. 播放加载动画（最低播放 最低动画时长 秒）
 *        2. 异步加载目标场景
 *      两者并行，等两者都完成后再切场景。
 *      → 场景加载快时，等动画播完（不会一闪而过）
 *      → 场景加载慢时，等加载完（动画继续循环播放，不会黑屏）
 *
 *  【平滑过渡】
 *      • 出现：CanvasGroup alpha 0→1，ease-out 缓动
 *      • 消失：CanvasGroup alpha 1→0，ease-in 缓动
 *      → 不会再硬出现/硬消失
 *
 *  【全屏修复】
 *      • 生成动画后自动设置 RectTransform 锚点为屏幕居中
 *      • 解决 Build 全屏模式下加载动画错位的问题
 *
 *  【使用方法】
 *      1. 创建一个空 GameObject（如 "_SceneLoader"）
 *      2. 挂上这个脚本
 *      3. Inspector 拖入加载动画预制体
 *      4. 任何地方调用：SceneLoader.LoadScene("游戏界面");
 *
 *  【特性】
 *      • DontDestroyOnLoad —— 切场景不销毁，动画跨场景存活
 *      • 单例防重复
 *      • 防重入
 *      • WaitForSecondsRealtime —— 不受 timeScale 影响
 *      • 异步加载 + 动画并行 —— 不浪费时间
 *      • CanvasGroup 淡入淡出 —— 平滑过渡
 * ════════════════════════════════════════════════════════════════════ */

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneLoader : MonoBehaviour
{
    // ==================== 单例 ====================
    public static SceneLoader Instance { get; private set; }

    // ==================== Inspector 字段 ====================
    [Header("=== 加载动画配置 ===")]
    [Tooltip("加载动画预制体（如：少女分形）")]
    public GameObject 加载动画预制体;

    [Tooltip("动画生成位置的 Transform（可为空，默认屏幕中央）")]
    public Transform 动画位置;

    [Tooltip("动画最低播放时长（秒）。\n场景加载比这个快时，会等动画播完；场景加载比这个慢时，动画继续播放直到加载完成。")]
    public float 最低动画时长 = 2.5f;

    [Tooltip("场景加载完后，动画再停留多久才开始淡出（秒）")]
    public float 消失延迟 = 0.3f;

    [Tooltip("动画缩放倍数（1=原始大小）")]
    public float 动画缩放 = 1.0f;

    [Header("=== 平滑过渡 ===")]
    [Tooltip("加载动画淡入时长（秒），0=不淡入直接出现")]
    public float 淡入时长 = 0.3f;

    [Tooltip("加载动画淡出时长（秒），0=不淡出直接消失")]
    public float 淡出时长 = 0.3f;

    [Header("=== 进度条（可选） ===")]
    [Tooltip("进度条 Slider（可选，不拖就不显示进度）")]
    public Slider 进度条;

    // ==================== 内部状态 ====================
    private GameObject 当前动画实例;
    private CanvasGroup 动画CanvasGroup;
    private bool isLoading = false;

    // ==================== 生命周期 ====================
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ==================== 公开静态方法 ====================

    /// <summary>
    /// 加载场景（带加载动画过渡）。
    /// 任何脚本任何地方都可以直接调：SceneLoader.LoadScene("游戏界面");
    /// </summary>
    public static void LoadScene(string sceneName)
    {
        if (Instance == null)
        {
            Debug.LogWarning("[SceneLoader] SceneLoader 实例不存在！直接加载场景（无动画）。");
            SceneManager.LoadScene(sceneName);
            return;
        }
        Instance.StartLoadScene(sceneName);
    }

    /// <summary>当前是否正在加载中</summary>
    public static bool IsLoading => Instance != null && Instance.isLoading;

    // ==================== 内部实现 ====================

    private void StartLoadScene(string sceneName)
    {
        if (isLoading)
        {
            Debug.LogWarning($"[SceneLoader] 正在加载中，忽略重复请求: {sceneName}");
            return;
        }
        isLoading = true;
        StartCoroutine(LoadSceneRoutine(sceneName));
    }

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        Debug.Log($"[SceneLoader] 开始加载: {sceneName}");

        // 1. 生成加载动画
        生成加载动画();

        // 2. 平滑淡入
        if (动画CanvasGroup != null && 淡入时长 > 0f)
        {
            动画CanvasGroup.alpha = 0f;
            动画CanvasGroup.blocksRaycasts = false;
            yield return StartCoroutine(FadeCanvasGroup(动画CanvasGroup, 0f, 1f, 淡入时长));
            动画CanvasGroup.blocksRaycasts = true;
        }

        // 3. 同时启动异步加载（不让它自动切换场景）
        AsyncOperation asyncOp = SceneManager.LoadSceneAsync(sceneName);
        asyncOp.allowSceneActivation = false;  // 加载完先不切，等动画

        // 4. 并行等待：动画最低时长 + 异步加载
        float elapsed = 0f;
        while (elapsed < 最低动画时长 || asyncOp.progress < 0.9f)
        {
            elapsed += Time.unscaledDeltaTime;

            // 更新进度条（如果有的话）
            if (进度条 != null)
            {
                float displayProgress = Mathf.Clamp01(asyncOp.progress / 0.9f);
                if (elapsed >= 最低动画时长)
                {
                    displayProgress = Mathf.Lerp(displayProgress, 1f, 0.5f * Time.unscaledDeltaTime);
                }
                进度条.value = displayProgress;
            }

            yield return null;
        }

        Debug.Log($"[SceneLoader] 动画播放 {elapsed:F1f}s，场景已加载 {asyncOp.progress * 100:F0}%，准备切换");

        // 5. 切场景
        asyncOp.allowSceneActivation = true;

        // 6. 切场景后再停留一会儿
        yield return new WaitForSecondsRealtime(消失延迟);

        // 7. 平滑淡出
        if (动画CanvasGroup != null && 淡出时长 > 0f)
        {
            动画CanvasGroup.blocksRaycasts = false;
            yield return StartCoroutine(FadeCanvasGroup(动画CanvasGroup, 1f, 0f, 淡出时长));
        }

        // 8. 隐藏进度条
        if (进度条 != null)
        {
            进度条.value = 0f;
            进度条.gameObject.SetActive(false);
        }

        // 9. 销毁动画
        销毁加载动画();

        isLoading = false;
        Debug.Log($"[SceneLoader] 加载完成: {sceneName}");
    }

    /// <summary>
    /// CanvasGroup 平滑淡入淡出协程。
    /// 淡入用 ease-out（先快后慢），淡出用 ease-in（先慢后快），视觉更自然。
    /// </summary>
    private IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // 淡入: ease-out (1-(1-t)³)  淡出: ease-in (t²)
            float eased = (to > from)
                ? (1f - Mathf.Pow(1f - t, 3f))
                : (t * t);
            cg.alpha = Mathf.Lerp(from, to, eased);
            yield return null;
        }
        cg.alpha = to;
    }

    private void 生成加载动画()
    {
        if (加载动画预制体 == null)
        {
            Debug.LogWarning("[SceneLoader] 加载动画预制体未设置，跳过动画。");
            return;
        }

        当前动画实例 = Instantiate(加载动画预制体);
        当前动画实例.transform.SetParent(transform, false);
        当前动画实例.transform.localScale = Vector3.one * 动画缩放;

        // ★新增：全屏锚点修复
        // 解决 Build 全屏模式下加载动画错位的问题
        // 强制 RectTransform 锚点为屏幕正中央，offset 归零
        RectTransform rt = 当前动画实例.GetComponent<RectTransform>();
        if (rt != null)
        {
            if (动画位置 != null)
            {
                // 有指定位置 → 用指定位置
                rt.position = 动画位置.position;
                rt.rotation = 动画位置.rotation;
            }
            else
            {
                // 没有指定位置 → 居中
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
            }
        }
        else if (动画位置 != null)
        {
            // 非 UI 模式（没有 RectTransform）→ 按世界坐标定位
            当前动画实例.transform.position = 动画位置.position;
            当前动画实例.transform.rotation = 动画位置.rotation;
        }

        // 强制显示在最顶层
        ForceShowOnTop(当前动画实例);

        // 获取或添加 CanvasGroup（用于平滑淡入淡出）
        动画CanvasGroup = 当前动画实例.GetComponent<CanvasGroup>();
        if (动画CanvasGroup == null)
        {
            动画CanvasGroup = 当前动画实例.AddComponent<CanvasGroup>();
        }

        Debug.Log("[SceneLoader] 加载动画已生成");
    }

    /// <summary>
    /// 强制让该 GameObject 及其子物体下的所有 Canvas 渲染在最顶层
    /// </summary>
    private void ForceShowOnTop(GameObject obj)
    {
        Canvas[] canvases = obj.GetComponentsInChildren<Canvas>(true);

        if (canvases.Length > 0)
        {
            foreach (var c in canvases)
            {
                c.overrideSorting = true;
                c.sortingOrder = 9999;
            }
        }
        else
        {
            Canvas newCanvas = obj.GetComponent<Canvas>();
            if (newCanvas == null)
            {
                newCanvas = obj.AddComponent<Canvas>();
            }
            newCanvas.overrideSorting = true;
            newCanvas.sortingOrder = 9999;

            if (obj.GetComponent<GraphicRaycaster>() == null)
            {
                obj.AddComponent<GraphicRaycaster>();
            }
        }
    }

    private void 销毁加载动画()
    {
        if (当前动画实例 != null)
        {
            Destroy(当前动画实例);
            当前动画实例 = null;
            动画CanvasGroup = null;
            Debug.Log("[SceneLoader] 加载动画已销毁");
        }
    }
}
