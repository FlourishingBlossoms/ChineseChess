using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hard难度AI - 困难难度（重新设计）
/// 
/// 【设计思路】
/// Hard AI使用神经网络评估 + 4-5层Minimax搜索。
/// 能够看到多步棋的发展，进行战术计算。
/// 
/// 【特点】
/// 1. 4层Minimax搜索（可配置到5层）
/// 2. 神经网络 + 规则混合评估
/// 3. Alpha-Beta剪枝 + 走法排序优化
/// 4. 置换表加速
/// 5. 将军检测和杀棋检测
/// 
/// 【适合人群】
/// 有较高象棋水平的玩家，需要深入计算才能获胜
/// </summary>
public class HardAI
{
    // ==================== 核心组件 ====================
    
    /// <summary>
    /// 神经网络评估器
    /// </summary>
    private ChessNeuralNetwork neuralNetwork;
    
    /// <summary>
    /// 规则评估器
    /// </summary>
    private ChessEvaluator evaluator;
    
    /// <summary>
    /// 随机数生成器
    /// </summary>
    private System.Random random;
    
    /// <summary>
    /// 置换表（缓存已评估的局面）
    /// </summary>
    private Dictionary<string, float> transpositionTable;
    
    // ==================== 搜索参数 ====================
    
    /// <summary>
    /// 搜索深度：4层（可以看2个完整回合）
    /// ★加强：考虑提升到5层（如果性能允许）
    /// </summary>
    private const int SEARCH_DEPTH = 4;
    
    /// <summary>
    /// 无穷大值
    /// </summary>
    private const float INFINITY = 100000f;
    
    /// <summary>
    /// 最大搜索时间（毫秒）
    /// </summary>
    private const int MAX_THINK_TIME = 5000;
    
    /// <summary>
    /// 搜索开始时间
    /// </summary>
    private System.DateTime searchStartTime;
    
    /// <summary>
    /// 是否超时
    /// </summary>
    private bool isTimeout;
    
    // ★新增：Killer Move 表（存储各层的最佳走法，用于走法排序）
    // killerMoves[depth] = 该层产生beta剪枝的走法
    private AIMove[] killerMoves;
    
    // ★新增：LMR（Late Move Reduction）参数
    // 搜索到第N个走法后，减少搜索深度（因为排序靠后的走法通常不好）
    private const int LMR_THRESHOLD = 4;    // 第5个走法开始考虑减深
    private const int LMR_REDUCTION = 1;     // 减少1层深度
    
    // ==================== 棋子价值表 ====================
    
    private static readonly Dictionary<ChessManager.Chess.ChessType, int> PieceValues = 
        new Dictionary<ChessManager.Chess.ChessType, int>
    {
        { ChessManager.Chess.ChessType.帅, 10000 },
        { ChessManager.Chess.ChessType.车, 900 },
        { ChessManager.Chess.ChessType.炮, 450 },
        { ChessManager.Chess.ChessType.马, 400 },
        { ChessManager.Chess.ChessType.象, 200 },
        { ChessManager.Chess.ChessType.士, 200 },
        { ChessManager.Chess.ChessType.卒, 100 }
    };
    
    // ==================== 位置价值表 ====================
    
    /// <summary>
    /// 车的位置价值表
    /// </summary>
    private static readonly int[,] RookPositionTable = new int[,]
    {
        {   0,   0,   0,   5,  10,   5,   0,   0,   0 },
        {   5,  10,  10,  15,  20,  15,  10,  10,   5 },
        {  10,  15,  15,  20,  25,  20,  15,  15,  10 },
        {  15,  20,  20,  25,  30,  25,  20,  20,  15 },
        {  15,  20,  20,  25,  30,  25,  20,  20,  15 },
        {  15,  20,  20,  25,  30,  25,  20,  20,  15 },
        {  10,  15,  15,  20,  25,  20,  15,  15,  10 },
        {   5,  10,  10,  15,  20,  15,  10,  10,   5 },
        {   0,   5,   5,  10,  15,  10,   5,   5,   0 },
        {   0,   0,   0,   5,  10,   5,   0,   0,   0 }
    };
    
