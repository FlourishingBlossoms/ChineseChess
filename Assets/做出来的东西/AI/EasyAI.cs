using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Easy难度AI - 简单难度（重新设计）
/// 
/// 【设计思路】
/// 即使是简单难度，也不能只是随机走棋，否则玩家无法学到东西。
/// Easy AI使用"贪心策略"：评估每个走法的即时价值，选择最优走法。
/// 
/// 【特点】
/// 1. 只考虑当前一步，不进行深度搜索
/// 2. 使用简化的评估函数（子力价值 + 基本位置）
/// 3. 会优先吃子、避免送子
/// 4. 不会犯明显的低级错误
/// 
/// 【适合人群】
/// 有一定象棋基础的玩家，需要认真思考才能获胜
/// </summary>
public class EasyAI
{
    // ==================== 核心组件 ====================
    
    /// <summary>
    /// 评估器实例（用于生成合法走法）
    /// </summary>
    private ChessEvaluator evaluator;
    
    /// <summary>
    /// 随机数生成器（用于同等分数时随机选择）
    /// </summary>
    private System.Random random;
    
    // ==================== 棋子价值表 ====================
    // Easy难度使用简化版价值表
    
    /// <summary>
    /// 棋子基础价值
    /// </summary>
    private static readonly Dictionary<ChessManager.Chess.ChessType, int> PieceValues = 
        new Dictionary<ChessManager.Chess.ChessType, int>
    {
        { ChessManager.Chess.ChessType.帅, 10000 },  // 将/帅：最重要
        { ChessManager.Chess.ChessType.车, 900 },    // 车：最强棋子
        { ChessManager.Chess.ChessType.炮, 450 },    // 炮：远程攻击
        { ChessManager.Chess.ChessType.马, 400 },    // 马：机动性强
        { ChessManager.Chess.ChessType.象, 200 },    // 象：防守
        { ChessManager.Chess.ChessType.士, 200 },    // 士：防守
        { ChessManager.Chess.ChessType.卒, 100 }     // 兵/卒：基础价值
    };
    
    /// <summary>
    /// 兵卒过河后的价值加成
    /// </summary>
    private const int PAWN_CROSS_RIVER_BONUS = 50;
    
    /// <summary>
    /// 将军加分
    /// </summary>
    private const int CHECK_BONUS = 30;
    
    // ==================== 构造函数 ====================
    
    /// <summary>
    /// 构造函数：初始化AI
    /// </summary>
    public EasyAI()
    {
        evaluator = new ChessEvaluator();
        random = new System.Random();
        
        Debug.Log("[EasyAI] 简单AI初始化完成（贪心策略）");
    }
    
    // ==================== 主要接口 ====================
    
    /// <summary>
    /// 获取最佳走法
    /// Easy难度策略：贪心选择评估分数最高的走法
    /// </summary>
    /// <returns>AI选择的走法</returns>
    public AIMove GetBestMove()
    {
        // 第一步：生成所有合法走法
        List<AIMove> legalMoves = evaluator.GenerateAllLegalMoves(forBlack: true);
        
        // 如果没有合法走法，返回空走法
        if (legalMoves.Count == 0)
        {
            Debug.Log("[EasyAI] 没有合法走法");
            return AIMove.Empty;
        }
        
        // 第二步：评估每个走法
        List<ScoredMove> scoredMoves = new List<ScoredMove>();
        
        foreach (AIMove move in legalMoves)
        {
            float score = EvaluateMove(move);
            scoredMoves.Add(new ScoredMove { Move = move, Score = score });
        }
        
        // 第三步：按分数排序（降序）
        scoredMoves.Sort((a, b) => b.Score.CompareTo(a.Score));
        
        // 第四步：收集最高分的走法（可能有多个同分）
        float bestScore = scoredMoves[0].Score;
        List<AIMove> bestMoves = new List<AIMove>();
        
        foreach (ScoredMove sm in scoredMoves)
        {
            // 允许小范围误差，增加一点随机性
            if (sm.Score >= bestScore - 5f)
                bestMoves.Add(sm.Move);
            else
                break;
        }
        
        // 第五步：在最优走法中随机选择（增加不可预测性）
        AIMove selectedMove = bestMoves[random.Next(bestMoves.Count)];
        
        Debug.Log($"[EasyAI] 选择走法：棋子{selectedMove.FromChessId} 从({selectedMove.FromLogX},{selectedMove.FromLogY}) 到({selectedMove.ToLogX},{selectedMove.ToLogY})，评分{bestScore:F1}");
        
        return selectedMove;
    }
    
    // ==================== 评估函数 ====================
    
