using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Normal难度AI - 普通难度（重新设计）
/// 
/// 【设计思路】
/// Normal AI使用2层Minimax搜索，能够看到一步对手的应对。
/// 这意味着AI会考虑"我走这步后，对手最好的应对是什么"。
/// 
/// 【特点】
/// 1. 2层Minimax搜索（看一步）
/// 2. 完整的评估函数（子力+位置+威胁+安全）
/// 3. Alpha-Beta剪枝优化
/// 4. 会避免明显的战术陷阱
/// 
/// 【适合人群】
/// 有一定象棋水平的玩家，需要认真计算才能获胜
/// </summary>
public class NormalAI
{
    // ==================== 核心组件 ====================
    
    /// <summary>
    /// 评估器实例
    /// </summary>
    private ChessEvaluator evaluator;
    
    /// <summary>
    /// 随机数生成器
    /// </summary>
    private System.Random random;
    
    // ==================== 搜索参数 ====================
    
    /// <summary>
    /// 搜索深度：3层（看一步对手应对+自己再走一步）
    /// ★加强：从2层提升到3层，AI能看到更远的局面
    /// 2层=只看对手怎么应对自己这步
    /// 3层=看对手应对后自己还有没有后续手段
    /// </summary>
    private const int SEARCH_DEPTH = 3;
    
    /// <summary>
    /// 无穷大值
    /// </summary>
    private const float INFINITY = 100000f;
    
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
    /// 马的位置价值表
    /// </summary>
    private static readonly int[,] KnightPositionTable = new int[,]
    {
        { -50, -40, -30, -30, -30, -30, -30, -40, -50 },
        { -40, -20,   0,   5,  10,   5,   0, -20, -40 },
        { -30,   5,  10,  15,  20,  15,  10,   5, -30 },
        { -30,   0,  15,  20,  25,  20,  15,   0, -30 },
        { -30,   5,  15,  20,  25,  20,  15,   5, -30 },
        { -30,   0,  15,  20,  25,  20,  15,   0, -30 },
        { -30,   5,  10,  15,  20,  15,  10,   5, -30 },
        { -40, -20,   0,   5,  10,   5,   0, -20, -40 },
        { -50, -40, -30, -30, -30, -30, -30, -40, -50 },
        { -60, -50, -40, -40, -40, -40, -40, -50, -60 }
    };
    
