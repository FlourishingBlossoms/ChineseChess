/* ════════════════════════════════════════════════════════════════════
 *  选择象棋.cs  —  游戏核心：棋子选择、移动、规则判定、AI对弈、悔棋
 * ════════════════════════════════════════════════════════════════════
 *
 *  【这个脚本干什么？】
 *      游戏界面的核心控制器。处理玩家点击棋子→选中→点击目标→规则检测→
 *      移动动画→吃子的完整流程。还负责AI自动走棋和悔棋功能。
 *
 *  【Update() 每帧执行顺序】
 *      1. AI回合判断  → 如果该AI走，启动AIThinkCoroutine
 *      2. 移动动画    → 如果 AllowMove=true，移动棋子到目标位置
 *      3. 玩家点击    → 处理鼠标点击(选棋/走棋/吃子)
 *      4. UI警告计时  → 警告提示1秒后自动消失
 *
 *  【玩家点击流程】
 *      第一次点击：选中己方棋子 → 显示选择框
 *      第二次点击：
 *        ├─ 点空棋盘 → CheckMoveRules检测 → 合法则移动
 *        ├─ 点己方棋子 → 切换选中
 *        ├─ 点敌方棋子 → CheckMoveRules检测 → 合法则吃子
 *        └─ 点其他 → 取消选中
 *
 *  【Inspector 需要拖拽的字段】
 *      选择框 (GameObject):     选择框预制体(选中棋子时显示的高亮框)
 *      ChessBoardTransform:     棋盘物体的Transform(坐标转换用)
 *      WorringSign1 (GameObject): 规则非法警告UI
 *      WorringSign2 (GameObject): 回合非法警告UI
 *
 *  【外部脚本访问入口】
 *      选择象棋.Instance              → 单例引用
 *      选择象棋.IsRedTurn             → 当前是否红方回合(静态)
 *      选择象棋.Instance.UndoMove()   → 悔棋(游戏界面UI 的悔棋按钮调用)
 *      选择象棋.Instance.IsAIThinkingState → AI是否正在思考
 *
 *  【想加新功能从哪里改？】
 *      ▸ 加联机同步:  在 B1(点棋盘) 和 B2(吃子) 的 AllowMove=true 后，
 *                     添加 ChessNetwork.Instance.SendMove(...) 发送给对手
 *      ▸ 加新棋子类型: CheckMoveRules() 的 switch 里加 case
 *      ▸ 加将军提示:  在移动完成后(Update 第2步动画结束时)加将军检测
 *      ▸ 加计时器:    在 Update 里加倒计时逻辑，超时自动走棋
 *      ▸ 加音效:      在 AllowMove=true 时播放移动音效
 *
 *  【不要碰的地方】
 *      Update() 的4段顺序——它们有依赖关系，调换会出bug
 *      CheckMoveRules() 的"送将检测"逻辑——它防止玩家送将
 *      UndoMove() 的5步撤销顺序——顺序错了会复活错的棋子
 * ════════════════════════════════════════════════════════════════════ */

using System.Collections;
using System.IO;
using UnityEngine;
using static ToolManager;
using SocketGameProtocol;
using System.Collections.Generic;
public class 选择象棋 : MonoBehaviour
{
    // ==================== 单例模式 ====================
    public static 选择象棋 Instance { get; private set; }

    // ==================== 棋子选择相关变量 ====================
    private GameObject 选择棋子;          // 当前生成的高亮选择框实例
    public GameObject 选择框;             // 选择框预制体(Inspector拖拽)
    public int SelectedID = -1;           // 当前选中的棋子ID，-1=未选中
    public int SelectedID1 = -1;          // 移动动画中的临时ID(动画期间用的)
    public bool AllowMove = false;        // 是否正在播放移动动画
    private GameObject SelectedChessObj;  // 当前选中的棋子GameObject
    public Vector3 ClickPosition;         // 棋子移动目标(世界坐标)
    public Vector3 LocalTargetPos;        // 棋子移动目标(本地坐标)

    // ==================== 回合控制变量 ====================
    static public bool IsRedTurn = true;  // 当前是否红方回合(静态，全局可访问)
    public GameObject WorringSign1;       // 警告UI: 规则非法(走法不符合棋规)
    public GameObject WorringSign2;       // 警告UI: 回合非法(不是你的回合)
    private bool IsIllegal = false;       // 规则非法标志
    private bool IsIllegal2 = false;      // 回合非法标志
    private float TimeCounter = 0;        // 警告1计时器
    private float TimeCounter2 = 0;       // 警告2计时器

    // ==================== 棋盘与动画变量 ====================
    public Transform ChessBoardTransform;  // 棋盘Transform(Inspector拖拽，坐标转换用)
    private int pendingCaptureID = -1;     // 动画结束后要隐藏的被吃棋子ID

    // ==================== AI相关变量 ====================
    private bool isAIThinking = false;     // AI正在思考标志
    public bool IsAIThinkingState => isAIThinking;  // 只读属性供外部查询
    private float aiThinkDelay = 0.5f;     // AI思考延迟(秒)

    // ==================== 悔棋记录变量 ====================
    public BlackStepSave BlackStep = new BlackStepSave();
    public RedStepSave RedStep = new RedStepSave();
    static public bool LastMoveTurn;
    public bool LastMove;

    // ==================== 网络对战变量 ★新增 ====================
    public enum GameMode { AI, Local, Network }
    public enum PlayerColor { Red, Black }
    private GameMode gameMode = GameMode.AI;
    private PlayerColor myColor = PlayerColor.Red;
    private GameRequest gameRequest;
    /// <summary>网络模式下是否轮到本地玩家走棋</summary>
    public bool IsMyTurn => gameMode != GameMode.Network || (myColor == PlayerColor.Red) == IsRedTurn;
    private bool gameOver = false;

    // ★新增：回放模式标志
    public bool IsReplayMode = false;

    // ★新增：走棋计时器
    public const float MOVE_TIME_LIMIT = 30f;     // 每步限时30秒
    public float MyMoveTimer { get; private set; } = MOVE_TIME_LIMIT;  // 当前剩余时间
    public bool IsTimerRunning { get; private set; } = false;         // 计时器是否在跑
    private int lastTickSecond = -1;   // ★音效：滴答去重用（记录上次响的是第几秒）

    // ★新增：用于将死/困毙检测的评估器
    private ChessEvaluator checkmateEvaluator;

    /// <summary>黑方走棋记录(用于悔棋)</summary>
    public struct BlackStepSave
    {
        public int StartLogX; public int StartLogY; // 起点坐标
        public int LogX; public int LogY;           // 终点坐标
        public int ChessId;                         // 移动的棋子ID
        public bool IsKilled; public bool KillOthers; public int KilledId; // 吃子记录
        public void step(int startLogX, int startLogY, int logX, int logY, int chessId, bool isKilled, bool killOthers, int killedId)
        {
            StartLogX = startLogX; StartLogY = startLogY; LogX = logX; LogY = logY;
            ChessId = chessId; IsKilled = isKilled; KillOthers = killOthers; KilledId = killedId;
        }
    }

