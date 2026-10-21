using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 棋局评估器 - 用于评估当前局面的优劣
/// 这是所有AI的基础组件，提供局面评分功能
/// </summary>
public class ChessEvaluator
{
    // ==================== 棋子价值表 ====================
    // 这些值代表每种棋子的基础价值
    // 价值越高，棋子越重要
    
    /// <summary>
    /// 棋子基础价值字典
    /// 单位：分（相当于兵的价值为100分）
    /// 这些值是根据象棋理论和经验设定的
    /// </summary>
    private static readonly Dictionary<ChessManager.Chess.ChessType, int> PieceValues = 
        new Dictionary<ChessManager.Chess.ChessType, int>
    {
        { ChessManager.Chess.ChessType.帅, 10000 },  // 将/帅：最重要，被将死就输了
        { ChessManager.Chess.ChessType.车, 900 },    // 车：最强棋子，价值最高
        { ChessManager.Chess.ChessType.马, 400 },    // 马：中等价值，机动性强
        { ChessManager.Chess.ChessType.炮, 450 },    // 炮：中等价值，远程攻击
        { ChessManager.Chess.ChessType.象, 200 },    // 象/相：防守型棋子
        { ChessManager.Chess.ChessType.士, 200 },    // 士：防守型棋子
        { ChessManager.Chess.ChessType.卒, 100 }     // 兵/卒：价值最低，但过河后价值提升
    };
    
    // ==================== 位置价值表 ====================
    // 棋子在不同位置有不同的价值
    // 这些表格定义了每个位置的价值加成
    
    /// <summary>
    /// 车的位置价值表
    /// 车在棋盘中央和对方底线价值更高
    /// </summary>
    private static readonly int[,] RookPositionTable = new int[,]
    {
        // 列 0-8，行 0-9（从红方视角）
        {  0,  0,  0,  0,  0,  0,  0,  0,  0 },  // 行0（红方底线）
        {  0,  0,  0,  0,  0,  0,  0,  0,  0 },  // 行1
        {  0,  0,  0,  0,  0,  0,  0,  0,  0 },  // 行2
        {  0,  0,  0,  0,  0,  0,  0,  0,  0 },  // 行3
        {  0,  0,  0,  0,  0,  0,  0,  0,  0 },  // 行4
        {  0,  0,  0,  0,  0,  0,  0,  0,  0 },  // 行5
        {  0,  0,  0,  0,  0,  0,  0,  0,  0 },  // 行6
        {  0,  0,  0,  0,  0,  0,  0,  0,  0 },  // 行7
        {  0,  0,  0,  0,  0,  0,  0,  0,  0 },  // 行8
        {  0,  0,  0,  0,  0,  0,  0,  0,  0 }   // 行9（黑方底线）
    };
    
    /// <summary>
    /// 马的位置价值表
    /// 马在中央位置价值更高，边角位置价值低
    /// </summary>
    private static readonly int[,] KnightPositionTable = new int[,]
    {
        { -50, -40, -30, -30, -30, -30, -30, -40, -50 },
        { -40, -20,   0,   0,   0,   0,   0, -20, -40 },
        { -30,   0,  10,  15,  15,  15,  10,   0, -30 },
        { -30,   5,  15,  20,  20,  20,  15,   5, -30 },
        { -30,   0,  15,  20,  20,  20,  15,   0, -30 },
        { -30,   5,  15,  20,  20,  20,  15,   5, -30 },
        { -30,   0,  10,  15,  15,  15,  10,   0, -30 },
        { -40, -20,   0,   5,   5,   5,   0, -20, -40 },
        { -50, -40, -30, -30, -30, -30, -30, -40, -50 },
        { -60, -50, -40, -40, -40, -40, -40, -50, -60 }
    };
    
