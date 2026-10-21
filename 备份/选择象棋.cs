
using System.Collections;
using System.IO;
using UnityEngine;
using static ToolManager;

public class 选择象棋 : MonoBehaviour
{
    public static 选择象棋 Instance { get; private set; }

    // 棋子选择相关变量
    private GameObject 选择棋子; // 选择的棋子对象
    public GameObject 选择框; // 选择框预制体
    public int SelectedID = -1; // 当前选中的棋子ID，-1表示未选中
    public int SelectedID1 = -1; // 用于移动动画的临时ID
    public bool AllowMove = false; // 是否允许移动
    private GameObject SelectedChessObj; // 当前选中的棋子游戏对象
    public Vector3 ClickPosition; // 点击位置
    public Vector3 LocalTargetPos;

    // 回合控制变量
    static public bool IsRedTurn = true; // 当前是否是红方回合
    public GameObject WorringSign1; // 警告提示UI
    public GameObject WorringSign2; // 警告提示UI
    private bool IsIllegal = false; // 是否为非法移动
    private bool IsIllegal2 = false; // 是否为非法移动
    private float TimeCounter = 0; // 时间计数器
    private float TimeCounter2 = 0;
    public Transform ChessBoardTransform; // 在Inspector面板中拖拽棋盘物体到这里

    // ==================== 新增变量 ====================
    /// <summary>
    /// 待销毁的棋子ID（用于延迟销毁动画）
    /// </summary>
    private int pendingCaptureID = -1;

    /// <summary>
    /// AI是否正在思考
    /// 防止AI思考时玩家进行操作
    /// </summary>
    private bool isAIThinking = false;
    public bool IsAIThinkingState => isAIThinking;
    /// <summary>
    /// AI思考延迟时间（秒）
    /// 让玩家有时间看到AI的思考过程
    /// </summary>
    private float aiThinkDelay = 0.5f;
   public  BlackStepSave BlackStep = new BlackStepSave();
    public  RedStepSave RedStep = new RedStepSave();

    static public bool LastMoveTurn;
    public bool LastMove;
    public struct BlackStepSave
    {
        public int StartLogX; // 【新增】起点的列
        public int StartLogY; // 【新增】起点的行
        public int LogX;      // 终点的列
        public int LogY;      // 终点的行
        public int ChessId;   // 移动的棋子ID
        public bool IsKilled;
        public bool KillOthers;
        public int KilledId;  // 被吃的棋子ID

        public void step(int startLogX, int startLogY, int logX, int logY, int chessId, bool isKilled, bool killOthers, int killedId)
        {
            StartLogX = startLogX;
            StartLogY = startLogY;
            LogX = logX;
            LogY = logY;
            ChessId = chessId;
            IsKilled = isKilled;
            KillOthers = killOthers;
            KilledId = killedId;
        }
    }

    public struct RedStepSave
    {
        public int StartLogX;
        public int StartLogY;
        public int LogX;
        public int LogY;
        public int ChessId;
        public bool IsKilled;
        public bool KillOthers;
        public int KilledId;