    /// <summary>
    /// 评估单个走法的分数
    /// Easy难度的评估函数相对简单，但足以避免低级错误
    /// </summary>
    private float EvaluateMove(AIMove move)
    {
        float score = 0f;
        
        // ========== 1. 吃子价值（最重要） ==========
        if (move.CaptureId >= 0 && move.CaptureId < 32)
        {
            ChessManager.Chess captured = ChessManager.ChessArray[move.CaptureId];
            if (captured != null && !captured.Is_Dead)
            {
                // 吃子获得被吃棋子的价值
                score += GetPieceValue(captured.Type);
                
                // 额外加成：吃重要棋子
                if (captured.Type == ChessManager.Chess.ChessType.车)
                    score += 50;  // 吃车额外加分
                if (captured.Type == ChessManager.Chess.ChessType.帅)
                    score += 10000;  // 吃将/帅，直接获胜
            }
        }
        
        // ========== 2. 位置改善评估 ==========
        ChessManager.Chess movingPiece = ChessManager.ChessArray[move.FromChessId];
        if (movingPiece != null)
        {
            // 兵卒过河加分
            if (movingPiece.Type == ChessManager.Chess.ChessType.卒)
            {
                // 黑卒向红方阵地移动（Y减小）
                if (move.ToLogY < 5)  // 过河
                {
                    score += PAWN_CROSS_RIVER_BONUS;
                    // 越深入加分越多
                    score += (5 - move.ToLogY) * 10;
                }
            }
            
            // 车占据要道（中路或开放线）
            if (movingPiece.Type == ChessManager.Chess.ChessType.车)
            {
                // 占据中路
                if (move.ToLogX >= 3 && move.ToLogX <= 5)
                    score += 20;
                // 占据对方底线
                if (move.ToLogY <= 2)
                    score += 30;
            }
            
            // 马跳到好位置
            if (movingPiece.Type == ChessManager.Chess.ChessType.马)
            {
                // 马在中央位置更好
                if (move.ToLogX >= 2 && move.ToLogX <= 6 && 
                    move.ToLogY >= 3 && move.ToLogY <= 6)
                    score += 15;
            }
            
            // 炮的位置
            if (movingPiece.Type == ChessManager.Chess.ChessType.炮)
            {
                // 炮在对方阵地有威胁
                if (move.ToLogY <= 4)
                    score += 10;
            }
        }
        
        // ========== 3. 威胁评估（能否将军） ==========
        score += EvaluateCheck(move);
        
        // ========== 4. 安全性评估（移动后是否安全） ==========
        score -= EvaluateDanger(move);
        
        // ========== 5. ★新增：对手最佳吃子应对 ==========
        // 模拟自己走完后，对手能吃我什么子
        // 这是让EasyAI"不送子"的关键——只看1步对手应对
        score -= EvaluateOpponentCapture(move);
        
        return score;
    }
    
    /// <summary>
    /// 评估走法是否能将军
    /// </summary>
    private float EvaluateCheck(AIMove move)
    {
        float score = 0f;
        
        ChessManager.Chess movingPiece = ChessManager.ChessArray[move.FromChessId];
        if (movingPiece == null) return 0f;
        
        // 找到红方将/帅的位置
        for (int i = 0; i < 16; i++)  // 红方棋子ID是0-15
        {
            ChessManager.Chess king = ChessManager.ChessArray[i];
            if (king != null && !king.Is_Dead && 
                king.Type == ChessManager.Chess.ChessType.帅)
            {
                int kingLogX = ToolManager.ChangeToLineX(king.Vec_X);
                int kingLogY = ToolManager.ChangeToLineY(king.Vec_Y);
                
                // 检查是否能攻击到将/帅
                if (CanAttackFrom(movingPiece.Type, move.ToLogX, move.ToLogY, kingLogX, kingLogY))
                {
                    score += CHECK_BONUS;
                }
            }
        }
        
        return score;
    }
    
    /// <summary>
    /// 检查棋子是否能从指定位置攻击目标位置
    /// </summary>
    private bool CanAttackFrom(ChessManager.Chess.ChessType type, int fromX, int fromY, int targetX, int targetY)
    {
        switch (type)
        {
            case ChessManager.Chess.ChessType.车:
                // 车走直线
                if (fromX == targetX || fromY == targetY)
                {
                    // 简化：假设路径畅通
                    return true;
                }
                return false;
                
            case ChessManager.Chess.ChessType.马:
                // 马走日
                int dx = Math.Abs(fromX - targetX);
                int dy = Math.Abs(fromY - targetY);
                return (dx == 1 && dy == 2) || (dx == 2 && dy == 1);
                
            case ChessManager.Chess.ChessType.炮:
                // 炮需要炮架才能吃子
                if (fromX == targetX || fromY == targetY)
                {
                    // 简化：假设有炮架
                    return true;
                }
                return false;
                
            case ChessManager.Chess.ChessType.卒:
                // 黑卒向下攻击
                if (fromY > targetY && Math.Abs(fromX - targetX) <= 1)
                {
                    // 过河后可以横移攻击
                    if (fromY < 5 || fromX == targetX)
                        return true;
                }
                return false;
                
            default:
                return false;
        }
    }
    