    /// <summary>
    /// 炮的位置价值表
    /// 炮在中央和对方阵地价值更高
    /// ★修复：原表全0，导致炮位置完全不影响评估
    /// 新表参考中国象棋炮的经典位置理论：
    ///   - 巡河炮（河界线）有加成
    ///   - 沉底炮（对方底线）有较大加成
    ///   - 炮架中路(4列)在己方半场有减分（容易被对方利用）
    ///   - 过河炮有加成
    /// </summary>
    private static readonly int[,] CannonPositionTable = new int[,]
    {
        {  6,  4,  0, -10, -12, -10,  0,  4,  6 },  // 行0（黑方底线）
        {  2,  2,  0,  -4,  -5,  -4,  0,  2,  2 },  // 行1
        {  2,  2,  0, -10,  -8, -10,  0,  2,  2 },  // 行2
        {  0,  1,  2,  -4,  -5,  -4,  2,  1,  0 },  // 行3
        {  0,  0,  0,   0,   0,   0,  0,  0,  0 },  // 行4（河界，巡河炮）
        { -2,  0,  4,  10,  12,  10,  4,  0, -2 },  // 行5（过河炮）
        {  0,  0,  0,   0,   0,   0,  0,  0,  0 },  // 行6
        {  2,  2,  0,  -4,  -5,  -4,  0,  2,  2 },  // 行7
        { 18, 20, 27,  30,  30,  30, 27, 20, 18 },  // 行8（红方巡河/沉底炮）
        {  6,  4,  0, -10, -12, -10,  0,  4,  6 }   // 行9（红方底线）
    };
    
    /// <summary>
    /// 兵/卒的位置价值表
    /// 过河后价值大幅提升，越深入敌阵价值越高
    /// </summary>
    private static readonly int[,] PawnPositionTable = new int[,]
    {
        // 红方兵的位置价值（从红方视角）
        {   0,   0,   0,   0,   0,   0,   0,   0,   0 },  // 行0
        {  90,  90, 110, 120, 120, 120, 110,  90,  90 },  // 行1
        {  90,  90, 110, 120, 120, 120, 110,  90,  90 },  // 行2
        {  70,  90, 110, 110, 110, 110, 110,  90,  70 },  // 行3
        {  70,  70,  70,  70,  70,  70,  70,  70,  70 },  // 行4（河界）
        {   0,   0,   0,   0,   0,   0,   0,   0,   0 },  // 行5（河界）
        {   0,   0,   0,   0,   0,   0,   0,   0,   0 },  // 行6
        {   0,   0,   0,   0,   0,   0,   0,   0,   0 },  // 行7
        {   0,   0,   0,   0,   0,   0,   0,   0,   0 },  // 行8
        {   0,   0,   0,   0,   0,   0,   0,   0,   0 }   // 行9
    };

    // ==================== 核心评估方法 ====================
    
    /// <summary>
    /// 评估当前局面分数
    /// 正数表示红方优势，负数表示黑方优势
    /// ★加强：新增机动性、将帅安全、捉子威胁评估
    /// </summary>
    /// <param name="forBlack">是否从黑方视角评估</param>
    /// <returns>局面分数</returns>
    public float EvaluateBoard(bool forBlack = false)
    {
        float score = 0f;
        int redMobility = 0, blackMobility = 0;
        int redKingSafety = 0, blackKingSafety = 0;
        
        // 遍历所有棋子
        for (int i = 0; i < 32; i++)
        {
            ChessManager.Chess chess = ChessManager.ChessArray[i];
            
            // 跳过已死亡的棋子
            if (chess == null || chess.Is_Dead)
                continue;
            
            // 计算棋子价值
            float pieceValue = GetPieceValue(chess);
            
            // 计算位置加成
            float positionBonus = GetPositionBonus(chess);
            
            // 总价值 = 基础价值 + 位置加成
            float totalValue = pieceValue + positionBonus;
            
            // 红方棋子加分，黑方棋子减分
            if (chess.Is_Red)
            {
                score += totalValue;
                // ★新增：机动性统计（每多一个合法走位 +1分）
                // 只统计车马炮，避免过多噪音
                if (chess.Type == ChessManager.Chess.ChessType.车 ||
                    chess.Type == ChessManager.Chess.ChessType.马 ||
                    chess.Type == ChessManager.Chess.ChessType.炮)
                {
                    redMobility += CountPieceMoves(chess);
                }
            }
            else
            {
                score -= totalValue;
                if (chess.Type == ChessManager.Chess.ChessType.车 ||
                    chess.Type == ChessManager.Chess.ChessType.马 ||
                    chess.Type == ChessManager.Chess.ChessType.炮)
                {
                    blackMobility += CountPieceMoves(chess);
                }
            }
        }
        
        // ★新增：机动性评分（每步 +1.5分）
        score += (redMobility - blackMobility) * 1.5f;
        
        // ★新增：将帅安全评分
        redKingSafety = EvaluateKingSafety(true);
        blackKingSafety = EvaluateKingSafety(false);
        score += (redKingSafety - blackKingSafety);
        
        // 如果从黑方视角评估，取反
        if (forBlack)
            score = -score;
        
        return score;
    }
    