        public void step(int startLogX, int startLogY, int logX, int logY, int chessId, bool isKilled, bool killOthers, int killedId)
        {
            StartLogX = startLogX;
            StartLogY = startLogY;
            LogX = logX;
            LogY = logY;
            ChessId = chessId;
            IsKilled = isKilled;
            KillOthers = killOthers;
            KilledId = killedId;
        }
    }
    public bool IsMove()
    {
        if (选择象棋.IsRedTurn != LastMove)
        {
            LastMove = 选择象棋.IsRedTurn;
            LastMoveTurn = !LastMoveTurn;
            return true;
        }
        else
        {
            return false;
        }
    }
    public void SaveStep(bool isRed, int startLogX, int startLogY, int targetLogX, int targetLogY, int chessId, bool killOthers, int killedId)
    {
        if (isRed)
        {
            RedStep.step(startLogX, startLogY, targetLogX, targetLogY, chessId, false, killOthers, killedId);
            Debug.Log("红方走：" + RedStep.StartLogX + "," + RedStep.StartLogY + "," + RedStep.LogX + "," + RedStep.LogY + "," + RedStep.ChessId + "," + RedStep);
        }
        else
        {
            BlackStep.step(startLogX, startLogY, targetLogX, targetLogY, chessId, false, killOthers, killedId);
            Debug.Log("黑方走：" + BlackStep.StartLogX + "," + BlackStep.StartLogY + "," + BlackStep.LogX + "," + BlackStep.LogY + "," + BlackStep.ChessId + "," + BlackStep);

        }
    }
    public void UndoMove()
    {
        // 防御判断：如果正在播放移动动画，或者AI正在思考，则禁止悔棋
        if (选择象棋.Instance.AllowMove || 选择象棋.Instance.IsAIThinkingState)
            return;

        // 【修正1】合并判定：既然是同时撤销红黑各一步，必须双方都有记录才能执行
        if (RedStep.ChessId == -1 || BlackStep.ChessId == -1)
            return;

        // 提取红方记录
        int RstartLogX = RedStep.StartLogX, RstartLogY = RedStep.StartLogY;
        int RtargetLogX = RedStep.LogX, RtargetLogY = RedStep.LogY;
        int RchessId = RedStep.ChessId, RkilledId = RedStep.KilledId;
        bool RkillOthers = RedStep.KillOthers;

        // 提取黑方记录
        int BstartLogX = BlackStep.StartLogX, BstartLogY = BlackStep.StartLogY;
        int BtargetLogX = BlackStep.LogX, BtargetLogY = BlackStep.LogY;
        int BchessId = BlackStep.ChessId, BkilledId = BlackStep.KilledId;
        bool BkillOthers = BlackStep.KillOthers;

        // ================= 1. 撤销红方棋子移动 =================
        // 【修正2】独立命名变量，避免与下方黑方逻辑的变量相互覆盖引发幽灵Bug
        ChessManager.Chess redChess = ChessManager.ChessArray[RchessId];
        float redBackX = ChangeBackX(RstartLogX);
        float redBackY = ChangeBackY(RstartLogY);
        redChess.Obj.transform.localPosition = new Vector3(redBackX, redBackY, -2f);
        redChess.Vec_X = redBackX;
        redChess.Vec_Y = redBackY;

        // ================= 2. 撤销黑方棋子移动 =================
        // 【核心修正】彻底修复坐标复制粘贴错误，必须使用 BstartLogX 和 BstartLogY
        ChessManager.Chess blackChess = ChessManager.ChessArray[BchessId];
        float blackBackX = ChangeBackX(BstartLogX);
        float blackBackY = ChangeBackY(BstartLogY);
        blackChess.Obj.transform.localPosition = new Vector3(blackBackX, blackBackY, -2f);
        blackChess.Vec_X = blackBackX;
        blackChess.Vec_Y = blackBackY;

        // ================= 3. 复活红方被吃的子 =================
        if (RkillOthers && RkilledId != -1)
        {
            ChessManager.Chess capturedRed = ChessManager.ChessArray[RkilledId];
            capturedRed.Is_Dead = false;

            GameObject prefab = ChessManager.Instance.GetPrefab(capturedRed.Id, capturedRed.Type);
            GameObject newObj = Instantiate(prefab);
            newObj.transform.SetParent(ChessManager.Instance.棋盘.transform);

            float capX = ChangeBackX(RtargetLogX);
            float capY = ChangeBackY(RtargetLogY);
            newObj.transform.localPosition = new Vector3(capX, capY, -2f);
            newObj.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
            newObj.tag = capturedRed.Is_Red ? "红棋" : "黑棋";

            SpriteRenderer sr1 = newObj.GetComponent<SpriteRenderer>();
            if (sr1 != null) sr1.sortingOrder = 1;
            capturedRed.Obj = newObj;
        }

        // ================= 4. 复活黑方被吃的子 =================
        if (BkillOthers && BkilledId != -1)
        {
            ChessManager.Chess capturedBlack = ChessManager.ChessArray[BkilledId];
            capturedBlack.Is_Dead = false;

            GameObject prefab = ChessManager.Instance.GetPrefab(capturedBlack.Id, capturedBlack.Type);
            GameObject newObj = Instantiate(prefab);
            newObj.transform.SetParent(ChessManager.Instance.棋盘.transform);

            float capX = ChangeBackX(BtargetLogX);
            float capY = ChangeBackY(BtargetLogY);
            newObj.transform.localPosition = new Vector3(capX, capY, -2f);
            newObj.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
            newObj.tag = capturedBlack.Is_Red ? "红棋" : "黑棋";

            SpriteRenderer sr2 = newObj.GetComponent<SpriteRenderer>();
            if (sr2 != null) sr2.sortingOrder = 1;
            capturedBlack.Obj = newObj;
        }

        // 【修正3】删除了原来的：选择象棋.IsRedTurn = isRedLastStep;
        // 逻辑纠错：因为这里是同时撤销了红方一步、黑方一步（相当于撤销了一个完整的回合交锋）
        // 既然红黑双方的动作都抵消了，那么当前该谁走，撤销后还是该谁走，回合标志位不需要改变！

        // 5. 清除界面上的选择框状态
        选择象棋.Instance.SelectedID = -1;
        选择象棋.Instance.ClearSelectionUI();
    }

