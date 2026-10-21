using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AI管理器 - 负责根据难度选择不同的AI策略
/// 这是AI系统的入口点，所有AI决策都通过这个类来调度
/// </summary>
public class AIManager : MonoBehaviour
{
    public static AIManager Instance { get; private set; }
    
    // 当前难度设置
    private string currentDifficulty;
    
    // 四个不同难度的AI实例
    private EasyAI easyAI;
    private NormalAI normalAI;
    private HardAI hardAI;
    private LunaticAI lunaticAI;
    
    // AI思考状态
    public bool IsThinking { get; private set; }
    
    private void Awake()
    {
        // 单例模式：确保整个游戏只有一个AIManager实例
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        // 从PlayerPrefs获取当前难度设置
        currentDifficulty = 游戏界面UI.Difficulty;
        
        // 初始化四个难度的AI
        InitializeAIs();
    }
    
    /// <summary>
    /// 初始化所有难度的AI实例
    /// 每个AI都有不同的决策算法和策略
    /// </summary>
    private void InitializeAIs()
    {
        // Easy: 简单AI - 随机选择合法走法
        easyAI = new EasyAI();
        
        // Normal: 普通AI - 使用评估函数进行浅层搜索
        normalAI = new NormalAI();
        
        // Hard: 困难AI - 使用神经网络 + Minimax算法
        hardAI = new HardAI();
        
        // Lunatic: 疯狂AI - 接入大语言模型
        lunaticAI = new LunaticAI();
    }
    
    /// <summary>
    /// 获取AI的最佳走法
    /// 这是外部调用的主要接口
    /// </summary>
    /// <returns>包含移动信息的结构体</returns>
    public AIMove GetBestMove()
    {
        IsThinking = true;
        AIMove bestMove = new AIMove();
        
        try
        {
            // 根据当前难度选择对应的AI
            switch (currentDifficulty)
            {
                case "Easy":
                    bestMove = easyAI.GetBestMove();
                    break;
                case "Normal":
                    bestMove = normalAI.GetBestMove();
                    break;
                case "Hard":
                    bestMove = hardAI.GetBestMove();
                    break;
                case "Lunatic":
                    bestMove = lunaticAI.GetBestMove();
                    break;
                default:
                    bestMove = easyAI.GetBestMove();
                    break;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"AI计算出错: {e.Message}");
            // 出错时回退到简单AI
            bestMove = easyAI.GetBestMove();
        }
        
        IsThinking = false;
        return bestMove;
    }
    
    /// <summary>
    /// 更新难度设置
    /// </summary>
    public void SetDifficulty(string difficulty)
    {
        currentDifficulty = difficulty;
        PlayerPrefs.SetString("Difficulty", difficulty);
        PlayerPrefs.Save();
    }
}

/// <summary>
/// AI走法结构体
/// 用于存储一次完整的移动信息
/// </summary>
public struct AIMove
{
    public int FromChessId;      // 起始棋子ID
    public int FromLogX;         // 起始列坐标
    public int FromLogY;         // 起始行坐标
    public int ToLogX;           // 目标列坐标
    public int ToLogY;           // 目标行坐标
    public int CaptureId;        // 被吃棋子ID（-1表示没有吃子）
    public float Score;          // 这步棋的评分
    
    /// <summary>
    /// 创建一个空的走法
    /// </summary>
    public static AIMove Empty => new AIMove 
    { 
        FromChessId = -1, 
        FromLogX = -1, 
        FromLogY = -1, 
        ToLogX = -1, 
        ToLogY = -1, 
        CaptureId = -1, 
        Score = float.MinValue 
    };
    
    /// <summary>
    /// 判断是否是有效的走法
    /// </summary>
    public bool IsValid => FromChessId >= 0;
}