    /// <summary>
    /// 获取棋子的基础价值
    /// </summary>
    public int GetPieceValue(ChessManager.Chess chess)
    {
        if (PieceValues.TryGetValue(chess.Type, out int value))
            return value;
        return 0;
    }
    
    /// <summary>
    /// 获取棋子的位置加成
    /// 根据棋子类型和位置查表获取
    /// </summary>
    public int GetPositionBonus(ChessManager.Chess chess)
    {
        int logX = ToolManager.ChangeToLineX(chess.Vec_X);
        int logY = ToolManager.ChangeToLineY(chess.Vec_Y);
        
        // 边界检查
        if (logX < 0 || logX > 8 || logY < 0 || logY > 9)
            return 0;
        
        // 黑方棋子需要翻转坐标
        if (!chess.Is_Red)
        {
            logY = 9 - logY;
        }
        
        // 根据棋子类型选择位置表
        switch (chess.Type)
        {
            case ChessManager.Chess.ChessType.车:
                return RookPositionTable[logY, logX];
            case ChessManager.Chess.ChessType.马:
                return KnightPositionTable[logY, logX];
            case ChessManager.Chess.ChessType.炮:
                return CannonPositionTable[logY, logX];
            case ChessManager.Chess.ChessType.卒:
                return PawnPositionTable[logY, logX];
            default:
                return 0;
        }
    }
    
    // ════════════════════════════════════════════════════════════════
    //  ★新增：机动性评估
    // ════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// 统计单个棋子的合法走法数量（用于机动性评估）
    /// 不做送将检测（太慢），只做基本走法生成
    /// </summary>
    private int CountPieceMoves(ChessManager.Chess chess)
    {
        List<AIMove> moves = GeneratePieceMoves(chess);
        return moves.Count;
    }
    
    // ════════════════════════════════════════════════════════════════
    //  ★新增：将帅安全评估
    // ════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// 评估将帅安全性，返回安全分（正数=安全，负数=危险）
    /// 检查项：
    ///   1. 将帅是否在九宫格中心（中心最安全）
    ///   2. 将帅周围是否有士/相保护
    ///   3. 对方车马炮是否近距离威胁将帅
    /// </summary>
    private int EvaluateKingSafety(bool isRedKing)
    {
        int safety = 0;
        
        // 1. 找到将帅位置
        int kingId = -1;
        int kingLogX = -1, kingLogY = -1;
        for (int i = 0; i < 32; i++)
        {
            ChessManager.Chess chess = ChessManager.ChessArray[i];
            if (chess == null || chess.Is_Dead) continue;
            if (chess.Type == ChessManager.Chess.ChessType.帅 && chess.Is_Red == isRedKing)
            {
                kingId = i;
                kingLogX = ToolManager.ChangeToLineX(chess.Vec_X);
                kingLogY = ToolManager.ChangeToLineY(chess.Vec_Y);
                break;
            }
        }
        if (kingId < 0) return -500; // 将帅不存在，极度危险
        
        // 2. 将帅在九宫格中心位置最安全
        // 红方九宫格：行7-9，列3-5
        // 黑方九宫格：行0-2，列3-5
        int centerCol = 4;
        int centerRow = isRedKing ? 8 : 1;
        
        int colDist = Math.Abs(kingLogX - centerCol);
        int rowDist = Math.Abs(kingLogY - centerRow);
        safety -= (colDist + rowDist) * 8; // 偏离中心扣分
        
        // 3. 将帅附近有士保护加分
        for (int i = 0; i < 32; i++)
        {
            ChessManager.Chess chess = ChessManager.ChessArray[i];
            if (chess == null || chess.Is_Dead) continue;
            if (chess.Is_Red != isRedKing) continue;
            if (chess.Type == ChessManager.Chess.ChessType.士 || 
                chess.Type == ChessManager.Chess.ChessType.象)
            {
                int guardX = ToolManager.ChangeToLineX(chess.Vec_X);
                int guardY = ToolManager.ChangeToLineY(chess.Vec_Y);
                int dist = Math.Abs(guardX - kingLogX) + Math.Abs(guardY - kingLogY);
                if (dist <= 2)
                    safety += 15; // 士/相在将帅附近，加分
            }
        }
        
        // 4. 对方车马炮近距离威胁扣分
        for (int i = 0; i < 32; i++)
        {
            ChessManager.Chess chess = ChessManager.ChessArray[i];
            if (chess == null || chess.Is_Dead) continue;
            if (chess.Is_Red == isRedKing) continue; // 只看对方
            
            int enemyX = ToolManager.ChangeToLineX(chess.Vec_X);
            int enemyY = ToolManager.ChangeToLineY(chess.Vec_Y);
            int dist = Math.Abs(enemyX - kingLogX) + Math.Abs(enemyY - kingLogY);
            
            if (chess.Type == ChessManager.Chess.ChessType.车)
            {
                // 车在同一行或同一列且距离近，威胁大
                if ((enemyX == kingLogX || enemyY == kingLogY) && dist <= 5)
                    safety -= 25;
            }
            else if (chess.Type == ChessManager.Chess.ChessType.马)
            {
                // 马在跳跃范围内，有威胁
                if (dist <= 3)
                    safety -= 15;
            }
            else if (chess.Type == ChessManager.Chess.ChessType.炮)
            {
                // 炮在同列且距离适中，有威胁
                if (enemyX == kingLogX && dist <= 4)
                    safety -= 20;
            }
        }
        
        return safety;
    }
    