    /// <summary>
    /// 马的位置价值表
    /// </summary>
    private static readonly int[,] KnightPositionTable = new int[,]
    {
        { -60, -50, -40, -30, -30, -30, -40, -50, -60 },
        { -50, -30, -10,   0,  10,   0, -10, -30, -50 },
        { -40, -10,  10,  20,  25,  20,  10, -10, -40 },
        { -30,   0,  20,  30,  35,  30,  20,   0, -30 },
        { -30,   5,  25,  35,  40,  35,  25,   5, -30 },
        { -30,   5,  25,  35,  40,  35,  25,   5, -30 },
        { -30,   0,  20,  30,  35,  30,  20,   0, -30 },
        { -40, -10,  10,  20,  25,  20,  10, -10, -40 },
        { -50, -30, -10,   0,  10,   0, -10, -30, -50 },
        { -60, -50, -40, -30, -30, -30, -40, -50, -60 }
    };
    
    /// <summary>
    /// 炮的位置价值表
    /// </summary>
    private static readonly int[,] CannonPositionTable = new int[,]
    {
        {   0,   0,   5,  10,  15,  10,   5,   0,   0 },
        {   0,   5,  10,  15,  20,  15,  10,   5,   0 },
        {   5,  10,  15,  20,  25,  20,  15,  10,   5 },
        {  10,  15,  20,  25,  30,  25,  20,  15,  10 },
        {  10,  15,  20,  25,  30,  25,  20,  15,  10 },
        {  10,  15,  20,  25,  30,  25,  20,  15,  10 },
        {   5,  10,  15,  20,  25,  20,  15,  10,   5 },
        {   0,   5,  10,  15,  20,  15,  10,   5,   0 },
        {   0,   0,   5,  10,  15,  10,   5,   0,   0 },
        {   0,   0,   0,   5,  10,   5,   0,   0,   0 }
    };
    
    /// <summary>
    /// 兵/卒的位置价值表
    /// </summary>
    private static readonly int[,] PawnPositionTable = new int[,]
    {
        {   0,   0,   0,   0,   0,   0,   0,   0,   0 },
        { 100, 100, 120, 130, 140, 130, 120, 100, 100 },
        { 100, 100, 120, 130, 140, 130, 120, 100, 100 },
        {  80, 100, 120, 120, 120, 120, 120, 100,  80 },
        {  80,  80,  80,  80,  80,  80,  80,  80,  80 },
        {   0,   0,   0,   0,   0,   0,   0,   0,   0 },
        {   0,   0,   0,   0,   0,   0,   0,   0,   0 },
        {   0,   0,   0,   0,   0,   0,   0,   0,   0 },
        {   0,   0,   0,   0,   0,   0,   0,   0,   0 },
        {   0,   0,   0,   0,   0,   0,   0,   0,   0 }
    };
    
    // ==================== 构造函数 ====================
    
    public HardAI()
    {
        neuralNetwork = new ChessNeuralNetwork();
        evaluator = new ChessEvaluator();
        random = new System.Random();
        transpositionTable = new Dictionary<string, float>();
        
        Debug.Log("[HardAI] 困难AI初始化完成，搜索深度: " + SEARCH_DEPTH);
    }
    
    // ==================== 主要接口 ====================
    
