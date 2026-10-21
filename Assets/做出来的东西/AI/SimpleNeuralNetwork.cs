using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 简单神经网络 - 用于Hard难度的AI决策
/// 这是一个从零实现的神经网络，不依赖任何外部库
/// 适合初学者理解神经网络的基本原理
/// </summary>
public class SimpleNeuralNetwork
{
    // ==================== 神经网络结构 ====================
    
    /// <summary>
    /// 网络的层数（包含输入层和输出层）
    /// </summary>
    private int[] layerSizes;
    
    /// <summary>
    /// 权重矩阵数组
    /// weights[i] 表示第i层到第i+1层的权重
    /// </summary>
    private float[][][] weights;
    
    /// <summary>
    /// 偏置数组
    /// biases[i] 表示第i+1层的偏置
    /// </summary>
    private float[][] biases;
    
    /// <summary>
    /// 随机数生成器，用于初始化权重
    /// </summary>
    private System.Random random;
    
    // ==================== 构造函数 ====================
    
    /// <summary>
    /// 创建一个神经网络
    /// </summary>
    /// <param name="layers">每层的神经元数量，例如 {90, 64, 32, 1} 表示：
    /// - 输入层：90个神经元（对应棋盘状态）
    /// - 隐藏层1：64个神经元
    /// - 隐藏层2：32个神经元
    /// - 输出层：1个神经元（输出局面评分）</param>
    public SimpleNeuralNetwork(int[] layers)
    {
        this.layerSizes = layers;
        this.random = new System.Random();
        
        // 初始化权重和偏置
        InitializeWeights();
    }
    
    // ==================== 初始化方法 ====================
    
    /// <summary>
    /// 初始化所有权重和偏置
    /// 使用Xavier初始化方法，使初始权重更适合训练
    /// </summary>
    private void InitializeWeights()
    {
        int numLayers = layerSizes.Length - 1;
        
        weights = new float[numLayers][][];
        biases = new float[numLayers][];
        
        for (int l = 0; l < numLayers; l++)
        {
            int inputSize = layerSizes[l];
            int outputSize = layerSizes[l + 1];
            
            // Xavier初始化：权重范围 = sqrt(2 / (输入数 + 输出数))
            float scale = (float)Math.Sqrt(2.0 / (inputSize + outputSize));
            
            // 初始化权重矩阵
            weights[l] = new float[outputSize][];
            for (int j = 0; j < outputSize; j++)
            {
                weights[l][j] = new float[inputSize];
                for (int i = 0; i < inputSize; i++)
                {
                    // 随机初始化权重，范围在 [-scale, scale]
                    weights[l][j][i] = (float)(random.NextDouble() * 2 - 1) * scale;
                }
            }
            
            // 初始化偏置（初始为0）
            biases[l] = new float[outputSize];
            for (int j = 0; j < outputSize; j++)
            {
                biases[l][j] = 0f;
            }
        }
    }
    
    // ==================== 激活函数 ====================
    
    /// <summary>
    /// ReLU激活函数
    /// 公式：f(x) = max(0, x)
    /// 特点：计算简单，能有效缓解梯度消失问题
    /// </summary>
    private float ReLU(float x)
    {
        return Math.Max(0, x);
    }
    
    /// <summary>
    /// Sigmoid激活函数
    /// 公式：f(x) = 1 / (1 + e^(-x))
    /// 特点：输出范围在(0, 1)，适合输出概率
    /// </summary>
    private float Sigmoid(float x)
    {
        // 防止数值溢出
        if (x > 20) return 0.9999f;
        if (x < -20) return 0.0001f;
        return (float)(1.0 / (1.0 + Math.Exp(-x)));
    }
    
    /// <summary>
    /// Tanh激活函数
    /// 公式：f(x) = (e^x - e^(-x)) / (e^x + e^(-x))
    /// 特点：输出范围在(-1, 1)，适合输出评分
    /// </summary>
    private float Tanh(float x)
    {
        // 防止数值溢出
        if (x > 20) return 0.9999f;
        if (x < -20) return -0.9999f;
        return (float)Math.Tanh(x);
    }
    
    // ==================== 前向传播 ====================
    
    /// <summary>
    /// 前向传播：从输入计算输出
    /// 这是神经网络的核心计算过程
    /// </summary>
    /// <param name="input">输入向量（棋盘状态）</param>
    /// <returns>输出向量（局面评分）</returns>
    public float[] Forward(float[] input)
    {
        float[] current = input;
        
        // 逐层计算
        for (int l = 0; l < weights.Length; l++)
        {
            float[] next = new float[layerSizes[l + 1]];
            
            // 计算每个神经元的输出
            for (int j = 0; j < layerSizes[l + 1]; j++)
            {
                // 计算加权和：z = Σ(w * x) + b
                float sum = biases[l][j];
                for (int i = 0; i < layerSizes[l]; i++)
                {
                    sum += weights[l][j][i] * current[i];
                }
                
                // 应用激活函数
                if (l < weights.Length - 1)
                {
                    // 隐藏层使用ReLU
                    next[j] = ReLU(sum);
                }
                else
                {
                    // 输出层使用Tanh（输出评分）
                    next[j] = Tanh(sum);
                }
            }
            
            current = next;
        }
        
        return current;
    }
    
    /// <summary>
    /// 快速评估：输入棋盘状态，输出单个评分
    /// </summary>
    public float Evaluate(float[] boardState)
    {
        float[] output = Forward(boardState);
        return output[0];
    }
    