    /// <summary>
    /// 评估单个走法的价值
    /// 用于快速评估走法优劣
    /// </summary>
    public float EvaluateMove(AIMove move)
    {
        float score = 0f;
        
        // 如果吃子，加上被吃棋子的价值
        if (move.CaptureId >= 0 && move.CaptureId < 32)
        {
            ChessManager.Chess captured = ChessManager.ChessArray[move.CaptureId];
            if (captured != null && !captured.Is_Dead)
            {
                score += GetPieceValue(captured);
                score += GetPositionBonus(captured) * 0.5f;
            }
        }
        
        // 计算移动后的位置改善
        ChessManager.Chess movingPiece = ChessManager.ChessArray[move.FromChessId];
        if (movingPiece != null)
        {
            // 模拟移动后的位置价值
            int oldLogY = ToolManager.ChangeToLineY(movingPiece.Vec_Y);
            int newLogY = move.ToLogY;
            
            // 过河加分
            if (movingPiece.Type == ChessManager.Chess.ChessType.卒)
            {
                if (!movingPiece.Is_Red && newLogY < 5)
                    score += 50;  // 黑卒过河加分
                else if (movingPiece.Is_Red && newLogY > 4)
                    score += 50;  // 红兵过河加分
            }
        }
        
        return score;
    }
    
    /// <summary>
    /// 检查是否被将军
    /// </summary>
    public bool IsInCheck(bool isRed)
    {
        // 找到将/帅的位置
        int kingId = -1;
        int kingLogX = -1, kingLogY = -1;
        
        for (int i = 0; i < 32; i++)
        {
            ChessManager.Chess chess = ChessManager.ChessArray[i];
            if (chess != null && !chess.Is_Dead && 
                chess.Type == ChessManager.Chess.ChessType.帅 && 
                chess.Is_Red == isRed)
            {
                kingId = i;
                kingLogX = ToolManager.ChangeToLineX(chess.Vec_X);
                kingLogY = ToolManager.ChangeToLineY(chess.Vec_Y);
                break;
            }
        }
        
        if (kingId < 0) return false;
        
        // 检查对方所有棋子是否能攻击到将/帅
        for (int i = 0; i < 32; i++)
        {
            ChessManager.Chess chess = ChessManager.ChessArray[i];
            if (chess == null || chess.Is_Dead || chess.Is_Red == isRed)
                continue;
            
            // 检查这个棋子是否能吃到将/帅
            if (CanAttack(chess, kingLogX, kingLogY))
                return true;
        }
        
        return false;
    }

    /// <summary>
    /// 检查棋子是否能攻击到指定位置
    /// </summary>
    //private bool CanAttack(ChessManager.Chess chess, int targetLogX, int targetLogY)
    //{
    //    int startLogX = ToolManager.ChangeToLineX(chess.Vec_X);
    //    int startLogY = ToolManager.ChangeToLineY(chess.Vec_Y);