    /// <summary>
    /// 获取最佳走法
    /// </summary>
    public AIMove GetBestMove()
    {
        searchStartTime = System.DateTime.Now;
        isTimeout = false;
        transpositionTable.Clear();
        killerMoves = new AIMove[SEARCH_DEPTH + 1]; // ★新增：初始化Killer Move表
        
        // 生成所有合法走法
        List<AIMove> legalMoves = evaluator.GenerateAllLegalMoves(forBlack: true);
        
        if (legalMoves.Count == 0)
        {
            Debug.Log("[HardAI] 没有合法走法");
            return AIMove.Empty;
        }
        
        // 走法排序（顶层不需要Killer Move，但保持接口一致）
        legalMoves = OrderMoves(legalMoves, SEARCH_DEPTH);
        
        // 迭代加深搜索
        AIMove bestMove = AIMove.Empty;
        float bestScore = -INFINITY;
        
        for (int depth = 1; depth <= SEARCH_DEPTH; depth++)
        {
            if (isTimeout) break;
            
            List<ScoredMove> scoredMoves = new List<ScoredMove>();
            
            foreach (AIMove move in legalMoves)
            {
                if (isTimeout) break;
                
                SimulateMove(move, out float oldVecX, out float oldVecY, out bool wasDead);
                float score = Minimax(depth - 1, -INFINITY, INFINITY, false);
                UndoMove(move, oldVecX, oldVecY, wasDead);
                
                scoredMoves.Add(new ScoredMove { Move = move, Score = score });
            }
            
            if (!isTimeout && scoredMoves.Count > 0)
            {
                scoredMoves.Sort((a, b) => b.Score.CompareTo(a.Score));
                bestMove = scoredMoves[0].Move;
                bestScore = scoredMoves[0].Score;
                
                // 更新走法顺序，最优走法放前面
                legalMoves.Insert(0, bestMove);
                legalMoves.Remove(bestMove);
            }
        }
        
        if (!bestMove.IsValid && legalMoves.Count > 0)
        {
            bestMove = legalMoves[0];
        }
        
        Debug.Log($"[HardAI] 选择走法：棋子{bestMove.FromChessId}，评分{bestScore:F1}");
        
        return bestMove;
    }
    
    // ==================== Minimax算法 ====================
    
    /// <summary>
    /// Minimax算法核心
    /// </summary>
    private float Minimax(int depth, float alpha, float beta, bool isMaximizing)
    {
        // 检查超时
        if ((System.DateTime.Now - searchStartTime).TotalMilliseconds > MAX_THINK_TIME)
        {
            isTimeout = true;
            return EvaluateBoard(isMaximizing);
        }
        
        // 到达搜索深度
        if (depth == 0)
        {
            return QuiescenceSearch(alpha, beta, isMaximizing, 4);
        }
        
        // 置换表查询
        string boardKey = GetBoardKey();
        if (transpositionTable.TryGetValue(boardKey, out float cachedScore))
        {
            return cachedScore;
        }
        
        // 生成走法
        List<AIMove> moves = evaluator.GenerateAllLegalMoves(forBlack: isMaximizing);
        
        // 没有合法走法
        if (moves.Count == 0)
        {
            if (evaluator.IsInCheck(isMaximizing))
            {
                // 被将死，返回极差分数（考虑深度，越快被将死越差）
                return isMaximizing ? -INFINITY + (SEARCH_DEPTH - depth) : INFINITY - (SEARCH_DEPTH - depth);
            }
            return 0f;  // 和棋
        }
        
        // 走法排序（含Killer Move优先）
        moves = OrderMoves(moves, depth);
        
        float bestScore;
        int moveIndex = 0; // ★新增：用于LMR
        
        if (isMaximizing)
        {
            bestScore = -INFINITY;
            
            foreach (AIMove move in moves)
            {
                SimulateMove(move, out float oldVecX, out float oldVecY, out bool wasDead);
                
                float score;
                // ★新增：LMR — 排序靠后的非吃子走法，减少搜索深度
                if (moveIndex >= LMR_THRESHOLD && move.CaptureId < 0 && depth > 2)
                {
                    // 先用减少的深度搜
                    score = Minimax(depth - 1 - LMR_REDUCTION, alpha, beta, false);
                    // 如果结果比预期好（接近alpha），用完整深度重新搜
                    if (score > alpha)
                    {
                        score = Minimax(depth - 1, alpha, beta, false);
                    }
                }
                else
                {
                    score = Minimax(depth - 1, alpha, beta, false);
                }
                
                UndoMove(move, oldVecX, oldVecY, wasDead);
                
                bestScore = Math.Max(bestScore, score);
                alpha = Math.Max(alpha, score);
                
                // ★新增：记录Killer Move（产生剪枝的好走法）
                if (beta <= alpha)
                {
                    if (move.CaptureId < 0) // 只记录非吃子走法
                        killerMoves[depth] = move;
                    break;
                }
                moveIndex++;
            }
        }
        else
        {
            bestScore = INFINITY;
            
            foreach (AIMove move in moves)
            {
                SimulateMove(move, out float oldVecX, out float oldVecY, out bool wasDead);
                
                float score;
                if (moveIndex >= LMR_THRESHOLD && move.CaptureId < 0 && depth > 2)
                {
                    score = Minimax(depth - 1 - LMR_REDUCTION, alpha, beta, true);
                    if (score < beta)
                    {
                        score = Minimax(depth - 1, alpha, beta, true);
                    }
                }
                else
                {
                    score = Minimax(depth - 1, alpha, beta, true);
                }
                
                UndoMove(move, oldVecX, oldVecY, wasDead);
                
                bestScore = Math.Min(bestScore, score);
                beta = Math.Min(beta, score);
                
                if (beta <= alpha)
                {
                    if (move.CaptureId < 0)
                        killerMoves[depth] = move;
                    break;
                }
                moveIndex++;
            }
        }
        
        // 存入置换表
        transpositionTable[boardKey] = bestScore;
        
        return bestScore;
    }
    
