using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class 死亡检查多人版 : MonoBehaviour
{
    public GameObject 满身疮痍;
    public GameObject Title0; // 暂停
    public GameObject Title1; // 红胜（黑将死）
    public GameObject Title2; // 黑胜（红帅死）
    public GameObject 死了界面终点;
    public GameObject 死了界面起点;
    public GameObject 黑幕;

    [Header("结算按钮")]
    public Button 再来一局;
    public Button 返回开始界面;
    public Button 返回大厅;
    public Button 退出游戏;

    [Header("★再来一局邀请弹窗（需要自己搭建UI）")]
    [Tooltip("邀请弹窗面板，包含邀请文本、是/否按钮")]
    public GameObject 邀请弹窗;
    public TMP_Text 邀请文本;
    public Button 邀请是按钮;
    public Button 邀请否按钮;

    [Header("★拒绝弹窗（需要自己搭建UI）")]
    [Tooltip("拒绝/失败弹窗面板，包含文本和确定按钮")]
    public GameObject 拒绝弹窗;
    public TMP_Text 拒绝文本;
    public Button 拒绝确定按钮;

    private 选择象棋 选择象棋脚本;
    private bool isPaused = false;

    // ★修复问题5：允许外部触发游戏结束界面（网络模式对手断线/投降时）
    private bool forceGameOver = false;
    private int forcedWinner = 0; // 1=红胜 2=黑胜

    // ★再来一局状态
    private bool rematchRequested = false;  // 我已发出邀请，等待对方回应
    private bool rematchInvitationShown = false;  // 对方发来邀请，我正在看
    private bool rematchButtonDisabled = false;  // 按钮已被禁用（对方拒绝/离开）

    private void Start()
    {
        黑幕.SetActive(false);
        选择象棋脚本 = FindObjectOfType<选择象棋>();
        if (选择象棋脚本 == null)
        {
            Debug.LogError("找不到对象: 选择象棋脚本");
        }

        if (死了界面起点 != null)
        {
            满身疮痍.transform.position = 死了界面起点.transform.position;
        }

        // ★自愈绑定：如果Inspector没拖拽，用名字查找按钮
        if (再来一局 == null)
        {
            Transform t0 = transform.Find("再来一局");
            if (t0 != null) 再来一局 = t0.GetComponent<Button>();
            if (再来一局 == null) 再来一局 = FindButtonByName("再来一局");
        }
        if (返回开始界面 == null)
        {
            Transform t1 = transform.Find("返回开始界面");
            if (t1 != null) 返回开始界面 = t1.GetComponent<Button>();
            if (返回开始界面 == null) 返回开始界面 = FindButtonByName("返回开始界面");
        }
        if (返回大厅 == null)
        {
            Transform t2 = transform.Find("返回大厅");
            if (t2 != null) 返回大厅 = t2.GetComponent<Button>();
            if (返回大厅 == null) 返回大厅 = FindButtonByName("返回大厅");
        }
        if (退出游戏 == null)
        {
            Transform t3 = transform.Find("退出游戏");
            if (t3 != null) 退出游戏 = t3.GetComponent<Button>();
            if (退出游戏 == null) 退出游戏 = FindButtonByName("退出游戏");
        }

        // 绑定结算按钮
        if (再来一局 != null) 再来一局.onClick.AddListener(OnRematchClick);
        if (返回开始界面 != null) 返回开始界面.onClick.AddListener(OnReturnToStartMenu);
        if (返回大厅 != null) 返回大厅.onClick.AddListener(GoToGameHall);
        if (退出游戏 != null) 退出游戏.onClick.AddListener(OnQuitGame);

        // 绑定邀请弹窗按钮
        if (邀请是按钮 != null) 邀请是按钮.onClick.AddListener(OnInvitationYes);
        if (邀请否按钮 != null) 邀请否按钮.onClick.AddListener(OnInvitationNo);

        // 绑定拒绝弹窗按钮
        if (拒绝确定按钮 != null) 拒绝确定按钮.onClick.AddListener(OnRejectionConfirm);

        // 初始隐藏弹窗
        if (邀请弹窗 != null) 邀请弹窗.SetActive(false);
        if (拒绝弹窗 != null) 拒绝弹窗.SetActive(false);

        // ★防御性检查
        if (再来一局 == null) Debug.LogError("[死亡检查多人版] 找不到再来一局按钮！请检查Inspector或场景层级");
        if (返回开始界面 == null) Debug.LogError("[死亡检查多人版] 找不到返回开始界面按钮！");
        if (返回大厅 == null) Debug.LogError("[死亡检查多人版] 找不到返回大厅按钮！");
        if (退出游戏 == null) Debug.LogWarning("[死亡检查多人版] 退出游戏按钮未赋值");
        if (邀请弹窗 == null) Debug.LogWarning("[死亡检查多人版] 邀请弹窗未赋值，再来一局邀请功能无法使用。请搭建UI后在Inspector赋值");
        if (拒绝弹窗 == null) Debug.LogWarning("[死亡检查多人版] 拒绝弹窗未赋值，拒绝提示功能无法使用。请搭建UI后在Inspector赋值");
    }

    /// <summary>★自愈：递归查找指定名称的Button</summary>
    private Button FindButtonByName(string name)
    {
        Button[] allButtons = FindObjectsOfType<Button>();
        foreach (var btn in allButtons)
        {
            if (btn.gameObject.name == name) return btn;
        }
        return null;
    }

    private void Update()
    {
        if (选择象棋脚本 == null) return;

        bool isRedDead = false;
        bool isBlackDead = false;

        foreach (var chess in ChessManager.ChessArray)
        {
            if (chess != null && chess.Type == ChessManager.Chess.ChessType.帅)
            {
                if (chess.Is_Red)
                    isRedDead = chess.Is_Dead;
                else
                    isBlackDead = chess.Is_Dead;
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
            {
                isPaused = false;
                黑幕.SetActive(false);
                选择象棋脚本.enabled = true;
            }
            else if (!isRedDead && !isBlackDead)
            {
                isPaused = true;
                黑幕.SetActive(true);
                选择象棋脚本.enabled = false;
            }
        }

        bool isPlayingAnimation = 选择象棋脚本.AllowMove;
        bool shouldShowResult = isPaused ||
            ((isRedDead || isBlackDead) && !isPlayingAnimation) ||
            (forceGameOver && !isPlayingAnimation);

        if (shouldShowResult)
        {
            if (!满身疮痍.activeSelf) 满身疮痍.SetActive(true);

            if (forceGameOver && !isRedDead && !isBlackDead)
            {
                Title0.SetActive(false);
                Title1.SetActive(forcedWinner == 1);
                Title2.SetActive(forcedWinner == 2);
            }
            else if (isRedDead)
            {
                Title0.SetActive(false);
                Title1.SetActive(false);
                Title2.SetActive(true);
            }
            else if (isBlackDead)
            {
                Title0.SetActive(false);
                Title1.SetActive(true);
                Title2.SetActive(false);
            }
            else if (isPaused)
            {
                Title0.SetActive(true);
                Title1.SetActive(false);
                Title2.SetActive(false);
            }

            满身疮痍.transform.position = Vector3.Lerp(满身疮痍.transform.position, 死了界面终点.transform.position, Time.unscaledDeltaTime * 5f);
        }
        else
        {
            if (!满身疮痍.activeSelf) 满身疮痍.SetActive(true);
            Title0.SetActive(false);
            Title1.SetActive(false);
            Title2.SetActive(false);
            满身疮痍.transform.position = Vector3.Lerp(满身疮痍.transform.position, 死了界面起点.transform.position, Time.unscaledDeltaTime * 5f);
        }

        if (((isRedDead || isBlackDead) && !isPlayingAnimation) || (forceGameOver && !isPlayingAnimation))
        {
            黑幕.gameObject.SetActive(true);
            选择象棋脚本.enabled = false;
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  ★修复问题5：外部调用 —— 强制显示游戏结束界面
    // ════════════════════════════════════════════════════════════════
    public void ShowResult(int winner)
    {
        forceGameOver = true;
        forcedWinner = winner;
        Debug.Log($"[死亡检查] ShowResult: winner={winner}");
        // ★音效：按本机颜色判断输赢（winner: 1=红胜 2=黑胜）
        bool iWon = (winner == 1) == (GameFace.MyColor == "Red");
        AudioManager.PlaySfx(iWon ? "win" : "lose");
    }

    // ════════════════════════════════════════════════════════════════
    //  ★再来一局（Rematch）系统
    // ════════════════════════════════════════════════════════════════

    /// <summary>点击"再来一局"按钮</summary>
    public void OnRematchClick()
    {
        if (rematchButtonDisabled)
        {
            Debug.Log("[死亡检查多人版] 再来一局按钮已禁用");
            return;
        }
        if (rematchRequested)
        {
            Debug.Log("[死亡检查多人版] 已发出再来一局邀请，等待对方回应...");
            return;
        }
        if (rematchInvitationShown)
        {
            Debug.Log("[死亡检查多人版] 对方已发来邀请，请先回应");
            return;
        }

        rematchRequested = true;

        // 禁用按钮防止重复点击
        if (再来一局 != null)
        {
            再来一局.interactable = false;
            var text = 再来一局.GetComponentInChildren<TMP_Text>();
            if (text != null) text.text = "等待对方回应...";
        }

        // 通过 选择象棋 发送再来一局请求
        if (选择象棋脚本 != null)
        {
            选择象棋脚本.RequestRematch();
        }
        Debug.Log("[死亡检查多人版] 已发送再来一局请求");
    }

    /// <summary>收到对手发来的再来一局邀请（由 选择象棋.OnRematchResponse 调用）</summary>
    public void ShowRematchInvitation(string opponentName)
    {
        Debug.Log($"[死亡检查多人版] 收到再来一局邀请: {opponentName}");

        rematchInvitationShown = true;
        // ★音效：邀请提示音
        AudioManager.PlaySfx("message");

        if (邀请弹窗 != null)
        {
            邀请弹窗.SetActive(true);
            if (邀请文本 != null)
                邀请文本.text = $"{opponentName} 邀请你再来一局\n是否接受？";
        }
        else
        {
            // 没有UI弹窗 → 自动接受（方便测试）
            Debug.LogWarning("[死亡检查多人版] 邀请弹窗未设置，自动接受");
            OnInvitationYes();
        }
    }

    /// <summary>点击邀请弹窗的"是"按钮</summary>
    public void OnInvitationYes()
    {
        Debug.Log("[死亡检查多人版] 接受了再来一局");

        rematchInvitationShown = false;
        if (邀请弹窗 != null) 邀请弹窗.SetActive(false);

        if (选择象棋脚本 != null)
            选择象棋脚本.AcceptRematch();
    }

    /// <summary>点击邀请弹窗的"否"按钮</summary>
    public void OnInvitationNo()
    {
        Debug.Log("[死亡检查多人版] 拒绝了再来一局");

        rematchInvitationShown = false;
        if (邀请弹窗 != null) 邀请弹窗.SetActive(false);

        if (选择象棋脚本 != null)
            选择象棋脚本.RejectRematch();
    }

    /// <summary>对手接受了再来一局（由 选择象棋.OnRematchResponse 调用）</summary>
    
    // 改为
    public void OnRematchAccepted()
    {
        Debug.Log("[死亡检查多人版] 对手接受了再来一局，重载场景...");
        rematchRequested = false;
        rematchInvitationShown = false;
        // ★改为调用 选择象棋.ReloadGameScene()，保持颜色和游戏模式
        if (选择象棋脚本 != null)
        {
            选择象棋脚本.ReloadGameScene();
        }
        else
        {
            // 兜底：找不到脚本时直接加载
            Time.timeScale = 1f;
            PlayerPrefs.SetString("GameMode", "Network");
            PlayerPrefs.Save();
            SceneLoader.LoadScene("ChessGame");
        }
    }
    /// <summary>对手拒绝了再来一局（由 选择象棋.OnRematchResponse 调用）</summary>
    public void OnRematchRejected(string message)
    {
        Debug.Log($"[死亡检查多人版] 再来一局被拒: {message}");

        rematchRequested = false;
        rematchButtonDisabled = true;

        // 按钮变灰
        if (再来一局 != null)
        {
            再来一局.interactable = false;
            var text = 再来一局.GetComponentInChildren<TMP_Text>();
            if (text != null) text.text = "再来一局";
        }

        // 显示拒绝弹窗
        if (拒绝弹窗 != null)
        {
            拒绝弹窗.SetActive(true);
            if (拒绝文本 != null)
                拒绝文本.text = string.IsNullOrEmpty(message) ? "对手拒绝了再来一局" : message;
        }
    }

    /// <summary>点击拒绝弹窗的"确定"按钮</summary>
    public void OnRejectionConfirm()
    {
        if (拒绝弹窗 != null) 拒绝弹窗.SetActive(false);
    }

    // ================= 按钮执行函数 =================
    public void OnReturnToStartMenu()
    {
        Time.timeScale = 1f;
        // ★修复：返回标题同样要先通知服务器（对局中退出→对手立刻收到判负结算）。
        //   原来只有 GoToGameHall 加了通知，"返回标题"按钮是漏网之鱼 → 对手卡在游戏界面
        if (选择象棋.Instance != null) 选择象棋.Instance.NotifyServerLeaveGame();
        SceneLoader.LoadScene("开始界面");
    }

    public void GoToGameHall()
    {
        Time.timeScale = 1f;
        // ★修复：退出前通知服务器。对局中退出→对手立刻收到判负结算；
        //   已结算退出→正常清理房间。不发的话对手会永远卡在对局界面。
        if (选择象棋.Instance != null) 选择象棋.Instance.NotifyServerLeaveGame();
        PlayerPrefs.SetInt("ReturnToLobby", 1);
        PlayerPrefs.Save();
        SceneLoader.LoadScene("开始界面");
    }

    public void OnQuitGame()
    {
        // ★修复：退出程序前先通知服务器（否则对手要等60秒断线超时才结算）
        if (选择象棋.Instance != null) 选择象棋.Instance.NotifyServerLeaveGame();
        StartCoroutine(QuitAfterSend());
    }

    /// <summary>★等网络包发出后再退出进程（Application.Quit 会掐断未完成的异步发送）</summary>
    private System.Collections.IEnumerator QuitAfterSend()
    {
        yield return new WaitForSecondsRealtime(0.3f);
        Application.Quit();
    }
}
