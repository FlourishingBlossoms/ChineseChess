using System.Collections.Generic;
using UnityEngine;

public class ChessAI : MonoBehaviour
{
    public static ChessAI Instance;

    [Tooltip("1=简单, 2=一般, 3=困难, 4=极难")]
    public int DifficultyLevel = 2;

    private UNeuronNet_Test brain;

    public struct MoveData
    {
        public int ChessId;
        public int TargetLogX;
        public int TargetLogY;
        public int KillId;
    }

    // 使用数组备份，避免Dictionary的GC问题
    private float[] backupX = new float[32];
    private float[] backupY = new float[32];
    private bool[] backupDead = new bool[32];
    private const float INFINITY = 99999f;

    void Awake()
    {
        Instance = this;
        InitBrain();
    }

    void Start()
    {
        // 读取UI难度
        if (游戏界面UI.Difficulty == "Easy") DifficultyLevel = 1;
        else if (游戏界面UI.Difficulty == "Normal") DifficultyLevel = 2;
        else if (游戏界面UI.Difficulty == "Hard") DifficultyLevel = 3;
        else DifficultyLevel = 4;
    }

    void InitBrain()
    {
        brain = new UNeuronNet_Test();
        UNeuronNet.ConfigData config = new UNeuronNet.ConfigData();
        config.NumInputs = 90;
        config.NumHiddenLayer = 1;
        config.NumNeuronPerHiddenLayer = 20;
        config.NumOutputs = 1;
        brain.Init(config);
    }

    // 核心入口
    public MoveData GetBestMove(bool isRedTurn)
    {
        List<MoveData> allMoves = GetAllLegalMoves(isRedTurn);
        if (allMoves.Count == 0) return new MoveData();

        switch (DifficultyLevel)
        {
            case 1: return GetBestMove_Simple(allMoves);
            case 3: return GetBestMove_Hard(allMoves, isRedTurn);
            case 4: return GetBestMove_Extreme(allMoves, isRedTurn);
            default: return GetBestMove_Normal(allMoves, isRedTurn);
        }
    }

    // 简单难度：随机
    MoveData GetBestMove_Simple(List<MoveData> allMoves)
    {
        List<MoveData> eatMoves = allMoves.FindAll(m => m.KillId != -1);
        if (eatMoves.Count > 0) return eatMoves[Random.Range(0, eatMoves.Count)];
        return allMoves[Random.Range(0, allMoves.Count)];
    }

    // 一般难度：贪心（使用人工评分，避免神经网络随机性）
    MoveData GetBestMove_Normal(List<MoveData> allMoves, bool isRedTurn)
    {
        MoveData bestMove = allMoves[0];
        float bestScore = isRedTurn ? -INFINITY : INFINITY;

        foreach (var move in allMoves)
        {
            // 模拟走棋
            SimulateMove(move, true);

            // 评估（人工评分，快速且准确）
            float score = EvaluateBoard();

            // 撤销走棋
            SimulateMove(move, false);

            // 更新最佳走法
            if (isRedTurn)
            {
                if (score > bestScore) { bestScore = score; bestMove = move; }
            }
            else
            {
                if (score < bestScore) { bestScore = score; bestMove = move; }
            }
        }
        return bestMove;
    }

    // 困难难度：预判一步
    MoveData GetBestMove_Hard(List<MoveData> allMoves, bool isRedTurn)
    {
        MoveData bestMove = allMoves[0];
        float bestScore = isRedTurn ? -INFINITY : INFINITY;

        foreach (var move in allMoves)
        {
            SimulateMove(move, true);
            float score = MinimaxSearch(1, !isRedTurn);
            SimulateMove(move, false);

            if (isRedTurn)
            {
                if (score > bestScore) { bestScore = score; bestMove = move; }
            }
            else
            {
                if (score < bestScore) { bestScore = score; bestMove = move; }
            }
        }
        return bestMove;
    }

