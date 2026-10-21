//using System.Collections;
//using System.IO;
//using UnityEngine;
//using static ToolManager;

//public class 选择象棋 : MonoBehaviour
//{
//    public static 选择象棋 Instance { get; private set; }

//    // 棋子选择相关变量
//    private GameObject 选择棋子; // 选择的棋子对象
//    public GameObject 选择框; // 选择框预制体
//    public int SelectedID = -1; // 当前选中的棋子ID，-1表示未选中
//    public int SelectedID1 = -1; // 用于移动动画的临时ID
//    public bool AllowMove = false; // 是否允许移动
//    private GameObject SelectedChessObj; // 当前选中的棋子游戏对象
//    public Vector3 ClickPosition; // 点击位置
//    public Vector3 LocalTargetPos;

//    // 回合控制变量
//    static public bool IsRedTurn = true; // 当前是否是红方回合

//    public GameObject WorringSign1; // 警告提示UI
//    public GameObject WorringSign2; // 警告提示UI
//    private bool IsIllegal = false; // 是否为非法移动
//    private bool IsIllegal2 = false; // 是否为非法移动
//    private float TimeCounter = 0; // 时间计数器
//    private float TimeCounter2 = 0;

//    public Transform ChessBoardTransform; // 在Inspector面板中拖拽棋盘物体到这里

//    // ==================== 新增变量 ====================
//    /// <summary>
//    /// 待隐藏的棋子ID（用于延迟隐藏动画）
//    /// </summary>
//    private int pendingCaptureID = -1;

//    /// <summary>
//    /// AI是否正在思考
//    /// 防止AI思考时玩家进行操作
//    /// </summary>
//    private bool isAIThinking = false;
//    public bool IsAIThinkingState => isAIThinking;

//    /// <summary>
//    /// AI思考延迟时间（秒）
//    /// 让玩家有时间看到AI的思考过程
//    /// </summary>
//    private float aiThinkDelay = 0.5f;

//    public BlackStepSave BlackStep = new BlackStepSave();
//    public RedStepSave RedStep = new RedStepSave();
//    static public bool LastMoveTurn;
//    public bool LastMove;

//    public struct BlackStepSave
//    {
//        public int StartLogX; // 【新增】起点的列
//        public int StartLogY; // 【新增】起点的行
//        public int LogX; // 终点的列
//        public int LogY; // 终点的行
//        public int ChessId; // 移动的棋子ID
//        public bool IsKilled;
//        public bool KillOthers;
//        public int KilledId; // 被吃的棋子ID

//        public void step(int startLogX, int startLogY, int logX, int logY, int chessId, bool isKilled, bool killOthers, int killedId)
//        {
//            StartLogX = startLogX;
//            StartLogY = startLogY;
//            LogX = logX;
//            LogY = logY;
//            ChessId = chessId;
//            IsKilled = isKilled;
//            KillOthers = killOthers;
//            KilledId = killedId;
//        }
//    }

//    public struct RedStepSave
//    {
//        public int StartLogX;
//        public int StartLogY;
//        public int LogX;
//        public int LogY;
//        public int ChessId;
//        public bool IsKilled;
//        public bool KillOthers;
//        public int KilledId;

//        public void step(int startLogX, int startLogY, int logX, int logY, int chessId, bool isKilled, bool killOthers, int killedId)
//        {
//            StartLogX = startLogX;
//            StartLogY = startLogY;
//            LogX = logX;
//            LogY = logY;
//            ChessId = chessId;
//            IsKilled = isKilled;
//            KillOthers = killOthers;
//            KilledId = killedId;
//        }
//    }

//    public bool IsMove()
//    {
//        if (选择象棋.IsRedTurn != LastMove)
//        {
//            LastMove = 选择象棋.IsRedTurn;
//            LastMoveTurn = !LastMoveTurn;
//            return true;
//        }
//        else
//        {
//            return false;
//        }
//    }

//    public void SaveStep(bool isRed, int startLogX, int startLogY, int targetLogX, int targetLogY, int chessId, bool killOthers, int killedId)
//    {
//        if (isRed)
//        {
//            RedStep.step(startLogX, startLogY, targetLogX, targetLogY, chessId, false, killOthers, killedId);
//            Debug.Log("红方走：" + RedStep.StartLogX + "," + RedStep.StartLogY + "," + RedStep.LogX + "," + RedStep.LogY + "," + RedStep.ChessId + "," + RedStep);
//        }
//        else
//        {
//            BlackStep.step(startLogX, startLogY, targetLogX, targetLogY, chessId, false, killOthers, killedId);
//            Debug.Log("黑方走：" + BlackStep.StartLogX + "," + BlackStep.StartLogY + "," + BlackStep.LogX + "," + BlackStep.LogY + "," + BlackStep.ChessId + "," + BlackStep);
//        }
//    }

//    public void UndoMove()
//    {
//        // 防御判断：如果正在播放移动动画，或者AI正在思考，则禁止悔棋
//        if (选择象棋.Instance.AllowMove || 选择象棋.Instance.IsAIThinkingState) return;

//        // 【修正1】合并判定：既然是同时撤销红黑各一步，必须双方都有记录才能执行
//        if (RedStep.ChessId == -1 || BlackStep.ChessId == -1) return;

//        // 提取红方记录
//        int RstartLogX = RedStep.StartLogX, RstartLogY = RedStep.StartLogY;
//        int RtargetLogX = RedStep.LogX, RtargetLogY = RedStep.LogY;
//        int RchessId = RedStep.ChessId, RkilledId = RedStep.KilledId;
//        bool RkillOthers = RedStep.KillOthers;

