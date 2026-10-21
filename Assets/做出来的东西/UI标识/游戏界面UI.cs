/* ════════════════════════════════════════════════════════════════════
 *  游戏界面UI.cs  —  游戏场景UI控制器 (v2)
 * ════════════════════════════════════════════════════════════════════
 *
 *  【v2 新增】
 *      1. 走棋计时器进度条（Slider，30秒从满到空；Fill 颜色 G 从 255→0 渐变）
 *      2. 对手信息面板（用户名、颜色、"思考中..."状态）
 *
 *  【Inspector 需要拖拽的字段】
 *      Easy/Normal/Hard/Lunatic (GameObject): 4个难度AI对象
 *      悔棋 (Button): 悔棋按钮
 *      计时进度条 (Slider): Min Value=0, Max Value=1, Whole Numbers=关；
 *                           需有 Fill Area → Fill 子物体，Fill 上挂 Image（用于变色）
 *      计时数字 (TMP_Text): 显示剩余秒数（可选）
 *      对手名Text (TMP_Text): 显示对手用户名（网络模式）
 *      对手状态Text (TMP_Text): 显示"思考中..."/"轮到你"（网络模式）
 *
 *  【想加新功能从哪里改？】
 *      ▸ 加计时器:  已在 Update() 中实现，从 选择象棋.Instance 读取计时器状态
 *      ▸ 加认输按钮: 声明 public Button 认输; 在 Start() 里绑事件
 *
 *  【超时行为】
 *      走棋超时不再判负，而是"跳过本轮、轮到对方走棋"（见 选择象棋.OnMoveTimeout）。
 *      网络模式下通过 ChessId=-1 的 Move 包同步跳过。
 * ════════════════════════════════════════════════════════════════════ */

