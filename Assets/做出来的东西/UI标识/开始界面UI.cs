/* ════════════════════════════════════════════════════════════════════
 *  开始界面UI.cs  —  游戏启动后的主菜单控制器 (房间系统增强版 v3.1)
 * ════════════════════════════════════════════════════════════════════
 *
 *  【v3.1 变更说明（修复问题③：退出房间不回大厅）】
 *      在 Start() 中订阅 roomUIControl.OnReturnToLobby 回调，
 *      退出房间时自动返回初始界面（大厅）。
 *
 *  【v3 变更说明】
 *      1. 保留模式选择界面（开始游戏进入模式选择界面）
 *      2. 模式选择界面提供 单人/多人/EX 三个按钮
 *      3. 登录界面改为浮在初始界面上方的浮层，不再替换整个面板
 *      4. 新增平滑显示/隐藏动画 (缩放+淡入淡出) for 登录界面
 *      5. 新增 pendingMode 记忆：点击按钮→弹登录框→登录成功→执行原操作
 *      6. 新增房间按钮绑定 (创建/加入/返回)
 *
 *  【面板层级关系 (v3)】
 *      初始界面 (StartButten/Quit/Practice/EXtra)
 *        ├─ 登录界面 (浮层，LoginUIControl 控制，平滑动画显示/隐藏)
 *        └─ 模式选择界面 (开始游戏直接进入)
 *             ├─ 单人
 *             ├─ 多人
 *             └─ EX
 *
 *  【操作流程】
 *      1. 点击"开始游戏" → 已登录→模式选择界面 / 未登录→平滑弹出登录浮层
 *      2. 登录成功 → 平滑收起登录浮层 → 进入模式选择界面
 *      3. 模式选择界面：选择 单人/多人/EX
 *      4. 多人面板：创建房间/加入房间/返回模式选择界面
 *      5. ★v3.1 等待面板"退出房间" → 自动返回初始界面（大厅）
 *
 *  【Inspector 需要拖拽的字段】
 *      面板(5个):     初始界面/单人/双人/EX/登录界面
 *      按钮(4个):     StartButten/Quit/Practice/EXtra
 *      模式选择按钮(3个): 单人按钮/多人按钮/EX按钮
 *      多人面板按钮(3个): 创建房间按钮/加入房间按钮/返回按钮
 *      多人面板组件(6个): 房间操作面板/房间列表面板/房间列表容器/房间列表项预制体/房间等待面板/房间名输入框
 *      搜索框组件(4个): 房间搜索框/搜索框输入框/搜索确认按钮/搜索取消按钮
 *      登录UI:        LoginUIControl 脚本引用
 * ════════════════════════════════════════════════════════════════════ */

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class 开始界面UI : MonoBehaviour
{
    [Header("=== 面板 ===")]
    public GameObject 初始界面;
    public GameObject 单人;
    public GameObject 双人;
    public GameObject EX;
    public GameObject 登录界面;
    public GameObject 历史记录界面;

    [Header("=== 初始界面按钮 ===")]
    public Button Quit;

    [Header("=== 模式选择按钮 ===")]
    public Button 单人按钮;
    public Button 多人按钮;
    public Button EX按钮;
    public Button 历史记录; 

    [Header("=== 单人难度按钮（Inspector 拖拽） ===")]
    public Button 简单按钮;
    public Button 普通按钮;
    public Button 困难按钮;
    public Button 极限按钮;

    [Header("=== 多人面板按钮 ===")]
    public Button 创建房间按钮;
    public Button 加入房间按钮;
    public Button 返回按钮;

    [Header("=== 多人面板组件 ===")]
    public GameObject 房间操作面板;
    public GameObject 房间列表面板;
    public Transform 房间列表容器;
    public GameObject 房间列表项预制体;
    public GameObject 房间等待面板;
    public TMP_InputField 房间名输入框;

    [Header("=== 搜索框组件 ===")]
    public GameObject 房间搜索框;
    public TMP_InputField 搜索框输入框;
    public Button 搜索确认按钮;
    public Button 搜索取消按钮;

    [Header("=== 登录UI ===")]
    public LoginUIControl loginUIControl;

    // 内部引用
    private RoomUIControl roomUIControl;

    // 登录状态
    private bool isLoggedIn = false;
    private string pendingMode = "";

    // 动画状态
    private bool isLoginAnimating = false;
    private bool isLoading = false;

    // ════════════════════════════════════════════════════════════════
    //  Start
    // ════════════════════════════════════════════════════════════════
    private void Start()
    {
        // ★音效：主界面BGM（重复调用不重启，AudioManager内部已处理）
        AudioManager.PlayBgm("lobby");

        // 绑定模式选择按钮
        if (单人按钮 != null) 单人按钮.onClick.AddListener(OnSinglePlayerClicked);
        if (多人按钮 != null) 多人按钮.onClick.AddListener(OnMultiPlayerClicked);
        if (EX按钮 != null) EX按钮.onClick.AddListener(OnEXClicked);
        if (Quit != null) Quit.onClick.AddListener(OnQuitClicked);
        if (历史记录 != null) 历史记录.onClick.AddListener(OnHistoryReplyClicked);

        // ★绑定单人难度按钮（Inspector 拖拽）
        if (简单按钮 != null) 简单按钮.onClick.AddListener(() => LoadDifficulty("Easy"));
        if (普通按钮 != null) 普通按钮.onClick.AddListener(() => LoadDifficulty("Normal"));
        if (困难按钮 != null) 困难按钮.onClick.AddListener(() => LoadDifficulty("Hard"));
        if (极限按钮 != null) 极限按钮.onClick.AddListener(() => LoadDifficulty("Lunatic"));

        // 绑定多人面板按钮
        if (创建房间按钮 != null) 创建房间按钮.onClick.AddListener(OnCreateRoomClicked);
        if (加入房间按钮 != null) 加入房间按钮.onClick.AddListener(OnJoinRoomClicked);
        if (返回按钮 != null) 返回按钮.onClick.AddListener(OnReturnClicked);

        // ★音效：所有主界面按钮统一点击音
        foreach (var b in new[] { 单人按钮, 多人按钮, EX按钮, Quit, 历史记录,
            简单按钮, 普通按钮, 困难按钮, 极限按钮, 创建房间按钮, 加入房间按钮,
            返回按钮, 搜索确认按钮, 搜索取消按钮 })
            if (b != null) b.onClick.AddListener(() => AudioManager.PlaySfx("click"));

        // 初始化 RoomUIControl
        if (双人 != null)
        {
            roomUIControl = 双人.GetComponent<RoomUIControl>();
            if (roomUIControl == null)
            {
                roomUIControl = 双人.AddComponent<RoomUIControl>();
            }

            // 调用 Init，传入所有需要的引用
            roomUIControl.Init(
                房间操作面板,
                房间列表面板,
                房间列表容器,
                房间列表项预制体,
                房间等待面板,
                房间名输入框,
                房间搜索框,
                搜索框输入框,
                搜索确认按钮,
                搜索取消按钮
            );

            // ★v3.1：订阅退出房间回调，返回大厅
            roomUIControl.OnReturnToLobby = () =>
            {
                Debug.Log("[开始界面UI] 收到退出房间通知，返回大厅");
                ShowOnlyPanel(初始界面);
            };
        }

        // 订阅登录成功静态事件
        LoginUIControl.OnLoginSuccessStatic += OnLoginSuccessStatic;

        // ★新增：检查是否从多人模式游戏结束返回
        // 如果是，直接显示"双人"面板（多人模式大厅），而非初始界面
        if (PlayerPrefs.GetInt("ReturnToLobby", 0) == 1)
        {
            PlayerPrefs.DeleteKey("ReturnToLobby"); // 清除标记，只生效一次
            PlayerPrefs.Save();
            Debug.Log("[开始界面UI] 检测到从多人模式返回，直接显示双人面板");
            // 用静态登录状态判断（本地isLoggedIn在换场景后会重置）
            if (LoginUIControl.IsLoggedIn)
            {
                // 已登录 → 直接显示双人面板 + 房间操作面板
                ShowOnlyPanel(双人);
                if (roomUIControl != null)
                {
                    roomUIControl.ShowOperatePanel();
                }
            }
            else
            {
                // 未登录（比如断线后重连）→ 显示双人面板，会弹出登录浮层
                ShowOnlyPanel(双人);
            }
        }
        else
        {
            // 初始显示初始界面
            ShowOnlyPanel(初始界面);
        }

        Debug.Log("[开始界面UI] 初始化完成");
    }





    // ════════════════════════════════════════════════════════════════
    //  模式选择按钮回调
    // ════════════════════════════════════════════════════════════════

    private void ShowModeSelect()
    {
        // 模式选择界面 = 初始界面（v3 保留模式选择在初始界面上）
        ShowOnlyPanel(初始界面);
    }
    private void OnQuitClicked()
    {
        Debug.Log("[开始界面UI] 退出游戏");
        Application.Quit();
    }

    private void OnSinglePlayerClicked()
    {
        Debug.Log("[开始界面UI] 选择单人模式");
        PlayerPrefs.SetString("GameMode", "Single");  // ★修复：设置游戏模式
        ShowOnlyPanel(单人);
    }

    private void OnMultiPlayerClicked()
    {
        Debug.Log("[开始界面UI] 选择多人模式");
        if (!LoginUIControl.IsLoggedIn)
        {
            Debug.Log("[开始界面UI] 未登录，弹出登录浮层");
            pendingMode = "Multi";
            ShowLoginPanelSmooth();
            return;
        }
        ShowOnlyPanel(双人);
        if (roomUIControl != null)
        {
            roomUIControl.ShowOperatePanel();
        }
    }

    private void OnEXClicked()
    {
        Debug.Log("[开始界面UI] 选择EX模式");
        if (!LoginUIControl.IsLoggedIn)
        {
            Debug.Log("[开始界面UI] 未登录，弹出登录浮层");
            pendingMode = "EX";
            ShowLoginPanelSmooth();
            return;
        }
        ShowOnlyPanel(EX);
    }
    private void OnHistoryReplyClicked()
    {
        Debug.Log("[开始界面UI] 回放历史记录");
        if (!LoginUIControl.IsLoggedIn)
        {
            Debug.Log("[开始界面UI] 未登录，弹出登录浮层");
            pendingMode = "History";
            ShowLoginPanelSmooth();
            return;
        }
        ShowOnlyPanel(历史记录界面);

        // ★修复：显示面板后主动触发查询对局列表
        var recordUI = 历史记录界面 != null ? 历史记录界面.GetComponent<对局记录UI>() : null;
        if (recordUI == null)
        {
            // 脚本可能不在面板物体上，全局查找
            recordUI = FindFirstObjectByType<对局记录UI>();
        }
        if (recordUI != null)
        {
            recordUI.RefreshList();
        }
        else
        {
            Debug.LogError("[开始界面UI] 场景中找不到 对局记录UI 脚本！请确保已挂载。");
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  多人面板按钮回调
    // ════════════════════════════════════════════════════════════════

    private void OnCreateRoomClicked()
    {
        Debug.Log("[开始界面UI] 点击创建房间");
        if (roomUIControl != null)
        {
            roomUIControl.CreateRoom();
        }
    }

    private void OnJoinRoomClicked()
    {
        Debug.Log("[开始界面UI] 点击加入房间");
        if (roomUIControl != null)
        {
            roomUIControl.ShowSearchDialog();
        }
    }

    private void OnReturnClicked()
    {
        Debug.Log("[开始界面UI] 点击返回");
        ShowOnlyPanel(初始界面);
    }

    // ════════════════════════════════════════════════════════════════
    //  登录相关
    // ════════════════════════════════════════════════════════════════

    private void OnLoginSuccessStatic(int userId, string account)
    {
        Debug.Log($"[开始界面UI] 收到登录成功事件: userId={userId}, account={account}");
        isLoggedIn = true;

        // 收起登录浮层
        HideLoginPanelSmooth();

        // 执行 pendingMode
        //if (!string.IsNullOrEmpty(pendingMode))
        //{
        //    string mode = pendingMode;
        //    pendingMode = "";

        //    switch (mode)
        //    {
        //        case "ModeSelect":
        //            ShowModeSelect();
        //            break;
        //        case "Single":
        //            ShowOnlyPanel(单人);
        //            break;
        //        case "Multi":
        //            ShowOnlyPanel(双人);
        //            if (roomUIControl != null)
        //            {
        //                roomUIControl.ShowOperatePanel();
        //            }
        //            break;
        //        case "EX":
        //            ShowOnlyPanel(EX);
        //            break;
        //    }
        //}
    }

    // ════════════════════════════════════════════════════════════════
    //  登录界面平滑动画
    // ════════════════════════════════════════════════════════════════

    private void ShowLoginPanelSmooth()
    {
        if (登录界面 != null)
        {
            StartCoroutine(ShowLoginPanelCoroutine());
        }
    }

    private void HideLoginPanelSmooth()
    {
        if (登录界面 != null)
        {
            StartCoroutine(HideLoginPanelCoroutine());
        }
    }

    private IEnumerator ShowLoginPanelCoroutine()
    {
        if (isLoginAnimating) yield break;
        isLoginAnimating = true;

        登录界面.SetActive(true);

        CanvasGroup cg = 登录界面.GetComponent<CanvasGroup>();
        if (cg == null) cg = 登录界面.AddComponent<CanvasGroup>();

        RectTransform rt = 登录界面.GetComponent<RectTransform>();
        if (rt == null) rt = 登录界面.AddComponent<RectTransform>();

        cg.blocksRaycasts = false;
        float elapsed = 0f;
        float duration = 0.25f;

        while (elapsed < duration)
        {
            if (!isLoginAnimating) yield break;

            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;

            // ease-out 缓动
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            cg.alpha = eased;
            rt.localScale = Vector3.Lerp(new Vector3(0.85f, 0.85f, 1f), Vector3.one, eased);

            yield return null;
        }

        cg.alpha = 1f;
        rt.localScale = Vector3.one;
        cg.blocksRaycasts = true;
        isLoginAnimating = false;
    }

    private IEnumerator HideLoginPanelCoroutine()
    {
        if (isLoginAnimating) yield break;
        isLoginAnimating = true;

        // 先停止显示协程（防止冲突）
        StopCoroutine(ShowLoginPanelCoroutine());

        CanvasGroup cg = 登录界面.GetComponent<CanvasGroup>();
        if (cg == null) cg = 登录界面.AddComponent<CanvasGroup>();

        RectTransform rt = 登录界面.GetComponent<RectTransform>();
        if (rt == null) rt = 登录界面.AddComponent<RectTransform>();

        cg.blocksRaycasts = false;
        float elapsed = 0f;
        float duration = 0.2f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;

            // ease-in 缓动
            float eased = t * t;

            cg.alpha = 1f - eased;
            rt.localScale = Vector3.Lerp(Vector3.one, new Vector3(0.92f, 0.92f, 1f), eased);

            yield return null;
        }

        cg.alpha = 0f;
        rt.localScale = Vector3.one;
        登录界面.SetActive(false);
        isLoginAnimating = false;
    }

    // ════════════════════════════════════════════════════════════════
    //  ★修复问题1：单人模式难度选择（Inspector 拖拽按钮 + 统一加载方法）
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 选择难度后加载游戏场景。由 Start() 中 AddListener 调用。
    /// </summary>
    /// <param name="difficulty">"Easy" / "Normal" / "Hard" / "Lunatic"</param>
    private void LoadDifficulty(string difficulty)
    {
        Debug.Log($"[开始界面UI] 选择难度: {difficulty}");
        PlayerPrefs.SetString("GameMode", "Single");
        PlayerPrefs.SetString("Difficulty", difficulty);
        StartCoroutine(LoadSceneCoroutine("游戏界面"));
    }

    // ════════════════════════════════════════════════════════════════
    //  单人模式场景加载（改为调用 SceneLoader 统一处理加载动画）
    // ════════════════════════════════════════════════════════════════

    public void LoadSinglePlayerScene(string sceneName)
    {
        if (isLoading) return;
        isLoading = true;
        SceneLoader.LoadScene(sceneName);
    }

    private IEnumerator LoadSceneCoroutine(string sceneName)
    {
        isLoading = true;
        SceneLoader.LoadScene(sceneName);
        yield break;
    }

    // ════════════════════════════════════════════════════════════════
    //  面板切换工具方法
    //  注意：不操作 登录界面(它是浮层，由 ShowLoginPanelSmooth/HideLoginPanelSmooth 管理)
    // ════════════════════════════════════════════════════════════════
    private void ShowOnlyPanel(GameObject panelToShow)
    {
        if (初始界面 != null) 初始界面.SetActive(false);
        if (单人 != null) 单人.SetActive(false);
        if (双人 != null) 双人.SetActive(false);
        if (EX != null) EX.SetActive(false);
        if (历史记录界面 != null) 历史记录界面.SetActive(false);
        if (panelToShow != null) panelToShow.SetActive(true);
    }

    // ★修复：取消订阅静态事件，防止重复调用
    private void OnDestroy()
    {
        LoginUIControl.OnLoginSuccessStatic -= OnLoginSuccessStatic;
    }
}
