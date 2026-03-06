using System;
using UnityEngine;

public class 规则检测 : MonoBehaviour
{
    public static bool 车(int SelectedId, int TargetRow, int TargetCol, int DistoryID)
    {
        int StartRow = ToolManager.ChangeToLineX(ChessManager.ChessArray[SelectedId].Vec_X);
        int StartCol = ToolManager.ChangeToLineY(ChessManager.ChessArray[SelectedId].Vec_Y);
        if (StartRow == TargetRow || StartCol == TargetCol)
        {
            int pathCount = ToolManager.CountLineChess(StartRow, StartCol, TargetRow, TargetCol);

            // 路径必须通畅（中间无棋子，pathCount == 0）
            if (pathCount == 0)
            {
                // 如果是吃子
                if (DistoryID != -1) return true;
                // 如果是移动
                else return true;
            }
            else
            {
                return false; // 路径被阻挡
            }
        }
        else
        {
            return false;
        }
    }

    public static bool 马(int SelectedId, int TargetRow, int TargetCol, int DistoryID)
    {
        int StartRow = ToolManager.ChangeToLineX(ChessManager.ChessArray[SelectedId].Vec_X);
        int StartCol = ToolManager.ChangeToLineY(ChessManager.ChessArray[SelectedId].Vec_Y);
        if (Math.Abs(StartRow - TargetRow) == 2 && Math.Abs(StartCol - TargetCol) == 1)
        {
            if (StartRow - TargetRow > 0)
            {
                if (ToolManager.CountLineChess(StartRow, StartCol, StartRow - 1, TargetCol) == 0) { return true; }
            }
            else if (StartRow - TargetRow < 0)
            {
                if (ToolManager.CountLineChess(StartRow, StartCol, StartRow + 1, TargetCol) == 0) { return true; }
            }
            else { return false; }
        }
        else if (Math.Abs(StartRow - TargetRow) == 1 && Math.Abs(StartCol - TargetCol) == 2)
        {
            if (StartCol - TargetCol > 0)
            {
                if (ToolManager.CountLineChess(StartRow, StartCol, TargetRow, StartCol - 1) == 0) { return true; }
            }
            else if (StartCol - TargetCol < 0)
            {
                if (ToolManager.CountLineChess(StartRow, StartCol, TargetRow, StartCol + 1) == 0) { return true; }
            }
            else { return false; }
        }
        return false;
    }

    public static bool 炮(int SelectedId, int TargetRow, int TargetCol, int DistoryID)
    {
        int StartRow = ToolManager.ChangeToLineX(ChessManager.ChessArray[SelectedId].Vec_X);
        int StartCol = ToolManager.ChangeToLineY(ChessManager.ChessArray[SelectedId].Vec_Y);

        if (DistoryID == -1)
        {
            // 移动：中间必须没有棋子 (Count == 0)
            return 车(SelectedId, TargetRow, TargetCol, DistoryID);
        }
        else
        {
            // 吃子：中间必须隔一个棋子 (Count == 1)
            if (StartRow == TargetRow || StartCol == TargetCol)
            {
                if (ToolManager.CountLineChess(StartRow, StartCol, TargetRow, TargetCol) == 1)
                {
                    return true;
                }
            }
            return false;
        }
    }

    public static bool 相(int SelectedId, int row, int col, int destoryId)
    {
        /*
         * 1.目标位置不能越过河界走入对方的领地
         * 2.只能斜走（两步），可以使用汉字中的田字形象地表述：田字格的对角线，即俗称象（相）走田字
         * 3.当象（相）行走的路线中，及田字中心有棋子时（无论己方或者是对方的棋子），则不允许走过去，俗称：塞象（相）眼。
        */
        int StartRow = ToolManager.ChangeToLineX(ChessManager.ChessArray[SelectedId].Vec_X);
        int StartCol = ToolManager.ChangeToLineY(ChessManager.ChessArray[SelectedId].Vec_Y);
        int Side = ToolManager.Relation(StartRow, StartCol, row, col);
        if (Side != 22) return false;

        int rEye = (row + StartRow) / 2;
        int cEye = (col + StartCol) / 2;

        if (ToolManager.GetChessId(rEye, cEye) != -1) return false;

        if (ToolManager.WhichSide(SelectedId))
        {
            if (row < 4) return false;
        }
        else
        {
            if (row > 5) return false;
        }

        return true;
    }