    /// <summary>
    /// 兵/卒的位置价值表（从黑方视角）
    /// </summary>
    private static readonly int[,] PawnPositionTable = new int[,]
    {
        {   0,   0,   0,   0,   0,   0,   0,   0,   0 },  // 行0（红方底线）
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
    
    // ==================== 构造函数 ====================
    
    public NormalAI()
    {
        evaluator = new ChessEvaluator();
        random = new System.Random();
        
        Debug.Log("[NormalAI] 普通AI初始化完成，搜索深度: " + SEARCH_DEPTH);
    }
    
    // ==================== 主要接口 ====================
    
    /// <summary>
    /// 获取最佳走法
    /// 使用Minimax算法进行浅层搜索
    /// </summary>
    public AIMove GetBestMove()
    {
        // 生成所有合法走法
        List<AIMove> legalMoves = evaluator.GenerateAllLegalMoves(forBlack: true);
        
        if (legalMoves.Count == 0)
        {
            Debug.Log("[NormalAI] 没有合法走法");
            return AIMove.Empty;
        }
        
        // 对每个走法进行搜索评估
        List<ScoredMove> scoredMoves = new List<ScoredMove>();
        
        foreach (AIMove move in legalMoves)
        {
            // 模拟走法
            SimulateMove(move, out float oldVecX, out float oldVecY, out bool wasDead);
            
            // 递归搜索（对手回合）
            float score = Minimax(SEARCH_DEPTH - 1, -INFINITY, INFINITY, false);
            
            // 撤销走法
            UndoMove(move, oldVecX, oldVecY, wasDead);
            
            scoredMoves.Add(new ScoredMove { Move = move, Score = score });
        }
        
        // 按分数排序
        scoredMoves.Sort((a, b) => b.Score.CompareTo(a.Score));
        
        // 收集最优走法
        float bestScore = scoredMoves[0].Score;
        List<AIMove> bestMoves = new List<AIMove>();
        
        foreach (ScoredMove sm in scoredMoves)
        {
            if (sm.Score >= bestScore - 10f)  // 允许小误差
                bestMoves.Add(sm.Move);
            else
                break;
        }
        
        // 随机选择
        AIMove selectedMove = bestMoves[random.Next(bestMoves.Count)];
        
        Debug.Log($"[NormalAI] 选择走法：棋子{selectedMove.FromChessId}，评分{bestScore:F1}");
        
        return selectedMove;
    }
    
    // ==================== Minimax算法 ====================
    
    /// <summary>
    /// Minimax算法核心
    /// </summary>
    private float Minimax(int depth, float alpha, float beta, bool isMaximizing)
    {
        // 到达搜索深度，返回评估值
        if (depth == 0)
        {
            return EvaluateBoard(isMaximizing);
        }
        
        // 生成当前方的走法
        List<AIMove> moves = evaluator.GenerateAllLegalMoves(forBlack: isMaximizing);
        
        // 没有合法走法
        if (moves.Count == 0)
        {
            // 被将死
            if (evaluator.IsInCheck(isMaximizing))
            {
                return isMaximizing ? -INFINITY + (SEARCH_DEPTH - depth) : INFINITY - (SEARCH_DEPTH - depth);
            }
            // 和棋
            return 0f;
        }
        
        // 走法排序（提高剪枝效率）
        moves = OrderMoves(moves);
        
        if (isMaximizing)
        {
            // MAX节点：选择最大分数
            float maxScore = -INFINITY;
            
            foreach (AIMove move in moves)
            {
                SimulateMove(move, out float oldVecX, out float oldVecY, out bool wasDead);
                float score = Minimax(depth - 1, alpha, beta, false);
                UndoMove(move, oldVecX, oldVecY, wasDead);
                
                maxScore = Math.Max(maxScore, score);
                alpha = Math.Max(alpha, score);
                
                // Alpha-Beta剪枝
                if (beta <= alpha)
                    break;
            }
            
            return maxScore;
        }
        else
        {
            // MIN节点：选择最小分数
            float minScore = INFINITY;
            
            foreach (AIMove move in moves)
            {
                SimulateMove(move, out float oldVecX, out float oldVecY, out bool wasDead);
                float score = Minimax(depth - 1, alpha, beta, true);
                UndoMove(move, oldVecX, oldVecY, wasDead);
                
                minScore = Math.Min(minScore, score);
                beta = Math.Min(beta, score);
                
                // Alpha-Beta剪枝
                if (beta <= alpha)
                    break;
            }
            
            return minScore;
        }
    }
    
    // ==================== 评估函数 ====================
    
    /// <summary>
    /// 评估当前局面
    /// </summary>
    private float EvaluateBoard(bool forBlack)
    {
        float score = 0f;
        
        for (int i = 0; i < 32; i++)
        {
            ChessManager.Chess chess = ChessManager.ChessArray[i];
            if (chess == null || chess.Is_Dead) continue;
            
            // 基础价值
            float value = GetPieceValue(chess.Type);
            
            // 位置加成
            value += GetPositionBonus(chess);
            
            // 机动性加成（简化：只考虑攻击范围）
            value += GetMobilityBonus(chess) * 0.5f;
            
            // 红方加分，黑方减分
            if (chess.Is_Red)
                score += value;
            else
                score -= value;
        }
        
        // 从黑方视角返回
        return forBlack ? -score : score;
    }
    
    /// <summary>
    /// 获取棋子价值
    /// </summary>
    private float GetPieceValue(ChessManager.Chess.ChessType type)
    {
        if (PieceValues.TryGetValue(type, out int value))
            return value;
        return 0f;
    }
    
    /// <summary>
    /// 获取位置加成
    /// </summary>
    private float GetPositionBonus(ChessManager.Chess chess)
    {
        int logX = ToolManager.ChangeToLineX(chess.Vec_X);
        int logY = ToolManager.ChangeToLineY(chess.Vec_Y);
        
        if (logX < 0 || logX > 8 || logY < 0 || logY > 9)
            return 0;
        
        // 黑方需要翻转坐标
        if (!chess.Is_Red)
        {
            logY = 9 - logY;
        }
        
        switch (chess.Type)
        {
            case ChessManager.Chess.ChessType.马:
                return KnightPositionTable[logY, logX];
            case ChessManager.Chess.ChessType.卒:
                return PawnPositionTable[logY, logX];
            case ChessManager.Chess.ChessType.车:
                // 车在中路加分
                if (logX >= 3 && logX <= 5) return 20;
                return 0;
            default:
                return 0;
        }
    }
    
    /// <summary>
    /// 获取机动性加成（简化版）
    /// </summary>
    private float GetMobilityBonus(ChessManager.Chess chess)
    {
        int logX = ToolManager.ChangeToLineX(chess.Vec_X);
        int logY = ToolManager.ChangeToLineY(chess.Vec_Y);
        
        switch (chess.Type)
        {
            case ChessManager.Chess.ChessType.车:
            case ChessManager.Chess.ChessType.炮:
                // 检查能控制的格子数（简化）
                return CountControlledSquares(chess, logX, logY);
            default:
                return 0;
        }
    }
    
    /// <summary>
    /// 计算棋子能控制的格子数（简化版）
    /// </summary>
    private float CountControlledSquares(ChessManager.Chess chess, int logX, int logY)
    {
        int count = 0;
        
        // 四个方向
        int[] dx = { 0, 0, -1, 1 };
        int[] dy = { -1, 1, 0, 0 };
        
        for (int dir = 0; dir < 4; dir++)
        {
            for (int step = 1; step < 10; step++)
            {
                int newX = logX + dx[dir] * step;
                int newY = logY + dy[dir] * step;
                
                if (newX < 0 || newX > 8 || newY < 0 || newY > 9)
                    break;
                
                count++;
                
                // 遇到棋子停止
                if (ToolManager.GetChessId(newX, newY) != -1)
                    break;
            }
        }
        
        return count * 2;  // 每个格子2分
    }
    
    // ==================== 辅助方法 ====================
    
    /// <summary>
    /// 走法排序
    /// </summary>
    private List<AIMove> OrderMoves(List<AIMove> moves)
    {
        List<AIMove> captureMoves = new List<AIMove>();
        List<AIMove> normalMoves = new List<AIMove>();
        
        foreach (AIMove move in moves)
        {
            if (move.CaptureId >= 0)
                captureMoves.Add(move);
            else
                normalMoves.Add(move);
        }
        
        // 按被吃棋子价值排序
        captureMoves.Sort((a, b) =>
        {
            ChessManager.Chess ca = ChessManager.ChessArray[a.CaptureId];
            ChessManager.Chess cb = ChessManager.ChessArray[b.CaptureId];
            float va = ca != null ? GetPieceValue(ca.Type) : 0;
            float vb = cb != null ? GetPieceValue(cb.Type) : 0;
            return vb.CompareTo(va);
        });
        
        captureMoves.AddRange(normalMoves);
        return captureMoves;
    }
    
    /// <summary>
    /// 模拟走法
    /// </summary>
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
    
    /// <summary>
    /// 撤销走法
    /// </summary>
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