//        // 提取黑方记录
//        int BstartLogX = BlackStep.StartLogX, BstartLogY = BlackStep.StartLogY;
//        int BtargetLogX = BlackStep.LogX, BtargetLogY = BlackStep.LogY;
//        int BchessId = BlackStep.ChessId, BkilledId = BlackStep.KilledId;
//        bool BkillOthers = BlackStep.KillOthers;

//        // ================= 1. 撤销红方棋子移动 =================
//        ChessManager.Chess redChess = ChessManager.ChessArray[RchessId];
//        float redBackX = ChangeBackX(RstartLogX);
//        float redBackY = ChangeBackY(RstartLogY);
//        redChess.Obj.transform.localPosition = new Vector3(redBackX, redBackY, -2f);
//        redChess.Vec_X = redBackX;
//        redChess.Vec_Y = redBackY;

//        // ================= 2. 撤销黑方棋子移动 =================
//        ChessManager.Chess blackChess = ChessManager.ChessArray[BchessId];
//        float blackBackX = ChangeBackX(BstartLogX);
//        float blackBackY = ChangeBackY(BstartLogY);
//        blackChess.Obj.transform.localPosition = new Vector3(blackBackX, blackBackY, -2f);
//        blackChess.Vec_X = blackBackX;
//        blackChess.Vec_Y = blackBackY;

//        // ================= 3. 复活红方被吃的子 =================
//        if (RkillOthers && RkilledId != -1)
//        {
//            ChessManager.Chess capturedRed = ChessManager.ChessArray[RkilledId];
//            capturedRed.Is_Dead = false;
//            // 【修改点】取消实例化，直接重置坐标并激活显示
//            float capX = ChangeBackX(RtargetLogX);
//            float capY = ChangeBackY(RtargetLogY);
//            capturedRed.Obj.transform.localPosition = new Vector3(capX, capY, -2f);
//            capturedRed.Obj.SetActive(true);
//        }

//        // ================= 4. 复活黑方被吃的子 =================
//        if (BkillOthers && BkilledId != -1)
//        {
//            ChessManager.Chess capturedBlack = ChessManager.ChessArray[BkilledId];
//            capturedBlack.Is_Dead = false;
//            // 【修改点】取消实例化，直接重置坐标并激活显示
//            float capX = ChangeBackX(BtargetLogX);
//            float capY = ChangeBackY(BtargetLogY);
//            capturedBlack.Obj.transform.localPosition = new Vector3(capX, capY, -2f);
//            capturedBlack.Obj.SetActive(true);
//        }

//        // 5. 清除界面上的选择框状态
//        选择象棋.Instance.SelectedID = -1;
//        选择象棋.Instance.ClearSelectionUI();
//    }

//    public bool WhichTurn(int SelectedID)
//    {
//        if (ChessManager.ChessArray[SelectedID].Is_Red == IsRedTurn)
//        {
//            IsIllegal = false;
//            return true;
//        }
//        else
//        {
//            IsIllegal = true;
//            return false;
//        }
//    }

//    public void ClearSelectionUI()
//    {
//        SelectedID = -1;
//        选择棋子.SetActive(false);
//    }

//    // 根据棋子类型调用相应的规则检测方法
//    public bool CheckMoveRules(int selectedId, float VecX, float VecY, int destroyId)
//    {
//        int targetLogX = ToolManager.ChangeToLineX(VecX);
//        int targetLogY = ToolManager.ChangeToLineY(VecY);

//        ChessManager.Chess chess = ChessManager.ChessArray[selectedId];

//        switch (chess.Type)
//        {
//            case ChessManager.Chess.ChessType.车: return 规则检测.车(selectedId, targetLogY, targetLogX, destroyId);
//            case ChessManager.Chess.ChessType.马: return 规则检测.马(selectedId, targetLogY, targetLogX, destroyId);
//            case ChessManager.Chess.ChessType.炮: return 规则检测.炮(selectedId, targetLogY, targetLogX, destroyId);
//            case ChessManager.Chess.ChessType.象: return 规则检测.相(selectedId, targetLogY, targetLogX, destroyId);
//            case ChessManager.Chess.ChessType.士: return 规则检测.士(selectedId, targetLogY, targetLogX, destroyId);
//            case ChessManager.Chess.ChessType.帅: return 规则检测.将(selectedId, targetLogY, targetLogX, destroyId);
//            case ChessManager.Chess.ChessType.卒: return 规则检测.兵(selectedId, targetLogY, targetLogX, destroyId);
//            default: return false;
//        }
//    }

//    private void Awake()
//    {
//        IsRedTurn = true;
//        WorringSign1.SetActive(false);
//        WorringSign2.SetActive(false);
//        选择棋子 = Instantiate(选择框, Vector3.zero, Quaternion.identity);
//        选择棋子.SetActive(false);
//        Instance = this;
//    }

//    private void Update()
//    {
//        // ========== AI回合处理 ==========
//        if (!IsRedTurn && !AllowMove && !isAIThinking)
//        {
//            StartCoroutine(AIThinkCoroutine());
//            return;
//        }

//        if (AllowMove)
//        {
//            SelectedChessObj.transform.position = Vector3.MoveTowards(
//                SelectedChessObj.transform.position, ClickPosition, Time.deltaTime * 10);