    public bool WhichTurn(int SelectedID)
    {
        if (ChessManager.ChessArray[SelectedID].Is_Red == IsRedTurn)
        {
            IsIllegal = false;
            return true;
        }
        else
        {
            IsIllegal = true;
            return false;
        }
    }
    public void ClearSelectionUI()
    {
        SelectedID = -1;
        选择棋子.SetActive(false);
    }
    // 根据棋子类型调用相应的规则检测方法
    public bool CheckMoveRules(int selectedId, float VecX, float VecY, int destroyId)
    {
        // 【修正1】变量名与实际含义统一
        // ChangeToLineX 处理 X坐标 -> 返回列索引 -> 赋值给 LogX
        int targetLogX = ToolManager.ChangeToLineX(VecX);
        // ChangeToLineY 处理 Y坐标 -> 返回行索引 -> 赋值给 LogY
        int targetLogY = ToolManager.ChangeToLineY(VecY);

        ChessManager.Chess chess = ChessManager.ChessArray[selectedId];

        // 【修正2】调整参数传递顺序
        // 规则检测函数签名统一为：
        // 所以这里传入顺序应为：
        switch (chess.Type)
        {
            case ChessManager.Chess.ChessType.车:
                return 规则检测.车(selectedId, targetLogY, targetLogX, destroyId);
            case ChessManager.Chess.ChessType.马:
                return 规则检测.马(selectedId, targetLogY, targetLogX, destroyId);
            case ChessManager.Chess.ChessType.炮:
                return 规则检测.炮(selectedId, targetLogY, targetLogX, destroyId);
            case ChessManager.Chess.ChessType.象:
                return 规则检测.相(selectedId, targetLogY, targetLogX, destroyId);
            case ChessManager.Chess.ChessType.士:
                return 规则检测.士(selectedId, targetLogY, targetLogX, destroyId);
            case ChessManager.Chess.ChessType.帅:
                return 规则检测.将(selectedId, targetLogY, targetLogX, destroyId);
            case ChessManager.Chess.ChessType.卒:
                return 规则检测.兵(selectedId, targetLogY, targetLogX, destroyId);
            default:
                return false;
        }
    }

