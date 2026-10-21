/* ════════════════════════════════════════════════════════════════════
 *  RoomWaitUIControl.cs  —  房间等待界面控制器 (v2.1)
 * ════════════════════════════════════════════════════════════════════
 *
 *  【v2.1 变更说明（修复三个问题）】
 *      问题①：UpdateRoomInfo 正常工作（确保被调用即可）
 *      问题②：按钮绑定从 Awake 改为 Awake+OnEnable 双保险，新增自愈机制
 *      问题③：OnLeaveRoomCallback 由 RoomUIControl 绑定
 *
 *  【v2 变更说明】
 *      1. 去掉"开始游戏按钮"——改为双方准备+人数满时自动启动倒计时
 *      2. 接入协议层 Ready/StartGame，准备状态通过网络同步
 *
 *  【这个脚本干什么？】
 *      挂在"房间等待面板"上。玩家创建/加入房间成功后进入此面板。
 *      负责显示房间信息、玩家信息、准备状态、倒计时，并自动进入游戏场景。
 *
 *  【UI 结构 (Inspector 拖拽)】
 *      房间等待面板 (GameObject，挂本脚本)
 *        ├─ 房间信息Text   (TMP_Text)  → 显示房间号/房间名/状态
 *        ├─ 房主信息Text   (TMP_Text)  → 显示房主名+准备标记
 *        ├─ 加入者信息Text (TMP_Text)  → 显示对手名+准备标记
 *        ├─ 倒计时Text     (TMP_Text)  → 平时隐藏，触发时显示 3/2/1/开始!
 *        ├─ 提示Text       (TMP_Text)  → 状态提示文字
 *        ├─ 准备按钮       (Button)    → 切换准备/取消准备
 *        └─ 退出房间按钮   (Button)    → 离开房间
 * ════════════════════════════════════════════════════════════════════ */

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SocketGameProtocol;
using UnityEngine.SceneManagement;

public class RoomWaitUIControl : MonoBehaviour
{
    [Header("文本组件")]
    public TMP_Text 房间信息Text;
    public TMP_Text 房主信息Text;
    public TMP_Text 加入者信息Text;
    public TMP_Text 倒计时Text;
    public TMP_Text 提示Text;

    [Header("按钮")]
    public Button 准备按钮;
    public Button 退出房间按钮;

    [Header("场景")]
    public string 游戏场景名 = "ChessGame";

    // ════════════════════════════════════════════════════════════════
    //  回调（由 RoomUIControl.BindWaitPanelCallbacks 绑定）
    // ════════════════════════════════════════════════════════════════
    [System.NonSerialized]
    public System.Action<bool> OnReadyCallback;

    [System.NonSerialized]
    public System.Action OnLeaveRoomCallback;

    // ════════════════════════════════════════════════════════════════
    //  内部状态
    // ════════════════════════════════════════════════════════════════
    private RoomPack currentRoom;
    private bool 本地已准备 = false;
    private bool 是否房主 = false;
    private Coroutine 倒计时协程;
    private bool 按钮已绑定 = false;

    // ════════════════════════════════════════════════════════════════
    //  生命周期
    // ════════════════════════════════════════════════════════════════

    private void Awake()
    {
        Debug.Log("[RoomWaitUIControl] Awake");

        // 检查 Inspector 字段
        if (准备按钮 == null)
            Debug.LogError("[RoomWaitUIControl] Awake: 准备按钮未在 Inspector 中赋值！按钮点击将无响应。");
        if (退出房间按钮 == null)
            Debug.LogError("[RoomWaitUIControl] Awake: 退出房间按钮未在 Inspector 中赋值！");
        if (房间信息Text == null)
            Debug.LogWarning("[RoomWaitUIControl] Awake: 房间信息Text 未赋值");

        BindButtons();

        // 初始隐藏倒计时
        if (倒计时Text != null)
            倒计时Text.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        // ★v2.1：双保险——面板重新激活时确保按钮已绑定
        BindButtons();
    }

