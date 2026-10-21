using System;
using UnityEngine;
using static ChessManager;

public class 规则检测 : MonoBehaviour
{

    public static bool 车(int SelectedId, int TargetLogY, int TargetLogX, int DistoryID)
    {
        // 【修正】变量名与实际含义对齐
        // ChangeToLineX 返回列 -> 赋值给 StartLogX
        int StartLogX = ToolManager.ChangeToLineX(ChessManager.ChessArray[SelectedId].Vec_X);
        // ChangeToLineY 返回行 -> 赋值给 StartLogY
        int StartLogY = ToolManager.ChangeToLineY(ChessManager.ChessArray[SelectedId].Vec_Y);

        // 【修正】逻辑判断：行相同 或 列相同
        if (StartLogY == TargetLogY || StartLogX == TargetLogX)
        {
            // 【修正】CountLineChess 参数顺序已修正为
            int pathCount = ToolManager.CountLineChess(StartLogX, StartLogY, TargetLogX, TargetLogY);

            // #region agent log

            if (pathCount == 0)
            {
                 return true;
            }
            else
            {
                return false;
            }
        }
        else
        {
            return false;
        }
    }
    public static bool 马(int SelectedId, int TargetLogY, int TargetLogX, int DistoryID)
    {
        int StartLogX = ToolManager.ChangeToLineX(ChessManager.ChessArray[SelectedId].Vec_X);
        int StartLogY = ToolManager.ChangeToLineY(ChessManager.ChessArray[SelectedId].Vec_Y);

        int dy = Math.Abs(StartLogY - TargetLogY);
        int dx = Math.Abs(StartLogX - TargetLogX);

        // 情况1：纵向走2格，横向走1格
        if (dy == 2 && dx == 1)
        {
            // 蹩马腿：检查纵向中点是否有棋子
            int midY = (StartLogY + TargetLogY) / 2;
            if (ToolManager.GetChessId(StartLogX, midY) == -1)
                return true;
        }
        // 情况2：横向走2格，纵向走1格
        else if (dy == 1 && dx == 2)
        {
            // 蹩马腿：检查横向中点是否有棋子
            int midX = (StartLogX + TargetLogX) / 2;
            if (ToolManager.GetChessId(midX, StartLogY) == -1)
                return true;
        }

        return false;
    }
    public static bool 炮(int SelectedId, int TargetLogY, int TargetLogX, int DistoryID)
    {
        int StartLogX = ToolManager.ChangeToLineX(ChessManager.ChessArray[SelectedId].Vec_X);
        int StartLogY = ToolManager.ChangeToLineY(ChessManager.ChessArray[SelectedId].Vec_Y);

        if (DistoryID == -1)
        {
            // 移动：逻辑同车
            return 车(SelectedId, TargetLogY, TargetLogX, DistoryID);
        }
        else
        {
            // 吃子：必须隔一个子
            // 【修正】判断直线
            if (StartLogY == TargetLogY || StartLogX == TargetLogX)
            {
                // 【修正】参数顺序
                int pathCount = ToolManager.CountLineChess(StartLogX, StartLogY, TargetLogX, TargetLogY);

                // #region agent log

                if (pathCount == 1)
                {
                    return true;
                }
            }
            return false;
        }
    }

    public static bool 相(int SelectedId, int TargetLogY, int TargetLogX, int destoryId)
    {
        int StartLogX = ToolManager.ChangeToLineX(ChessManager.ChessArray[SelectedId].Vec_X);
        int StartLogY = ToolManager.ChangeToLineY(ChessManager.ChessArray[SelectedId].Vec_Y);

        // 【修正】参数顺序
        int Side = ToolManager.Relation(StartLogX, StartLogY, TargetLogX, TargetLogY);
        if (Side != 22) return false;

        // 计算象眼位置
        int eyeLogY = (TargetLogY + StartLogY) / 2;
        int eyeLogX = (TargetLogX + StartLogX) / 2;

        // 【修正】GetChessId 参数顺序
        if (ToolManager.GetChessId(eyeLogX, eyeLogY) != -1) return false;

        // 检查过河
        if (ChessManager.ChessArray[SelectedId].Is_Red == false) // 黑方
        {
            // 黑方只能在下方 (LogY 5-9)，不能去上方 (LogY < 5)
            // 注意：原代码写的是 row < 4，根据上下文如果是标准棋盘，黑方一般在 5-9
            // 这里保持原逻辑意图，但变量名修正
            if (TargetLogY < 5) return false;
        }
        else // 红方
        {
            // 红方只能在上方 (LogY 0-4)，不能去下方 (LogY > 4)
            if (TargetLogY > 4) return false;
        }

        return true;
    }