    /// <summary>
    /// 静态搜索（Quiescence Search）
    /// 在搜索到底部后，继续搜索吃子走法，避免水平线效应
    /// </summary>
    private float QuiescenceSearch(float alpha, float beta, bool isMaximizing, int depth)
    {
        // 静态评估
        float standPat = EvaluateBoard(isMaximizing);
        
        if (depth == 0)
            return standPat;
        
        if (isMaximizing)
        {
            if (standPat >= beta)
                return beta;
            if (standPat > alpha)
                alpha = standPat;
        }
        else
        {
            if (standPat <= alpha)
                return alpha;
            if (standPat < beta)
                beta = standPat;
        }
        
        // 只搜索吃子走法
        List<AIMove> moves = evaluator.GenerateAllLegalMoves(forBlack: isMaximizing);
        List<AIMove> captureMoves = moves.FindAll(m => m.CaptureId >= 0);
        
        if (captureMoves.Count == 0)
            return standPat;
        
        // 按被吃棋子价值排序
        captureMoves.Sort((a, b) =>
        {
            ChessManager.Chess ca = ChessManager.ChessArray[a.CaptureId];
            ChessManager.Chess cb = ChessManager.ChessArray[b.CaptureId];
            float va = ca != null ? GetPieceValue(ca.Type) : 0;
            float vb = cb != null ? GetPieceValue(cb.Type) : 0;
            return vb.CompareTo(va);
        });
        
        foreach (AIMove move in captureMoves)
        {
            SimulateMove(move, out float oldVecX, out float oldVecY, out bool wasDead);
            float score = QuiescenceSearch(alpha, beta, !isMaximizing, depth - 1);
            UndoMove(move, oldVecX, oldVecY, wasDead);
            
            if (isMaximizing)
            {
                if (score >= beta)
                    return beta;
                if (score > alpha)
                    alpha = score;
            }
            else
            {
                if (score <= alpha)
                    return alpha;
                if (score < beta)
                    beta = score;
            }
        }
        
        return isMaximizing ? alpha : beta;
    }
    
    // ==================== 评估函数 ====================
    
    /// <summary>
    /// 评估当前局面
    /// </summary>
    private float EvaluateBoard(bool forBlack)
    {
        float score = 0f;
        
        // 神经网络评估
        float nnScore = neuralNetwork.EvaluatePosition();
        
        // 规则评估
        float ruleScore = 0f;
        
        for (int i = 0; i < 32; i++)
        {
            ChessManager.Chess chess = ChessManager.ChessArray[i];
            if (chess == null || chess.Is_Dead) continue;
            
            float value = GetPieceValue(chess.Type);
            value += GetPositionBonus(chess);
            value += GetMobilityBonus(chess);
            value += GetKingSafetyBonus(chess);
            
            if (chess.Is_Red)
                ruleScore += value;
            else
                ruleScore -= value;
        }
        
        // 混合评估：神经网络60%，规则评估40%
        score = nnScore * 0.6f + ruleScore * 0.004f;
        
        return forBlack ? -score : score;
    }
    
    private float GetPieceValue(ChessManager.Chess.ChessType type)
    {
        if (PieceValues.TryGetValue(type, out int value))
            return value;
        return 0f;
    }
    