    // 极难难度：Alpha-Beta（深度限制为2，防止卡顿）
    MoveData GetBestMove_Extreme(List<MoveData> allMoves, bool isRedTurn)
    {
        MoveData bestMove = allMoves[0];
        float bestScore = isRedTurn ? -INFINITY : INFINITY;

        foreach (var move in allMoves)
        {
            SimulateMove(move, true);
            float score = AlphaBeta(2, -INFINITY, INFINITY, !isRedTurn);
            SimulateMove(move, false);

            if (isRedTurn)
            {
                if (score > bestScore) { bestScore = score; bestMove = move; }
            }
            else
            {
                if (score < bestScore) { bestScore = score; bestMove = move; }
            }
        }
        return bestMove;
    }

    // Minimax递归
    float MinimaxSearch(int depth, bool isMaximizingPlayer)
    {
        if (depth == 0) return EvaluateBoard();
        List<MoveData> moves = GetAllLegalMoves(isMaximizingPlayer);
        if (moves.Count == 0) return EvaluateBoard();

        float bestScore = isMaximizingPlayer ? -INFINITY : INFINITY;
        foreach (var m in moves)
        {
            SimulateMove(m, true);
            float score = MinimaxSearch(depth - 1, !isMaximizingPlayer);
            SimulateMove(m, false);

            if (isMaximizingPlayer) bestScore = Mathf.Max(bestScore, score);
            else bestScore = Mathf.Min(bestScore, score);
        }
        return bestScore;
    }

    // Alpha-Beta递归
    float AlphaBeta(int depth, float alpha, float beta, bool isMaximizingPlayer)
    {
        if (depth == 0) return EvaluateBoard();
        List<MoveData> moves = GetAllLegalMoves(isMaximizingPlayer);
        if (moves.Count == 0) return EvaluateBoard();

        if (isMaximizingPlayer)
        {
            float maxEval = -INFINITY;
            foreach (var m in moves)
            {
                SimulateMove(m, true);
                float eval = AlphaBeta(depth - 1, alpha, beta, false);
                SimulateMove(m, false);

                maxEval = Mathf.Max(maxEval, eval);
                alpha = Mathf.Max(alpha, eval);
                if (beta <= alpha) break;
            }
            return maxEval;
        }
        else
        {
            float minEval = INFINITY;
            foreach (var m in moves)
            {
                SimulateMove(m, true);
                float eval = AlphaBeta(depth - 1, alpha, beta, true);
                SimulateMove(m, false);

                minEval = Mathf.Min(minEval, eval);
                beta = Mathf.Min(beta, eval);
                if (beta <= alpha) break;
            }
            return minEval;
        }
    }

    // 高性能模拟/撤销走棋
    void SimulateMove(MoveData move, bool isDo)
    {
        // 安全检查：防止ID越界
        if (move.ChessId < 0 || move.ChessId >= 32) return;
        ChessManager.Chess chess = ChessManager.ChessArray[move.ChessId];
        if (chess == null) return;

        if (isDo)
        {
            // 备份原始状态
            backupX[move.ChessId] = chess.Vec_X;
            backupY[move.ChessId] = chess.Vec_Y;

            // 模拟移动
            chess.Vec_X = ToolManager.ChangeBackX(move.TargetLogX);
            chess.Vec_Y = ToolManager.ChangeBackY(move.TargetLogY);

            // 处理吃子
            if (move.KillId != -1)
            {
                if (move.KillId >= 0 && move.KillId < 32)
                {
                    var killed = ChessManager.ChessArray[move.KillId];
                    if (killed != null)
                    {
                        backupDead[move.KillId] = killed.Is_Dead;
                        killed.Is_Dead = true;
                    }
                }
            }
        }
        else
        {
            // 恢复原始状态
            chess.Vec_X = backupX[move.ChessId];
            chess.Vec_Y = backupY[move.ChessId];

            // 恢复被吃棋子
            if (move.KillId != -1)
            {
                if (move.KillId >= 0 && move.KillId < 32)
                {
                    var killed = ChessManager.ChessArray[move.KillId];
                    if (killed != null) killed.Is_Dead = backupDead[move.KillId];
                }
            }
        }
    }