    public static bool 兵(int SelectedId, int TargetLogY, int TargetLogX, int destoryId)
    {
        int StartLogX = ToolManager.ChangeToLineX(ChessManager.ChessArray[SelectedId].Vec_X);
        int StartLogY = ToolManager.ChangeToLineY(ChessManager.ChessArray[SelectedId].Vec_Y);

        int Side = ToolManager.Relation(StartLogX, StartLogY, TargetLogX, TargetLogY);

        if (Side != 1 && Side != 10) return false;

        if (ChessManager.ChessArray[SelectedId].Is_Red == false) // 黑方
        {
            if (TargetLogY > StartLogY) return false;
            if (StartLogY > 4 && StartLogX != TargetLogX) return false;
        }
        else // 红方
        {
            if (TargetLogY < StartLogY) return false;

            if (StartLogY < 5 && StartLogX != TargetLogX) return false;
        }

        return true;
    }
    public static bool 将(int SelectedId, int TargetLogY, int TargetLogX, int destoryId)
    {
        // 将帅对面检测
        if (destoryId != -1 && ChessManager.ChessArray[destoryId].Type == ChessManager.Chess.ChessType.帅)
            return 车(SelectedId, TargetLogY, TargetLogX, destoryId);

        // 九宫格列限制 (LogX: 3-5)
        if (TargetLogX < 3 || TargetLogX > 5) return false;

        if (ChessManager.ChessArray[SelectedId].Is_Red == false) // 黑方
        {
            // 黑方九宫行范围 (LogY 7-9)
            if (TargetLogY < 7 || TargetLogY > 9) return false;
        }
        else // 红方
        {
            // 红方九宫行范围 (LogY 0-2)
            if (TargetLogY < 0 || TargetLogY > 2) return false;
        }

        int StartLogX = ToolManager.ChangeToLineX(ChessManager.ChessArray[SelectedId].Vec_X);
        int StartLogY = ToolManager.ChangeToLineY(ChessManager.ChessArray[SelectedId].Vec_Y);

        // 【修正】参数顺序
        int Side = ToolManager.Relation(StartLogX, StartLogY, TargetLogX, TargetLogY);
        if (Side != 1 && Side != 10) return false;

        return true;
    }

    /// <summary>
    /// 士的走棋规则
    /// </summary>
    public static bool 士(int SelectedId, int TargetLogY, int TargetLogX, int destoryId)
    {
        if (ChessManager.ChessArray[SelectedId].Is_Red == false) // 黑方
        {
            if (TargetLogY < 7 || TargetLogY > 9) return false;
        }
        else // 红方
        {
            if (TargetLogY < 0 || TargetLogY > 2) return false;
        }

        if (TargetLogX < 3 || TargetLogX > 5) return false;

        int StartLogX = ToolManager.ChangeToLineX(ChessManager.ChessArray[SelectedId].Vec_X);
        int StartLogY = ToolManager.ChangeToLineY(ChessManager.ChessArray[SelectedId].Vec_Y);

        // 【修正】参数顺序
        int Side = ToolManager.Relation(StartLogX, StartLogY, TargetLogX, TargetLogY);
        if (Side != 11) return false;

        return true;
    }
    /// <summary>
    /// 模拟走棋后，检查己方将/帅是否被攻击（送将检测）
    /// </summary>
    /// <param name="selectedId">选中的棋子ID</param>
    /// <param name="targetLogX">目标位置的列（逻辑坐标）</param>
    /// <param name="targetLogY">目标位置的行（逻辑坐标）</param>
    /// <param name="isRedTurn">当前回合（true=红方，false=黑方）</param>
    /// <returns>是否送将（true=送将，走法无效；false=安全）</returns>
    //public static bool 送将检测(int selectedId, int targetLogX, int targetLogY, bool isRedTurn)
    //{
    //    // 1. 获取己方将/帅的位置
    //    int myKingId = -1;
    //    float myKingX = 0, myKingY = 0;
    //    foreach (var chess in ChessManager.ChessArray)
    //    {
    //        if (chess != null && !chess.Is_Dead && chess.Is_Red == isRedTurn && chess.Type == ChessManager.Chess.ChessType.帅)
    //        {
    //            myKingId = chess.Id;
    //            myKingX = chess.Vec_X;
    //            myKingY = chess.Vec_Y;
    //            break;
    //        }
    //    }
    //    if (myKingId == -1) return false; // 未找到将/帅（游戏可能结束）