    private float GetPositionBonus(ChessManager.Chess chess)
    {
        int logX = ToolManager.ChangeToLineX(chess.Vec_X);
        int logY = ToolManager.ChangeToLineY(chess.Vec_Y);
        
        if (logX < 0 || logX > 8 || logY < 0 || logY > 9)
            return 0;
        
        if (!chess.Is_Red)
            logY = 9 - logY;
        
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
    
    /// <summary>
    /// 机动性加成
    /// </summary>
    private float GetMobilityBonus(ChessManager.Chess chess)
    {
        // 简化：根据棋子类型给固定机动性分数
        switch (chess.Type)
        {
            case ChessManager.Chess.ChessType.车:
                return 15;
            case ChessManager.Chess.ChessType.马:
                return 10;
            case ChessManager.Chess.ChessType.炮:
                return 12;
            default:
                return 0;
        }
    }
    
    /// <summary>
    /// 将帅安全加成
    /// </summary>
    private float GetKingSafetyBonus(ChessManager.Chess chess)
    {
        if (chess.Type != ChessManager.Chess.ChessType.帅)
            return 0;
        
        int logX = ToolManager.ChangeToLineX(chess.Vec_X);
        int logY = ToolManager.ChangeToLineY(chess.Vec_Y);
        
        float safety = 0f;
        
        // 检查周围是否有士/象保护
        for (int i = 0; i < 32; i++)
        {
            ChessManager.Chess protector = ChessManager.ChessArray[i];
            if (protector == null || protector.Is_Dead) continue;
            if (protector.Is_Red != chess.Is_Red) continue;
            
            if (protector.Type == ChessManager.Chess.ChessType.士 ||
                protector.Type == ChessManager.Chess.ChessType.象)
            {
                int px = ToolManager.ChangeToLineX(protector.Vec_X);
                int py = ToolManager.ChangeToLineY(protector.Vec_Y);
                
                // 在九宫格附近
                if (Math.Abs(px - logX) <= 2 && Math.Abs(py - logY) <= 2)
                    safety += 10;
            }
        }
        
        return safety;
    }
    
    // ==================== 辅助方法 ====================
    
    /// <summary>
    /// 走法排序
    /// ★加强：加入Killer Move优先排序
    /// 排序优先级：吃子(MVV-LVA) > Killer Move > 将军走法 > 普通走法
    /// </summary>
    private List<AIMove> OrderMoves(List<AIMove> moves, int depth = -1)
    {
        List<AIMove> captureMoves = new List<AIMove>();
        List<AIMove> killerMoveList = new List<AIMove>();
        List<AIMove> checkMoves = new List<AIMove>();
        List<AIMove> normalMoves = new List<AIMove>();
        
        // ★新增：获取当前层的Killer Move
        AIMove killer = (depth >= 0 && depth < killerMoves.Length) ? killerMoves[depth] : AIMove.Empty;
        
        foreach (AIMove move in moves)
        {
            if (move.CaptureId >= 0)
            {
                captureMoves.Add(move);
            }
            else if (killer.IsValid && move.FromChessId == killer.FromChessId && 
                     move.ToLogX == killer.ToLogX && move.ToLogY == killer.ToLogY)
            {
                // ★新增：Killer Move 优先
                killerMoveList.Add(move);
            }
            else if (IsCheckMove(move))
            {
                checkMoves.Add(move);
            }
            else
            {
                normalMoves.Add(move);
            }
        }
        
        // 吃子走法按MVV-LVA排序（Most Valuable Victim - Least Valuable Attacker）
        captureMoves.Sort((a, b) =>
        {
            ChessManager.Chess ca = ChessManager.ChessArray[a.CaptureId];
            ChessManager.Chess cb = ChessManager.ChessArray[b.CaptureId];
            ChessManager.Chess aa = ChessManager.ChessArray[a.FromChessId];
            ChessManager.Chess ab = ChessManager.ChessArray[b.FromChessId];
            
            float va = ca != null ? GetPieceValue(ca.Type) : 0;
            float vb = cb != null ? GetPieceValue(cb.Type) : 0;
            float aaVal = aa != null ? GetPieceValue(aa.Type) : 0;
            float abVal = ab != null ? GetPieceValue(ab.Type) : 0;
            
            // MVV-LVA：优先吃高价值棋子，用低价值棋子吃
            return (vb - abVal).CompareTo(va - aaVal);
        });
        
        List<AIMove> result = new List<AIMove>();
        result.AddRange(captureMoves);
        result.AddRange(killerMoveList);  // ★新增：Killer Move 排在吃子之后
        result.AddRange(checkMoves);
        result.AddRange(normalMoves);
        
        return result;
    }
    
    /// <summary>
    /// 检查是否是将军走法
    /// </summary>
    private bool IsCheckMove(AIMove move)
    {
        ChessManager.Chess movingPiece = ChessManager.ChessArray[move.FromChessId];
        if (movingPiece == null) return false;
        
        // 找对方将/帅
        for (int i = 0; i < 32; i++)
        {
            ChessManager.Chess king = ChessManager.ChessArray[i];
            if (king == null || king.Is_Dead) continue;
            if (king.Type != ChessManager.Chess.ChessType.帅) continue;
            if (king.Is_Red == movingPiece.Is_Red) continue;
            
            int kingLogX = ToolManager.ChangeToLineX(king.Vec_X);
            int kingLogY = ToolManager.ChangeToLineY(king.Vec_Y);
            
            if (CanAttack(movingPiece.Type, move.ToLogX, move.ToLogY, kingLogX, kingLogY))
                return true;
        }
        
        return false;
    }
    
    private bool CanAttack(ChessManager.Chess.ChessType type, int fromX, int fromY, int toX, int toY)
    {
        int dx = Math.Abs(fromX - toX);
        int dy = Math.Abs(fromY - toY);
        
        switch (type)
        {
            case ChessManager.Chess.ChessType.车:
                return dx == 0 || dy == 0;
            case ChessManager.Chess.ChessType.马:
                return (dx == 1 && dy == 2) || (dx == 2 && dy == 1);
            case ChessManager.Chess.ChessType.炮:
                return dx == 0 || dy == 0;
            default:
                return false;
        }
    }
    
    /// <summary>
    /// 获取棋盘状态键（用于置换表）
    /// </summary>
    private string GetBoardKey()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        
        for (int i = 0; i < 32; i++)
        {
            ChessManager.Chess chess = ChessManager.ChessArray[i];
            if (chess == null || chess.Is_Dead)
            {
                sb.Append("x");
            }
            else
            {
                sb.Append((int)chess.Type);
                sb.Append(chess.Is_Red ? "R" : "B");
            }
        }
        
        return sb.ToString();
    }
    