    // 人工评分函数（快速且准确）
    float EvaluateBoard()
    {
        float totalScore = 0;
        foreach (var chess in ChessManager.ChessArray)
        {
            if (chess == null || chess.Is_Dead) continue;

            float value = 0;
            switch (chess.Type)
            {
                case ChessManager.Chess.ChessType.车: value = 100; break;
                case ChessManager.Chess.ChessType.马: value = 45; break;
                case ChessManager.Chess.ChessType.炮: value = 45; break;
                case ChessManager.Chess.ChessType.帅: value = 10000; break;
                case ChessManager.Chess.ChessType.士: value = 20; break;
                case ChessManager.Chess.ChessType.象: value = 20; break;
                case ChessManager.Chess.ChessType.卒:
                    value = 10;
                    int y = ToolManager.ChangeToLineY(chess.Vec_Y);
                    if (chess.Is_Red && y > 4) value = 20;
                    if (!chess.Is_Red && y < 5) value = 20;
                    break;
            }

            // 位置分：越靠近敌方腹地越好
            int logY = ToolManager.ChangeToLineY(chess.Vec_Y);
            if (chess.Is_Red)
            {
                value += logY * 0.5f;
                totalScore += value;
            }
            else
            {
                value += (9 - logY) * 0.5f;
                totalScore -= value;
            }
        }
        return totalScore;
    }

    // 获取合法走法
    List<MoveData> GetAllLegalMoves(bool isRedTurn)
    {
        List<MoveData> moves = new List<MoveData>();
        if (ChessManager.ChessArray == null) return moves;

        foreach (var chess in ChessManager.ChessArray)
        {
            if (chess == null || chess.Is_Dead || chess.Is_Red != isRedTurn) continue;

            for (int x = 0; x < 9; x++)
            {
                for (int y = 0; y < 10; y++)
                {
                    int targetId = ToolManager.GetChessId(x, y);
                    if (targetId != -1 && ChessManager.ChessArray[targetId].Is_Red == isRedTurn) continue;

                    float targetVecX = ToolManager.ChangeBackX(x);
                    float targetVecY = ToolManager.ChangeBackY(y);

                    if (CheckMoveRules(chess.Id, targetVecX, targetVecY, targetId))
                    {
                        moves.Add(new MoveData { ChessId = chess.Id, TargetLogX = x, TargetLogY = y, KillId = targetId });
                    }
                }
            }
        }
        return moves;
    }

    // 规则检测包装
    bool CheckMoveRules(int id, float x, float y, int targetId)
    {
        var chess = ChessManager.ChessArray[id];
        int tLogX = ToolManager.ChangeToLineX(x);
        int tLogY = ToolManager.ChangeToLineY(y);

        switch (chess.Type)
        {
            case ChessManager.Chess.ChessType.车: return 规则检测.车(id, tLogY, tLogX, targetId);
            case ChessManager.Chess.ChessType.马: return 规则检测.马(id, tLogY, tLogX, targetId);
            case ChessManager.Chess.ChessType.炮: return 规则检测.炮(id, tLogY, tLogX, targetId);
            case ChessManager.Chess.ChessType.象: return 规则检测.相(id, tLogY, tLogX, targetId);
            case ChessManager.Chess.ChessType.士: return 规则检测.士(id, tLogY, tLogX, targetId);
            case ChessManager.Chess.ChessType.帅: return 规则检测.将(id, tLogY, tLogX, targetId);
            case ChessManager.Chess.ChessType.卒: return 规则检测.兵(id, tLogY, tLogX, targetId);
            default: return false;
        }
    }
}