//            if (Vector3.Distance(SelectedChessObj.transform.position, ClickPosition) < 0.1f)
//            {
//                SelectedChessObj.transform.position = ClickPosition;
//                ChessManager.ChessArray[SelectedID1].Vec_X = LocalTargetPos.x;
//                ChessManager.ChessArray[SelectedID1].Vec_Y = LocalTargetPos.y;

//                // 【修改点】动画结束时隐藏被吃的棋子，而不是销毁
//                if (pendingCaptureID != -1)
//                {
//                    if (ChessManager.ChessArray[pendingCaptureID] != null && ChessManager.ChessArray[pendingCaptureID].Obj != null)
//                    {
//                        ChessManager.ChessArray[pendingCaptureID].Obj.SetActive(false);
//                    }
//                    pendingCaptureID = -1;
//                }

//                AllowMove = false;
//                SelectedID = -1;
//                SelectedID1 = -1;
//                SelectedChessObj = null;
//            }
//            return;
//        }

//        if (Input.GetMouseButtonDown(0))
//        {
//            Vector2 MousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
//            RaycastHit2D hit = Physics2D.Raycast(MousePosition, Vector2.zero);

//            if (hit.collider != null)
//            {
//                if (SelectedID != -1)
//                {
//                    if (WhichTurn(SelectedID) == false)
//                    {
//                        IsIllegal2 = true;
//                        WorringSign2.SetActive(true);
//                        SelectedID = -1;
//                        选择棋子.SetActive(false);
//                        return;
//                    }
//                }

//                if (SelectedID == -1)
//                {
//                    if (hit.transform.tag == "红棋" || hit.transform.tag == "黑棋")
//                    {
//                        SelectedID = ToolManager.GetChessId(hit.transform.gameObject);
//                        SelectedChessObj = hit.transform.gameObject;
//                        选择棋子.transform.position = hit.transform.position;
//                        选择棋子.SetActive(true);

//                        if (SelectedID != -1 && ChessManager.ChessArray[SelectedID] != null && ChessManager.ChessArray[SelectedID].Is_Red != IsRedTurn)
//                        {
//                            IsIllegal2 = true;
//                            WorringSign2.SetActive(true);
//                            SelectedID = -1;
//                            SelectedChessObj = null;
//                            选择棋子.SetActive(false);
//                            return;
//                        }
//                    }
//                }
//                else
//                {
//                    选择棋子.SetActive(false);

//                    if (hit.transform.tag == "棋盘")
//                    {
//                        Vector3 localTargetPos = ChessBoardTransform.InverseTransformPoint(hit.point);

//                        if (CheckMoveRules(SelectedID, localTargetPos.x, localTargetPos.y, -1))
//                        {
//                            AllowMove = true;
//                            int targetLogY = ToolManager.ChangeToLineY(localTargetPos.y);
//                            int targetLogX = ToolManager.ChangeToLineX(localTargetPos.x);

//                            float centeredX = ToolManager.ChangeBackX(targetLogX);
//                            float centeredY = ToolManager.ChangeBackY(targetLogY);

//                            int startLogX = ToolManager.ChangeToLineX(ChessManager.ChessArray[SelectedID].Vec_X);
//                            int startLogY = ToolManager.ChangeToLineY(ChessManager.ChessArray[SelectedID].Vec_Y);

//                            SaveStep(IsRedTurn, startLogX, startLogY, targetLogX, targetLogY, SelectedID, false, -1);

//                            LocalTargetPos = new Vector3(centeredX, centeredY, 0);
//                            Vector3 worldPos = ChessBoardTransform.TransformPoint(LocalTargetPos);
//                            ClickPosition = new Vector3(worldPos.x, worldPos.y, -2f);
//                            SelectedID1 = SelectedID;
//                            SelectedID = -1;
//                            IsRedTurn = !IsRedTurn;
//                        }
//                        else
//                        {
//                            IsIllegal = true;
//                            WorringSign1.SetActive(true);
//                            SelectedID = -1;
//                        }
//                    }
//                    else if (hit.transform.tag == "红棋" || hit.transform.tag == "黑棋")
//                    {
//                        int targetChessId = ToolManager.GetChessId(hit.transform.gameObject);

//                        if (ChessManager.ChessArray[SelectedID].Is_Red == ChessManager.ChessArray[targetChessId].Is_Red)
//                        {
//                            SelectedID = targetChessId;
//                            SelectedChessObj = hit.transform.gameObject;
//                            选择棋子.transform.position = hit.transform.position;
//                            选择棋子.SetActive(true);
//                            return;
//                        }

//                        Vector3 localTargetPos = ChessBoardTransform.InverseTransformPoint(hit.point);

//                        if (CheckMoveRules(SelectedID, localTargetPos.x, localTargetPos.y, targetChessId))
//                        {
//                            ChessManager.ChessArray[targetChessId].Is_Dead = true;
//                            pendingCaptureID = targetChessId;
//                            AllowMove = true;

//                            int targetLogY = ToolManager.ChangeToLineY(localTargetPos.y);
//                            int targetLogX = ToolManager.ChangeToLineX(localTargetPos.x);

//                            float centeredX = ToolManager.ChangeBackX(targetLogX);
//                            float centeredY = ToolManager.ChangeBackY(targetLogY);

//                            int startLogX = ToolManager.ChangeToLineX(ChessManager.ChessArray[SelectedID].Vec_X);
//                            int startLogY = ToolManager.ChangeToLineY(ChessManager.ChessArray[SelectedID].Vec_Y);

