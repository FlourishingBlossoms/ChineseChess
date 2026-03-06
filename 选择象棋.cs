using UnityEngine;

//由于判断规则用的是在棋盘为父物体时的坐标，而射线检测用的是世界坐标
public class 选择象棋 : MonoBehaviour
{
    // 棋子选择相关变量
    private GameObject 选择棋子;      // 选择的棋子对象

    public GameObject 选择框;        // 选择框预制体
    public int SelectedID = -1;     // 当前选中的棋子ID，-1表示未选中
    public int SelectedID1 = -1;     // 当前选中的棋子ID，-1表示未选中

    public bool AllowMove = false;   // 是否允许移动
    private GameObject SelectedChessObj; // 当前选中的棋子游戏对象
    public Vector3 ClickPosition;   // 点击位置

    // 回合控制变量
    public bool IsRedTurn = true;   // 当前是否是红方回合

    public GameObject WorringSign1;   // 警告提示UI
    public GameObject WorringSign2;    // 警告提示UI
    private bool IsIllegal = false;  // 是否为非法移动
    private bool IsIllegal2 = false; // 是否为非法移动
    private float TimeCounter = 0;   // 时间计数器
    private float TimeCounter2 = 0;

    public Transform ChessBoardTransform; // 在Inspector面板中拖拽棋盘物体到这里

    //调试信息
    public float hitx;

    public float hity;
    public int hitraw;
    public int hitcol;
    public float ChessX;
    public float ChessY;
    public float ChessRaw;
    public float ChessCol;

    // 判断当前选中棋子是否属于当前回合
    // 参数: SelectedID - 棋子ID
    // 返回: bool - 是否属于当前回合
    public bool WhichTurn(int SelectedID)
    {
        // 检查棋子颜色是否与当前回合一致
        if (ChessManager.ChessArray[SelectedID].Is_Red == IsRedTurn)
        {
            IsIllegal = false;  // 重置非法移动标志
            return true;        // 属于当前回合
        }
        else
        {
            IsIllegal = true;   // 设置非法移动标志
            return false;       // 不属于当前回合
        }
    }

    // 根据棋子类型调用相应的规则检测方法
    // 参数: selectedId - 选中的棋子ID
    //       targetRow - 目标行
    //       targetCol - 目标列
    //       destroyId - 被吃掉的棋子ID（-1表示无）
    // 返回: bool - 移动是否合法

    public bool CheckMoveRules(int selectedId, float VecX, float VecY, int destroyId)
    {
        // 获取棋子信息
        int targetRow = ToolManager.ChangeToLineX(VecX);
        int targetCol = ToolManager.ChangeToLineY(VecY);
        ChessManager.Chess chess = ChessManager.ChessArray[selectedId];

        // 根据棋子类型调用对应的规则检测方法
        switch (chess.Type)
        {
            case ChessManager.Chess.ChessType.车:
                return 规则检测.车(selectedId, targetRow, targetCol, destroyId);

            case ChessManager.Chess.ChessType.马:
                return 规则检测.马(selectedId, targetRow, targetCol, destroyId);

            case ChessManager.Chess.ChessType.炮:
                return 规则检测.炮(selectedId, targetRow, targetCol, destroyId);

            case ChessManager.Chess.ChessType.象:
                return 规则检测.相(selectedId, targetRow, targetCol, destroyId);

            case ChessManager.Chess.ChessType.士:
                return 规则检测.士(selectedId, targetRow, targetCol, destroyId);

            case ChessManager.Chess.ChessType.帅:
                return 规则检测.将(selectedId, targetRow, targetCol, destroyId);

            case ChessManager.Chess.ChessType.卒:
                return 规则检测.兵(selectedId, targetRow, targetCol, destroyId);

            default:
                return false; // 默认返回非法
        }
    }

    // 初始化方法
    private void Awake()
    {
        // 初始化警告提示
        WorringSign1.SetActive(false);
        WorringSign2.SetActive(false);

        // 实例化选择框
        选择棋子 = Instantiate(选择框, Vector3.zero, Quaternion.identity);
        选择棋子.SetActive(false); // 初始不显示
    }

    // 游戏主循环
    private void Update()
    {
        // 检测鼠标左键按下
        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log("【点击】检测到鼠标左键按下");

            // 将屏幕坐标转换为世界坐标
            Vector2 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);

            // 进行2D射线检测
            RaycastHit2D hit = Physics2D.Raycast(mousePosition, Vector2.zero);