using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class 游戏界面UI : MonoBehaviour
{
    // ==================== 静态状态 ====================
    public static 游戏界面UI Instance { get; private set; }
    public static string Difficulty;

    // ==================== 难度AI对象 (Inspector 拖拽) ====================
    public GameObject Easy;
    public GameObject Normal;
    public GameObject Hard;
    public GameObject Lunatic;

    // ==================== 功能按钮 (Inspector 拖拽) ====================
    public Button 悔棋;

    // ==================== 走棋计时器 UI (Inspector 拖拽) ====================
    [Header("★走棋计时器")]
    [Tooltip("Slider 组件：Min Value=0, Max Value=1, Whole Numbers=关。Fill(Fill Area子物体)的Image用于变色。")]
    public Slider 计时进度条;
    [Tooltip("显示剩余秒数（可选，不设也行）")]
    public TMP_Text 计时数字;
    // 进度条颜色：R 恒为 255，G 随剩余时间从 255 线性渐变到 0（满血黄→归零红），B=0。
    [Tooltip("进度条满血时的颜色（剩余时间充足）")]
    public Color 正常色 = new Color(1f, 1f, 0f, 1f);      // (255,255,0) 黄
    // 旧字段，保留兼容（不再用于计时条，对手状态文字仍可用）
    public Color 警告色 = new Color(1f, 0.5f, 0f, 1f);     // 橙
    public Color 危险色 = new Color(1f, 0.2f, 0.2f, 1f);    // 红
    // 缓存 Slider Fill 的 Image，避免每帧 GetComponent
    private Image 计时条FillImage;

    // ==================== 对手信息面板 UI (Inspector 拖拽) ====================
    [Header("★对手信息面板（网络模式）")]
    public TMP_Text 对手名Text;
    public TMP_Text 对手状态Text;
    [Tooltip("旧字段，保留兼容")]
    public TMP_Text 对手信息Text;
    public GameObject 游戏结束面板;

    // ==================== 内部状态 ====================
    private string gameModeStr = "";
    private string myColor = "Red";
    private string opponentName = "";
    private bool opponentReconnecting = false;   // ★对手断线等待重连中（抑制每帧状态刷新）

    // ════════════════════════════════════════════════════════════════
    //  Start()
    // ════════════════════════════════════════════════════════════════
    private void Start()
    {
        Instance = this;
        gameModeStr = PlayerPrefs.GetString("GameMode", "");
        Difficulty = PlayerPrefs.GetString("Difficulty");
        // ★修复：用 static 替代 PlayerPrefs，防止双开 .exe 共享颜色
        myColor = GameFace.MyColor;
        opponentName = GameFace.OpponentName;

        // ★缓存 Slider 的 Fill Image（用于颜色渐变）
        if (计时进度条 != null && 计时进度条.fillRect != null)
        {
            计时条FillImage = 计时进度条.fillRect.GetComponent<Image>();
            计时进度条.minValue = 0f;
            计时进度条.maxValue = 1f;
            计时进度条.wholeNumbers = false;
        }

        if (gameModeStr == "Network")
        {
            // 隐藏所有AI
            if (Easy != null) Easy.SetActive(false);
            if (Normal != null) Normal.SetActive(false);
            if (Hard != null) Hard.SetActive(false);
            if (Lunatic != null) Lunatic.SetActive(false);

            // 显示对手信息
            if (对手信息Text != null)
            {
                对手信息Text.text = $"网络对战 | 你是{(myColor == "Red" ? "红方" : "黑方")}";
                对手信息Text.gameObject.SetActive(true);
            }
            if (对手名Text != null)
            {
                对手名Text.text = string.IsNullOrEmpty(opponentName) ? "对手" : opponentName;
                对手名Text.gameObject.SetActive(true);
            }
            if (对手状态Text != null)
            {
                对手状态Text.gameObject.SetActive(true);
            }

            // 隐藏游戏结束面板
            if (游戏结束面板 != null) 游戏结束面板.SetActive(false);

            // 网络模式下隐藏悔棋按钮
            if (悔棋 != null) 悔棋.gameObject.SetActive(false);

            Debug.Log($"[游戏界面UI] 网络对战模式: MyColor={myColor}, Opponent={opponentName}");
            return;
        }

        // ===== AI / 本地双人模式 =====
        if (Difficulty == "Easy")        { Easy?.SetActive(true); Normal?.SetActive(false); Hard?.SetActive(false); Lunatic?.SetActive(false); }
        else if (Difficulty == "Normal") { Easy?.SetActive(false); Normal?.SetActive(true); Hard?.SetActive(false); Lunatic?.SetActive(false); }
        else if (Difficulty == "Hard")   { Easy?.SetActive(false); Normal?.SetActive(false); Hard?.SetActive(true); Lunatic?.SetActive(false); }
        else if (Difficulty == "Lunatic") { Easy?.SetActive(false); Normal?.SetActive(false); Hard?.SetActive(false); Lunatic?.SetActive(true); }
        else if (Difficulty == "Double") { Easy?.SetActive(false); Normal?.SetActive(false); Hard?.SetActive(false); Lunatic?.SetActive(false); }

        // 隐藏对手信息面板（非网络模式）
        if (对手名Text != null) 对手名Text.gameObject.SetActive(false);
        if (对手状态Text != null) 对手状态Text.gameObject.SetActive(false);

        // 绑定悔棋按钮事件（仅非网络模式）
        if (悔棋 != null && gameModeStr != "Network")
        {
            悔棋.onClick.AddListener(选择象棋.Instance.UndoMove);
            // ★音效：悔棋按钮点击音
            悔棋.onClick.AddListener(() => AudioManager.PlaySfx("click"));
        }

        // ★新增：初始隐藏计时条（等 UpdateMoveTimer 启动后才显示）
        if (计时进度条 != null) 计时进度条.gameObject.SetActive(false);
        if (计时数字 != null) 计时数字.gameObject.SetActive(false);
    }

    // ════════════════════════════════════════════════════════════════
    //  Update() — 计时器进度条 + 对手状态更新
    // ════════════════════════════════════════════════════════════════
    private void Update()
    {
        UpdateTimerUI();
        UpdateOpponentStatus();
    }

    /// <summary>更新计时器进度条（Slider + Fill 颜色渐变）
    /// ★v3：不在计时时直接隐藏计时条和计时数字，只在计时时显示
    /// </summary>
    private void UpdateTimerUI()
    {
        选择象棋 chess = 选择象棋.Instance;
        if (chess == null) return;

        // ★不在计时 → 隐藏计时条和计时数字
        if (!chess.IsTimerRunning)
        {
            if (计时进度条 != null && 计时进度条.gameObject.activeSelf)
                计时进度条.gameObject.SetActive(false);
            if (计时数字 != null && 计时数字.gameObject.activeSelf)
                计时数字.gameObject.SetActive(false);
            return;
        }

        // ★正在计时 → 显示并更新
        if (计时进度条 != null)
        {
            if (!计时进度条.gameObject.activeSelf)
                计时进度条.gameObject.SetActive(true);

            float ratio = Mathf.Clamp01(chess.MyMoveTimer / 选择象棋.MOVE_TIME_LIMIT);
            计时进度条.value = ratio;

            // 颜色渐变：R 恒为 255，G 随剩余比例从 255(满) 线性渐变到 0(归零)，B=0
            // 满→(255,255,0)黄；归零→(255,0,0)红。
            if (计时条FillImage != null)
            {
                计时条FillImage.color = new Color(1f, ratio, 0f, 1f);
            }
        }

        if (计时数字 != null)
        {
            if (!计时数字.gameObject.activeSelf)
                计时数字.gameObject.SetActive(true);
            int seconds = Mathf.CeilToInt(chess.MyMoveTimer);
            计时数字.text = $"{seconds}s";
        }
    }

    /// <summary>更新对手状态文字</summary>
    private void UpdateOpponentStatus()
    {
        if (对手状态Text == null) return;
        if (gameModeStr != "Network") return;
        // ★对手断线等待重连期间，锁定提示文字，不被"轮到你/思考中"覆盖
        if (opponentReconnecting) return;

        选择象棋 chess = 选择象棋.Instance;
        if (chess == null) return;

        if (chess.IsMyTurn)
        {
            对手状态Text.text = "轮到你走棋";
            对手状态Text.color = 正常色;
        }
        else
        {
            对手状态Text.text = "对手思考中...";
            对手状态Text.color = 警告色;
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  ★新增：对手断线等待重连提示（服务端 Reason 30/31 通知）
    // ════════════════════════════════════════════════════════════════
    /// <summary>show=true 显示"等待重连"提示；false 恢复正常状态显示</summary>
    public void ShowOpponentReconnecting(bool show, string message)
    {
        opponentReconnecting = show;
        if (对手状态Text == null) return;
        if (show)
        {
            对手状态Text.gameObject.SetActive(true);
            对手状态Text.text = string.IsNullOrEmpty(message) ? "对手断线，等待重连..." : message;
            对手状态Text.color = 警告色;
        }
        // false 时不主动改文字，下一帧 UpdateOpponentStatus 恢复正常刷新
    }

    // ════════════════════════════════════════════════════════════════
    //  ★新增：显示游戏结束面板（由 选择象棋.cs 调用）
    // ════════════════════════════════════════════════════════════════
    public void ShowGameOverPanel(string resultMessage)
    {
        if (游戏结束面板 != null)
        {
            游戏结束面板.SetActive(true);
        }
        Debug.Log($"[游戏界面UI] 游戏结束: {resultMessage}");
    }
}