//                            SaveStep(IsRedTurn, startLogX, startLogY, targetLogX, targetLogY, SelectedID, true, targetChessId);

//                            LocalTargetPos = new Vector3(centeredX, centeredY, 0);
//                            Vector3 worldPos = ChessBoardTransform.TransformPoint(LocalTargetPos);
//                            ClickPosition = new Vector3(worldPos.x, worldPos.y, -2f);
//                            SelectedID1 = SelectedID;
//                            SelectedID = -1;
//                            IsRedTurn = !IsRedTurn;
//                        }
//                        else
//                        {
//                            IsIllegal = true;
//                            WorringSign1.SetActive(true);
//                            SelectedID = -1;
//                        }
//                    }
//                    else
//                    {
//                        SelectedID = -1;
//                    }
//                }
//            }
//        }

//        if (IsIllegal)
//        {
//            TimeCounter += Time.deltaTime;
//            if (TimeCounter >= 1)
//            {
//                WorringSign1.SetActive(false);
//                TimeCounter = 0;
//                IsIllegal = false;
//            }
//        }
//        else if (IsIllegal2)
//        {
//            TimeCounter2 += Time.deltaTime;
//            if (TimeCounter2 >= 1)
//            {
//                WorringSign2.SetActive(false);
//                TimeCounter2 = 0;
//                IsIllegal2 = false;
//            }
//        }
//    }

//    private IEnumerator AIThinkCoroutine()
//    {
//        isAIThinking = true;
//        Debug.Log("[AI] 开始思考...");

//        yield return new WaitForSeconds(aiThinkDelay);

//        AIMove bestMove = AIManager.Instance.GetBestMove();

//        if (!bestMove.IsValid)
//        {
//            Debug.Log("[AI] 没有有效走法，游戏可能结束");
//            isAIThinking = false;
//            yield break;
//        }

//        yield return ExecuteAIMove(bestMove);

//        isAIThinking = false;
//        Debug.Log("[AI] 思考完成，回合切换");
//    }

//    private IEnumerator ExecuteAIMove(AIMove move)
//    {
//        ChessManager.Chess chess = ChessManager.ChessArray[move.FromChessId];
//        if (chess == null || chess.Is_Dead)
//        {
//            Debug.LogError("[AI] 无效的棋子ID");
//            yield break;
//        }

//        SelectedChessObj = chess.Obj;

//        if (move.CaptureId >= 0 && move.CaptureId < 32)
//        {
//            ChessManager.Chess captured = ChessManager.ChessArray[move.CaptureId];
//            if (captured != null && !captured.Is_Dead)
//            {
//                captured.Is_Dead = true;
//                pendingCaptureID = move.CaptureId;
//                Debug.Log($"[AI] 准备吃掉 {captured.Type}");
//            }
//        }

//        LocalTargetPos = new Vector3(
//            ToolManager.ChangeBackX(move.ToLogX),
//            ToolManager.ChangeBackY(move.ToLogY),
//            0);
//        Vector3 worldPos = ChessBoardTransform.TransformPoint(LocalTargetPos);
//        ClickPosition = new Vector3(worldPos.x, worldPos.y, -2f);

//        int startLogX = ToolManager.ChangeToLineX(chess.Vec_X);
//        int startLogY = ToolManager.ChangeToLineY(chess.Vec_Y);

//        bool isCapture = (move.CaptureId >= 0 && move.CaptureId < 32);

//        SaveStep(IsRedTurn, startLogX, startLogY, move.ToLogX, move.ToLogY, move.FromChessId, isCapture, move.CaptureId);

//        chess.Vec_X = LocalTargetPos.x;
//        chess.Vec_Y = LocalTargetPos.y;

//        AllowMove = true;
//        SelectedID1 = move.FromChessId;

//        while (AllowMove)
//        {
//            SelectedChessObj.transform.position = Vector3.MoveTowards(
//                SelectedChessObj.transform.position, ClickPosition, Time.deltaTime * 10);

//            if (Vector3.Distance(SelectedChessObj.transform.position, ClickPosition) < 0.1f)
//            {
//                SelectedChessObj.transform.position = ClickPosition;

//                // 【修改点】动画结束时隐藏被吃的棋子，而不是销毁
//                if (pendingCaptureID != -1)
//                {
//                    if (ChessManager.ChessArray[pendingCaptureID] != null && ChessManager.ChessArray[pendingCaptureID].Obj != null)
//                    {
//                        ChessManager.ChessArray[pendingCaptureID].Obj.SetActive(false);
//                    }
//                    pendingCaptureID = -1;
//                }

//                AllowMove = false;
//            }
//            yield return null;
//        }

//        SelectedID1 = -1;
//        SelectedChessObj = null;
//        IsRedTurn = true;
//        Debug.Log($"[AI] 移动完成：{chess.Type} 从 ({startLogX},{startLogY}) 到 ({move.ToLogX}, {move.ToLogY})");
//    }
//}
using System.Collections;
using System.IO;
using UnityEngine;
using static ToolManager;

public class 选择象棋多人版 : MonoBehaviour
{
    // ==================== 单例模式 ====================
    public static 选择象棋 Instance { get; private set; }

    // ==================== 棋子选择相关变量 ====================
    private GameObject 选择棋子; // 选择的棋子对象
    public GameObject 选择框; // 选择框预制体
    public int SelectedID = -1; // 当前选中的棋子ID，-1表示未选中
    public int SelectedID1 = -1; // 用于移动动画的临时ID
    public bool AllowMove = false; // 是否允许移动
    private GameObject SelectedChessObj; // 当前选中的棋子游戏对象
    public Vector3 ClickPosition; // 点击位置(世界坐标)
    public Vector3 LocalTargetPos; // 目标位置(本地坐标)