    private void SimulateMove(AIMove move, out float oldVecX, out float oldVecY, out bool wasDead)
    {
        ChessManager.Chess movingPiece = ChessManager.ChessArray[move.FromChessId];
        
        oldVecX = movingPiece.Vec_X;
        oldVecY = movingPiece.Vec_Y;
        wasDead = false;
        
        movingPiece.Vec_X = ToolManager.ChangeBackX(move.ToLogX);
        movingPiece.Vec_Y = ToolManager.ChangeBackY(move.ToLogY);
        
        if (move.CaptureId >= 0 && move.CaptureId < 32)
        {
            ChessManager.Chess captured = ChessManager.ChessArray[move.CaptureId];
            if (captured != null)
            {
                wasDead = captured.Is_Dead;
                captured.Is_Dead = true;
            }
        }
    }
    
    private void UndoMove(AIMove move, float oldVecX, float oldVecY, bool wasDead)
    {
        ChessManager.Chess movingPiece = ChessManager.ChessArray[move.FromChessId];
        
        movingPiece.Vec_X = oldVecX;
        movingPiece.Vec_Y = oldVecY;
        
        if (move.CaptureId >= 0 && move.CaptureId < 32)
        {
            ChessManager.Chess captured = ChessManager.ChessArray[move.CaptureId];
            if (captured != null)
            {
                captured.Is_Dead = wasDead;
            }
        }
    }
    
    private struct ScoredMove
    {
        public AIMove Move;
        public float Score;
    }
}