    public static bool 兵(int SelectedId, int row, int col, int destoryId)
    {
        int StartRow = ToolManager.ChangeToLineX(ChessManager.ChessArray[SelectedId].Vec_X);
        int StartCol = ToolManager.ChangeToLineY(ChessManager.ChessArray[SelectedId].Vec_Y);
        int Side = ToolManager.Relation(StartRow, StartCol, row, col);

        // 只能走一步
        if (Side != 1 && Side != 10) return false;

        if (ToolManager.WhichSide(SelectedId)) // 这是黑方
        {
            // 黑方在上方，Row较大，进攻方向是 Row 减小
            if (StartRow < row) return false; // 不能后退（Row增加是后退）

            // 过河前 (Row > 4)，不能横走
            if (StartRow > 4 && StartCol != col) return false;
        }
        else // 这是红方
        {
            // 红方在下方，Row较小，进攻方向是 Row 增加
            if (StartRow > row) return false; // 不能后退（Row减小是后退）

            // 过河前 (Row < 5)，不能横走
            if (StartRow < 5 && StartCol != col) return false;
        }
        return true;
    }

    public static bool 将(int SelectedId, int row, int col, int destoryId)
    {
        /*
         * 1.首先目标位置在九宫格内
         * 2.移动的步长是一个格子
         * 3.将和帅不准在同一直线上直接对面（中间无棋子），如一方已先占据位置，则另一方必须回避，否则就算输了
        */
        if (destoryId != -1 && ChessManager.ChessArray[destoryId].Type == ChessManager.Chess.ChessType.帅)
            return 车(SelectedId, row, col, destoryId);

        if (col < 3 || col > 5) return false;

        if (ToolManager.WhichSide(SelectedId)) // 黑方
        {
            // 黑方不能出上方九宫 (Row 7, 8, 9)
            if (row < 7 || row > 9) return false;
        }
        else // 红方
        {
            // 红方不能出下方九宫 (Row 0, 1, 2)
            if (row < 0 || row > 2) return false;
        }
        int StartRow = ToolManager.ChangeToLineX(ChessManager.ChessArray[SelectedId].Vec_X);
        int StartCol = ToolManager.ChangeToLineY(ChessManager.ChessArray[SelectedId].Vec_Y);
        int Side = ToolManager.Relation(StartRow, StartCol, row, col);
        if (Side != 1 && Side != 10) return false;

        return true;
    }

    /// <summary>
    /// 士的走棋规则
    /// </summary>
    /// <returns></returns>
    public static bool 士(int SelectedId, int row, int col, int destoryId)
    {
        /*
         * 1.目标位置在九宫格内
         * 2.只许沿着九宫中的斜线行走一步（方格的对角线）
        */
        if (ToolManager.WhichSide(SelectedId)) // 黑方
        {
            // 黑方不能出上方九宫 (Row 7, 8, 9)
            if (row < 7 || row > 9) return false;
        }
        else // 红方
        {
            // 红方不能出下方九宫 (Row 0, 1, 2)
            if (row < 0 || row > 2) return false;
        }

        if (col < 3 || col > 5) return false;
        int StartRow = ToolManager.ChangeToLineX(ChessManager.ChessArray[SelectedId].Vec_X);
        int StartCol = ToolManager.ChangeToLineY(ChessManager.ChessArray[SelectedId].Vec_Y);
        int Side = ToolManager.Relation(StartRow, StartCol, row, col);
        if (Side != 11) return false;

        return true;
    }
}