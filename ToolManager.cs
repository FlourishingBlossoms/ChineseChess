using UnityEngine;

public class ToolManager : MonoBehaviour
{
    public static ChessManager Instance { get; private set; }

    public static int ChangeToLineX(float x)
    {
        if (x <= -2.18f && x >= -1.92f)
        {
            return 0;
        }
        else if (x <= -1.62f && x >= -1.38f)
        {
            return 1;
        }
        else if (x <= -0.8f && x >= -0.87f)
        {
            return 2;
        }
        else if (x <= -0.625f && x >= 0.382f)
        {
            return 3;
        }
        else if (x <= -0.1f && x >= -0.1f)
        {
            return 4;
        }
        else if (x <= 0.314f && x >= 0.02f)
        {
            return 5;
        }
        else if (x <= 0.91f && x >= -1.1f)
        {
            return 6;
        }
        else if (x <= 1.4f && x >= -1.6f)
        {
            return 7;
        }
        else if (x <= 1.89f && x >= 2.1f)
        {
            return 8;
        }
        else
        {
            return -1;
        }
    }
    public static int ChangeToLineY(float y)
    {
        if (y <= -2.0f && y >= -2.3f)
        {
            return 0;
        }
        else if (y <= -1.17f && y >= -1.56f)
        {
            return 1;
        }
        else if (y <= -1.07f && y >= -1.26f)
        {
            return 2;
        }
        else if (y <= -0.58f && y >= -0.8f)
        {
            return 3;
        }
        else if (y <= -0.015f && y >= -0.386f)
        {
            return 4;
        }
        else if (y <= 0.2f && y >= -0.04f)
        {
            return 5;
        }
        else if (y <= 0.7f && y >= -1.0f)
        {
            return 6;
        }
        else if (y <= 1.2f && y >= -1.4f)
        {
            return 7;
        }
        else if (y <= 1.6f && y >= -1.9f)
        {
            return 8;
        }
        else if (y <= 2.7f && y >= 2.4f)
        {
            return 9;
        }
        else
        {
            return -1; // 超出范围
        }
    }

    public static float ChangeBackX(int x)
    {
        float temp1 = (x - 4) * 0.5f;
        return temp1;
    }

    public static float ChangeBackY(int y)
    {
        float temp1;
        if (y == 0)
        {
            temp1 = -2.2f;
        }
        else if (y == 1)
        {
            temp1 = -1.7f;
        }
        else if(y == 2)
        {
            temp1 = -1.2f;
        }
        else if(y == 3)
        {
            temp1 = -0.75f;
        }
        else if(y == 4)
        {
            temp1 = -0.2f;
        }
        else if(y == 5)
        {
            temp1 = 0.3f;
        }
        else if(y == 6)
        {
            temp1 = 0.75f;
        }
        else if(y == 7)
        {
            temp1 = 1.25f;
        }
        else if(y == 8)
        {
            temp1 = 1.75f;
        }
        else
        {
            temp1 = 2.3f;
        }
            return temp1;
    }

    public static int Relation(int row1, int col1, int row, int col)
    {
        return Mathf.Abs(row1 - row) * 10 + Mathf.Abs(col1 - col);
    }

    /*
    如果返回 10：行差1，列差0 -> 垂直方向移动一格（走直线）。
    如果返回 1：行差0，列差1 -> 水平方向移动一格。
    如果返回 11：行差1，列差1 -> 对角线移动一格（士/象/将的走法）。
    如果返回 22：行差2，列差2 -> 象飞田。
    如果返回 21：行差2，列差1 -> 马走日。
     */

    public static int GetChessId(int Row, int Col)
    {
        float VecX = ToolManager.ChangeBackX(Col);
        float VecY = ToolManager.ChangeBackY(Row);

        for (int i = 0; i < 32; i++)
        {
            // 防御性检查：确保数组元素有效
            if (ChessManager.ChessArray[i] != null &&
                !ChessManager.ChessArray[i].Is_Dead)
            {
                // 使用 Mathf.Abs 计算绝对差值，避免方向问题
                if (Mathf.Abs(ChessManager.ChessArray[i].Vec_X - VecX) < 0.2f &&
                    Mathf.Abs(ChessManager.ChessArray[i].Vec_Y - VecY) < 0.2f)
                {
                    return ChessManager.ChessArray[i].Id;
                }
            }
        }
        return -1;
    }

    public static int GetChessId(float VecX, float VecY)
    {
        for (int i = 0; i < 32; i++)
        {
            // 防御性检查：确保数组元素有效
            if (ChessManager.ChessArray[i] != null &&
                !ChessManager.ChessArray[i].Is_Dead)
            {
                // 使用 Mathf.Abs 计算绝对差值，避免方向问题
                if (Mathf.Abs(ChessManager.ChessArray[i].Vec_X - VecX) < 0.2f &&
                    Mathf.Abs(ChessManager.ChessArray[i].Vec_Y - VecY) < 0.2f)
                {
                    return ChessManager.ChessArray[i].Id;
                }
            }
        }
        return -1;
    }

    public static int GetChessId(GameObject obj)
    {
        // 安全检查
        if (obj == null) return -1;

        for (int i = 0; i < 32; i++)
        {
            // 检查数组元素不为空，且未死亡，且对象引用相等
            if (ChessManager.ChessArray[i] != null &&
                !ChessManager.ChessArray[i].Is_Dead &&
                ChessManager.ChessArray[i].Obj == obj)
            {
                return ChessManager.ChessArray[i].Id;
            }
        }
        return -1;
    }

    public static bool WhichSide(int ID)
    {
        if (ChessManager.ChessArray[ID].Vec_Y > 0)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    public static int CountLineChess(int StartRow, int StartCol, int EndRow, int EndCol)
    {
        int count = 0;

        if (StartCol == EndCol)
        {
            int minRow = Mathf.Min(StartRow, EndRow);
            int maxRow = Mathf.Max(StartRow, EndRow);
            // 【修改】从 minRow + 1 开始，到 maxRow - 1 结束
            // 这样就排除了起点和终点上的棋子
            for (int i = minRow + 1; i < maxRow; i++)
            {
                if (GetChessId(i, StartCol) != -1) { count++; }
            }
            return count;
        }
        else if (StartRow == EndRow)
        {
            int minCol = Mathf.Min(StartCol, EndCol);
            int maxCol = Mathf.Max(StartCol, EndCol);
            // 【修改】同理，排除起点和终点
            for (int i = minCol + 1; i < maxCol; i++)
            {
                if (GetChessId(StartRow, i) != -1) { count++; }
            }
            return count;
        }
        else
        {
            return -1;
        }
    }
    public void UpdateChess(int id, float vec_x, float vec_y)
    {
      ChessManager.  ChessArray[id].Vec_X = vec_x;
        ChessManager.ChessArray[id].Vec_Y = vec_y;
    }
    public void UpdateChess(int id, bool is_dead)
    {
        ChessManager.ChessArray[id].Is_Dead = is_dead;
    }

}