    /// <summary>
    /// 绑定按钮事件（防重复）
    /// </summary>
    private void BindButtons()
    {
        if (按钮已绑定) return;

        if (准备按钮 != null)
        {
            准备按钮.onClick.RemoveAllListeners();
            准备按钮.onClick.AddListener(OnReadyButtonClicked);
            Debug.Log("[RoomWaitUIControl] 准备按钮已绑定");
        }

        if (退出房间按钮 != null)
        {
            退出房间按钮.onClick.RemoveAllListeners();
            退出房间按钮.onClick.AddListener(OnLeaveButtonClicked);
            Debug.Log("[RoomWaitUIControl] 退出房间按钮已绑定");
        }

        按钮已绑定 = true;
    }

    // ════════════════════════════════════════════════════════════════
    //  按钮回调
    // ════════════════════════════════════════════════════════════════

    private void OnReadyButtonClicked()
    {
        Debug.Log("[RoomWaitUIControl] === 准备按钮被点击 ===");

        本地已准备 = !本地已准备;
        Debug.Log($"[RoomWaitUIControl] 点击准备: {(本地已准备 ? "准备" : "取消准备")}");

        UpdateReadyButtonLabel();

        // ★v2.1：自愈机制——如果 OnReadyCallback 为 null，尝试重新绑定
        if (OnReadyCallback == null)
        {
            Debug.LogWarning("[RoomWaitUIControl] OnReadyCallback 为 null！尝试自愈...");
            TryRebindCallbacks();
        }

        if (OnReadyCallback != null)
        {
            OnReadyCallback.Invoke(本地已准备);
            Debug.Log($"[RoomWaitUIControl] 已调用 OnReadyCallback({本地已准备})");
        }
        else
        {
            Debug.LogError("[RoomWaitUIControl] OnReadyCallback 仍为 null！准备请求未发送。请检查 RoomUIControl 是否已调用 Init()。");
        }
    }

    private void OnLeaveButtonClicked()
    {
        Debug.Log("[RoomWaitUIControl] === 退出房间按钮被点击 ===");

        if (OnLeaveRoomCallback == null)
        {
            Debug.LogWarning("[RoomWaitUIControl] OnLeaveRoomCallback 为 null！尝试自愈...");
            TryRebindCallbacks();
        }

        if (OnLeaveRoomCallback != null)
        {
            OnLeaveRoomCallback.Invoke();
        }
        else
        {
            Debug.LogError("[RoomWaitUIControl] OnLeaveRoomCallback 仍为 null！退出房间请求未发送。");
        }
    }