    /// <summary>
    /// 评估走法后的危险性
    /// 检查移动后是否会被对方棋子攻击
    /// </summary>
    private float EvaluateDanger(AIMove move)
    {
        float danger = 0f;
        
        ChessManager.Chess movingPiece = ChessManager.ChessArray[move.FromChessId];
        if (movingPiece == null) return 0f;
        
        float pieceValue = GetPieceValue(movingPiece.Type);
        
        // 检查目标位置是否在对方棋子的攻击范围内
        for (int i = 0; i < 16; i++)  // 只检查红方棋子
        {
            ChessManager.Chess enemy = ChessManager.ChessArray[i];
            if (enemy == null || enemy.Is_Dead) continue;
            
            int enemyLogX = ToolManager.ChangeToLineX(enemy.Vec_X);
            int enemyLogY = ToolManager.ChangeToLineY(enemy.Vec_Y);
            
            if (CanAttackFrom(enemy.Type, enemyLogX, enemyLogY, move.ToLogX, move.ToLogY))
            {
                // 被威胁，根据棋子价值扣分
                // 但如果移动的棋子价值低，扣分少（可以用小棋子换位置）
                danger += pieceValue * 0.3f;
            }
        }
        
        // 如果是吃子走法，检查是否是"交换"（对方能否吃回来）
        if (move.CaptureId >= 0)
        {
            ChessManager.Chess captured = ChessManager.ChessArray[move.CaptureId];
            if (captured != null)
            {
                float capturedValue = GetPieceValue(captured.Type);
                // 如果吃的是低价值棋子，但自己会被吃，可能不划算
                if (danger > 0 && capturedValue < pieceValue * 0.8f)
                {
                    danger += (pieceValue - capturedValue) * 0.5f;
                }
            }
        }
        
        return danger;
    }
    
    // ════════════════════════════════════════════════════════════════
    //  ★新增：对手最佳吃子应对评估
    //  模拟自己走完后，找对手能吃的最大子，用于防止送子
    // ════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// 模拟走完后，对手能吃的最大价值棋子
    /// 返回对手能获得的子力收益（AI的损失）
    /// </summary>
    private float EvaluateOpponentCapture(AIMove move)
    {
        ChessManager.Chess movingPiece = ChessManager.ChessArray[move.FromChessId];
        if (movingPiece == null) return 0f;
        
        // 保存原始位置
        float origX = movingPiece.Vec_X;
        float origY = movingPiece.Vec_Y;
        bool capturedWasDead = false;
        ChessManager.Chess capturedChess = null;
        
        // 模拟走棋
        movingPiece.Vec_X = ToolManager.ChangeBackX(move.ToLogX);
        movingPiece.Vec_Y = ToolManager.ChangeBackY(move.ToLogY);
        
        if (move.CaptureId >= 0 && move.CaptureId < 32)
        {
            capturedChess = ChessManager.ChessArray[move.CaptureId];
            if (capturedChess != null)
            {
                capturedWasDead = capturedChess.Is_Dead;
                capturedChess.Is_Dead = true;
            }
        }
        
        // 找对手（红方）能吃的最大价值黑方棋子
        float maxCapture = 0f;
        for (int i = 0; i < 16; i++) // 红方棋子ID 0-15
        {
            ChessManager.Chess enemy = ChessManager.ChessArray[i];
            if (enemy == null || enemy.Is_Dead) continue;
            
            int enemyLogX = ToolManager.ChangeToLineX(enemy.Vec_X);
            int enemyLogY = ToolManager.ChangeToLineY(enemy.Vec_Y);
            
            // 检查红方棋子能吃到哪些黑方棋子
            for (int j = 16; j < 32; j++) // 黑方棋子ID 16-31
            {
                ChessManager.Chess target = ChessManager.ChessArray[j];
                if (target == null || target.Is_Dead) continue;
                
                int targetLogX = ToolManager.ChangeToLineX(target.Vec_X);
                int targetLogY = ToolManager.ChangeToLineY(target.Vec_Y);
                
                if (CanAttackFrom(enemy.Type, enemyLogX, enemyLogY, targetLogX, targetLogY))
                {
                    float val = GetPieceValue(target.Type);
                    if (val > maxCapture)
                        maxCapture = val;
                }
            }
        }
        
        // 恢复状态
        movingPiece.Vec_X = origX;
        movingPiece.Vec_Y = origY;
        if (capturedChess != null)
        {
            capturedChess.Is_Dead = capturedWasDead;
        }
        
        // 对手能吃到多少，就扣多少分（乘以0.8的折扣，因为对手不一定吃）
        return maxCapture * 0.8f;
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
    /// 带分数的走法结构
    /// </summary>
    private struct ScoredMove
    {
        public AIMove Move;
        public float Score;
    }
}