    // ==================== 回合控制变量 ====================
    static public bool IsRedTurn = true; // 当前是否是红方回合(静态，方便全局访问)
    public GameObject WorringSign1; // 警告提示UI (规则非法)
    public GameObject WorringSign2; // 警告提示UI (回合非法)
    private bool IsIllegal = false;
    private bool IsIllegal2 = false;
    private float TimeCounter = 0;
    private float TimeCounter2 = 0;

    // ==================== 棋盘与动画变量 ====================
    public Transform ChessBoardTransform; // 在Inspector面板中拖拽棋盘物体到这里
    private int pendingCaptureID = -1; // 待隐藏的棋子ID（用于移动动画结束后再隐藏，支持悔棋）

    // ==================== AI相关变量 ====================
    private bool isAIThinking = false; // 防止AI思考时玩家乱点
    public bool IsAIThinkingState => isAIThinking; // 暴露只读属性供外部(如悔棋按钮)判断
    private float aiThinkDelay = 0.5f; // AI思考延迟时间

    // ==================== 悔棋与记录相关变量 ====================
    public BlackStepSave BlackStep = new BlackStepSave();
    public RedStepSave RedStep = new RedStepSave();
    static public bool LastMoveTurn;
    public bool LastMove;

    /// <summary>
    /// 黑方走棋记录结构体
    /// </summary>
    public struct BlackStepSave
    {
        public int StartLogX; public int StartLogY; // 起点
        public int LogX; public int LogY;           // 终点
        public int ChessId;                        // 移动的棋子ID
        public bool IsKilled; public bool KillOthers; public int KilledId; // 吃子记录
        public void step(int startLogX, int startLogY, int logX, int logY, int chessId, bool isKilled, bool killOthers, int killedId)
        {
            StartLogX = startLogX; StartLogY = startLogY; LogX = logX; LogY = logY;
            ChessId = chessId; IsKilled = isKilled; KillOthers = killOthers; KilledId = killedId;
        }
    }

    /// <summary>
    /// 红方走棋记录结构体
    /// </summary>
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

    // ==================== 系统方法 ====================

    private void Awake()
    {
        //Instance = this;
        IsRedTurn = true;
        WorringSign1.SetActive(false);
        WorringSign2.SetActive(false);
        选择棋子 = Instantiate(选择框, Vector3.zero, Quaternion.identity);
        选择棋子.SetActive(false);
    }

