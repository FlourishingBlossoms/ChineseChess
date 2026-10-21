/* ════════════════════════════════════════════════════════════════════
 *  回放控制UI.cs  —  回放场景的控制面板（★调试增强版）
 * ════════════════════════════════════════════════════════════════════
 *
 *  【这个脚本干什么？】
 *      1. 场景加载后读 PlayerPrefs 里的 gameId
 *      2. 通过 GameRecordRequest 向服务端查走棋详情
 *      3. 收到详情后调 选择象棋.InitReplayBoard() 重置棋盘
 *      4. 提供 上一步/下一步/返回 按钮控制回放
 *      5. ★自动播放：暂停/播放切换，倍速 0.25x / 1x / 1.5x / 2x 循环切换
 *
 *  【Inspector 需要拖拽的字段】
 *      recordRequest (GameRecordRequest): 对局记录请求脚本
 *      回放棋盘 (选择象棋):               棋盘脚本引用
 *      上一步按钮 (Button):
 *      下一步按钮 (Button):
 *      返回按钮 (Button):
 *      播放暂停按钮 (Button):             ★自动播放开关
 *      速度按钮 (Button):                 ★倍速切换
 *      步骤信息 (TMP_Text):             显示 "XXX vs YYY  步骤 5/30"
 *      速度显示 (TMP_Text):             ★显示当前倍速
 *      回放控制面板 (GameObject):         整个控制面板（收到详情前隐藏）
 *
 *  【场景跳转】
 *      返回 → SceneLoader.LoadScene("历史记录")
 *
 *  【挂在哪？】
 *      回放场景的 Canvas 下的控制面板物体上
 * ════════════════════════════════════════════════════════════════════ */