    //    // 2. 模拟移动选中的棋子（临时修改位置，不实际提交）
    //    Chess selectedChess = ChessManager.ChessArray[selectedId];
    //    float originalX = selectedChess.Vec_X;
    //    float originalY = selectedChess.Vec_Y;
    //    selectedChess.Vec_X = ToolManager.ChangeBackX(targetLogX); // 转换为世界坐标
    //    selectedChess.Vec_Y = ToolManager.ChangeBackY(targetLogY);

    //    // 3. 检查将/帅是否被对方棋子攻击
    //    bool isCheck = false;
    //    foreach (var enemyChess in ChessManager.ChessArray)
    //    {
    //        if (enemyChess != null && !enemyChess.Is_Dead && enemyChess.Is_Red != isRedTurn)
    //        {
    //            // 获取将/帅的逻辑坐标（用于规则检测）
    //            int kingLogX = ToolManager.ChangeToLineX(myKingX);
    //            int kingLogY = ToolManager.ChangeToLineY(myKingY);
    //            bool canAttack = false;

    //            // 根据对方棋子类型，调用对应的规则检测方法
    //            switch (enemyChess.Type)
    //            {
    //                case ChessManager.Chess.ChessType.车:
    //                    canAttack = 车(enemyChess.Id, kingLogY, kingLogX, -1);
    //                    break;
    //                case ChessManager.Chess.ChessType.马:
    //                    canAttack = 马(enemyChess.Id, kingLogY, kingLogX, -1);
    //                    break;
    //                case ChessManager.Chess.ChessType.炮:
    //                    canAttack = 炮(enemyChess.Id, kingLogY, kingLogX, 0);
    //                    break;
    //                case ChessManager.Chess.ChessType.象:
    //                    canAttack = 相(enemyChess.Id, kingLogY, kingLogX, -1);
    //                    break;
    //                case ChessManager.Chess.ChessType.士:
    //                    canAttack = 士(enemyChess.Id, kingLogY, kingLogX, -1);
    //                    break;
    //                case ChessManager.Chess.ChessType.卒:
    //                    canAttack = 兵(enemyChess.Id, kingLogY, kingLogX, -1);
    //                    break;
    //            }

    //            if (canAttack)
    //            {
    //                isCheck = true;
    //                break; // 发现送将，无需继续检查
    //            }
    //        }
    //    }

    //    // 4. 恢复选中棋子的原始位置（避免影响实际游戏状态）
    //    selectedChess.Vec_X = originalX;
    //    selectedChess.Vec_Y = originalY;