    /// <summary>
    /// 自愈：尝试通过 FindObjectOfType 找到 RoomUIControl 并重新绑定回调
    /// </summary>
    private void TryRebindCallbacks()
    {
        RoomUIControl roomUI = FindObjectOfType<RoomUIControl>();
        if (roomUI != null)
        {
            Debug.Log("[RoomWaitUIControl] 自愈: 找到 RoomUIControl，调用 RebindWaitPanelCallbacks");
            roomUI.RebindWaitPanelCallbacks();
        }
        else
        {
            Debug.LogError("[RoomWaitUIControl] 自愈失败: 场景中未找到 RoomUIControl！");
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  更新面板显示
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 更新房间信息显示（由 RoomUIControl 调用）
    /// </summary>
    public void UpdateRoomInfo(RoomPack room)
    {
        if (room == null)
        {
            Debug.LogWarning("[RoomWaitUIControl] UpdateRoomInfo: room 为 null");
            return;
        }

        currentRoom = room;
        Debug.Log($"[RoomWaitUIControl] UpdateRoomInfo: RoomId={room.RoomId}, RoomName={room.RoomName}, Host={room.HostName}, Guest={room.GuestName}, HostReady={room.HostReady}, GuestReady={room.GuestReady}");

        // 判断本地是否房主
        // ★修复：用 HostName 比对 CurrentAccount（原来的 HostId==CurrentUserId 永远不相等）
        是否房主 = (room.HostName == LoginUIControl.CurrentAccount);
        Debug.Log($"[RoomWaitUIControl] 本地是否房主: {是否房主} (HostName={room.HostName}, MyAccount={LoginUIControl.CurrentAccount})");

        // 更新房间信息文本
        if (房间信息Text != null)
        {
            房间信息Text.text = $"房间号: {room.RoomId}\n房间名: {room.RoomName}\n状态: {GetStatusText(room.Status)}";
        }

        // 更新房主信息
        if (房主信息Text != null)
        {
            // ★修复：本地玩家用本地状态（即时反馈），对方用服务端状态
            bool hostReady = 是否房主 ? 本地已准备 : room.HostReady;
            string readyMark = hostReady ? " ✓已准备" : " 未准备";
            房主信息Text.text = $"房主: {room.HostName}{readyMark}";
        }

        // 更新加入者信息
        if (加入者信息Text != null)
        {
            if (!string.IsNullOrEmpty(room.GuestName))
            {
                bool guestReady = 是否房主 ? room.GuestReady : 本地已准备;
                string readyMark = guestReady ? " ✓已准备" : " 未准备";
                加入者信息Text.text = $"对手: {room.GuestName}{readyMark}";
            }
            else
            {
                加入者信息Text.text = "对手: 等待加入...";
            }
        }

        // ★修复：不从服务端同步本地准备状态，避免竞态条件
        //   本地准备状态由玩家点击"准备"按钮直接管理，服务端只用于显示对方状态
        //   （之前的 sync 代码会在 JoinRoom 通知到达时用旧的服务端 HostReady 覆盖本地状态）

        // 检查自动开始
        CheckAutoStart(room);
    }

    /// <summary>
    /// ★新增：房间已不存在（典型场景：服务器重启后内存房间丢失）。
    /// 显示提示文字、禁用准备按钮、清掉本地房间状态。
    /// 面板关闭由 RoomUIControl 的兜底协程统一处理。
    /// </summary>
    public void ShowRoomGoneAndExit(string reason)
    {
        Debug.Log($"[RoomWaitUIControl] 房间已不存在: {reason}");

        // 停掉倒计时等本地协程
        if (倒计时协程 != null)
        {
            StopCoroutine(倒计时协程);
            倒计时协程 = null;
        }

        本地已准备 = false;
        currentRoom = null;

        if (提示Text != null)
        {
            提示Text.text = reason;
        }

        if (准备按钮 != null)
        {
            准备按钮.interactable = false;   // 防止继续点出更多失败请求
        }
    }

    /// <summary>
    /// 重置等待面板状态（退出房间时调用）
    /// </summary>
    public void ResetState()
    {
        Debug.Log("[RoomWaitUIControl] ResetState: 重置等待面板状态");

        本地已准备 = false;
        currentRoom = null;

        if (倒计时协程 != null)
        {
            StopCoroutine(倒计时协程);
            倒计时协程 = null;
        }

        if (倒计时Text != null)
        {
            倒计时Text.gameObject.SetActive(false);
            倒计时Text.text = "";
        }

        if (提示Text != null)
        {
            提示Text.text = "";
        }

        UpdateReadyButtonLabel();

        // 恢复按钮可交互
        if (准备按钮 != null)
        {
            准备按钮.interactable = true;
        }
        if (退出房间按钮 != null)
        {
            退出房间按钮.interactable = true;  // ★修复问题3：恢复退出按钮
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  自动开始检测
    // ════════════════════════════════════════════════════════════════

    private void CheckAutoStart(RoomPack room)
    {
        // 双方都在 + 双方都准备 → 开始倒计时
        bool bothPresent = !string.IsNullOrEmpty(room.HostName) && !string.IsNullOrEmpty(room.GuestName);
        bool bothReady = room.HostReady && room.GuestReady;

        if (bothPresent && bothReady)
        {
            if (倒计时协程 == null)
            {
                Debug.Log("[RoomWaitUIControl] 双方已准备，启动倒计时！");
                倒计时协程 = StartCoroutine(CountdownCoroutine());

                // ★修复问题3：倒计时中禁用准备按钮和退出按钮
                if (准备按钮 != null)
                    准备按钮.interactable = false;
                if (退出房间按钮 != null)
                    退出房间按钮.interactable = false;
            }
        }
        else
        {
            // 如果之前在倒计时但有人取消了准备
            if (倒计时协程 != null)
            {
                Debug.Log("[RoomWaitUIControl] 有人取消准备，停止倒计时");
                StopCoroutine(倒计时协程);
                倒计时协程 = null;

                if (倒计时Text != null)
                    倒计时Text.gameObject.SetActive(false);

                if (准备按钮 != null)
                    准备按钮.interactable = true;
                if (退出房间按钮 != null)
                    退出房间按钮.interactable = true;  // ★修复问题3：恢复退出按钮
            }

            // 更新提示
            if (提示Text != null)
            {
                if (!bothPresent)
                    提示Text.text = "等待对手加入...";
                else if (!bothReady)
                    提示Text.text = "等待双方准备...";
            }
        }
    }

    private IEnumerator CountdownCoroutine()
    {
        if (倒计时Text != null)
        {
            倒计时Text.gameObject.SetActive(true);
        }

        int count = 3;
        while (count > 0)
        {
            if (倒计时Text != null)
                倒计时Text.text = count.ToString();
            if (提示Text != null)
                提示Text.text = $"游戏即将开始... {count}";

            Debug.Log($"[RoomWaitUIControl] 倒计时: {count}");
            // ★音效：倒计时滴答
            AudioManager.PlaySfx("countdown");
            yield return new WaitForSecondsRealtime(1f);   // ★改Realtime：不受timeScale影响，防窗口失焦/加载期间timeScale异常冻结倒计时
            count--;
        }

        if (倒计时Text != null)
            倒计时Text.text = "开始!";
        if (提示Text != null)
            提示Text.text = "加载中...";
        // ★音效：开始提示（高音）
        AudioManager.PlaySfx("countdown_last");

        Debug.Log("[RoomWaitUIControl] 倒计时结束，开始加载游戏场景");
        yield return new WaitForSecondsRealtime(0.5f);

        // ★修复：用 static 替代 PlayerPrefs，防止双开 .exe 共享注册表导致颜色覆盖
        //   PlayerPrefs 在 Windows 上存注册表，两个 .exe 共享 → 后写的覆盖先写的
        GameFace.MyColor = IsLocalHost() ? "Red" : "Black";
        GameFace.OpponentName = IsLocalHost() ? (currentRoom?.GuestName ?? "") : (currentRoom?.HostName ?? "");
        // GameMode 和 RoomId 保留 PlayerPrefs（不影响双开）
        PlayerPrefs.SetString("GameMode", "Network");
        PlayerPrefs.SetInt("RoomId", currentRoom != null ? currentRoom.RoomId : -1);
        PlayerPrefs.Save();
        Debug.Log($"[RoomWaitUIControl] 已设置: MyColor={GameFace.MyColor}, Opponent={GameFace.OpponentName}");

        // 加载游戏场景（改为调用 SceneLoader 统一处理加载动画）
        if (!string.IsNullOrEmpty(游戏场景名))
        {
            SceneLoader.LoadScene(游戏场景名);
        }
        else
        {
            Debug.LogError("[RoomWaitUIControl] 游戏场景名为空! 请在 Inspector 设置。");
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  辅助方法
    // ════════════════════════════════════════════════════════════════

    private void UpdateReadyButtonLabel()
    {
        if (准备按钮 == null) return;
        TMP_Text label = 准备按钮.GetComponentInChildren<TMP_Text>();
        if (label != null)
        {
            label.text = 本地已准备 ? "取消准备" : "准备";
        }
    }

    /// <summary>★新增：判断本地玩家是否是房主（红方）</summary>
    private bool IsLocalHost()
    {
        if (currentRoom != null && LoginUIControl.IsLoggedIn)
        {
            // ★修复：用 HostName 比对 CurrentAccount
            return currentRoom.HostName == LoginUIControl.CurrentAccount;
        }
        return false;
    }

    private string GetStatusText(int status)
    {
        switch (status)
        {
            case 0: return "等待中";
            case 1: return "游戏中";
            case 2: return "已结束";
            default: return "未知";
        }
    }

    private void OnDestroy()
    {
        if (倒计时协程 != null)
        {
            StopCoroutine(倒计时协程);
            倒计时协程 = null;
        }
    }
}