    /// <summary>红方走棋记录(用于悔棋)</summary>
    public struct RedStepSave
    {
        public int StartLogX; public int StartLogY;
        public int LogX; public int LogY;
        public int ChessId;
        public bool IsKilled; public bool KillOthers; public int KilledId;
        public void step(int startLogX, int startLogY, int logX, int logY, int chessId, bool isKilled, bool killOthers, int killedId)
        {
            StartLogX = startLogX; StartLogY = startLogY; LogX = logX; LogY = logY;
            ChessId = chessId; IsKilled = isKilled; KillOthers = killOthers; KilledId = killedId;
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  Awake() —— 初始化单例、回合、警告UI、选择框
    // ════════════════════════════════════════════════════════════════
    private void Awake()
    {
        Debug.Log("[选择象棋] ========== Awake 执行 ==========");
        Debug.Log($"[选择象棋] GameObject: {gameObject.name}, active: {gameObject.activeInHierarchy}");

        Instance = this;
        IsRedTurn = true;

        // ★调试：检查关键 Inspector 字段
        Debug.Log($"[选择象棋] Inspector引用: WorringSign1={(WorringSign1 != null ? "OK" : "NULL!")}, " +
                  $"WorringSign2={(WorringSign2 != null ? "OK" : "NULL!")}, " +
                  $"选择框={(选择框 != null ? "OK" : "NULL!")}, " +
                  $"ChessBoardTransform={(ChessBoardTransform != null ? "OK" : "NULL!")}");

        // ★新增：检测回放模式
        if (PlayerPrefs.HasKey("ReplayGameId"))
        {
            IsReplayMode = true;
            gameOver = true;  // 回放模式下禁止正常游戏逻辑
            Debug.Log($"[选择象棋] ★ 检测到 ReplayGameId={PlayerPrefs.GetInt("ReplayGameId")}，进入回放模式");
        }
        else
        {
            Debug.Log("[选择象棋] 未检测到 ReplayGameId，进入正常游戏模式");
        }

        // ★安全检查：WorringSign1/2 未赋值时不阻塞 Awake
        if (WorringSign1 != null) WorringSign1.SetActive(false);
        else Debug.LogWarning("[选择象棋] WorringSign1 未赋值，跳过");

        if (WorringSign2 != null) WorringSign2.SetActive(false);
        else Debug.LogWarning("[选择象棋] WorringSign2 未赋值，跳过");

        if (选择框 != null)
        {
            选择棋子 = Instantiate(选择框, Vector3.zero, Quaternion.identity);
            选择棋子.SetActive(false);
        }
        else
        {
            Debug.LogWarning("[选择象棋] 选择框(prefab) 未赋值，跳过实例化");
        }

        checkmateEvaluator = new ChessEvaluator(); // ★初始化将死检测评估器

        // 回放模式跳过游戏模式初始化（不需要AI/网络/双人）
        if (IsReplayMode)
        {
            Debug.Log("[选择象棋] 回放模式：跳过游戏模式初始化（AI/网络/双人），Awake 完成");
            return;
        }

        // ★新增：读取游戏模式
        string mode = PlayerPrefs.GetString("GameMode", "");
        if (mode == "Network")
        {
            gameMode = GameMode.Network;
            // ★修复：必须读进程级静态变量 GameFace.MyColor（RoomWaitUIControl 倒计时结束时设置）
            //   不能读 PlayerPrefs —— Windows 双开 .exe 共享注册表，后启动的会覆盖先启动的，
            //   且开局流程根本没人写 PlayerPrefs 的 MyColor → 两边都读到默认 "Red" → 双红方+黑方不翻转
            myColor = (GameFace.MyColor == "Black") ? PlayerColor.Black : PlayerColor.Red;
            gameRequest = gameObject.AddComponent<GameRequest>();
            gameRequest.OnResponseCallback = OnGameResponse;
            Debug.Log($"[选择象棋] 网络对战模式: MyColor={myColor}");
        }
        else if (PlayerPrefs.GetString("Difficulty", "") == "Double")
        {
            gameMode = GameMode.Local;
            Debug.Log("[选择象棋] 本地双人模式");
        }
        else
        {
            gameMode = GameMode.AI;
            Debug.Log("[选择象棋] AI对战模式");
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  Start() —— ★黑方视角翻转棋盘
    //
    //  原理：ChessManager.Awake() 创建棋子（子物体），选择象棋.Awake() 读模式。
    //  Start() 在两个 Awake 都跑完后执行，此时棋子已存在。
    //  旋转 ChessBoardTransform 180° → 红方在上、黑方在下。
    //  TransformPoint/InverseTransformPoint 自动适配，坐标转换不受影响。
    //  棋子精灵文字会倒转 → 逐个反旋转180°修正。
    // ════════════════════════════════════════════════════════════════
    private void Start()
    {
        if (gameMode == GameMode.Network && myColor == PlayerColor.Black)
        {
            FlipBoardForBlack();
        }

        // ★音效：进入对局场景 → 播放对局BGM（回放场景也会播，无碍）
        AudioManager.PlayBgm("game");
    }

    /// <summary>★黑方视角：翻转棋盘180°，棋子反向旋转保持文字正向</summary>
    private void FlipBoardForBlack()
    {
        if (ChessBoardTransform == null)
        {
            Debug.LogError("[选择象棋] ChessBoardTransform 未赋值，无法翻转棋盘！");
            return;
        }

        // 1. 旋转棋盘容器 180°（绕Z轴，2D视角下上下翻转）
        ChessBoardTransform.Rotate(0, 0, 180);

        // 2. 逐个反旋转棋子精灵，使文字保持正方向
        for (int i = 0; i < ChessManager.ChessArray.Length; i++)
        {
            if (ChessManager.ChessArray[i] != null && ChessManager.ChessArray[i].Obj != null)
            {
                ChessManager.ChessArray[i].Obj.transform.Rotate(0, 0, 180);
            }
        }

        Debug.Log("[选择象棋] 黑方视角：棋盘已翻转180°，黑方棋子在下");
    }

    // ════════════════════════════════════════════════════════════════
    //  Update() —— 每帧执行4个阶段(顺序不能调换！)
    // ════════════════════════════════════════════════════════════════
    private void Update()
    {
        // ★回放模式：跳过所有游戏逻辑
        if (IsReplayMode) return;

        // ★新增：走棋计时器 — 在所有早期返回之前执行，确保动画/AI期间也倒计时
        UpdateMoveTimer();

        // ========== 阶段1: AI回合判断 ==========
        // 如果当前是黑方回合 && 没有动画在播放 && AI没在思考 && 游戏未结束 → 启动AI
        // ★修复卡死：必须检查 !gameOver，否则AI无路可走后TriggerGameOver设了gameOver=true
        //   但Update不检查，下一帧又启动AIThinkCoroutine → 无限循环 → 卡死
        if (gameMode == GameMode.AI && !IsRedTurn && !AllowMove && !isAIThinking && !gameOver)
        {
            StartCoroutine(AIThinkCoroutine());
            return;  // 本帧不再处理玩家点击
        }

        // ========== 阶段2: 移动动画播放 ==========
        if (AllowMove)
        {
            // 向目标位置移动
            SelectedChessObj.transform.position = Vector3.MoveTowards(
                SelectedChessObj.transform.position, ClickPosition, Time.deltaTime * 10);

            // 到达目标
            if (Vector3.Distance(SelectedChessObj.transform.position, ClickPosition) < 0.1f)
            {
                SelectedChessObj.transform.position = ClickPosition;
                ChessManager.ChessArray[SelectedID1].Vec_X = LocalTargetPos.x;
                ChessManager.ChessArray[SelectedID1].Vec_Y = LocalTargetPos.y;

                // ★音效：在状态被清理前记录本步信息（走子颜色/是否吃子）
                bool sfxMoverRed = ChessManager.ChessArray[SelectedID1].Is_Red;
                bool sfxCapture = (pendingCaptureID != -1);

                // 动画结束时隐藏被吃的棋子(用SetActive(false)而非Destroy，配合悔棋)
                if (pendingCaptureID != -1)
                {
                    if (ChessManager.ChessArray[pendingCaptureID] != null &&
                        ChessManager.ChessArray[pendingCaptureID].Obj != null)
                    {
                        ChessManager.ChessArray[pendingCaptureID].Obj.SetActive(false);
                    }
                    pendingCaptureID = -1;
                }

                // 清理动画状态
                AllowMove = false;
                SelectedID = -1;
                SelectedID1 = -1;
                SelectedChessObj = null;

                // ★音效：落地声——吃子播capture，普通落子播move
                //   单机红方/本地黑方/联机对手/AI 的动画都走这里，一处覆盖全部
                AudioManager.PlaySfx(sfxCapture ? "capture" : "move");

                // ★新增：检测游戏结束(将帅被吃)
                CheckGameOver();
                // ★新增：将死/困毙检测 — 修复多人模式被将死后卡住
                CheckCheckmate();
                // ★音效：将军提示（游戏未结束时，刚走的一方将了对方）
                if (!gameOver) PlayCheckSoundIfCheck(sfxMoverRed);
            }
            return;  // 动画期间禁止玩家操作
        }

        // ========== 阶段3: 玩家鼠标点击 ==========
        if (Input.GetMouseButtonDown(0) && !gameOver)
        {
            // ★新增：网络模式下，不是你的回合就不让操作
            if (gameMode == GameMode.Network && !IsMyTurn)
                return;

            Vector2 MousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            RaycastHit2D hit = Physics2D.Raycast(MousePosition, Vector2.zero);

            if (hit.collider != null)
            {
                // --- 检查：已选中的棋子是否是当前回合的 ---
                if (SelectedID != -1 && WhichTurn(SelectedID) == false)
                {
                    IsIllegal2 = true; WorringSign2.SetActive(true);
                    SelectedID = -1; 选择棋子.SetActive(false);
                    return;
                }

                // --- 情况A：当前没有选中棋子 → 选中一个 ---
                if (SelectedID == -1)
                {
                    if (hit.transform.tag == "红棋" || hit.transform.tag == "黑棋")
                    {
                        SelectedID = ToolManager.GetChessId(hit.transform.gameObject);
                        SelectedChessObj = hit.transform.gameObject;
                        选择棋子.transform.position = hit.transform.position;
                        选择棋子.SetActive(true);

                        // 二次确认：选的棋子必须是当前回合方的
                        if (SelectedID != -1 &&
                            ChessManager.ChessArray[SelectedID] != null &&
                            ChessManager.ChessArray[SelectedID].Is_Red != IsRedTurn)
                        {
                            IsIllegal2 = true; WorringSign2.SetActive(true);
                            SelectedID = -1; SelectedChessObj = null;
                            选择棋子.SetActive(false);
                            return;
                        }

                        // ★音效：选中棋子成功
                        AudioManager.PlaySfx("select");
                    }
                }
                // --- 情况B：已经选中了棋子 → 尝试移动/吃子/换选 ---
                else
                {
                    选择棋子.SetActive(false);

                    // ---- B1: 点击棋盘空位 → 普通移动 ----
                    if (hit.transform.tag == "棋盘")
                    {
                        Vector3 localTargetPos = ChessBoardTransform.InverseTransformPoint(hit.point);
                        if (CheckMoveRules(SelectedID, localTargetPos.x, localTargetPos.y, -1))
                        {
                            AllowMove = true;

                            // 坐标转换：本地坐标 → 逻辑坐标
                            int targetLogY = ToolManager.ChangeToLineY(localTargetPos.y);
                            int targetLogX = ToolManager.ChangeToLineX(localTargetPos.x);
                            int startLogX = ToolManager.ChangeToLineX(ChessManager.ChessArray[SelectedID].Vec_X);
                            int startLogY = ToolManager.ChangeToLineY(ChessManager.ChessArray[SelectedID].Vec_Y);

                            // 记录步骤(供悔棋用)
                            SaveStep(IsRedTurn, startLogX, startLogY, targetLogX, targetLogY, SelectedID, false, -1);

                            // 逻辑坐标 → 本地坐标(居中) → 世界坐标(动画目标)
                            float centeredX = ToolManager.ChangeBackX(targetLogX);
                            float centeredY = ToolManager.ChangeBackY(targetLogY);
                            LocalTargetPos = new Vector3(centeredX, centeredY, 0);
                            ClickPosition = ChessBoardTransform.TransformPoint(new Vector3(centeredX, centeredY, -2f));

                            SelectedID1 = SelectedID;
                            SelectedID = -1;
                            IsRedTurn = !IsRedTurn;  // 切换回合

                            // ★新增：网络模式发送走棋
                            if (gameMode == GameMode.Network && gameRequest != null)
                            {
                                int playerColor = (myColor == PlayerColor.Red) ? 1 : 2;
                                gameRequest.SendMove(startLogX, startLogY, targetLogX, targetLogY, SelectedID1, -1, playerColor);
                            }
                        }
                        else
                        {
                            IsIllegal = true; WorringSign1.SetActive(true); SelectedID = -1;
                        }
                    }
                    // ---- B2: 点击了棋子 → 吃子或换选 ----
                    else if (hit.transform.tag == "红棋" || hit.transform.tag == "黑棋")
                    {
                        int targetChessId = ToolManager.GetChessId(hit.transform.gameObject);

                        // 点的是自己人 → 切换选中
                        if (ChessManager.ChessArray[SelectedID].Is_Red == ChessManager.ChessArray[targetChessId].Is_Red)
                        {
                            SelectedID = targetChessId;
                            SelectedChessObj = hit.transform.gameObject;
                            选择棋子.transform.position = hit.transform.position;
                            选择棋子.SetActive(true);
                            // ★音效：切换选中
                            AudioManager.PlaySfx("select");
                            return;
                        }

                        // 点的是敌人 → 尝试吃子
                        Vector3 localTargetPos = ChessBoardTransform.InverseTransformPoint(hit.point);
                        if (CheckMoveRules(SelectedID, localTargetPos.x, localTargetPos.y, targetChessId))
                        {
                            // 标记死亡，但不Destroy，等动画结束再隐藏(配合悔棋复活)
                            ChessManager.ChessArray[targetChessId].Is_Dead = true;
                            pendingCaptureID = targetChessId;
                            AllowMove = true;

                            int targetLogY = ToolManager.ChangeToLineY(localTargetPos.y);
                            int targetLogX = ToolManager.ChangeToLineX(localTargetPos.x);
                            int startLogX = ToolManager.ChangeToLineX(ChessManager.ChessArray[SelectedID].Vec_X);
                            int startLogY = ToolManager.ChangeToLineY(ChessManager.ChessArray[SelectedID].Vec_Y);

                            SaveStep(IsRedTurn, startLogX, startLogY, targetLogX, targetLogY, SelectedID, true, targetChessId);

                            float centeredX = ToolManager.ChangeBackX(targetLogX);
                            float centeredY = ToolManager.ChangeBackY(targetLogY);
                            LocalTargetPos = new Vector3(centeredX, centeredY, 0);
                            ClickPosition = ChessBoardTransform.TransformPoint(new Vector3(centeredX, centeredY, -2f));

                            SelectedID1 = SelectedID;
                            SelectedID = -1;
                            IsRedTurn = !IsRedTurn;

                            // ★新增：网络模式发送吃子
                            if (gameMode == GameMode.Network && gameRequest != null)
                            {
                                int playerColor = (myColor == PlayerColor.Red) ? 1 : 2;
                                gameRequest.SendMove(startLogX, startLogY, targetLogX, targetLogY, SelectedID1, targetChessId, playerColor);
                            }
                        }
                        else
                        {
                            IsIllegal = true; WorringSign1.SetActive(true); SelectedID = -1;
                        }
                    }
                    else
                    {
                        SelectedID = -1;  // 点了其他东西，取消选中
                    }
                }
            }
        }

        // ========== 阶段4: UI警告计时器 ==========
        if (IsIllegal)
        {
            TimeCounter += Time.deltaTime;
            if (TimeCounter >= 1) { WorringSign1.SetActive(false); TimeCounter = 0; IsIllegal = false; }
        }
        else if (IsIllegal2)
        {
            TimeCounter2 += Time.deltaTime;
            if (TimeCounter2 >= 1) { WorringSign2.SetActive(false); TimeCounter2 = 0; IsIllegal2 = false; }
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  回合判定与UI清理
    // ════════════════════════════════════════════════════════════════

    /// <summary>检查 SelectedID 棋子是否属于当前回合方</summary>
    public bool WhichTurn(int SelectedID)
    {
        if (ChessManager.ChessArray[SelectedID].Is_Red == IsRedTurn) { IsIllegal = false; return true; }
        else { IsIllegal = true; return false; }
    }

    /// <summary>清除选择框UI</summary>
    public void ClearSelectionUI()
    {
        SelectedID = -1;
        选择棋子.SetActive(false);
    }

    // ════════════════════════════════════════════════════════════════
    //  规则检测 —— 先检测基础棋规，再检测送将
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 检测走法是否合法。
    /// 流程：1.按棋子类型调用规则检测 → 2.基础规则通过后再做送将预检
    /// </summary>
    /// <param name="selectedId">要移动的棋子ID</param>
    /// <param name="VecX">目标X(本地坐标)</param>
    /// <param name="VecY">目标Y(本地坐标)</param>
    /// <param name="destroyId">被吃的棋子ID(无吃子传-1)</param>
    /// <returns>true=合法, false=非法</returns>
    public static bool CheckMoveRules(int selectedId, float VecX, float VecY, int destroyId)
    {
        int targetLogX = ToolManager.ChangeToLineX(VecX);
        int targetLogY = ToolManager.ChangeToLineY(VecY);
        ChessManager.Chess chess = ChessManager.ChessArray[selectedId];
        bool ruleValid = false;

        // 1. 基础规则检测(车马炮象士帅卒)
        switch (chess.Type)
        {
            case ChessManager.Chess.ChessType.车: ruleValid = 规则检测.车(selectedId, targetLogY, targetLogX, destroyId); break;
            case ChessManager.Chess.ChessType.马: ruleValid = 规则检测.马(selectedId, targetLogY, targetLogX, destroyId); break;
            case ChessManager.Chess.ChessType.炮: ruleValid = 规则检测.炮(selectedId, targetLogY, targetLogX, destroyId); break;
            case ChessManager.Chess.ChessType.象: ruleValid = 规则检测.相(selectedId, targetLogY, targetLogX, destroyId); break;
            case ChessManager.Chess.ChessType.士: ruleValid = 规则检测.士(selectedId, targetLogY, targetLogX, destroyId); break;
            case ChessManager.Chess.ChessType.帅: ruleValid = 规则检测.将(selectedId, targetLogY, targetLogX, destroyId); break;
            case ChessManager.Chess.ChessType.卒: ruleValid = 规则检测.兵(selectedId, targetLogY, targetLogX, destroyId); break;
            default: return false;
        }

        // 2. 送将预检：基础规则通过后，模拟走一步看自己是否被将军
        if (ruleValid)
        {
            bool isRedTurn = ChessManager.ChessArray[selectedId].Is_Red;
            bool isCheck = 规则检测.送将检测(selectedId, targetLogX, targetLogY, isRedTurn, destroyId);
            if (isCheck)
            {
                return false;  // 送将，走法无效
            }
        }

        return ruleValid;  // 基础规则+送将检测均通过
    }

    // ════════════════════════════════════════════════════════════════
    //  悔棋系统
    // ════════════════════════════════════════════════════════════════

    /// <summary>检测是否发生回合切换(目前未使用，保留)</summary>
    public bool IsMove()
    {
        if (选择象棋.IsRedTurn != LastMove)
        {
            LastMove = 选择象棋.IsRedTurn;
            LastMoveTurn = !LastMoveTurn;
            return true;
        }
        return false;
    }

    /// <summary>记录一步棋(每次走棋后调用)</summary>
    public void SaveStep(bool isRed, int startLogX, int startLogY, int targetLogX, int targetLogY, int chessId, bool killOthers, int killedId)
    {
        if (isRed)
        {
            RedStep.step(startLogX, startLogY, targetLogX, targetLogY, chessId, false, killOthers, killedId);
            Debug.Log("红方走：" + RedStep.StartLogX + "," + RedStep.StartLogY + " -> " + RedStep.LogX + "," + RedStep.LogY);
        }
        else
        {
            BlackStep.step(startLogX, startLogY, targetLogX, targetLogY, chessId, false, killOthers, killedId);
            Debug.Log("黑方走：" + BlackStep.StartLogX + "," + BlackStep.StartLogY + " -> " + BlackStep.LogX + "," + BlackStep.LogY);
        }
    }

    /// <summary>
    /// 悔棋：同时撤销红黑各一步(由游戏界面UI的悔棋按钮调用)
    /// 流程：1.防御检查 → 2.撤销红方移动 → 3.撤销黑方移动 →
    ///       4.复活红方被吃的子 → 5.复活黑方被吃的子 → 6.清理UI
    /// </summary>
    public void UndoMove()
    {
        // 防御：动画播放中 或 AI思考中 禁止悔棋
        if (Instance.AllowMove || Instance.IsAIThinkingState) return;
        // 必须双方都有记录才能同时撤销
        if (RedStep.ChessId == -1 || BlackStep.ChessId == -1) return;

        // 1. 撤销红方棋子移动(回到起点)
        ChessManager.Chess redChess = ChessManager.ChessArray[RedStep.ChessId];
        redChess.Obj.transform.localPosition = new Vector3(ChangeBackX(RedStep.StartLogX), ChangeBackY(RedStep.StartLogY), -2f);
        redChess.Vec_X = redChess.Obj.transform.localPosition.x;
        redChess.Vec_Y = redChess.Obj.transform.localPosition.y;

        // 2. 撤销黑方棋子移动(回到起点)
        ChessManager.Chess blackChess = ChessManager.ChessArray[BlackStep.ChessId];
        blackChess.Obj.transform.localPosition = new Vector3(ChangeBackX(BlackStep.StartLogX), ChangeBackY(BlackStep.StartLogY), -2f);
        blackChess.Vec_X = blackChess.Obj.transform.localPosition.x;
        blackChess.Vec_Y = blackChess.Obj.transform.localPosition.y;

        // 3. 复活红方被吃的子(如果有)
        if (RedStep.KillOthers && RedStep.KilledId != -1)
        {
            ChessManager.Chess capturedRed = ChessManager.ChessArray[RedStep.KilledId];
            capturedRed.Is_Dead = false;
            capturedRed.Obj.transform.localPosition = new Vector3(ChangeBackX(RedStep.LogX), ChangeBackY(RedStep.LogY), -2f);
            capturedRed.Obj.SetActive(true);
        }

        // 4. 复活黑方被吃的子(如果有)
        if (BlackStep.KillOthers && BlackStep.KilledId != -1)
        {
            ChessManager.Chess capturedBlack = ChessManager.ChessArray[BlackStep.KilledId];
            capturedBlack.Is_Dead = false;
            capturedBlack.Obj.transform.localPosition = new Vector3(ChangeBackX(BlackStep.LogX), ChangeBackY(BlackStep.LogY), -2f);
            capturedBlack.Obj.SetActive(true);
        }

        // 5. 清理UI状态并重置记录
        Instance.ClearSelectionUI();
        RedStep.ChessId = -1;
        BlackStep.ChessId = -1;
    }

    // ════════════════════════════════════════════════════════════════
    //  ★新增：网络对战系统
    // ════════════════════════════════════════════════════════════════

    /// <summary>网络响应回调（由 GameRequest.OnResponse 调用）</summary>
    private void OnGameResponse(MainPack pack)
    {
        // ★修改：GameOver 后也允许处理 Surrender（再来一局）响应
        if (gameOver && pack.Actioncode != ActionCode.Surrender) return;

        switch (pack.Actioncode)
        {
            case ActionCode.Move:
                if (pack.Movepack != null)
                    OnOpponentMove(pack.Movepack);
                break;

            case ActionCode.GameOver:
                if (pack.Gameoverpack != null)
                    OnGameOver(pack.Gameoverpack);
                break;

            case ActionCode.Surrender:
                // ★再来一局：转发给 死亡检查多人版 处理
                if (pack.Gameoverpack != null)
                    OnRematchResponse(pack);
                break;
        }
    }

    /// <summary>收到对手走棋，在本地棋盘执行对手的移动</summary>
    public void OnOpponentMove(MovePack pack)
    {
        if (gameOver) return;

        int fromX = pack.FromX, fromY = pack.FromY;
        int toX = pack.ToX, toY = pack.ToY;
        int chessId = pack.ChessId;
        int captureId = pack.CaptureId;

        // ★跳过走棋：对手超时发了 ChessId=-1 的 Move → 不移动棋子，直接切换到我的回合
        if (chessId == -1)
        {
            Debug.Log("[选择象棋] 收到对手跳过走棋（超时），轮到我了");
            SkipCurrentTurn();   // 切回我的回合并重置计时器
            return;
        }

        Debug.Log($"[选择象棋] 收到对手走棋: chessId={chessId} ({fromX},{fromY})→({toX},{toY}) capture={captureId}");

        ChessManager.Chess chess = ChessManager.ChessArray[chessId];
        if (chess == null || chess.Is_Dead)
        {
            Debug.LogError($"[选择象棋] 对手走棋失败: 棋子 {chessId} 无效或已死亡");
            return;
        }

        // 清除本地玩家的选择状态（如果有的话）
        if (SelectedID != -1)
        {
            SelectedID = -1;
            if (选择棋子 != null) 选择棋子.SetActive(false);
        }

        SelectedChessObj = chess.Obj;
        SelectedID1 = chessId;

        // 处理吃子
        if (captureId >= 0 && captureId < 32)
        {
            ChessManager.Chess captured = ChessManager.ChessArray[captureId];
            if (captured != null && !captured.Is_Dead)
            {
                captured.Is_Dead = true;
                pendingCaptureID = captureId;
            }
        }

        // 计算目标坐标
        LocalTargetPos = new Vector3(ToolManager.ChangeBackX(toX), ToolManager.ChangeBackY(toY), 0);
        Vector3 worldPos = ChessBoardTransform.TransformPoint(LocalTargetPos);
        ClickPosition = new Vector3(worldPos.x, worldPos.y, -2f);

        // 记录步骤(对手走的是对方颜色)
        SaveStep(!IsRedTurn, fromX, fromY, toX, toY, chessId, captureId >= 0, captureId);

        // 更新逻辑坐标
        chess.Vec_X = LocalTargetPos.x;
        chess.Vec_Y = LocalTargetPos.y;

        // 启动动画 + 切换回合
        AllowMove = true;
        IsRedTurn = !IsRedTurn;

        // ★音效：走棋音在 Update() 阶段2 动画到达时统一播放（本地/联机/AI同一处，不会重复）
    }

    /// <summary>检测游戏是否结束（将帅被吃）</summary>
    private void CheckGameOver()
    {
        if (gameOver) return;

        // 找红帅和黑将（ChessType.帅 同时用于红帅和黑将，用 Is_Red 区分）
        int redKingId = -1, blackKingId = -1;
        for (int i = 0; i < 32; i++)
        {
            if (ChessManager.ChessArray[i] == null) continue;
            if (ChessManager.ChessArray[i].Type == ChessManager.Chess.ChessType.帅)
            {
                if (ChessManager.ChessArray[i].Is_Red)
                    redKingId = i;
                else
                    blackKingId = i;
            }
        }

        if (redKingId != -1 && ChessManager.ChessArray[redKingId].Is_Dead)
        {
            // 红帅被吃，黑胜
            TriggerGameOver(2, 1, "红帅被吃");
        }
        else if (blackKingId != -1 && ChessManager.ChessArray[blackKingId].Is_Dead)
        {
            // 黑将被吃，红胜
            TriggerGameOver(1, 1, "黑将被吃");
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  ★新增：将死/困毙检测
    //  在每次走棋动画结束后调用，检查当前轮到的玩家是否还有合法走法
    //  如果没有 → 被将死或困毙 → 对方胜
    //  修复多人模式被将死后回合卡住无法结算的问题
    // ════════════════════════════════════════════════════════════════
    private void CheckCheckmate()
    {
        if (gameOver) return;

        // 检查当前轮到的玩家是否有合法走法
        // IsRedTurn=true → 红方走 → forBlack=false
        // IsRedTurn=false → 黑方走 → forBlack=true
        List<AIMove> legalMoves = checkmateEvaluator.GenerateAllLegalMoves(forBlack: !IsRedTurn);

        if (legalMoves.Count == 0)
        {
            // 当前轮到的玩家无路可走，对方胜
            int winner = IsRedTurn ? 2 : 1; // 红方无路→黑胜(2)，黑方无路→红胜(1)
            gameOver = true;
            Debug.Log($"[选择象棋] 将死/困毙！{(IsRedTurn ? "红方" : "黑方")}无路可走，{(winner == 1 ? "红" : "黑")}方胜");

            // 网络模式：通知服务端
            if (gameMode == GameMode.Network && gameRequest != null)
            {
                gameRequest.SendGameOver(winner, 4, $"{(IsRedTurn ? "红方" : "黑方")}无路可走");
            }

            // 交给死亡检查/死亡检查多人版处理结算界面
            死亡检查 dc = FindObjectOfType<死亡检查>();
            if (dc != null)
            {
                dc.ShowResult(winner);
            }
            else
            {
                死亡检查多人版 dcm = FindObjectOfType<死亡检查多人版>();
                if (dcm != null) dcm.ShowResult(winner);
            }
        }
    }

    /// <summary>触发游戏结束</summary>
    private void TriggerGameOver(int winner, int reason, string msg)
    {
        if (gameOver) return;
        gameOver = true;

        Debug.Log($"[选择象棋] 游戏结束: winner={winner}, reason={reason}, msg={msg}");

        // 网络模式：通知服务端（服务端转发给对手）
        if (gameMode == GameMode.Network && gameRequest != null)
        {
            gameRequest.SendGameOver(winner, reason, msg);
        }

        // 本地显示游戏结束
        OnGameOverInternal(winner, reason, msg);
    }

    /// <summary>收到服务端的游戏结束通知（对手触发的）</summary>
    public void OnGameOver(GameOverPack pack)
    {
        int reason = pack.Reason;

        // ★修复：30/31 是"对手断线/重连"状态通知，不是终局，绝不能设 gameOver
        //   （原来不分 Reason 直接结算，导致对手临时退出就误弹结算、
        //    且 gameOver=true 后真正的判负通知(reason=3)和重连通知(reason=31)被 OnGameResponse 拦截）
        if (reason == 30)
        {
            Debug.Log($"[选择象棋] 对手断线，等待重连（60秒）: {pack.Message}");
            if (游戏界面UI.Instance != null)
                游戏界面UI.Instance.ShowOpponentReconnecting(true, pack.Message);
            return;
        }
        if (reason == 31)
        {
            Debug.Log("[选择象棋] 对手已重连，继续对局");
            if (游戏界面UI.Instance != null)
                游戏界面UI.Instance.ShowOpponentReconnecting(false, "");
            return;
        }

        // ↓ 以下是真正的终局通知（reason=3 断线判负 / 正常将死等）
        if (gameOver) return;
        gameOver = true;

        // 收起可能还挂着的"等待重连"提示
        if (游戏界面UI.Instance != null)
            游戏界面UI.Instance.ShowOpponentReconnecting(false, "");

        // ★修复：交给死亡检查/死亡检查多人版处理结算界面（网络场景优先多人版）
        死亡检查多人版 dcm = FindObjectOfType<死亡检查多人版>();
        if (dcm != null)
        {
            dcm.ShowResult(pack.Winner);
            return;
        }
        死亡检查 dc = FindObjectOfType<死亡检查>();
        if (dc != null) dc.ShowResult(pack.Winner);
    }

    /// <summary>游戏结束内部处理</summary>
    private void OnGameOverInternal(int winner, int reason, string msg)
    {
        string winnerText = (winner == 1) ? "红方" : "黑方";
        string reasonText;
        switch (reason)
        {
            case 1: reasonText = "将帅被吃"; break;
            case 2: reasonText = "对方投降"; break;
            case 3: reasonText = "对方断线"; break;
            case 4: reasonText = "将死/困毙"; break;
            default: reasonText = "未知原因"; break;
        }

        string result = $"{winnerText}胜！({reasonText})";
        if (gameMode == GameMode.Network)
        {
            bool iWon = (myColor == PlayerColor.Red && winner == 1) ||
                        (myColor == PlayerColor.Black && winner == 2);
            result = (iWon ? "你赢了！" : "你输了！") + result;
        }

        Debug.Log($"[选择象棋] 游戏结束: {result}");

        // 显示游戏结束面板
        if (游戏界面UI.Instance != null)
        {
            游戏界面UI.Instance.ShowGameOverPanel(result);
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  ★新增：再来一局（Rematch）系统
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// ★新增：通知服务器"我要离开对局"（ESC暂停→返回大厅 / 退出游戏 前调用）。
    /// 对局中退出 → 服务端给对手发判负结算；已结算退出 → 正常清理房间。
    /// 不发的话对手会永远卡在对局界面（连接还活着，服务器不认为你走了）。
    /// </summary>
    public void NotifyServerLeaveGame()
    {
        if (gameMode != GameMode.Network) return;
        if (gameRequest == null) return;
        gameRequest.SendLeaveRoomFromGame();
    }

    /// <summary>收到再来一局响应（请求/接受/拒绝/失败）</summary>
    private void OnRematchResponse(MainPack pack)
    {
        Debug.Log($"[选择象棋] 再来一局响应: ReturnCode={pack.Returncode}, Reason={pack.Gameoverpack?.Reason}, Msg={pack.Gameoverpack?.Message}");

        死亡检查多人版 dcm = FindObjectOfType<死亡检查多人版>();
        if (dcm == null)
        {
            Debug.LogError("[选择象棋] 找不到 死亡检查多人版");
            return;
        }

        // ★自己请求失败（对手已离开等）→ 当作拒绝处理
        if (pack.Returncode == ReturnCode.Fail)
        {
            string msg = pack.Gameoverpack?.Message;
            if (string.IsNullOrEmpty(msg)) msg = "对手已离开";
            dcm.OnRematchRejected(msg);
            return;
        }

        if (pack.Gameoverpack == null) return;
        int reason = pack.Gameoverpack.Reason;

        switch (reason)
        {
            case 10:
                // 对手发来再来一局请求 → 显示邀请弹窗
                dcm.ShowRematchInvitation(pack.Gameoverpack.Message);
                break;

            case 11:
                // 对手接受了再来一局 → 服务端会发 StartGame，这里只更新UI
                dcm.OnRematchAccepted();
                break;

            case 12:
                // 对手拒绝了再来一局 → 显示拒绝弹窗，按钮变灰
                dcm.OnRematchRejected(pack.Gameoverpack.Message);
                break;
        }
    }

    /// <summary>发送再来一局请求（由 死亡检查多人版 调用）</summary>
    public void RequestRematch()
    {
        if (gameRequest != null)
            gameRequest.SendRematchRequest();
    }

    /// <summary>接受再来一局（由 死亡检查多人版 调用）</summary>
    public void AcceptRematch()
    {
        if (gameRequest != null)
            gameRequest.SendRematchAccept();
        // ★服务端会向双方都发 Reason=11 通知，收到后 OnRematchAccepted → 重载场景
        // 如果对手已离开，服务端返回 Fail → OnRematchRejected 处理
    }

    /// <summary>拒绝再来一局（由 死亡检查多人版 调用）</summary>
    public void RejectRematch()
    {
        if (gameRequest != null)
            gameRequest.SendRematchReject();
    }

    /// <summary>★重载游戏场景（再来一局时调用，保持颜色不变）</summary>
    public void ReloadGameScene()
    {
        // 确保 GameMode 仍在 PlayerPrefs 中（Awake 会读取）
        // 颜色不用写回：GameFace.MyColor 是静态变量，同进程内跨场景存活，重载后 Awake 直接读它
        if (gameMode == GameMode.Network)
        {
            PlayerPrefs.SetString("GameMode", "Network");
            PlayerPrefs.Save();
        }
        Time.timeScale = 1f;
        Debug.Log("[选择象棋] 重载游戏场景（再来一局）");
        SceneLoader.LoadScene("ChessGame");
    }

    // ════════════════════════════════════════════════════════════════
    //  ★新增：走棋计时器系统
    //  每步限时30秒，超时跳过本轮、轮到对方走棋（不再判负）
    // ════════════════════════════════════════════════════════════════

    /// <summary>每帧更新走棋计时器</summary>
    private void UpdateMoveTimer()
    {
        if (gameOver) { IsTimerRunning = false; return; }

        // ★音效：最后5秒每秒一声滴答
        //   计时器刚被重置为满值时重新起算（MyMoveTimer回到MOVE_TIME_LIMIT的那一刻）
        if (MyMoveTimer >= MOVE_TIME_LIMIT - 0.0001f) lastTickSecond = -1;
        if (IsTimerRunning && MyMoveTimer > 0f && MyMoveTimer <= 5f)
        {
            int tickSec = Mathf.CeilToInt(MyMoveTimer);
            if (tickSec != lastTickSecond)
            {
                lastTickSecond = tickSec;
                AudioManager.PlaySfx("tick");
            }
        }

        // Network 模式：只在本地玩家回合计时
        if (gameMode == GameMode.Network)
        {
            if (IsMyTurn && !AllowMove)
            {
                if (!IsTimerRunning)
                {
                    IsTimerRunning = true;
                    MyMoveTimer = MOVE_TIME_LIMIT;
                }
                MyMoveTimer -= Time.deltaTime;
                if (MyMoveTimer <= 0)
                {
                    MyMoveTimer = 0;
                    IsTimerRunning = false;
                    OnMoveTimeout();
                }
            }
            else
            {
                IsTimerRunning = false;
            }
        }
        // AI 模式：只在玩家（红方）回合计时
        else if (gameMode == GameMode.AI)
        {
            if (IsRedTurn && !AllowMove && !isAIThinking)
            {
                if (!IsTimerRunning)
                {
                    IsTimerRunning = true;
                    MyMoveTimer = MOVE_TIME_LIMIT;
                }
                MyMoveTimer -= Time.deltaTime;
                if (MyMoveTimer <= 0)
                {
                    MyMoveTimer = 0;
                    IsTimerRunning = false;
                    OnMoveTimeout();
                }
            }
            else
            {
                IsTimerRunning = false;
            }
        }
        // Local 模式：双方都计时
        else if (gameMode == GameMode.Local)
        {
            if (!AllowMove)
            {
                if (!IsTimerRunning)
                {
                    IsTimerRunning = true;
                    MyMoveTimer = MOVE_TIME_LIMIT;
                }
                MyMoveTimer -= Time.deltaTime;
                if (MyMoveTimer <= 0)
                {
                    MyMoveTimer = 0;
                    IsTimerRunning = false;
                    OnMoveTimeout();
                }
            }
            else
            {
                IsTimerRunning = false;
            }
        }
    }

    /// <summary>走棋超时处理 —— 跳过本轮，把回合让给对方（不再判负）</summary>
    private void OnMoveTimeout()
    {
        if (gameOver) return;

        string skippedSide = IsRedTurn ? "红方" : "黑方";
        Debug.Log($"[选择象棋] 走棋超时！{skippedSide}跳过本轮，轮到对方");

        // 网络模式：通知对手"我跳过了"，对手收到后切换到他的回合
        if (gameMode == GameMode.Network && gameRequest != null)
        {
            // 复用 Move 通道，ChessId=-1 表示"跳过"，不移动任何棋子
            gameRequest.SendMove(-1, -1, -1, -1, -1, -1, (myColor == PlayerColor.Red) ? 1 : 2);
        }

        // 本地切换回合（网络模式下对方收到跳过包后也会再次切换，因此这里切换一次即可）
        SkipCurrentTurn();
    }

    /// <summary>跳过当前回合：不移动棋子，直接把回合交给对方</summary>
    private void SkipCurrentTurn()
    {
        if (gameOver) return;

        // 清理可能残留的选择状态
        if (SelectedID != -1)
        {
            SelectedID = -1;
            if (选择棋子 != null) 选择棋子.SetActive(false);
        }

        // 切换回合
        IsRedTurn = !IsRedTurn;
        MyMoveTimer = MOVE_TIME_LIMIT;   // 重置计时器（下一回合从满开始）
        IsTimerRunning = false;          // 由 UpdateMoveTimer 在合适时机重新启动
    }

    /// <summary>重置计时器（每次切换回合时由外部调用）</summary>
    public void ResetMoveTimer()
    {
        MyMoveTimer = MOVE_TIME_LIMIT;
        IsTimerRunning = false;  // 下一帧 UpdateMoveTimer 会重新启动
    }

    // ════════════════════════════════════════════════════════════════
    //  AI 系统
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// AI思考协程：等待延迟 → 获取最佳走法 → 执行移动
    /// 由 Update 阶段1 触发
    /// </summary>
    private IEnumerator AIThinkCoroutine()
    {
        isAIThinking = true;
        Debug.Log("[AI] 开始思考...");
        yield return new WaitForSeconds(aiThinkDelay);

        // 调用 AIManager 获取最佳走法
        AIMove bestMove = AIManager.Instance.GetBestMove();

        if (!bestMove.IsValid)
        {
            Debug.Log("[AI] 没有有效走法，AI被将死或困毙");
            isAIThinking = false;
            // AI（黑方）无路可走 → 红方胜
            // 直接调用 CheckCheckmate 会因为 IsRedTurn 还是 false（黑方回合）而正确判断
            gameOver = true;
            死亡检查 dc = FindObjectOfType<死亡检查>();
            if (dc != null) dc.ShowResult(1);
            else
            {
                死亡检查多人版 dcm = FindObjectOfType<死亡检查多人版>();
                if (dcm != null) dcm.ShowResult(1);
            }
            yield break;
        }

        yield return ExecuteAIMove(bestMove);
        isAIThinking = false;
        Debug.Log("[AI] 思考完成，回合切换");
    }

    /// <summary>
    /// AI执行移动动画
    /// 流程：1.校验棋子 → 2.合法性安全网(防AI送将) → 3.吃子标记 →
    ///       4.计算坐标 → 5.记录步骤 → 6.播放动画 → 7.清理状态
    /// </summary>
    private IEnumerator ExecuteAIMove(AIMove move)
    {
        // 1. 校验棋子有效性
        ChessManager.Chess chess = ChessManager.ChessArray[move.FromChessId];
        if (chess == null || chess.Is_Dead)
        {
            Debug.LogError("[AI] 无效的棋子ID");
            yield break;
        }

        // 2. 计算目标本地坐标
        LocalTargetPos = new Vector3(ToolManager.ChangeBackX(move.ToLogX), ToolManager.ChangeBackY(move.ToLogY), 0);

        // 3. 安全网：用 CheckMoveRules 验证 AI走步(包含送将检测)
        int captureId = (move.CaptureId >= 0 && move.CaptureId < 32) ? move.CaptureId : -1;
        if (!CheckMoveRules(move.FromChessId, LocalTargetPos.x, LocalTargetPos.y, captureId))
        {
            Debug.LogError("[AI] 尝试走出非法棋步(可能是送将)！已拦截。");
            IsRedTurn = true;  // 强制换回红方，防止游戏卡死
            yield break;
        }

        SelectedChessObj = chess.Obj;

        // 4. 处理吃子标记(标记死亡，等动画结束再隐藏)
        if (move.CaptureId >= 0 && move.CaptureId < 32)
        {
            ChessManager.Chess captured = ChessManager.ChessArray[move.CaptureId];
            if (captured != null && !captured.Is_Dead)
            {
                captured.Is_Dead = true;
                pendingCaptureID = move.CaptureId;
            }
        }

        // 5. 计算目标世界坐标
        Vector3 worldPos = ChessBoardTransform.TransformPoint(LocalTargetPos);
        ClickPosition = new Vector3(worldPos.x, worldPos.y, -2f);

        // 6. 记录步骤
        int startLogX = ToolManager.ChangeToLineX(chess.Vec_X);
        int startLogY = ToolManager.ChangeToLineY(chess.Vec_Y);
        bool isCapture = (move.CaptureId >= 0 && move.CaptureId < 32);
        SaveStep(IsRedTurn, startLogX, startLogY, move.ToLogX, move.ToLogY, move.FromChessId, isCapture, move.CaptureId);

        // 7. 提前更新逻辑坐标(防止路径判断出错)
        chess.Vec_X = LocalTargetPos.x;
        chess.Vec_Y = LocalTargetPos.y;
        AllowMove = true;
        SelectedID1 = move.FromChessId;

        // 8. AI独立运行移动动画(避免与Update冲突)
        while (AllowMove)
        {
            SelectedChessObj.transform.position = Vector3.MoveTowards(
                SelectedChessObj.transform.position, ClickPosition, Time.deltaTime * 10);

            if (Vector3.Distance(SelectedChessObj.transform.position, ClickPosition) < 0.1f)
            {
                SelectedChessObj.transform.position = ClickPosition;

                // 动画结束时隐藏被吃的棋子
                if (pendingCaptureID != -1)
                {
                    if (ChessManager.ChessArray[pendingCaptureID] != null &&
                        ChessManager.ChessArray[pendingCaptureID].Obj != null)
                        ChessManager.ChessArray[pendingCaptureID].Obj.SetActive(false);
                    pendingCaptureID = -1;
                }
                AllowMove = false;
            }
            yield return null;
        }

        // 9. 清理状态，换回红方回合
        SelectedID1 = -1;
        SelectedChessObj = null;
        IsRedTurn = true;

        // ★新增：AI走棋后，检查红方是否被将死/困毙
        CheckGameOver();
        CheckCheckmate();
    }

    // ════════════════════════════════════════════════════════════════
    //  ★音效：将军提示音检测
    //  判断刚走完棋的一方（moverIsRed）是否正攻击对方将/帅
    //  原理：遍历我方车/马/炮/卒，用规则检测判断能否"走到"对方王的位置
    //  （士/象/帅永远攻击不到对方王，无需检测）
    // ════════════════════════════════════════════════════════════════
    private void PlayCheckSoundIfCheck(bool moverIsRed)
    {
        // 1. 找对方的将/帅
        ChessManager.Chess enemyKing = null;
        for (int i = 0; i < ChessManager.ChessArray.Length; i++)
        {
            var c = ChessManager.ChessArray[i];
            if (c != null && !c.Is_Dead && c.Is_Red != moverIsRed &&
                c.Type == ChessManager.Chess.ChessType.帅)
            {
                enemyKing = c;
                break;
            }
        }
        if (enemyKing == null) return;   // 对方王已没了=游戏结束，不响

        // 2. 对方王的逻辑坐标（规则检测用逻辑坐标，注意参数顺序是 Y 在前）
        int kingLogX = ToolManager.ChangeToLineX(enemyKing.Vec_X);
        int kingLogY = ToolManager.ChangeToLineY(enemyKing.Vec_Y);

        // 3. 遍历走棋方的车马炮卒
        for (int i = 0; i < ChessManager.ChessArray.Length; i++)
        {
            var c = ChessManager.ChessArray[i];
            if (c == null || c.Is_Dead || c.Is_Red != moverIsRed) continue;

            bool canAttack = false;
            switch (c.Type)
            {
                case ChessManager.Chess.ChessType.车:
                    canAttack = 规则检测.车(c.Id, kingLogY, kingLogX, enemyKing.Id); break;
                case ChessManager.Chess.ChessType.马:
                    canAttack = 规则检测.马(c.Id, kingLogY, kingLogX, enemyKing.Id); break;
                case ChessManager.Chess.ChessType.炮:
                    canAttack = 规则检测.炮(c.Id, kingLogY, kingLogX, enemyKing.Id); break;
                case ChessManager.Chess.ChessType.卒:
                    canAttack = 规则检测.兵(c.Id, kingLogY, kingLogX, enemyKing.Id); break;
            }
            if (canAttack)
            {
                AudioManager.PlaySfx("check");
                return;   // 一声就够
            }
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  ★★★ 回放系统 — 三个公开方法 ★★★
    //
    //  InitReplayBoard()   → 重置32棋子到开局位置
    //  ReplayMove(...)     → 瞬移执行一步走棋（无动画、无规则检测、不切回合）
    //  ReplayToStep(...)   → 重置棋盘 + 正向回放到第N步
    //
    //  调用方：回放控制UI.cs
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 回放初始化：把32个棋子重置到开局位置
    /// 依赖 ChessManager.Chess 的 InitX/InitY 字段
    /// </summary>
    public void InitReplayBoard()
    {
        Debug.Log("[选择象棋] InitReplayBoard() 开始重置棋盘到开局位置");

        IsReplayMode = true;
        gameOver = true;  // 禁止正常游戏逻辑

        int aliveCount = 0, nullCount = 0;
        for (int i = 0; i < ChessManager.ChessArray.Length; i++)
        {
            if (ChessManager.ChessArray[i] == null) { nullCount++; continue; }
            var chess = ChessManager.ChessArray[i];

            // 复活 + 显示
            chess.Is_Dead = false;
            if (chess.Obj != null) chess.Obj.SetActive(true);
            aliveCount++;

            // 恢复初始坐标
            chess.Vec_X = chess.InitX;
            chess.Vec_Y = chess.InitY;
            if (chess.Obj != null)
                chess.Obj.transform.localPosition = new Vector3(chess.InitX, chess.InitY, -2f);
        }

        // 清理选择状态
        SelectedID = -1;
        SelectedID1 = -1;
        AllowMove = false;
        if (选择棋子 != null) 选择棋子.SetActive(false);

        Debug.Log($"[选择象棋] 回放初始化完成: 存活={aliveCount}, 空={nullCount}，棋盘已重置到开局");
    }

    /// <summary>
    /// 回放执行一步走棋（瞬移，无动画）
    /// </summary>
    public void ReplayMove(int fromX, int fromY, int toX, int toY, int chessId, int captureId)
    {
        if (chessId < 0 || chessId >= ChessManager.ChessArray.Length) return;
        var chess = ChessManager.ChessArray[chessId];
        if (chess == null) return;

        // 处理吃子：隐藏被吃的子
        if (captureId >= 0 && captureId < ChessManager.ChessArray.Length)
        {
            var captured = ChessManager.ChessArray[captureId];
            if (captured != null)
            {
                captured.Is_Dead = true;
                if (captured.Obj != null) captured.Obj.SetActive(false);
            }
        }

        // 移动棋子到目标位置（瞬移）
        float targetLocalX = ToolManager.ChangeBackX(toX);
        float targetLocalY = ToolManager.ChangeBackY(toY);
        chess.Vec_X = targetLocalX;
        chess.Vec_Y = targetLocalY;
        if (chess.Obj != null)
            chess.Obj.transform.localPosition = new Vector3(targetLocalX, targetLocalY, -2f);

        Debug.Log($"[回放] 棋子{chessId}: ({fromX},{fromY})→({toX},{toY}) capture={captureId}");
    }

    /// <summary>
    /// 回放到指定步数：重置棋盘 → 正向回放 0~step-1
    /// </summary>
    public void ReplayToStep(List<GameRecordRequest.MoveData> moves, int step)
    {
        // 1. 先重置棋盘
        InitReplayBoard();

        // 2. 正向回放 0 ~ step-1
        for (int i = 0; i < step && i < moves.Count; i++)
        {
            var m = moves[i];
            ReplayMove(m.FromX, m.FromY, m.ToX, m.ToY, m.ChessId, m.CaptureId);
        }

        Debug.Log($"[回放] 已跳转到第 {step} 步");
    }
}
