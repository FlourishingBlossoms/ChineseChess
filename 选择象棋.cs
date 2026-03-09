using UnityEngine;
using System.Collections;
public class 选择象棋 : MonoBehaviour
{
    // 棋子选择相关变量
    private GameObject 选择棋子;      // 选择的棋子对象

    public GameObject 选择框;        // 选择框预制体
    public int SelectedID = -1;     // 当前选中的棋子ID，-1表示未选中
    public int SelectedID1 = -1;     // 用于移动动画的临时ID

    public bool AllowMove = false;   // 是否允许移动
    private GameObject SelectedChessObj; // 当前选中的棋子游戏对象
    public Vector3 ClickPosition;   // 点击位置
    public Vector3 LocalTargetPos;
    // 回合控制变量
    public bool IsRedTurn = true;   // 当前是否是红方回合

    public GameObject WorringSign1;   // 警告提示UI
    public GameObject WorringSign2;    // 警告提示UI
    private bool IsIllegal = false;  // 是否为非法移动
    private bool IsIllegal2 = false; // 是否为非法移动
    private float TimeCounter = 0;   // 时间计数器
    private float TimeCounter2 = 0;
    private bool isAIThinking = false; // 防止AI思考时玩家乱点
    public Transform ChessBoardTransform; // 在Inspector面板中拖拽棋盘物体到这里

    // 判断当前选中棋子是否属于当前回合
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
    }

    private void Update()
    {
        if (!IsRedTurn && !AllowMove && !isAIThinking)
        {
            StartCoroutine(AIThinkCoroutine());
            return; // 必须return，阻止后续的鼠标点击代码执行
        }

        if (Input.GetMouseButtonDown(0))
        {
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
                        LocalTargetPos = localTargetPos;
                        if (CheckMoveRules(SelectedID, localTargetPos.x, localTargetPos.y, -1))
                        {
                            AllowMove = true;

                            // 【修正3】变量名统一
                            // Y坐标对应行(LogY)，X坐标对应列
                            int targetLogY = ToolManager.ChangeToLineY(localTargetPos.y);
                            int targetLogX = ToolManager.ChangeToLineX(localTargetPos.x);

                            // 将逻辑坐标转回世界坐标
                            // ChangeBackX 需要列 -> 传入 targetLogX
                            // ChangeBackY 需要行 -> 传入 targetLogY
                            float centeredX = ToolManager.ChangeBackX(targetLogX);
                            float centeredY = ToolManager.ChangeBackY(targetLogY);

                            ClickPosition = ChessBoardTransform.TransformPoint(new Vector3(centeredX, centeredY, 0));

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

                        // 【修复重点】必须在这里也更新 LocalTargetPos
                        // 否则移动结束后，数据会存储成错误的坐标
                        LocalTargetPos = localTargetPos;
                        if (CheckMoveRules(SelectedID, localTargetPos.x, localTargetPos.y, targetChessId))
                        {
                            // 吃子
                            ChessManager.ChessArray[targetChessId].Is_Dead = true;
                            Destroy(hit.transform.gameObject);

                            AllowMove = true;
                            ClickPosition = hit.transform.position;

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

        // 执行移动动画
        if (AllowMove)
        {
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

                AllowMove = false;
                SelectedID = -1;
                SelectedID1 = -1;
                SelectedChessObj = null;
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
    IEnumerator AIThinkCoroutine()
    {
        isAIThinking = true;
        Debug.Log("AI思考中...");
        yield return new WaitForSeconds(0.5f); // 假装思考一会儿，让游戏看起来自然

        // 1. 让AI计算走法
        ChessAI.MoveData bestMove = ChessAI.Instance.GetBestMove(false); // false代表黑方

        // 2. 执行走法 (复用你的移动逻辑)
        ExecuteAIMove(bestMove);

        isAIThinking = false;
    }

    // 执行AI的走法
    void ExecuteAIMove(ChessAI.MoveData move)
    {
        // 选中棋子
        SelectedID = move.ChessId;
        SelectedChessObj = ChessManager.ChessArray[move.ChessId].Obj;

        // 计算目标位置
        float targetX = ToolManager.ChangeBackX(move.TargetLogX);
        float targetY = ToolManager.ChangeBackY(move.TargetLogY);

        LocalTargetPos = new Vector3(targetX, targetY, 0);
        ClickPosition = ChessBoardTransform.TransformPoint(LocalTargetPos);

        // 处理吃子
        if (move.KillId != -1)
        {
            ChessManager.ChessArray[move.KillId].Is_Dead = true;
            Destroy(ChessManager.ChessArray[move.KillId].Obj);
        }

        // 触发移动动画
        AllowMove = true;
        SelectedID1 = SelectedID;
        SelectedID = -1; // 清空选择

        // 切换回合
        IsRedTurn = true; // 换回红方
    }
}