using SocketGameProtocol;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class 回放控制UI : MonoBehaviour
{
    [Header("网络请求")]
    public GameRecordRequest recordRequest;

    [Header("棋盘引用")]
    public 选择象棋 回放棋盘;

    [Header("控制按钮")]
    public Button 上一步按钮;
    public Button 下一步按钮;
    public Button 返回按钮;

    [Header("自动播放 ★")]
    public Button 播放暂停按钮;
    public Button 速度按钮;

    [Header("显示")]
    public TMPro.TMP_Text 步骤信息;
    public TMPro.TMP_Text 速度显示;        // ★倍速文字
    public GameObject 回放控制面板;

    [Header("场景配置")]
    public string 返回场景名 = "历史记录";

    // 回放数据
    private List<GameRecordRequest.MoveData> replayMoves;
    private int currentStep = 0;
    private string replayHostName = "";
    private string replayGuestName = "";

    // ★自动播放状态
    private bool isPlaying = false;          // 是否自动播放中
    private Coroutine playCoroutine = null;

    // ★倍速配置：正常 = 1秒2步 → 每步0.5秒
    // 速度档位：0.25x, 1x, 1.5x, 2x
    private readonly float[] speedOptions = { 0.25f, 1f, 1.5f, 2f };
    private readonly string[] speedLabels   = { "0.25x", "1x", "1.5x", "2x" };
    private int speedIndex = 1;              // 默认 1x
    private float StepInterval => 0.5f / speedOptions[speedIndex];  // 每步间隔(秒)

    // ════════════════════════════════════════════════════════════════
    //  生命周期
    // ════════════════════════════════════════════════════════════════

    private void Awake()
    {
        // ★调试：确认此脚本确实在运行
        Debug.Log("[回放控制UI] ========== Awake 执行 ==========");
        Debug.Log($"[回放控制UI] GameObject: {gameObject.name}, active: {gameObject.activeInHierarchy}");
    }

    private void Start()
    {
        Debug.Log("[回放控制UI] ========== Start 执行 ==========");

        // 绑定按钮
        if (上一步按钮 != null) 上一步按钮.onClick.AddListener(StepBackward);
        if (下一步按钮 != null) 下一步按钮.onClick.AddListener(StepForward);
        if (返回按钮 != null) 返回按钮.onClick.AddListener(OnReturnClicked);

        // ★自动播放按钮
        if (播放暂停按钮 != null) 播放暂停按钮.onClick.AddListener(TogglePlayPause);
        if (速度按钮 != null) 速度按钮.onClick.AddListener(CycleSpeed);

        // 默认隐藏控制面板（等收到详情再显示）
        if (回放控制面板 != null)
        {
            回放控制面板.SetActive(false);
            Debug.Log("[回放控制UI] 回放控制面板已隐藏（等待详情）");
        }
        else
        {
            Debug.LogWarning("[回放控制UI] 回放控制面板 字段未赋值！面板将无法显示。");
        }

        // ★初始速度显示
        UpdateSpeedDisplay();

        // ★调试：检查所有关键引用
        Debug.Log($"[回放控制UI] 引用检查 → recordRequest={(recordRequest != null ? "OK" : "NULL!")}, " +
                  $"回放棋盘={(回放棋盘 != null ? "OK" : "NULL!")}, " +
                  $"步骤信息={(步骤信息 != null ? "OK" : "NULL")}");

        // 接线回调
        if (recordRequest != null)
        {
            recordRequest.OnDetailResponse = OnDetailReceived;
            Debug.Log("[回放控制UI] 已注册 OnDetailResponse 回调");
        }
        else
        {
            Debug.LogError("[回放控制UI] ★★★ recordRequest 未赋值！请在 Inspector 拖入 GameRecordRequest 脚本。" +
                          " 回放场景需要一个空 GameObject 挂载 GameRecordRequest 脚本，然后拖到此处。");
            return;
        }

        // 读取 gameId
        if (!PlayerPrefs.HasKey("ReplayGameId"))
        {
            Debug.LogError("[回放控制UI] ★★★ PlayerPrefs 里没有 ReplayGameId！" +
                          " 是否从历史记录场景点击记录进入的？" +
                          " 如果是编辑器直接运行回放场景，需要先手动设置 PlayerPrefs。");
            return;
        }

        int gameId = PlayerPrefs.GetInt("ReplayGameId");
        PlayerPrefs.DeleteKey("ReplayGameId"); // 消费掉，防止重复触发
        Debug.Log($"[回放控制UI] 从 PlayerPrefs 读取 gameId={gameId}，已消费（DeleteKey）");

        // 检查 GameFace 是否存在（DontDestroyOnLoad 场景下应该有）
        if (GameFace.instance == null)
        {
            Debug.LogError("[回放控制UI] ★★★ GameFace.instance 为空！" +
                          " 回放场景需要 GameFace（DontDestroyOnLoad 从上一个场景带过来）。" +
                          " 如果直接运行回放场景，请先运行开始界面。");
            return;
        }

        Debug.Log("[回放控制UI] GameFace.instance 存在，准备发送查询详情请求...");

        // 查询走棋详情
        recordRequest.QueryDetail(gameId);
        Debug.Log("[回放控制UI] QueryDetail 已调用，等待服务端响应...");
    }

    private void OnDestroy()
    {
        // 确保协程不泄漏
        if (playCoroutine != null)
        {
            StopCoroutine(playCoroutine);
            playCoroutine = null;
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  详情响应 → 初始化棋盘 + 显示控制面板
    // ════════════════════════════════════════════════════════════════

    private void OnDetailReceived(GameRecordRequest.GameRecordDetail detail)
    {
        Debug.Log("[回放控制UI] ========== OnDetailReceived 回调触发 ==========");

        if (detail == null)
        {
            Debug.LogError("[回放控制UI] ★★★ detail 为 null！服务端可能返回了空数据。");
            步骤信息?.SetText("查询详情失败：返回数据为空");
            return;
        }

        Debug.Log($"[回放控制UI] detail.HostName={detail.HostName}, GuestName={detail.GuestName}, " +
                  $"Winner={detail.Winner}, Moves.Count={(detail.Moves != null ? detail.Moves.Count : -1)}");

        if (detail.Moves == null || detail.Moves.Count == 0)
        {
            Debug.LogWarning("[回放控制UI] ★ 走棋详情为空（Moves 为空列表或 null）");
            步骤信息?.SetText("该对局没有走棋记录");
            return;
        }

        // 打印前3步走棋，方便确认数据正确
        for (int i = 0; i < Mathf.Min(3, detail.Moves.Count); i++)
        {
            var m = detail.Moves[i];
            Debug.Log($"[回放控制UI]   步骤[{i}]: ({m.FromX},{m.FromY})→({m.ToX},{m.ToY}) " +
                      $"chessId={m.ChessId} capture={m.CaptureId} color={m.PlayerColor}");
        }

        replayMoves = detail.Moves;
        replayHostName = detail.HostName;
        replayGuestName = detail.GuestName;
        currentStep = 0;

        // 重置棋盘到开局
        if (回放棋盘 != null)
        {
            Debug.Log("[回放控制UI] 调用 回放棋盘.InitReplayBoard()...");
            回放棋盘.InitReplayBoard();
            Debug.Log("[回放控制UI] InitReplayBoard 完成");
        }
        else
        {
            Debug.LogError("[回放控制UI] ★★★ 回放棋盘(选择象棋) 未赋值！无法重置棋盘。");
        }

        // 显示控制面板
        if (回放控制面板 != null)
        {
            回放控制面板.SetActive(true);
            Debug.Log("[回放控制UI] 回放控制面板已显示");
        }

        UpdateReplayInfo();
        Debug.Log($"[回放控制UI] ★ 回放就绪: {replayHostName} vs {replayGuestName}, 共 {replayMoves.Count} 步");
    }

    // ════════════════════════════════════════════════════════════════
    //  下一步
    // ════════════════════════════════════════════════════════════════

    private void StepForward()
    {
        if (replayMoves == null || currentStep >= replayMoves.Count) return;

        var move = replayMoves[currentStep];

        if (回放棋盘 != null)
        {
            回放棋盘.ReplayMove(move.FromX, move.FromY, move.ToX, move.ToY,
                                move.ChessId, move.CaptureId);
        }

        currentStep++;
        UpdateReplayInfo();

        // ★手动走到末尾时自动停止播放
        if (currentStep >= replayMoves.Count && isPlaying)
        {
            StopPlay();
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  上一步
    // ════════════════════════════════════════════════════════════════

    private void StepBackward()
    {
        if (replayMoves == null || currentStep <= 0) return;

        // ★手动上一步时停止自动播放
        if (isPlaying) StopPlay();

        currentStep--;

        if (回放棋盘 != null)
        {
            回放棋盘.ReplayToStep(replayMoves, currentStep);
        }

        UpdateReplayInfo();
    }

    // ════════════════════════════════════════════════════════════════
    //  ★ 自动播放：暂停/播放切换
    // ════════════════════════════════════════════════════════════════

    private void TogglePlayPause()
    {
        if (replayMoves == null || replayMoves.Count == 0) return;

        if (isPlaying)
        {
            StopPlay();
        }
        else
        {
            StartPlay();
        }
    }

    private void StartPlay()
    {
        // 已到末尾 → 从头开始
        if (currentStep >= replayMoves.Count)
        {
            currentStep = 0;
            if (回放棋盘 != null) 回放棋盘.InitReplayBoard();
            UpdateReplayInfo();
        }

        isPlaying = true;
        UpdatePlayButtonLabel();
        playCoroutine = StartCoroutine(AutoPlayLoop());
    }

    private void StopPlay()
    {
        isPlaying = false;
        if (playCoroutine != null)
        {
            StopCoroutine(playCoroutine);
            playCoroutine = null;
        }
        UpdatePlayButtonLabel();
    }

    /// <summary>
    /// 自动播放协程：按当前倍速间隔不断执行 StepForward
    /// </summary>
    private IEnumerator AutoPlayLoop()
    {
        while (isPlaying && replayMoves != null && currentStep < replayMoves.Count)
        {
            StepForward();
            yield return new WaitForSeconds(StepInterval);
        }

        // 播放完毕自动停止
        if (currentStep >= replayMoves.Count)
        {
            StopPlay();
        }
    }

    /// <summary>
    /// 更新播放/暂停按钮文字
    /// </summary>
    private void UpdatePlayButtonLabel()
    {
        if (播放暂停按钮 != null)
        {
            var label = 播放暂停按钮.GetComponentInChildren<TMPro.TMP_Text>();
            if (label != null)
            {
                label.text = isPlaying ? "暂停" : "播放";
            }
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  ★ 倍速切换：0.25x → 1x → 1.5x → 2x → 0.25x ...
    // ════════════════════════════════════════════════════════════════

    private void CycleSpeed()
    {
        speedIndex = (speedIndex + 1) % speedOptions.Length;
        UpdateSpeedDisplay();
        Debug.Log($"[回放控制UI] 速度切换 → {speedLabels[speedIndex]} (每步 {StepInterval:F3}s)");
    }

    private void UpdateSpeedDisplay()
    {
        if (速度显示 != null)
        {
            速度显示.text = speedLabels[speedIndex];
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  更新步骤信息文字
    // ════════════════════════════════════════════════════════════════

    private void UpdateReplayInfo()
    {
        if (步骤信息 != null && replayMoves != null)
        {
            步骤信息.text = $"{replayHostName} vs {replayGuestName}\n步骤 {currentStep}/{replayMoves.Count}";
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  ★ ESC 键 → 返回历史记录场景
    //  （回放场景里不应该有 死亡检查 脚本，ESC 只由此脚本处理）
    // ════════════════════════════════════════════════════════════════

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            OnReturnClicked();
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  返回按钮
    // ════════════════════════════════════════════════════════════════

    private void OnReturnClicked()
    {
        // 离开前停止播放
        if (isPlaying) StopPlay();
        SceneLoader.LoadScene(返回场景名);
    }
}