    //    // 使用规则检测判断是否能移动到目标位置
    //    // 这里简化处理，实际应该调用规则检测
    //    switch (chess.Type)
    //    {
    //        case ChessManager.Chess.ChessType.车:
    //            return 规则检测.车(chess.Id, targetLogY, targetLogX, -1);
    //        case ChessManager.Chess.ChessType.马:
    //            return 规则检测.马(chess.Id, targetLogY, targetLogX, -1);
    //        case ChessManager.Chess.ChessType.炮:
    //            return 规则检测.炮(chess.Id, targetLogY, targetLogX, -1);
    //        default:
    //            return false;
    //    }
    //}
    /// <summary>
    /// 检查棋子是否能攻击到指定位置
    /// </summary>
    private bool CanAttack(ChessManager.Chess chess, int targetLogX, int targetLogY)
    {
        int startLogX = ToolManager.ChangeToLineX(chess.Vec_X);
        int startLogY = ToolManager.ChangeToLineY(chess.Vec_Y);

        switch (chess.Type)
        {
            case ChessManager.Chess.ChessType.车:
                return 规则检测.车(chess.Id, targetLogY, targetLogX, -1);
            case ChessManager.Chess.ChessType.马:
                return 规则检测.马(chess.Id, targetLogY, targetLogX, -1);
            case ChessManager.Chess.ChessType.炮:
                // 【修正】传 0 而不是 -1，强制走吃子逻辑（中间必须隔1子）
                return 规则检测.炮(chess.Id, targetLogY, targetLogX, 0);
            default:
                return false;
        }
    }

    /// <summary>
    /// 生成所有合法走法
    /// </summary>
    //public List<AIMove> GenerateAllLegalMoves(bool forBlack)
    //{
    //    List<AIMove> moves = new List<AIMove>();

    //    for (int i = 0; i < 32; i++)
    //    {
    //        ChessManager.Chess chess = ChessManager.ChessArray[i];

    //        // 跳过无效棋子
    //        if (chess == null || chess.Is_Dead)
    //            continue;

    //        // 只生成指定方的走法
    //        if (chess.Is_Red == forBlack)  // 黑方是false
    //            continue;

    //        // 生成该棋子的所有可能走法
    //        List<AIMove> pieceMoves = GeneratePieceMoves(chess);
    //        moves.AddRange(pieceMoves);
    //    }

    //    return moves;
    //}
    /// <summary>
    /// 生成所有合法走法（排除送将）
    /// </summary>
    public List<AIMove> GenerateAllLegalMoves(bool forBlack)
    {
        List<AIMove> moves = new List<AIMove>();
        // forBlack=true 代表黑方走棋，则 isRedTurn=false
        bool isRedTurn = !forBlack;

        for (int i = 0; i < 32; i++)
        {
            ChessManager.Chess chess = ChessManager.ChessArray[i];
            // 跳过无效棋子
            if (chess == null || chess.Is_Dead) continue;
            // 只生成指定方的走法
            if (chess.Is_Red == forBlack) // 黑方是false
                continue;

            // 生成该棋子的所有可能走法
            List<AIMove> pieceMoves = GeneratePieceMoves(chess);

            // 【新增】过滤掉会导致送将的走步
            foreach (var move in pieceMoves)
            {
                // 如果送将检测返回 false，说明安全，可以加入合法列表
                if (!规则检测.送将检测(move.FromChessId, move.ToLogX, move.ToLogY, isRedTurn, move.CaptureId))
                {
                    moves.Add(move);
                }
            }
        }

        return moves;
    }

    /// <summary>
    /// 生成单个棋子的所有可能走法
    /// </summary>
    private List<AIMove> GeneratePieceMoves(ChessManager.Chess chess)
    {
        List<AIMove> moves = new List<AIMove>();
        int startLogX = ToolManager.ChangeToLineX(chess.Vec_X);
        int startLogY = ToolManager.ChangeToLineY(chess.Vec_Y);
        
        // 根据棋子类型生成走法
        switch (chess.Type)
        {
            case ChessManager.Chess.ChessType.车:
                moves.AddRange(GenerateLineMoves(chess, startLogX, startLogY));
                break;
            case ChessManager.Chess.ChessType.马:
                moves.AddRange(GenerateKnightMoves(chess, startLogX, startLogY));
                break;
            case ChessManager.Chess.ChessType.炮:
                moves.AddRange(GenerateCannonMoves(chess, startLogX, startLogY));
                break;
            case ChessManager.Chess.ChessType.象:
                moves.AddRange(GenerateElephantMoves(chess, startLogX, startLogY));
                break;
            case ChessManager.Chess.ChessType.士:
                moves.AddRange(GenerateAdvisorMoves(chess, startLogX, startLogY));
                break;
            case ChessManager.Chess.ChessType.帅:
                moves.AddRange(GenerateKingMoves(chess, startLogX, startLogY));
                break;
            case ChessManager.Chess.ChessType.卒:
                moves.AddRange(GeneratePawnMoves(chess, startLogX, startLogY));
                break;
        }
        
        return moves;
    }
    
