using UnityEngine;

public class ToolManager : MonoBehaviour
{
    public static ToolManager Instance { get; private set; }

    public static int ChangeToLineX(float x)
    {
        // 棋盘是标准的等间距布局，X轴间距为0.5
        // 我们不再使用硬编码区间，而是计算“离哪个中心点最近”

        // 1. 计算理论上的列索引（浮点数）
        // x / 0.5f 得到的是相对于中心的偏移量
        // + 4 是因为中心列（第4列）的坐标是0，索引是4
        float colFloat = x / 0.5f + 4f;

        // 2. 四舍五入得到最近的整数列
        int col = Mathf.RoundToInt(colFloat);

        // 3. 容差检测
        // 计算点击位置与该列中心点的实际距离
        float centerX = (col - 4) * 0.5f;
        float distance = Mathf.Abs(x - centerX);

        // 如果距离小于0.25f（半个格子的一半），且在棋盘范围内(0-8)，则合法
        if (distance <= 0.25f && col >= 0 && col <= 8)
        {
            return col;
        }

        return -1; // 超出范围或偏离太远
    }
    public static int ChangeToLineY(float y)
    {
        // Y轴是不规则间距，我们需要列出每个中心点
        // 数据来源于你 ChangeBackY 函数中的定义
        float[] centers = new float[]
        {
        -2.2f,  // Row 0
        -1.7f,  // Row 1
        -1.2f,  // Row 2
        -0.75f, // Row 3
        -0.2f,  // Row 4
         0.3f,  // Row 5
         0.75f, // Row 6
         1.25f, // Row 7
         1.75f, // Row 8
         2.3f   // Row 9
        };

        // 遍历所有中心点，找到最近且在容差范围内的那一行
        float tolerance = 0.25f; // 合理容差：四分之一个格子

        for (int i = 0; i < centers.Length; i++)
        {
            // 计算点击位置与当前行中心点的距离
            if (Mathf.Abs(y - centers[i]) <= tolerance)
            {
                return i; // 找到匹配的行
            }
        }

        return -1; // 未匹配
    }
    public static float ChangeBackX(int LogX)
    {
        float temp1 = (LogX - 4) * 0.5f;
        return temp1;
    }

    // 参数名修改为 LogY，明确表示这是行索引
    public static float ChangeBackY(int LogY)
    {
        float temp1;
        if (LogY == 0) { temp1 = -2.2f; }
        else if (LogY == 1) { temp1 = -1.7f; }
        else if (LogY == 2) { temp1 = -1.2f; }
        else if (LogY == 3) { temp1 = -0.75f; }
        else if (LogY == 4) { temp1 = -0.2f; }
        else if (LogY == 5) { temp1 = 0.3f; }
        else if (LogY == 6) { temp1 = 0.75f; }
        else if (LogY == 7) { temp1 = 1.25f; }
        else if (LogY == 8) { temp1 = 1.75f; }
        else { temp1 = 2.3f; }

        return temp1;
    }
    // 逻辑：行差(LogY)放在十位，列差(LogX)放在个位
    public static int Relation(int LogX1, int LogY1, int LogX2, int LogY2)
    {
        return Mathf.Abs(LogY1 - LogY2) * 10 + Mathf.Abs(LogX1 - LogX2);
    }

    /*
    如果返回 10：LogY差1，LogX差0 -> 垂直方向移动一格（走直线）。
    如果返回 1：LogY差0，LogX差1 -> 水平方向移动一格。
    如果返回 11：LogY差1，LogX差1 -> 对角线移动一格（士/象/将的走法）。
    如果返回 22：LogY差2，LogX差2 -> 象飞田。
    如果返回 21：LogY差2，LogX差1 -> 马走日。
     */
    public static int GetChessId(int LogX, int LogY)
    {
        float VecX = ToolManager.ChangeBackX(LogX);
        float VecY = ToolManager.ChangeBackY(LogY);

        for (int i = 0; i < 32; i++)
        {
            // 防御性检查：确保数组元素有效
            if (ChessManager.ChessArray[i] != null &&
                !ChessManager.ChessArray[i].Is_Dead)
            {
                // 使用 Mathf.Abs 计算绝对差值，避免方向问题
                if (Mathf.Abs(ChessManager.ChessArray[i].Vec_X - VecX) < 0.25f &&
                    Mathf.Abs(ChessManager.ChessArray[i].Vec_Y - VecY) < 0.25f)
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


    public static int CountLineChess(int StartLogX, int StartLogY, int EndLogX, int EndLogY)
    {
        int count = 0;

        if (StartLogX == EndLogX)
        {
            int minLogY = Mathf.Min(StartLogY, EndLogY);
            int maxLogY = Mathf.Max(StartLogY, EndLogY);

            for (int i = minLogY + 1; i < maxLogY; i++)
            {
                if (GetChessId(StartLogX, i) != -1) { count++; }
            }
            return count;
        }
        else if (StartLogY == EndLogY)
        {
            int minLogX = Mathf.Min(StartLogX, EndLogX);
            int maxLogX = Mathf.Max(StartLogX, EndLogX);

            for (int i = minLogX + 1; i < maxLogX; i++)
            {
                if (GetChessId(i, StartLogY) != -1) { count++; }
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