    private void Awake()
    {
        WorringSign1.SetActive(false);
        WorringSign2.SetActive(false);
        选择棋子 = Instantiate(选择框, Vector3.zero, Quaternion.identity);
        选择棋子.SetActive(false);
        Instance = this;
    }

private void Update()
    {
        // ========== AI回合处理 ==========
        // 如果是黑方回合（AI回合），启动AI思考协程
        if (!IsRedTurn && !AllowMove && !isAIThinking)
        {
            StartCoroutine(AIThinkCoroutine());
            return; // 必须return，阻止后续的鼠标点击代码执行
        }

        // 【新增】如果正在移动动画中，禁止任何点击操作，防止状态错乱
        if (AllowMove)
        {
            // 继续执行移动动画
            SelectedChessObj.transform.position = Vector3.MoveTowards(
                SelectedChessObj.transform.position,
                ClickPosition,
                Time.deltaTime * 10
            );
            if (Vector3.Distance(SelectedChessObj.transform.position, ClickPosition) < 0.1f)
            {
                SelectedChessObj.transform.position = ClickPosition;
                ChessManager.ChessArray[SelectedID1].Vec_X = LocalTargetPos.x;
                ChessManager.ChessArray[SelectedID1].Vec_Y = LocalTargetPos.y;

                // 【修改】动画结束时执行延迟销毁
                if (pendingCaptureID != -1)
                {
                    if (ChessManager.ChessArray[pendingCaptureID] != null && ChessManager.ChessArray[pendingCaptureID].Obj != null)
                    {
                        Destroy(ChessManager.ChessArray[pendingCaptureID].Obj);
                    }
                    pendingCaptureID = -1;
                }

                AllowMove = false;
                SelectedID = -1;
                SelectedID1 = -1;
                SelectedChessObj = null;
            }
            return; // 动画未结束，直接返回，不处理点击
        }

        if (Input.GetMouseButtonDown(0))
        {
            // 移动动画或AI思考期间禁止操作，避免选中/改写状态导致混乱
            Vector2 MousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            RaycastHit2D hit = Physics2D.Raycast(MousePosition, Vector2.zero);

            if (hit.collider != null)
            {
                // 检查回合权限
                if (SelectedID != -1)
                {
                    if (WhichTurn(SelectedID) == false)
                    {
                        IsIllegal2 = true;
                        WorringSign2.SetActive(true);
                        SelectedID = -1;
                        选择棋子.SetActive(false);
                        return;
                    }
                }

                // === 情况1：当前没有选中棋子 ===
                if (SelectedID == -1)
                {
                    if (hit.transform.tag == "红棋" || hit.transform.tag == "黑棋")
                    {
                        SelectedID = ToolManager.GetChessId(hit.transform.gameObject);
                        SelectedChessObj = hit.transform.gameObject;
                        选择棋子.transform.position = hit.transform.position;
                        选择棋子.SetActive(true);

                        // 【关键修复】选中阶段就限制只能选当前回合阵营，避免“红方回合选中黑棋”等混乱
                        if (SelectedID != -1 && ChessManager.ChessArray[SelectedID] != null && ChessManager.ChessArray[SelectedID].Is_Red != IsRedTurn)
                        {
                            IsIllegal2 = true;
                            WorringSign2.SetActive(true);
                            SelectedID = -1;
                            SelectedChessObj = null;
                            选择棋子.SetActive(false);
                            return;
                        }
                    }
                }
                // === 情况2：当前已经选中了棋子 ===
                else
                {
                    选择棋子.SetActive(false);
                    // --- 点击了棋盘（移动） ---
                    if (hit.transform.tag == "棋盘")
                    {
                        Vector3 localTargetPos = ChessBoardTransform.InverseTransformPoint(hit.point);
                        if (CheckMoveRules(SelectedID, localTargetPos.x, localTargetPos.y, -1))
                        {
                            AllowMove = true;
                            // 【修正3】变量名统一
                            // Y坐标对应行，X坐标对应列

                            int targetLogY = ToolManager.ChangeToLineY(localTargetPos.y);
                            int targetLogX = ToolManager.ChangeToLineX(localTargetPos.x);
                            // 将逻辑坐标转回世界坐标
                            // ChangeBackX 需要列 -> 传入 targetLogX
                            // ChangeBackY 需要行 -> 传入 targetLogY
                            float centeredX = ToolManager.ChangeBackX(targetLogX);
                            float centeredY = ToolManager.ChangeBackY(targetLogY);

                            int startLogX = ToolManager.ChangeToLineX(ChessManager.ChessArray[SelectedID].Vec_X);
                            int startLogY = ToolManager.ChangeToLineY(ChessManager.ChessArray[SelectedID].Vec_Y);
                            SaveStep(IsRedTurn, startLogX, startLogY, targetLogX, targetLogY, SelectedID, false, -1);

                            // 【关键修复】LocalTargetPos/ClickPosition/写回Vec 统一使用格点中心，避免棋盘状态漂移
                            LocalTargetPos = new Vector3(centeredX, centeredY, 0);
                            Vector3 worldPos = ChessBoardTransform.TransformPoint(LocalTargetPos);
                            ClickPosition = new Vector3(worldPos.x, worldPos.y, -2f);
                            SelectedID1 = SelectedID;
                            SelectedID = -1;
                            IsRedTurn = !IsRedTurn;
                        }
                        else
                        {
                            IsIllegal = true;
                            WorringSign1.SetActive(true);
                            SelectedID = -1;
                        }
                    }
                    // --- 点击了其他棋子（吃子） ---
                    else if (hit.transform.tag == "红棋" || hit.transform.tag == "黑棋")
                    {
                        int targetChessId = ToolManager.GetChessId(hit.transform.gameObject);
                        if (ChessManager.ChessArray[SelectedID].Is_Red == ChessManager.ChessArray[targetChessId].Is_Red)
                        {
                            SelectedID = targetChessId;
                            SelectedChessObj = hit.transform.gameObject;
                            选择棋子.transform.position = hit.transform.position;
                            选择棋子.SetActive(true);
                            return;
                        }
                        Vector3 localTargetPos = ChessBoardTransform.InverseTransformPoint(hit.point);
                        if (CheckMoveRules(SelectedID, localTargetPos.x, localTargetPos.y, targetChessId))
                        {
                            // 【修改】吃子逻辑调整：只标记死亡，不立即销毁，记录ID等待动画结束
                            ChessManager.ChessArray[targetChessId].Is_Dead = true;
                            pendingCaptureID = targetChessId; // 记录待销毁ID

                            AllowMove = true;
                            // 【关键修复】吃子时同样对齐到格点中心，避免写回偏移
                            int targetLogY = ToolManager.ChangeToLineY(localTargetPos.y);
                            int targetLogX = ToolManager.ChangeToLineX(localTargetPos.x);
                            float centeredX = ToolManager.ChangeBackX(targetLogX);
                            float centeredY = ToolManager.ChangeBackY(targetLogY);
                            int startLogX = ToolManager.ChangeToLineX(ChessManager.ChessArray[SelectedID].Vec_X);
                            int startLogY = ToolManager.ChangeToLineY(ChessManager.ChessArray[SelectedID].Vec_Y);        
                            SaveStep(IsRedTurn, startLogX, startLogY, targetLogX, targetLogY, SelectedID, false, -1);
                            LocalTargetPos = new Vector3(centeredX, centeredY, 0);
                            Vector3 worldPos = ChessBoardTransform.TransformPoint(LocalTargetPos);
                            ClickPosition = new Vector3(worldPos.x, worldPos.y, -2f);
                            SelectedID1 = SelectedID;
                            SelectedID = -1;
                            IsRedTurn = !IsRedTurn;
                        }
                        else
                        {
                            IsIllegal = true;
                            WorringSign1.SetActive(true);
                            SelectedID = -1;
                        }
                    }
                    else
                    {
                        SelectedID = -1;
                    }
                }
            }
        }

        // UI计时器
        if (IsIllegal)
        {
            TimeCounter += Time.deltaTime;
            if (TimeCounter >= 1)
            {
                WorringSign1.SetActive(false);
                TimeCounter = 0;
                IsIllegal = false;
            }
        }
        else if (IsIllegal2)
        {
            TimeCounter2 += Time.deltaTime;
            if (TimeCounter2 >= 1)
            {
                WorringSign2.SetActive(false);
                TimeCounter2 = 0;
                IsIllegal2 = false;
            }
        }
    }