    //    return isCheck; // true=送将，false=安全
    //}
    /// <summary>
    /// 模拟走棋后，检查己方将/帅是否被攻击（送将检测）
    /// </summary>
    /// <param name="selectedId">选中的棋子ID</param>
    /// <param name="targetLogX">目标位置的列（逻辑坐标）</param>
    /// <param name="targetLogY">目标位置的行（逻辑坐标）</param>
    /// <param name="isRedTurn">当前回合（true=红方，false=黑方）</param>
    /// <param name="destroyId">被吃的棋子ID，-1表示未吃子</param>
    /// <returns>是否送将（true=送将，走法无效；false=安全）</returns>
    public static bool 送将检测(int selectedId, int targetLogX, int targetLogY, bool isRedTurn, int destroyId)
    {
        // 1. 获取己方将/帅的位置
        int myKingId = -1;
        float myKingX = 0, myKingY = 0;
        foreach (var chess in ChessManager.ChessArray)
        {
            if (chess != null && !chess.Is_Dead && chess.Is_Red == isRedTurn && chess.Type == ChessManager.Chess.ChessType.帅)
            {
                myKingId = chess.Id;
                myKingX = chess.Vec_X;
                myKingY = chess.Vec_Y;
                break;
            }
        }
        if (myKingId == -1) return false; // 未找到将/帅

        // 2. 模拟移动选中的棋子
        Chess selectedChess = ChessManager.ChessArray[selectedId];
        float originalX = selectedChess.Vec_X;
        float originalY = selectedChess.Vec_Y;
        selectedChess.Vec_X = ToolManager.ChangeBackX(targetLogX);
        selectedChess.Vec_Y = ToolManager.ChangeBackY(targetLogY);

        // 【新增】如果这步棋吃掉了对方棋子，必须临时标记为死亡，否则会影响路径计算(如炮架消失)
        bool capturedOriginalDeadState = false;
        Chess capturedChess = null;
        if (destroyId != -1 && destroyId >= 0 && destroyId < 32)
        {
            capturedChess = ChessManager.ChessArray[destroyId];
            capturedOriginalDeadState = capturedChess.Is_Dead;
            capturedChess.Is_Dead = true; // 临时死亡
        }

        // 3. 检查将/帅是否被对方棋子攻击
        bool isCheck = false;
        // ★修复致命bug：如果移动的棋子就是将/帅本身，必须检查将帅的新位置（目标位置）
        //   原代码用 myKingX/myKingY（旧位置），导致走王时检查的是空格子
        //   → 任何敌方棋子都能"攻击"空格子 → 误判送将 → 王无法移动 → "没将军但走不了"
        int kingLogX, kingLogY;
        if (selectedId == myKingId)
        {
            // 移动的就是王本身，用目标位置作为王的位置
            kingLogX = targetLogX;
            kingLogY = targetLogY;
        }
        else
        {
            // 移动的是其他棋子，用王的原始位置
            kingLogX = ToolManager.ChangeToLineX(myKingX);
            kingLogY = ToolManager.ChangeToLineY(myKingY);
        }

        foreach (var enemyChess in ChessManager.ChessArray)
        {
            if (enemyChess != null && !enemyChess.Is_Dead && enemyChess.Is_Red != isRedTurn)
            {
                bool canAttack = false;
                switch (enemyChess.Type)
                {
                    case ChessManager.Chess.ChessType.车:
                        canAttack = 车(enemyChess.Id, kingLogY, kingLogX, -1);
                        break;
                    case ChessManager.Chess.ChessType.马:
                        canAttack = 马(enemyChess.Id, kingLogY, kingLogX, -1);
                        break;
                    // 【修正1】炮必须传 myKingId，强制走吃子逻辑(中间隔1子)
                    case ChessManager.Chess.ChessType.炮:
                        canAttack = 炮(enemyChess.Id, kingLogY, kingLogX, myKingId);
                        break;
                    case ChessManager.Chess.ChessType.象:
                        canAttack = 相(enemyChess.Id, kingLogY, kingLogX, -1);
                        break;
                    case ChessManager.Chess.ChessType.士:
                        canAttack = 士(enemyChess.Id, kingLogY, kingLogX, -1);
                        break;
                    // 【修正2】新增将帅对面检测：不能直接调用将()，因为目标位置不在敌方九宫格内
                    case ChessManager.Chess.ChessType.帅:
                        int enemyKingLogX = ToolManager.ChangeToLineX(enemyChess.Vec_X);
                        if (enemyKingLogX == kingLogX) // 同列
                        {
                            int enemyKingLogY = ToolManager.ChangeToLineY(enemyChess.Vec_Y);
                            // 中间没有任何棋子，形成将帅对面
                            if (ToolManager.CountLineChess(enemyKingLogX, enemyKingLogY, kingLogX, kingLogY) == 0)
                            {
                                canAttack = true;
                            }
                        }
                        break;
                    case ChessManager.Chess.ChessType.卒:
                        canAttack = 兵(enemyChess.Id, kingLogY, kingLogX, -1);
                        break;
                }

                if (canAttack)
                {
                    isCheck = true;
                    break; // 发现送将，无需继续检查
                }
            }
        }

        // 4. 恢复选中棋子的原始位置
        selectedChess.Vec_X = originalX;
        selectedChess.Vec_Y = originalY;

        // 【新增】恢复被吃棋子的状态
        if (capturedChess != null)
        {
            capturedChess.Is_Dead = capturedOriginalDeadState;
        }

        return isCheck; // true=送将，false=安全
    }


}