    /// <summary>
    /// 生成直线移动棋子（车）的走法
    /// </summary>
    private List<AIMove> GenerateLineMoves(ChessManager.Chess chess, int startX, int startY)
    {
        List<AIMove> moves = new List<AIMove>();
        
        // 四个方向：上、下、左、右
        int[] dx = { 0, 0, -1, 1 };
        int[] dy = { -1, 1, 0, 0 };
        
        for (int dir = 0; dir < 4; dir++)
        {
            for (int step = 1; step < 10; step++)
            {
                int newX = startX + dx[dir] * step;
                int newY = startY + dy[dir] * step;
                
                // 边界检查
                if (newX < 0 || newX > 8 || newY < 0 || newY > 9)
                    break;
                
                // 检查目标位置
                int targetId = ToolManager.GetChessId(newX, newY);
                
                if (targetId == -1)
                {
                    // 空位，可以移动
                    moves.Add(new AIMove
                    {
                        FromChessId = chess.Id,
                        FromLogX = startX,
                        FromLogY = startY,
                        ToLogX = newX,
                        ToLogY = newY,
                        CaptureId = -1
                    });
                }
                else
                {
                    // 有棋子
                    ChessManager.Chess target = ChessManager.ChessArray[targetId];
                    if (target != null && target.Is_Red != chess.Is_Red)
                    {
                        // 敌方棋子，可以吃
                        moves.Add(new AIMove
                        {
                            FromChessId = chess.Id,
                            FromLogX = startX,
                            FromLogY = startY,
                            ToLogX = newX,
                            ToLogY = newY,
                            CaptureId = targetId
                        });
                    }
                    break;  // 无论敌我，都不能继续前进
                }
            }
        }
        
        return moves;
    }
    
    /// <summary>
    /// 生成马的走法
    /// </summary>
    private List<AIMove> GenerateKnightMoves(ChessManager.Chess chess, int startX, int startY)
    {
        List<AIMove> moves = new List<AIMove>();
        
        // 马走日的8个可能位置
        int[] dx = { -2, -2, -1, -1, 1, 1, 2, 2 };
        int[] dy = { -1, 1, -2, 2, -2, 2, -1, 1 };
        
        // 蹩马腿检查的偏移
        int[] blockX = { -1, -1, 0, 0, 0, 0, 1, 1 };
        int[] blockY = { 0, 0, -1, 1, -1, 1, 0, 0 };
        
        for (int i = 0; i < 8; i++)
        {
            int newX = startX + dx[i];
            int newY = startY + dy[i];
            int blockCheckX = startX + blockX[i];
            int blockCheckY = startY + blockY[i];
            
            // 边界检查
            if (newX < 0 || newX > 8 || newY < 0 || newY > 9)
                continue;
            
            // 蹩马腿检查
            if (ToolManager.GetChessId(blockCheckX, blockCheckY) != -1)
                continue;
            
            // 检查目标位置
            int targetId = ToolManager.GetChessId(newX, newY);
            
            if (targetId == -1)
            {
                moves.Add(new AIMove
                {
                    FromChessId = chess.Id,
                    FromLogX = startX,
                    FromLogY = startY,
                    ToLogX = newX,
                    ToLogY = newY,
                    CaptureId = -1
                });
            }
            else
            {
                ChessManager.Chess target = ChessManager.ChessArray[targetId];
                if (target != null && target.Is_Red != chess.Is_Red)
                {
                    moves.Add(new AIMove
                    {
                        FromChessId = chess.Id,
                        FromLogX = startX,
                        FromLogY = startY,
                        ToLogX = newX,
                        ToLogY = newY,
                        CaptureId = targetId
                    });
                }
            }
        }
        
        return moves;
    }
    
    /// <summary>
    /// 生成炮的走法
    /// </summary>
    private List<AIMove> GenerateCannonMoves(ChessManager.Chess chess, int startX, int startY)
    {
        List<AIMove> moves = new List<AIMove>();
        
        int[] dx = { 0, 0, -1, 1 };
        int[] dy = { -1, 1, 0, 0 };
        
        for (int dir = 0; dir < 4; dir++)
        {
            bool jumped = false;
            
            for (int step = 1; step < 10; step++)
            {
                int newX = startX + dx[dir] * step;
                int newY = startY + dy[dir] * step;
                
                if (newX < 0 || newX > 8 || newY < 0 || newY > 9)
                    break;
                
                int targetId = ToolManager.GetChessId(newX, newY);
                
                if (!jumped)
                {
                    if (targetId == -1)
                    {
                        // 没跳过炮架，空位可以移动
                        moves.Add(new AIMove
                        {
                            FromChessId = chess.Id,
                            FromLogX = startX,
                            FromLogY = startY,
                            ToLogX = newX,
                            ToLogY = newY,
                            CaptureId = -1
                        });
                    }
                    else
                    {
                        // 遇到棋子，作为炮架
                        jumped = true;
                    }
                }
                else
                {
                    if (targetId != -1)
                    {
                        ChessManager.Chess target = ChessManager.ChessArray[targetId];
                        if (target != null && target.Is_Red != chess.Is_Red)
                        {
                            // 跳过炮架后遇到敌方棋子，可以吃
                            moves.Add(new AIMove
                            {
                                FromChessId = chess.Id,
                                FromLogX = startX,
                                FromLogY = startY,
                                ToLogX = newX,
                                ToLogY = newY,
                                CaptureId = targetId
                            });
                        }
                        break;
                    }
                }
            }
        }
        
        return moves;
    }
    