            // 判断是否击中物体
            if (hit.collider != null)
            {
                Vector3 LocalTargetPos = ChessBoardTransform.InverseTransformPoint(hit.point);

                hitx = LocalTargetPos.x;
                hity = LocalTargetPos.y;
                hitraw = ToolManager.ChangeToLineX(LocalTargetPos.x);
                hitcol = ToolManager.ChangeToLineY(LocalTargetPos.y);
                // 检查是否轮到当前玩家
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
                    // 检查是否点击了棋子
                    if (hit.transform.tag == "红棋" || hit.transform.tag == "黑棋")
                    {
                        // 选中棋子
                        SelectedID = ToolManager.GetChessId(hit.transform.gameObject);
                        SelectedChessObj = hit.transform.gameObject;
                        ChessX= hit.point.x;//调试信息
                        ChessY = hit.point.y;
                        ChessRaw = ToolManager.ChangeToLineX(hit.point.x);
                        ChessCol = ToolManager.ChangeToLineY(hit.point.y);//
                        选择棋子.transform.position = hit.transform.position;
                        选择棋子.SetActive(true); // 显示选择框
                    }
                }
                // === 情况2：当前已经选中了棋子 ===
                else
                {
                    // 隐藏选择框
                    选择棋子.SetActive(false);

                    // 检查是否点击了棋盘
                    if (hit.transform.tag == "棋盘")
                    {  // hit.point 是鼠标点击的真实世界坐标
                        Vector3 localTargetPos = ChessBoardTransform.InverseTransformPoint(hit.point);

                        if (CheckMoveRules(SelectedID, localTargetPos.x, localTargetPos.y, -1))
                        {
                            // 允许移动
                            AllowMove = true;
                            int targetRow = ToolManager.ChangeToLineY(localTargetPos.y); // Y对应行
                            int targetCol = ToolManager.ChangeToLineX(localTargetPos.x); // X对应列

                            // 2. 将行列转回标准的局部坐标（修正 ChangeBackX 的错误，手动计算）
                            // 修正公式：(col - 4) * 0.5f，而不是源代码里的 * 2
                            float centeredX = ToolManager.ChangeBackX(targetCol);
                            float centeredY = ToolManager.ChangeBackY(targetRow); // 使用你定义好的偏移坐标
                            // 3. 将修正后的局部坐标转回世界坐标，赋值给 ClickPosition
                            ClickPosition = ChessBoardTransform.TransformPoint(new Vector3(centeredX, centeredY, 0));
                            SelectedID1= SelectedID;
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
                    // 检查是否点击了其他棋子
                    else if (hit.transform.tag == "红棋" || hit.transform.tag == "黑棋")
                    {
                        int targetChessId = ToolManager.GetChessId(hit.transform.gameObject);
                        if (ChessManager.ChessArray[SelectedID].Is_Red == ChessManager.ChessArray[targetChessId].Is_Red)
                        {
                            // 点击的是自家棋子，判定为非法移动或重新选择（根据需求定）
                            IsIllegal = true;
                            WorringSign1.SetActive(true);
                            SelectedID = -1; // 取消选择
                            return;
                        }
                        Vector3 localTargetPos = ChessBoardTransform.InverseTransformPoint(hit.point);

                        if (CheckMoveRules(SelectedID, localTargetPos.x, localTargetPos.y, targetChessId))
                        {
                            // 检查是否点击了自己的棋子
                            if (hit.transform.gameObject != SelectedChessObj)
                            {
                                // 吃子
                                ChessManager.ChessArray[ToolManager.GetChessId(hit.transform.gameObject)].Is_Dead = true;
                                Destroy(hit.transform.gameObject);

                                AllowMove = true;
                                ClickPosition = hit.transform.position;
                                SelectedID1 = SelectedID;
                                SelectedID = -1;
                                IsRedTurn = !IsRedTurn;
                            }
                            else
                            {
                                // 取消选择
                                SelectedID = -1;
                                SelectedChessObj = null;
                            }
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
                        // 取消选择
                        SelectedID = -1;
                    }

                    SelectedID = -1;
                }
            }
            else
            {
                // 未击中任何物体
                Debug.LogWarning("【2D射线】没有击中任何物体！请检查是否添加了Collider2D。");
            }
        }
        // 执行移动动画
        if (AllowMove)
        {
            // 直接移动到计算好的 ClickPosition
            SelectedChessObj.transform.position = Vector3.MoveTowards(
                SelectedChessObj.transform.position,
                ClickPosition, // 这里必须是纯世界坐标
                Time.deltaTime * 10
            );

            if (Vector3.Distance(SelectedChessObj.transform.position, ClickPosition) < 0.1f)
            {
                SelectedChessObj.transform.position = ClickPosition;
                ChessManager.ChessArray[SelectedID1].Vec_X = ClickPosition.x;
                ChessManager.ChessArray[SelectedID1].Vec_Y = ClickPosition.y;

                AllowMove = false;
                SelectedID = -1;
                SelectedID1 = -1;
                SelectedChessObj = null;
            }
        }
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
}