    // ==================== 新增：AI思考协程 ====================
    /// <summary>
    /// AI思考协程
    /// 这是AI决策的核心流程
    ///
    /// 【执行流程】
    /// 1. 设置思考状态标志
    /// 2. 等待一小段时间（让玩家看到局面）
    /// 3. 调用AIManager获取最佳走法
    /// 4. 执行AI走法
    /// 5. 切换回合
    /// </summary>
    private IEnumerator AIThinkCoroutine()
    {
        // 第一步：设置思考状态
        isAIThinking = true;
        Debug.Log("[AI] 开始思考...");

        // 第二步：等待一小段时间
        // 这让玩家有时间看到AI"正在思考"
        yield return new WaitForSeconds(aiThinkDelay);

        // 第三步：获取AI的最佳走法
        // AIManager会根据当前难度选择对应的AI策略
        AIMove bestMove = AIManager.Instance.GetBestMove();

        // 第四步：检查走法是否有效
        if (!bestMove.IsValid)
        {
            Debug.Log("[AI] 没有有效走法，游戏可能结束");
            isAIThinking = false;
            yield break;
        }

        // 第五步：执行AI走法
        yield return ExecuteAIMove(bestMove);

        // 第六步：重置思考状态
        isAIThinking = false;
        Debug.Log("[AI] 思考完成，回合切换");
    }