    /// <summary>
    /// 生成象/相的走法
    /// </summary>
    private List<AIMove> GenerateElephantMoves(ChessManager.Chess chess, int startX, int startY)
    {
        List<AIMove> moves = new List<AIMove>();
        
        int[] dx = { -2, -2, 2, 2 };
        int[] dy = { -2, 2, -2, 2 };
        int[] eyeX = { -1, -1, 1, 1 };
        int[] eyeY = { -1, 1, -1, 1 };
        
        for (int i = 0; i < 4; i++)
        {
            int newX = startX + dx[i];
            int newY = startY + dy[i];
            int eyeCheckX = startX + eyeX[i];
            int eyeCheckY = startY + eyeY[i];
            
            // 边界检查
            if (newX < 0 || newX > 8 || newY < 0 || newY > 9)
                continue;
            
            // 象眼检查
            if (ToolManager.GetChessId(eyeCheckX, eyeCheckY) != -1)
                continue;
            
            // 过河检查
            if (chess.Is_Red && newY > 4)
                continue;
            if (!chess.Is_Red && newY < 5)
                continue;
            
            int targetId = ToolManager.GetChessId(newX, newY);
            
            if (targetId == -1)
            {
                moves.Add(new AIMove
                {
                    FromChessId = chess.Id,
                    FromLogX = startX,
                    FromLogY = startY,
                    ToLogX = newX,
                    ToLogY = newY,
                    CaptureId = -1
                });
            }
            else
            {
                ChessManager.Chess target = ChessManager.ChessArray[targetId];
                if (target != null && target.Is_Red != chess.Is_Red)
                {
                    moves.Add(new AIMove
                    {
                        FromChessId = chess.Id,
                        FromLogX = startX,
                        FromLogY = startY,
                        ToLogX = newX,
                        ToLogY = newY,
                        CaptureId = targetId
                    });
                }
            }
        }
        
        return moves;
    }
    
    /// <summary>
    /// 生成士的走法
    /// </summary>
    private List<AIMove> GenerateAdvisorMoves(ChessManager.Chess chess, int startX, int startY)
    {
        List<AIMove> moves = new List<AIMove>();
        
        int[] dx = { -1, -1, 1, 1 };
        int[] dy = { -1, 1, -1, 1 };
        
        for (int i = 0; i < 4; i++)
        {
            int newX = startX + dx[i];
            int newY = startY + dy[i];
            
            // 九宫格限制
            if (newX < 3 || newX > 5)
                continue;
            
            if (chess.Is_Red && (newY < 0 || newY > 2))
                continue;
            if (!chess.Is_Red && (newY < 7 || newY > 9))
                continue;
            
            int targetId = ToolManager.GetChessId(newX, newY);
            
            if (targetId == -1)
            {
                moves.Add(new AIMove
                {
                    FromChessId = chess.Id,
                    FromLogX = startX,
                    FromLogY = startY,
                    ToLogX = newX,
                    ToLogY = newY,
                    CaptureId = -1
                });
            }
            else
            {
                ChessManager.Chess target = ChessManager.ChessArray[targetId];
                if (target != null && target.Is_Red != chess.Is_Red)
                {
                    moves.Add(new AIMove
                    {
                        FromChessId = chess.Id,
                        FromLogX = startX,
                        FromLogY = startY,
                        ToLogX = newX,
                        ToLogY = newY,
                        CaptureId = targetId
                    });
                }
            }
        }
        
        return moves;
    }
    