    private void Update()
    {
        // ========== 1. AI回合处理 ==========
        if (!IsRedTurn && !AllowMove && !isAIThinking)
        {
            StartCoroutine(AIThinkCoroutine());
            return; // 阻止后续的鼠标点击代码执行
        }

        // ========== 2. 执行移动动画(玩家与共用) ==========
        if (AllowMove)
        {
            SelectedChessObj.transform.position = Vector3.MoveTowards(
                SelectedChessObj.transform.position, ClickPosition, Time.deltaTime * 10);

            if (Vector3.Distance(SelectedChessObj.transform.position, ClickPosition) < 0.1f)
            {
                SelectedChessObj.transform.position = ClickPosition;
                ChessManager.ChessArray[SelectedID1].Vec_X = LocalTargetPos.x;
                ChessManager.ChessArray[SelectedID1].Vec_Y = LocalTargetPos.y;

                // 动画结束时隐藏被吃的棋子，而不是销毁（配合悔棋）
                if (pendingCaptureID != -1)
                {
                    if (ChessManager.ChessArray[pendingCaptureID] != null && ChessManager.ChessArray[pendingCaptureID].Obj != null)
                    {
                        ChessManager.ChessArray[pendingCaptureID].Obj.SetActive(false);
                    }
                    pendingCaptureID = -1;
                }

                AllowMove = false;
                SelectedID = -1;
                SelectedID1 = -1;
                SelectedChessObj = null;
            }
            return; // 动画播放期间禁止点击操作
        }

        // ========== 3. 玩家鼠标点击操作 ==========
        if (Input.GetMouseButtonDown(0))
        {
            Vector2 MousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            RaycastHit2D hit = Physics2D.Raycast(MousePosition, Vector2.zero);

            if (hit.collider != null)
            {
                // 检查是否错选了对方的棋子
                if (SelectedID != -1 && WhichTurn(SelectedID) == false)
                {
                    IsIllegal2 = true;
                    WorringSign2.SetActive(true);
                    SelectedID = -1;
                    选择棋子.SetActive(false);
                    return;
                }

                // --- 情况A：当前没有选中棋子 ---
                if (SelectedID == -1)
                {
                    if (hit.transform.tag == "红棋" || hit.transform.tag == "黑棋")
                    {
                        SelectedID = ToolManager.GetChessId(hit.transform.gameObject);
                        SelectedChessObj = hit.transform.gameObject;
                        选择棋子.transform.position = hit.transform.position;
                        选择棋子.SetActive(true);

                        // 二次确认回合权限
                        if (SelectedID != -1 && ChessManager.ChessArray[SelectedID] != null && ChessManager.ChessArray[SelectedID].Is_Red != IsRedTurn)
                        {
                            IsIllegal2 = true; WorringSign2.SetActive(true);
                            SelectedID = -1; SelectedChessObj = null; 选择棋子.SetActive(false);
                            return;
                        }
                    }
                }
                // --- 情况B：当前已经选中了棋子 ---
                else
                {
                    选择棋子.SetActive(false);

                    // ---- B1: 点击了棋盘（普通移动） ----
                    if (hit.transform.tag == "棋盘")
                    {
                        Vector3 localTargetPos = ChessBoardTransform.InverseTransformPoint(hit.point);
                        if (CheckMoveRules(SelectedID, localTargetPos.x, localTargetPos.y, -1))
                        {
                            AllowMove = true;

                            // 【坐标修正】Y坐标对应行，X坐标对应列
                            int targetLogY = ToolManager.ChangeToLineY(localTargetPos.y);
                            int targetLogX = ToolManager.ChangeToLineX(localTargetPos.x);
                            int startLogX = ToolManager.ChangeToLineX(ChessManager.ChessArray[SelectedID].Vec_X);
                            int startLogY = ToolManager.ChangeToLineY(ChessManager.ChessArray[SelectedID].Vec_Y);

                            // 记录步骤(用于悔棋)
                            SaveStep(IsRedTurn, startLogX, startLogY, targetLogX, targetLogY, SelectedID, false, -1);

                            // 将逻辑坐标转回世界坐标并统一Z轴
                            float centeredX = ToolManager.ChangeBackX(targetLogX);
                            float centeredY = ToolManager.ChangeBackY(targetLogY);
                            LocalTargetPos = new Vector3(centeredX, centeredY, 0);
                            ClickPosition = ChessBoardTransform.TransformPoint(new Vector3(centeredX, centeredY, -2f));

                            // 联网发送：移动棋子
                            //ChessNetwork.Instance.SendMove(SelectedID, targetLogX, targetLogY);

                            SelectedID1 = SelectedID;
                            SelectedID = -1;
                            IsRedTurn = !IsRedTurn;
                        }
                        else
                        {
                            IsIllegal = true; WorringSign1.SetActive(true); SelectedID = -1;
                        }
                    }
                    // ---- B2: 点击了其他棋子（吃子或换选） ----
                    else if (hit.transform.tag == "红棋" || hit.transform.tag == "黑棋")
                    {
                        int targetChessId = ToolManager.GetChessId(hit.transform.gameObject);

                        // 如果点的是自己人，切换选中框
                        if (ChessManager.ChessArray[SelectedID].Is_Red == ChessManager.ChessArray[targetChessId].Is_Red)
                        {
                            SelectedID = targetChessId;
                            SelectedChessObj = hit.transform.gameObject;
                            选择棋子.transform.position = hit.transform.position;
                            选择棋子.SetActive(true);
                            return;
                        }

                        // 如果点的是敌人，尝试吃子
                        Vector3 localTargetPos = ChessBoardTransform.InverseTransformPoint(hit.point);
                        if (CheckMoveRules(SelectedID, localTargetPos.x, localTargetPos.y, targetChessId))
                        {
                            // 标记死亡，但不Destroy，等动画结束再隐藏
                            ChessManager.ChessArray[targetChessId].Is_Dead = true;
                            pendingCaptureID = targetChessId;
                            AllowMove = true;

                            int targetLogY = ToolManager.ChangeToLineY(localTargetPos.y);
                            int targetLogX = ToolManager.ChangeToLineX(localTargetPos.x);
                            int startLogX = ToolManager.ChangeToLineX(ChessManager.ChessArray[SelectedID].Vec_X);
                            int startLogY = ToolManager.ChangeToLineY(ChessManager.ChessArray[SelectedID].Vec_Y);

                            // 记录步骤
                            SaveStep(IsRedTurn, startLogX, startLogY, targetLogX, targetLogY, SelectedID, true, targetChessId);

                            float centeredX = ToolManager.ChangeBackX(targetLogX);
                            float centeredY = ToolManager.ChangeBackY(targetLogY);
                            LocalTargetPos = new Vector3(centeredX, centeredY, 0);
                            ClickPosition = ChessBoardTransform.TransformPoint(new Vector3(centeredX, centeredY, -2f));

                            // 联网发送：吃子
                            //ChessNetwork.Instance.SendMove(SelectedID, targetLogX, targetLogY);

                            SelectedID1 = SelectedID;
                            SelectedID = -1;
                            IsRedTurn = !IsRedTurn;
                        }
                        else
                        {
                            IsIllegal = true; WorringSign1.SetActive(true); SelectedID = -1;
                        }
                    }
                    else
                    {
                        SelectedID = -1;
                    }
                }
            }
        }

        // ========== 4. UI警告计时器 ==========
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

    // ==================== 回合与规则判定 ====================

    public bool WhichTurn(int SelectedID)
    {
        if (ChessManager.ChessArray[SelectedID].Is_Red == IsRedTurn) { IsIllegal = false; return true; }
        else { IsIllegal = true; return false; }
    }

    public void ClearSelectionUI()
    {
        SelectedID = -1;
        选择棋子.SetActive(false);
    }

public static bool CheckMoveRules(int selectedId, float VecX, float VecY, int destroyId) {
    int targetLogX = ToolManager.ChangeToLineX(VecX);
    int targetLogY = ToolManager.ChangeToLineY(VecY);
    ChessManager.Chess chess = ChessManager.ChessArray[selectedId];
    bool ruleValid = false;

    // 1. 基础规则检测（车、马、炮等）
    switch (chess.Type) {
        case ChessManager.Chess.ChessType.车:
            ruleValid = 规则检测.车(selectedId, targetLogY, targetLogX, destroyId);
            break;
        case ChessManager.Chess.ChessType.马:
            ruleValid = 规则检测.马(selectedId, targetLogY, targetLogX, destroyId);
            break;
        case ChessManager.Chess.ChessType.炮:
            ruleValid = 规则检测.炮(selectedId, targetLogY, targetLogX, destroyId);
            break;
        case ChessManager.Chess.ChessType.象:
            ruleValid = 规则检测.相(selectedId, targetLogY, targetLogX, destroyId);
            break;
        case ChessManager.Chess.ChessType.士:
            ruleValid = 规则检测.士(selectedId, targetLogY, targetLogX, destroyId);
            break;
        case ChessManager.Chess.ChessType.帅:
            ruleValid = 规则检测.将(selectedId, targetLogY, targetLogX, destroyId);
            break;
        case ChessManager.Chess.ChessType.卒:
            ruleValid = 规则检测.兵(selectedId, targetLogY, targetLogX, destroyId);
            break;
        default:
            return false;
    }

        // 2. 送将预检（基础规则通过后执行）
        if (ruleValid)
        {
            bool isRedTurn = ChessManager.ChessArray[selectedId].Is_Red;
            // 【修改点】增加第三个参数 destroyId，传入被吃的棋子
            bool isCheck = 规则检测.送将检测(selectedId, targetLogX, targetLogY, isRedTurn, destroyId);
            if (isCheck)
            {
                return false; // 送将，走法无效
            }
        }

        return ruleValid; // 基础规则+送将检测均通过，走法有效
}


    // ==================== 悔棋系统 ====================

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

    public void UndoMove()
    {
        // 防御判断：动画播放中或AI思考中禁止悔棋
        if (Instance.AllowMove || Instance.IsAIThinkingState) return;
        // 必须双方都有记录才能执行同时撤销
        if (RedStep.ChessId == -1 || BlackStep.ChessId == -1) return;

        // 1. 撤销红方棋子移动
        ChessManager.Chess redChess = ChessManager.ChessArray[RedStep.ChessId];
        redChess.Obj.transform.localPosition = new Vector3(ChangeBackX(RedStep.StartLogX), ChangeBackY(RedStep.StartLogY), -2f);
        redChess.Vec_X = redChess.Obj.transform.localPosition.x;
        redChess.Vec_Y = redChess.Obj.transform.localPosition.y;

        // 2. 撤销黑方棋子移动
        ChessManager.Chess blackChess = ChessManager.ChessArray[BlackStep.ChessId];
        blackChess.Obj.transform.localPosition = new Vector3(ChangeBackX(BlackStep.StartLogX), ChangeBackY(BlackStep.StartLogY), -2f);
        blackChess.Vec_X = blackChess.Obj.transform.localPosition.x;
        blackChess.Vec_Y = blackChess.Obj.transform.localPosition.y;

        // 3. 复活红方被吃的子
        if (RedStep.KillOthers && RedStep.KilledId != -1)
        {
            ChessManager.Chess capturedRed = ChessManager.ChessArray[RedStep.KilledId];
            capturedRed.Is_Dead = false;
            capturedRed.Obj.transform.localPosition = new Vector3(ChangeBackX(RedStep.LogX), ChangeBackY(RedStep.LogY), -2f);
            capturedRed.Obj.SetActive(true);
        }

        // 4. 复活黑方被吃的子
        if (BlackStep.KillOthers && BlackStep.KilledId != -1)
        {
            ChessManager.Chess capturedBlack = ChessManager.ChessArray[BlackStep.KilledId];
            capturedBlack.Is_Dead = false;
            capturedBlack.Obj.transform.localPosition = new Vector3(ChangeBackX(BlackStep.LogX), ChangeBackY(BlackStep.LogY), -2f);
            capturedBlack.Obj.SetActive(true);
        }

        // 5. 清除UI状态并重置记录
        Instance.ClearSelectionUI();
        RedStep.ChessId = -1;
        BlackStep.ChessId = -1;
    }

    // ==================== AI 系统 ====================

    private IEnumerator AIThinkCoroutine()
    {
        isAIThinking = true;
        Debug.Log("[AI] 开始思考...");
        yield return new WaitForSeconds(aiThinkDelay);

        // 注：这里使用新版的 AIManager，如果你的项目还是旧版的 ChessAI，请替换下一行代码
        AIMove bestMove = AIManager.Instance.GetBestMove();

        if (!bestMove.IsValid)
        {
            Debug.Log("[AI] 没有有效走法，游戏可能结束");
            isAIThinking = false;
            yield break;
        }

        yield return ExecuteAIMove(bestMove);
        isAIThinking = false;
        Debug.Log("[AI] 思考完成，回合切换");
    }

    //private IEnumerator ExecuteAIMove(AIMove move)
    //{
    //    ChessManager.Chess chess = ChessManager.ChessArray[move.FromChessId];
    //    if (chess == null || chess.Is_Dead) { Debug.LogError("[AI] 无效的棋子ID"); yield break; }

    //    SelectedChessObj = chess.Obj;

    //    // 处理吃子标记
    //    if (move.CaptureId >= 0 && move.CaptureId < 32)
    //    {
    //        ChessManager.Chess captured = ChessManager.ChessArray[move.CaptureId];
    //        if (captured != null && !captured.Is_Dead)
    //        {
    //            captured.Is_Dead = true;
    //            pendingCaptureID = move.CaptureId;
    //        }
    //    }

    //    // 计算目标世界坐标
    //    LocalTargetPos = new Vector3(ToolManager.ChangeBackX(move.ToLogX), ToolManager.ChangeBackY(move.ToLogY), 0);
    //    Vector3 worldPos = ChessBoardTransform.TransformPoint(LocalTargetPos);
    //    ClickPosition = new Vector3(worldPos.x, worldPos.y, -2f);

    //    // 记录步骤
    //    int startLogX = ToolManager.ChangeToLineX(chess.Vec_X);
    //    int startLogY = ToolManager.ChangeToLineY(chess.Vec_Y);
    //    bool isCapture = (move.CaptureId >= 0 && move.CaptureId < 32);
    //    SaveStep(IsRedTurn, startLogX, startLogY, move.ToLogX, move.ToLogY, move.FromChessId, isCapture, move.CaptureId);

    //    // 提前更新逻辑坐标(防止路径判断出错)
    //    chess.Vec_X = LocalTargetPos.x;
    //    chess.Vec_Y = LocalTargetPos.y;

    //    AllowMove = true;
    //    SelectedID1 = move.FromChessId;

    //    // AI独立运行移动动画，避免与Update冲突
    //    while (AllowMove)
    //    {
    //        SelectedChessObj.transform.position = Vector3.MoveTowards(SelectedChessObj.transform.position, ClickPosition, Time.deltaTime * 10);
    //        if (Vector3.Distance(SelectedChessObj.transform.position, ClickPosition) < 0.1f)
    //        {
    //            SelectedChessObj.transform.position = ClickPosition;

    //            if (pendingCaptureID != -1)
    //            {
    //                if (ChessManager.ChessArray[pendingCaptureID] != null && ChessManager.ChessArray[pendingCaptureID].Obj != null)
    //                    ChessManager.ChessArray[pendingCaptureID].Obj.SetActive(false);
    //                pendingCaptureID = -1;
    //            }
    //            AllowMove = false;
    //        }
    //        yield return null;
    //    }

    //    SelectedID1 = -1;
    //    SelectedChessObj = null;
    //    IsRedTurn = true; // 换回红方
    //}
    private IEnumerator ExecuteAIMove(AIMove move)
    {
        ChessManager.Chess chess = ChessManager.ChessArray[move.FromChessId];
        if (chess == null || chess.Is_Dead)
        {
            Debug.LogError("[AI] 无效的棋子ID");
            yield break;
        }

        // 计算目标本地坐标
        LocalTargetPos = new Vector3(ToolManager.ChangeBackX(move.ToLogX), ToolManager.ChangeBackY(move.ToLogY), 0);

        // ========== 新增：AI走步合法性安全网 ==========
        // 用新版的 CheckMoveRules 验证 AI 的走步（包含送将检测）
        int captureId = (move.CaptureId >= 0 && move.CaptureId < 32) ? move.CaptureId : -1;
        if (!CheckMoveRules(move.FromChessId, LocalTargetPos.x, LocalTargetPos.y, captureId))
        {
            Debug.LogError("[AI] 尝试走出非法棋步(可能是送将)！已拦截。");
            // 终止这次移动，并强制换回红方回合，防止游戏卡死
            IsRedTurn = true;
            yield break;
        }
        // ==========================================

        SelectedChessObj = chess.Obj;

        // 处理吃子标记
        if (move.CaptureId >= 0 && move.CaptureId < 32)
        {
            ChessManager.Chess captured = ChessManager.ChessArray[move.CaptureId];
            if (captured != null && !captured.Is_Dead)
            {
                captured.Is_Dead = true;
                pendingCaptureID = move.CaptureId;
            }
        }

        // 计算目标世界坐标
        Vector3 worldPos = ChessBoardTransform.TransformPoint(LocalTargetPos);
        ClickPosition = new Vector3(worldPos.x, worldPos.y, -2f);

        // 记录步骤
        int startLogX = ToolManager.ChangeToLineX(chess.Vec_X);
        int startLogY = ToolManager.ChangeToLineY(chess.Vec_Y);
        bool isCapture = (move.CaptureId >= 0 && move.CaptureId < 32);
        SaveStep(IsRedTurn, startLogX, startLogY, move.ToLogX, move.ToLogY, move.FromChessId, isCapture, move.CaptureId);

        // 提前更新逻辑坐标(防止路径判断出错)
        chess.Vec_X = LocalTargetPos.x;
        chess.Vec_Y = LocalTargetPos.y;
        AllowMove = true;
        SelectedID1 = move.FromChessId;

        // AI独立运行移动动画，避免与Update冲突
        while (AllowMove)
        {
            SelectedChessObj.transform.position = Vector3.MoveTowards(SelectedChessObj.transform.position, ClickPosition, Time.deltaTime * 10);
            if (Vector3.Distance(SelectedChessObj.transform.position, ClickPosition) < 0.1f)
            {
                SelectedChessObj.transform.position = ClickPosition;
                if (pendingCaptureID != -1)
                {
                    if (ChessManager.ChessArray[pendingCaptureID] != null && ChessManager.ChessArray[pendingCaptureID].Obj != null)
                        ChessManager.ChessArray[pendingCaptureID].Obj.SetActive(false);
                    pendingCaptureID = -1;
                }
                AllowMove = false;
            }
            yield return null;
        }
        SelectedID1 = -1;
        SelectedChessObj = null;
        IsRedTurn = true; // 换回红方
    }

}