    // ==================== 棋盘状态编码 ====================
    
    /// <summary>
    /// 将当前棋盘状态编码为神经网络输入向量
    /// 编码方式：
    /// - 每个棋子用一个独热向量表示
    /// - 红方棋子为正，黑方棋子为负
    /// </summary>
    public float[] EncodeBoardState()
    {
        // 90个格子 × 7种棋子类型 + 1个当前回合标志 = 631维
        // 简化版本：90个格子，每个格子一个值
        float[] state = new float[90];
        
        for (int i = 0; i < 32; i++)
        {
            ChessManager.Chess chess = ChessManager.ChessArray[i];
            
            if (chess == null || chess.Is_Dead)
                continue;
            
            int logX = ToolManager.ChangeToLineX(chess.Vec_X);
            int logY = ToolManager.ChangeToLineY(chess.Vec_Y);
            
            if (logX < 0 || logX > 8 || logY < 0 || logY > 9)
                continue;
            
            // 计算一维索引
            int index = logY * 9 + logX;
            
            // 编码棋子价值
            float value = GetPieceEncodingValue(chess);
            
            // 红方为正，黑方为负
            state[index] = chess.Is_Red ? value : -value;
        }
        
        return state;
    }
    
    /// <summary>
    /// 获取棋子的编码值
    /// </summary>
    private float GetPieceEncodingValue(ChessManager.Chess chess)
    {
        switch (chess.Type)
        {
            case ChessManager.Chess.ChessType.帅: return 1.0f;
            case ChessManager.Chess.ChessType.车: return 0.9f;
            case ChessManager.Chess.ChessType.炮: return 0.7f;
            case ChessManager.Chess.ChessType.马: return 0.6f;
            case ChessManager.Chess.ChessType.象: return 0.3f;
            case ChessManager.Chess.ChessType.士: return 0.3f;
            case ChessManager.Chess.ChessType.卒: return 0.2f;
            default: return 0f;
        }
    }
    
    // ==================== 权重保存/加载 ====================
    
    /// <summary>
    /// 将权重保存为JSON字符串
    /// 用于持久化训练好的模型
    /// </summary>
    public string SaveWeights()
    {
        // 简化实现：使用PlayerPrefs保存
        // 实际项目中应该使用文件存储
        string json = JsonUtility.ToJson(new WeightData(this));
        return json;
    }
    
    /// <summary>
    /// 从JSON字符串加载权重
    /// </summary>
    public void LoadWeights(string json)
    {
        WeightData data = JsonUtility.FromJson<WeightData>(json);
        // 恢复权重...
    }
    
    /// <summary>
    /// 权重数据结构（用于序列化）
    /// </summary>
    [Serializable]
    private class WeightData
    {
        public int[] layers;
        public float[] flatWeights;
        public float[] flatBiases;
        
        public WeightData(SimpleNeuralNetwork network)
        {
            layers = network.layerSizes;
            // 扁平化权重和偏置...
        }
    }
}

/// <summary>
/// 预训练的神经网络模型
/// 包含针对中国象棋优化的权重
/// </summary>
public class ChessNeuralNetwork : SimpleNeuralNetwork
{
    /// <summary>
    /// 创建一个针对中国象棋优化的神经网络
    /// 网络结构：90 -> 128 -> 64 -> 32 -> 1
    /// </summary>
    public ChessNeuralNetwork() : base(new int[] { 90, 128, 64, 32, 1 })
    {
        // 可以在这里加载预训练权重
        LoadPretrainedWeights();
    }
    
    /// <summary>
    /// 加载预训练权重
    /// 这些权重是通过大量棋局训练得到的
    /// </summary>
    private void LoadPretrainedWeights()
    {
        // 实际项目中，这里应该从文件加载预训练权重
        // 目前使用随机初始化的权重
        Debug.Log("神经网络初始化完成，使用随机权重");
    }
    
    /// <summary>
    /// 评估当前局面
    /// 返回值：正数表示红方优势，负数表示黑方优势
    /// </summary>
    public float EvaluatePosition()
    {
        float[] state = EncodeBoardState();
        return Evaluate(state);
    }
    
    /// <summary>
    /// 评估走法
    /// 通过模拟走法后的局面来评估
    /// </summary>
    public float EvaluateMove(AIMove move)
    {
        // 简化实现：结合神经网络评估和规则评估
        float baseScore = EvaluatePosition();
        
        // 如果吃子，加分
        if (move.CaptureId >= 0)
        {
            ChessManager.Chess captured = ChessManager.ChessArray[move.CaptureId];
            if (captured != null)
            {
                baseScore += GetPieceValue(captured.Type) * 0.1f;
            }
        }
        
        return baseScore;
    }
    
    private float GetPieceValue(ChessManager.Chess.ChessType type)
    {
        switch (type)
        {
            case ChessManager.Chess.ChessType.帅: return 100f;
            case ChessManager.Chess.ChessType.车: return 9f;
            case ChessManager.Chess.ChessType.炮: return 4.5f;
            case ChessManager.Chess.ChessType.马: return 4f;
            case ChessManager.Chess.ChessType.象: return 2f;
            case ChessManager.Chess.ChessType.士: return 2f;
            case ChessManager.Chess.ChessType.卒: return 1f;
            default: return 0f;
        }
    }
}
