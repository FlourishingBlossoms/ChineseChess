using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class 死亡检查 : MonoBehaviour
{
    public GameObject 满身疮痍;
    public GameObject Title0; // 暂停
    public GameObject Title1; // 红胜（黑将死）
    public GameObject Title2; // 黑胜（红帅死）
    public GameObject 死了界面终点;
    public GameObject 死了界面起点;
    public GameObject 黑幕;
    public Button 返回开始界面;
    public Button 重新开始游戏;
    public Button 退出游戏;

    private 选择象棋 选择象棋脚本;
    private bool isPaused = false;

    // ★修复问题5：允许外部触发游戏结束界面（网络模式对手断线/投降时）
    private bool forceGameOver = false;
    private int forcedWinner = 0; // 1=红胜 2=黑胜

    private void Start()
    {
        黑幕.SetActive(false);
        选择象棋脚本 = FindObjectOfType<选择象棋>();
        if (选择象棋脚本 == null)
        {
            Debug.LogError("找不到对象: 选择象棋脚本");
        }

        if (死了界面起点 != null)
        {
            满身疮痍.transform.position = 死了界面起点.transform.position;
        }

        if (返回开始界面 != null) 返回开始界面.onClick.AddListener(OnReturnToStartMenu);
        if (重新开始游戏 != null) 重新开始游戏.onClick.AddListener(OnRestartGame);
        if (退出游戏 != null) 退出游戏.onClick.AddListener(OnQuitGame);

        // ★音效：结算界面按钮点击音
        if (返回开始界面 != null) 返回开始界面.onClick.AddListener(() => AudioManager.PlaySfx("click"));
        if (重新开始游戏 != null) 重新开始游戏.onClick.AddListener(() => AudioManager.PlaySfx("click"));
        if (退出游戏 != null) 退出游戏.onClick.AddListener(() => AudioManager.PlaySfx("click"));
    }

    private void Update()
    {
        if (选择象棋脚本 == null) return;

        // ================= 修改点1：改为遍历查找将/帅 =================
        // 不再依赖硬编码的ID(4和20)，而是通过棋子类型查找，更加安全
        bool isRedDead = false;
        bool isBlackDead = false;

        foreach (var chess in ChessManager.ChessArray)
        {
            if (chess != null && chess.Type == ChessManager.Chess.ChessType.帅)
            {
                if (chess.Is_Red)
                    isRedDead = chess.Is_Dead; // 红帅死 -> 黑胜
                else
                    isBlackDead = chess.Is_Dead; // 黑将死 -> 红胜
            }
        }

        // 暂停逻辑保持不变
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
            {
                isPaused = false;
                //Time.timeScale = 1f; 
                黑幕.SetActive(false);
                选择象棋脚本.enabled = true;
            }
            else if (!isRedDead && !isBlackDead)
            {
                isPaused = true;
                //Time.timeScale = 0f; 
                黑幕.SetActive(true);
                选择象棋脚本.enabled = false;
            }
        }

        // ================= 修改点2：核心移动与显示逻辑 =================
        // 获取动画播放状态：如果 AllowMove 为 true，说明棋子正在飞过去，还没落地
        bool isPlayingAnimation = 选择象棋脚本.AllowMove;

        // ★修复问题5：forceGameOver 也触发结算界面（网络模式对手断线/投降）
        bool shouldShowResult = isPaused ||
            ((isRedDead || isBlackDead) && !isPlayingAnimation) ||
            (forceGameOver && !isPlayingAnimation);

        if (shouldShowResult)
        {
            if (!满身疮痍.activeSelf) 满身疮痍.SetActive(true);

            if (forceGameOver && !isRedDead && !isBlackDead)
            {
                // ★网络模式：将帅没死但收到游戏结束通知
                Title0.SetActive(false);
                Title1.SetActive(forcedWinner == 1); // 红胜
                Title2.SetActive(forcedWinner == 2); // 黑胜
            }
            else if (isRedDead)
            {
                Title0.SetActive(false);
                Title1.SetActive(false);
                Title2.SetActive(true); // 红死 -> 黑胜
            }
            else if (isBlackDead)
            {
                Title0.SetActive(false);
                Title1.SetActive(true); // 黑死 -> 红胜
                Title2.SetActive(false);
            }
            else if (isPaused)
            {
                Title0.SetActive(true); // 暂停
                Title1.SetActive(false);
                Title2.SetActive(false);
            }

            满身疮痍.transform.position = Vector3.Lerp(满身疮痍.transform.position, 死了界面终点.transform.position, Time.unscaledDeltaTime * 5f);
        }
        else
        {
            // 正常游戏状态
            if (!满身疮痍.activeSelf) 满身疮痍.SetActive(true);

            // 隐藏所有标题
            Title0.SetActive(false);
            Title1.SetActive(false);
            Title2.SetActive(false);

            满身疮痍.transform.position = Vector3.Lerp(满身疮痍.transform.position, 死了界面起点.transform.position, Time.unscaledDeltaTime * 5f);
        }

        // ================= 修改点3：死亡锁定 =================
        // 同样需要等待动画播放完毕，才能禁用脚本，否则动画会被卡住
        if (((isRedDead || isBlackDead) && !isPlayingAnimation) || (forceGameOver && !isPlayingAnimation))
        {
            黑幕.gameObject.SetActive(true);
            选择象棋脚本.enabled = false;
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  ★修复问题5：外部调用 —— 强制显示游戏结束界面
    //  供 选择象棋.OnGameOverInternal() 调用
    //  winner: 1=红胜 2=黑胜
    // ════════════════════════════════════════════════════════════════
    public void ShowResult(int winner)
    {
        forceGameOver = true;
        forcedWinner = winner;
        Debug.Log($"[死亡检查] ShowResult: winner={winner}");
        // ★音效：胜利/失败（AI与本地双人模式，红胜=win，黑胜=lose）
        AudioManager.PlaySfx(winner == 1 ? "win" : "lose");
    }

    // ================= 按钮执行函数 =================
    public void OnReturnToStartMenu()
    {
        Time.timeScale = 1f;
        SceneLoader.LoadScene("开始界面");
    }

    public void OnRestartGame()
    {
        Time.timeScale = 1f;
        SceneLoader.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void OnQuitGame()
    {
        Application.Quit();
    }
}