    /// <summary>
    /// 生成将/帅的走法
    /// </summary>
    private List<AIMove> GenerateKingMoves(ChessManager.Chess chess, int startX, int startY)
    {
        List<AIMove> moves = new List<AIMove>();
        
        int[] dx = { -1, 1, 0, 0 };
        int[] dy = { 0, 0, -1, 1 };
        
        for (int i = 0; i < 4; i++)
        {
            int newX = startX + dx[i];
            int newY = startY + dy[i];
            
            // 九宫格限制
            if (newX < 3 || newX > 5)
                continue;
            
            if (chess.Is_Red && (newY < 0 || newY > 2))
                continue;
            if (!chess.Is_Red && (newY < 7 || newY > 9))
                continue;
            
            int targetId = ToolManager.GetChessId(newX, newY);
            
            if (targetId == -1)
            {
                moves.Add(new AIMove
                {
                    FromChessId = chess.Id,
                    FromLogX = startX,
                    FromLogY = startY,
                    ToLogX = newX,
                    ToLogY = newY,
                    CaptureId = -1
                });
            }
            else
            {
                ChessManager.Chess target = ChessManager.ChessArray[targetId];
                if (target != null && target.Is_Red != chess.Is_Red)
                {
                    moves.Add(new AIMove
                    {
                        FromChessId = chess.Id,
                        FromLogX = startX,
                        FromLogY = startY,
                        ToLogX = newX,
                        ToLogY = newY,
                        CaptureId = targetId
                    });
                }
            }
        }
        
        // 将帅对面（飞将）
        // 检查是否能直接吃掉对方将/帅
        for (int i = 0; i < 32; i++)
        {
            ChessManager.Chess target = ChessManager.ChessArray[i];
            if (target != null && !target.Is_Dead && 
                target.Type == ChessManager.Chess.ChessType.帅 && 
                target.Is_Red != chess.Is_Red)
            {
                int targetLogX = ToolManager.ChangeToLineX(target.Vec_X);
                int targetLogY = ToolManager.ChangeToLineY(target.Vec_Y);
                
                if (targetLogX == startX)
                {
                    // 同一列，检查中间是否有棋子
                    int minLogY = Math.Min(startY, targetLogY);
                    int maxLogY = Math.Max(startY, targetLogY);
                    int count = ToolManager.CountLineChess(startX, minLogY, startX, maxLogY);
                    
                    if (count == 0)
                    {
                        moves.Add(new AIMove
                        {
                            FromChessId = chess.Id,
                            FromLogX = startX,
                            FromLogY = startY,
                            ToLogX = targetLogX,
                            ToLogY = targetLogY,
                            CaptureId = target.Id
                        });
                    }
                }
            }
        }
        
        return moves;
    }
    
    /// <summary>
    /// 生成兵/卒的走法
    /// </summary>
    private List<AIMove> GeneratePawnMoves(ChessManager.Chess chess, int startX, int startY)
    {
        List<AIMove> moves = new List<AIMove>();
        
        // 兵只能向前，过河后可以左右
        List<int[]> directions = new List<int[]>();
        
        if (chess.Is_Red)
        {
            // 红兵向上（Y减小）
            directions.Add(new int[] { 0, 1 });  // 向前
            
            if (startY > 4)  // 过河后
            {
                directions.Add(new int[] { -1, 0 });  // 向左
                directions.Add(new int[] { 1, 0 });   // 向右
            }
        }
        else
        {
            // 黑卒向下（Y增大）
            directions.Add(new int[] { 0, -1 });  // 向前
            
            if (startY < 5)  // 过河后
            {
                directions.Add(new int[] { -1, 0 });  // 向左
                directions.Add(new int[] { 1, 0 });   // 向右
            }
        }
        
        foreach (int[] dir in directions)
        {
            int newX = startX + dir[0];
            int newY = startY + dir[1];
            
            if (newX < 0 || newX > 8 || newY < 0 || newY > 9)
                continue;
            
            int targetId = ToolManager.GetChessId(newX, newY);
            
            if (targetId == -1)
            {
                moves.Add(new AIMove
                {
                    FromChessId = chess.Id,
                    FromLogX = startX,
                    FromLogY = startY,
                    ToLogX = newX,
                    ToLogY = newY,
                    CaptureId = -1
                });
            }
            else
            {
                ChessManager.Chess target = ChessManager.ChessArray[targetId];
                if (target != null && target.Is_Red != chess.Is_Red)
                {
                    moves.Add(new AIMove
                    {
                        FromChessId = chess.Id,
                        FromLogX = startX,
                        FromLogY = startY,
                        ToLogX = newX,
                        ToLogY = newY,
                        CaptureId = targetId
                    });
                }
            }
        }
        
        return moves;
    }
}