    /// <summary>
    /// 执行AI走法
    /// </summary>
    ///     /// <summary>
    /// 执行AI走法
    /// </summary>
    private IEnumerator ExecuteAIMove(AIMove move)
    {
        // 获取移动的棋子对象
        ChessManager.Chess chess = ChessManager.ChessArray[move.FromChessId];
        if (chess == null || chess.Is_Dead)
        {
            Debug.LogError("[AI] 无效的棋子ID");
            yield break;
        }
        SelectedChessObj = chess.Obj;

        // 处理吃子：标记死亡，暂存ID，稍后销毁
        if (move.CaptureId >= 0 && move.CaptureId < 32)
        {
            ChessManager.Chess captured = ChessManager.ChessArray[move.CaptureId];
            if (captured != null && !captured.Is_Dead)
            {
                captured.Is_Dead = true;
                pendingCaptureID = move.CaptureId; // 记录待销毁ID
                Debug.Log($"[AI] 准备吃掉 {captured.Type}");
            }
        }

        // 计算目标位置的物理坐标
        LocalTargetPos = new Vector3(
            ToolManager.ChangeBackX(move.ToLogX),
            ToolManager.ChangeBackY(move.ToLogY),
            0);
        Vector3 worldPos = ChessBoardTransform.TransformPoint(LocalTargetPos);
        ClickPosition = new Vector3(worldPos.x, worldPos.y, -2f);

        // 【关键修复】：必须先计算起点，再修改坐标！
        int startLogX = ToolManager.ChangeToLineX(chess.Vec_X);
        int startLogY = ToolManager.ChangeToLineY(chess.Vec_Y);
        bool isCapture = (move.CaptureId >= 0 && move.CaptureId < 32);

        // 保存正确的起点和终点记录
        SaveStep(IsRedTurn, startLogX, startLogY, move.ToLogX, move.ToLogY, move.FromChessId, isCapture, move.CaptureId);

        // 保存完记录后，再更新底层逻辑坐标为目标点
        chess.Vec_X = LocalTargetPos.x;
        chess.Vec_Y = LocalTargetPos.y;

        // 执行移动动画
        AllowMove = true;
        SelectedID1 = move.FromChessId;

        // 等待移动动画完成
        while (AllowMove)
        {
            SelectedChessObj.transform.position = Vector3.MoveTowards(
                SelectedChessObj.transform.position, ClickPosition, Time.deltaTime * 10);

            if (Vector3.Distance(SelectedChessObj.transform.position, ClickPosition) < 0.1f)
            {
                SelectedChessObj.transform.position = ClickPosition;

                // 动画结束时执行延迟销毁被吃的棋子
                if (pendingCaptureID != -1)
                {
                    if (ChessManager.ChessArray[pendingCaptureID] != null && ChessManager.ChessArray[pendingCaptureID].Obj != null)
                    {
                        Destroy(ChessManager.ChessArray[pendingCaptureID].Obj);
                    }
                    pendingCaptureID = -1;
                }
                AllowMove = false;
            }
            yield return null;
        }

        // 重置状态
        SelectedID1 = -1;
        SelectedChessObj = null;

        // 切换回合
        IsRedTurn = true;
        Debug.Log($"[AI] 移动完成：{chess.Type} 从 ({startLogX},{startLogY}) 到 ({move.ToLogX}, {move.ToLogY})");
    }

}
