/* ════════════════════════════════════════════════════════════════════
 *  RoomUIControl.cs  —  房间系统UI控制器 (v4.3)
 * ════════════════════════════════════════════════════════════════════
 *
 *  【v4.3 变更说明（修复三个问题）】
 *      问题①：加入房间后信息不更新 → EnsureWaitPanelScript() 自愈 + 消除静默跳过
 *      问题②：准备按钮无响应 → RebindWaitPanelCallbacks() 公开方法供自愈调用
 *      问题③：退出房间不回大厅 → OnReturnToLobby 回调，由 开始界面UI 订阅
 *
 *  【v4.2 变更说明】
 *      1. 对接 RoomWaitUIControl —— 创建/加入房间后更新等待面板显示
 *      2. 新增 Ready / StartGame 响应处理
 *      3. 退出房间后重置等待面板状态
 *
 *  【v4 变更说明】
 *      搜索框改为 Inspector 字段拖拽（不再代码动态生成）
 *
 *  【Inspector 需要拖拽的字段】（通过 开始界面UI 传入）
 *      房间搜索框        → 搜索框面板 GameObject
 *      搜索框输入框      → TMP_InputField（ContentType=Integer）
 *      搜索确认按钮      → Button
 *      搜索取消按钮      → Button
 *
 *  【面板行为】
 *      操作面板+列表面板：同时显示（多人模式默认视图）
 *      等待面板：创建/加入成功后替换显示
 *      搜索框：点击"加入房间"弹出，Inspector 拖拽
 * ════════════════════════════════════════════════════════════════════ */

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SocketGameProtocol;

public class RoomUIControl : MonoBehaviour
{
    // ════════════════════════════════════════════════════════════════
    //  内部引用 (由 Init() 设置)
    // ════════════════════════════════════════════════════════════════
    private GameObject 房间操作面板;
    private GameObject 房间列表面板;
    private Transform 房间列表容器;
    private GameObject 房间列表项预制体;
    private GameObject 房间等待面板;
    private TMP_InputField 房间名输入框;

    // ★v4：搜索框引用（由 Init() 传入，Inspector 拖拽）
    private GameObject 房间搜索框;
    private TMP_InputField 搜索框输入框;
    private Button 搜索确认按钮;
    private Button 搜索取消按钮;

    // ★v4.2：等待面板脚本引用
    private RoomWaitUIControl 房间等待面板脚本;

    // ★v4.4：操作提示文字（可选。创建/加入/搜索失败时显示原因；不拖拽则只写日志，不影响运行）
    [Header("操作提示文字（可选）：创建/加入/搜索失败时显示原因")]
    public TMP_Text 操作提示Text;
    private Coroutine 操作提示协程;

    // ★新增：刷新按钮（通过 SetRefreshButton 设置）
    private Button 刷新按钮;

    // ★新增：自动刷新计时器
    private float autoRefreshTimer = 0f;
    private const float AUTO_REFRESH_INTERVAL = 5f;  // 每5秒自动刷新

    // ════════════════════════════════════════════════════════════════
    //  内部状态
    // ════════════════════════════════════════════════════════════════
    private RoomRequest roomRequest;
    private List<GameObject> spawnedItems = new List<GameObject>();
    private RoomPack currentRoom;

    // ★修复问题3/6：区分"我主动退"和"对手退"
    private bool 本地发起离开 = false;

    // ★v4.3：退出房间时触发，由 开始界面UI 订阅
    public Action OnReturnToLobby;

    // ════════════════════════════════════════════════════════════════
    //  Init —— 由 开始界面UI.Start() 调用
    // ════════════════════════════════════════════════════════════════
    public void Init(GameObject operatePanel, GameObject listPanel,
                     Transform listContainer, GameObject listItemPrefab,
                     GameObject waitPanel, TMP_InputField roomNameInput,
                     GameObject searchDialog, TMP_InputField searchInput,
                     Button searchConfirmBtn, Button searchCancelBtn)
    {
        房间操作面板 = operatePanel;
        房间列表面板 = listPanel;
        房间列表容器 = listContainer;
        房间列表项预制体 = listItemPrefab;
        房间等待面板 = waitPanel;
        房间名输入框 = roomNameInput;

        // ★v4：搜索框
        房间搜索框 = searchDialog;
        搜索框输入框 = searchInput;
        搜索确认按钮 = searchConfirmBtn;
        搜索取消按钮 = searchCancelBtn;

        // 创建 RoomRequest 组件并绑定回调
        EnsureRoomRequest();

        // ★v4：绑定搜索框按钮事件
        if (搜索确认按钮 != null)
            搜索确认按钮.onClick.AddListener(OnSearchConfirm);
        if (搜索取消按钮 != null)
            搜索取消按钮.onClick.AddListener(HideSearchDialog);

        // 初始隐藏搜索框
        if (房间搜索框 != null)
            房间搜索框.SetActive(false);

        // ★v4.3：获取等待面板脚本（带自愈）
        EnsureWaitPanelScript();

        // 初始隐藏所有子面板
        ShowPanel(null);

        Debug.Log("[RoomUIControl] 初始化完成");
    }

    // ════════════════════════════════════════════════════════════════
    //  ★新增：Update() — 自动刷新房间列表（每5秒）
    // ════════════════════════════════════════════════════════════════
    private void Update()
    {
        // 只在列表面板可见 + 不在房间内 + 搜索框关闭时自动刷新
        if (房间列表面板 != null && 房间列表面板.activeSelf &&
            currentRoom == null &&
            (房间搜索框 == null || !房间搜索框.activeSelf))
        {
            autoRefreshTimer -= Time.unscaledDeltaTime;
            if (autoRefreshTimer <= 0)
            {
                autoRefreshTimer = AUTO_REFRESH_INTERVAL;
                RefreshRoomList();
            }
        }
        else
        {
            // 不在列表界面时重置计时器
            autoRefreshTimer = AUTO_REFRESH_INTERVAL;
        }
    }

    /// <summary>★新增：手动刷新房间列表（刷新按钮调用）</summary>
    public void RefreshRoomList()
    {
        EnsureRoomRequest();
        if (roomRequest != null)
        {
            roomRequest.SendQueryRoom();
            Debug.Log("[RoomUIControl] 刷新房间列表");
        }
    }

    /// <summary>★新增：设置刷新按钮（由 开始界面UI 调用）</summary>
    public void SetRefreshButton(Button refreshBtn)
    {
        刷新按钮 = refreshBtn;
        if (刷新按钮 != null)
        {
            刷新按钮.onClick.AddListener(RefreshRoomList);
            Debug.Log("[RoomUIControl] 已绑定刷新按钮");
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  ★v4.3：自愈方法
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 确保 roomRequest 存在（防止被 Unity 销毁后引用失效）
    /// </summary>
    private void EnsureRoomRequest()
    {
        if (roomRequest == null)
        {
            roomRequest = gameObject.GetComponent<RoomRequest>();
            if (roomRequest == null)
            {
                roomRequest = gameObject.AddComponent<RoomRequest>();
            }
            roomRequest.OnResponseCallback = OnRoomResponse;
            Debug.Log("[RoomUIControl] EnsureRoomRequest: 已创建/绑定 RoomRequest");
        }
    }

    /// <summary>
    /// 确保等待面板脚本引用有效（自愈机制）
    /// 解决问题①：如果 Inspector 没拖字段或脚本没挂，原来静默跳过
    /// </summary>
    private bool EnsureWaitPanelScript()
    {
        if (房间等待面板脚本 != null) return true;

        if (房间等待面板 != null)
        {
            房间等待面板脚本 = 房间等待面板.GetComponent<RoomWaitUIControl>();
            if (房间等待面板脚本 != null)
            {
                BindWaitPanelCallbacks();
                return true;
            }
            Debug.LogError("[RoomUIControl] EnsureWaitPanelScript: 房间等待面板上未找到 RoomWaitUIControl 组件！请检查 Inspector 中等待面板是否挂了此脚本。");
        }
        else
        {
            Debug.LogError("[RoomUIControl] EnsureWaitPanelScript: 房间等待面板为 null！请检查 开始界面UI 的 Inspector 中 房间等待面板 字段是否已拖拽。");
        }

        // 尝试全局查找（自愈）
        房间等待面板脚本 = FindObjectOfType<RoomWaitUIControl>();
        if (房间等待面板脚本 != null)
        {
            Debug.LogWarning("[RoomUIControl] EnsureWaitPanelScript: 通过 FindObjectOfType 找到 RoomWaitUIControl（自愈成功）");
            if (房间等待面板 == null)
            {
                房间等待面板 = 房间等待面板脚本.gameObject;
            }
            BindWaitPanelCallbacks();
            return true;
        }

        return false;
    }

    /// <summary>
    /// 绑定等待面板的回调（准备、退出房间）
    /// 解决问题②：OnReadyCallback 没绑上导致准备按钮无响应
    /// </summary>
    public void RebindWaitPanelCallbacks()
    {
        if (EnsureWaitPanelScript())
        {
            BindWaitPanelCallbacks();
            Debug.Log("[RoomUIControl] RebindWaitPanelCallbacks: 已重新绑定等待面板回调");
        }
        else
        {
            Debug.LogError("[RoomUIControl] RebindWaitPanelCallbacks: 等待面板脚本不可用，无法绑定回调");
        }
    }

    private void BindWaitPanelCallbacks()
    {
        if (房间等待面板脚本 == null) return;

        // 绑定准备回调
        房间等待面板脚本.OnReadyCallback = (isReady) =>
        {
            Debug.Log($"[RoomUIControl] OnReadyCallback 被调用: isReady={isReady}");
            EnsureRoomRequest();
            if (roomRequest != null)
            {
                roomRequest.SendReady(isReady);
            }
            else
            {
                Debug.LogError("[RoomUIControl] OnReadyCallback: roomRequest 为 null，准备请求未发送！");
            }
        };

        // 绑定退出房间回调
        房间等待面板脚本.OnLeaveRoomCallback = () =>
        {
            Debug.Log("[RoomUIControl] OnLeaveRoomCallback 被调用");
            LeaveRoom();
        };

        Debug.Log("[RoomUIControl] 已绑定 RoomWaitUIControl 回调 (OnReady + OnLeaveRoom)");
    }

    // ════════════════════════════════════════════════════════════════
    //  公开方法
    // ════════════════════════════════════════════════════════════════

    /// <summary>显示操作面板+列表面板，并自动查询房间列表</summary>
    public void ShowOperatePanel()
    {
        ShowPanel(房间操作面板);
        ShowPanel(房间列表面板);
        // 自动查询房间列表
        EnsureRoomRequest();
        if (roomRequest != null)
        {
            roomRequest.SendQueryRoom();
            Debug.Log("[RoomUIControl] 进入多人模式，自动查询房间列表");
        }
    }

    /// <summary>创建房间</summary>
    public void CreateRoom()
    {
        // ★v4.3：登录态校验
        if (!LoginUIControl.IsLoggedIn)
        {
            Debug.LogError("[RoomUIControl] CreateRoom 失败: 用户未登录！请先登录。");
            return;
        }

        string roomName = "";
        if (房间名输入框 != null && !string.IsNullOrEmpty(房间名输入框.text))
        {
            roomName = 房间名输入框.text;
        }
        else
        {
            roomName = LoginUIControl.CurrentAccount + "的房间";
        }

        EnsureRoomRequest();
        if (roomRequest != null)
        {
            roomRequest.SendCreateRoom(roomName);
            Debug.Log("[RoomUIControl] 正在创建房间: " + roomName);
        }
        else
        {
            Debug.LogError("[RoomUIControl] CreateRoom 失败: roomRequest 为 null！");
        }
    }

    /// <summary>离开房间（本地主动触发）</summary>
    public void LeaveRoom()
    {
        本地发起离开 = true;  // ★标记：这是我自己发起的离开
        EnsureRoomRequest();
        if (roomRequest != null)
        {
            roomRequest.SendLeaveRoom();
        }
        currentRoom = null;

        // 重置等待面板状态
        if (房间等待面板脚本 != null)
        {
            房间等待面板脚本.ResetState();
        }

        ShowPanel(房间操作面板);

        // ★v4.3：触发返回大厅回调
        if (OnReturnToLobby != null)
        {
            Debug.Log("[RoomUIControl] 触发 OnReturnToLobby，返回大厅");
            OnReturnToLobby.Invoke();
        }
        else
        {
            Debug.LogWarning("[RoomUIControl] LeaveRoom: OnReturnToLobby 未订阅，无法返回大厅。请确保 开始界面UI 已订阅此回调。");
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  ★v4：搜索框 —— Inspector 拖拽方式
    // ════════════════════════════════════════════════════════════════

    /// <summary>显示搜索框（点击"加入房间"按钮调用）</summary>
    public void ShowSearchDialog()
    {
        if (房间搜索框 == null)
        {
            Debug.LogWarning("[RoomUIControl] 房间搜索框未在 Inspector 中赋值！");
            return;
        }

        房间搜索框.SetActive(true);

        // 清空输入框并聚焦
        if (搜索框输入框 != null)
        {
            搜索框输入框.text = "";
            搜索框输入框.ActivateInputField();
        }

        Debug.Log("[RoomUIControl] 显示搜索框");
    }

    /// <summary>隐藏搜索框</summary>
    public void HideSearchDialog()
    {
        if (房间搜索框 != null)
            房间搜索框.SetActive(false);
        Debug.Log("[RoomUIControl] 隐藏搜索框");
    }

    /// <summary>搜索框确认按钮回调</summary>
    private void OnSearchConfirm()
    {
        if (搜索框输入框 == null)
        {
            Debug.LogWarning("[RoomUIControl] 搜索框输入框未赋值！");
            return;
        }

        if (int.TryParse(搜索框输入框.text, out int roomId))
        {
            Debug.Log($"[RoomUIControl] 搜索房间 ID={roomId}");
            EnsureRoomRequest();
            if (roomRequest != null)
            {
                roomRequest.SendJoinRoom(roomId);
            }
            HideSearchDialog();
        }
        else
        {
            // ★v4.4：非法输入显示到提示文字
            ShowOperationHint("请输入有效的房间ID（纯数字）");
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  服务端响应处理
    // ════════════════════════════════════════════════════════════════
    private void OnRoomResponse(MainPack pack)
    {
        if (pack == null) return;

        switch (pack.Actioncode)
        {
            case ActionCode.CreateRoom: HandleCreateRoomResponse(pack); break;
            case ActionCode.JoinRoom:   HandleJoinRoomResponse(pack);   break;
            case ActionCode.QueryRoom:  HandleQueryRoomResponse(pack);  break;
            case ActionCode.LeaveRoom:  HandleLeaveRoomResponse(pack);  break;
            case ActionCode.Ready:      HandleReadyResponse(pack);      break;
            case ActionCode.StartGame:  HandleStartGameResponse(pack);  break;
            default:
                Debug.LogWarning($"[RoomUIControl] 未处理的 ActionCode: {pack.Actioncode}");
                break;
        }
    }

    private void HandleCreateRoomResponse(MainPack pack)
    {
        if (pack.Returncode == ReturnCode.Success)
        {
            currentRoom = pack.Roompack;
            Debug.Log($"[RoomUIControl] 房间创建成功! ID={currentRoom.RoomId}");

            if (房间等待面板 == null)
            {
                Debug.LogWarning("[RoomUIControl] 房间等待面板未在 Inspector 中赋值！无法显示等待界面");
            }
            ShowPanel(房间等待面板);

            // ★v4.2：更新等待面板显示
            if (EnsureWaitPanelScript())
            {
                房间等待面板脚本.UpdateRoomInfo(currentRoom);
                Debug.Log("[RoomUIControl] 已调用 UpdateRoomInfo 更新等待面板显示");
            }
            else
            {
                Debug.LogError("[RoomUIControl] HandleCreateRoomResponse: 房间等待面板脚本为 null，UpdateRoomInfo 未被调用！请检查: 1) 房间等待面板是否通过 Init() 的 waitPanel 参数传入 2) 等待面板上是否挂了 RoomWaitUIControl 脚本");
            }
        }
        else
        {
            // ★v4.4：失败原因显示到提示文字（原来只写日志，用户不知道为什么失败）
            ShowOperationHint("创建房间失败，请重试");
        }
    }

    /// <summary>★v4.4：在操作面板显示一条提示，3秒后自动清除。未拖拽 提示Text 时只写日志</summary>
    private void ShowOperationHint(string msg)
    {
        Debug.Log($"[RoomUIControl] 提示: {msg}");
        if (操作提示Text == null) return;

        if (操作提示协程 != null) StopCoroutine(操作提示协程);
        操作提示Text.text = msg;
        操作提示Text.gameObject.SetActive(true);
        操作提示协程 = StartCoroutine(ClearHintAfterDelay());
    }

    private System.Collections.IEnumerator ClearHintAfterDelay()
    {
        yield return new WaitForSecondsRealtime(3f);
        if (操作提示Text != null) 操作提示Text.text = "";
        操作提示协程 = null;
    }

    private void HandleJoinRoomResponse(MainPack pack)
    {
        if (pack.Returncode == ReturnCode.Success)
        {
            currentRoom = pack.Roompack;
            Debug.Log($"[RoomUIControl] 加入房间成功! RoomId={currentRoom.RoomId}");
            // ★音效：对手加入
            AudioManager.PlaySfx("join");
            ShowPanel(房间等待面板);

            // ★v4.2：更新等待面板显示
            if (EnsureWaitPanelScript())
            {
                房间等待面板脚本.UpdateRoomInfo(currentRoom);
                Debug.Log("[RoomUIControl] 已调用 UpdateRoomInfo 更新等待面板显示");
            }
            else
            {
                Debug.LogError("[RoomUIControl] HandleJoinRoomResponse: 房间等待面板脚本为 null，UpdateRoomInfo 未被调用！请检查: 1) 房间等待面板是否通过 Init() 的 waitPanel 参数传入 2) 等待面板上是否挂了 RoomWaitUIControl 脚本");
            }
        }
        else
        {
            string reason = pack.Returncode == ReturnCode.NotFound ? "房间不存在"
                          : pack.Returncode == ReturnCode.Full ? "房间已满"
                          : "加入失败";
            // ★v4.4：失败原因显示到提示文字（原来只写日志）
            ShowOperationHint($"加入房间失败：{reason}");

            // ★v4.3：如果未登录，提示可能原因
            if (!LoginUIControl.IsLoggedIn)
            {
                Debug.LogError($"[RoomUIControl] 当前用户未登录 (CurrentUserId={LoginUIControl.CurrentUserId})，服务端可能因 UserId 无效而拒绝加入。请确保先登录再加入房间。");
            }
        }
    }

    private void HandleQueryRoomResponse(MainPack pack)
    {
        // ★新增：重连后的"房间还在吗"校验响应（按ID查询，带单个Roompack）
        if (verifyingRoomAfterReconnect)
        {
            verifyingRoomAfterReconnect = false;
            bool exists = pack.Returncode == ReturnCode.Success
                          && pack.Roompack != null
                          && pack.Roompack.RoomId == verifyingRoomId;

            if (exists)
            {
                // 软断线重连：房间还在（人还在房间里），刷新信息留在等待面板
                currentRoom = pack.Roompack;
                Debug.Log($"[RoomUIControl] 重连校验：房间 {verifyingRoomId} 仍在，继续等待");
                if (EnsureWaitPanelScript())
                {
                    房间等待面板脚本.UpdateRoomInfo(currentRoom);
                }
            }
            else
            {
                // 服务器重启过：房间没了 → 自动退回房间搜索界面
                Debug.Log($"[RoomUIControl] 重连校验：房间 {verifyingRoomId} 已不存在，自动退回搜索界面");
                if (!roomGoneFlowRunning)
                {
                    roomGoneFlowRunning = true;
                    StartCoroutine(RoomGoneExitFlow());
                }
            }
            return;
        }

        if (pack.Returncode == ReturnCode.Success)
        {
            // 清空旧列表
            ClearRoomList();

            // 填充新列表
            if (pack.Roomlistpack != null && pack.Roomlistpack.Rooms.Count > 0)
            {
                foreach (RoomPack room in pack.Roomlistpack.Rooms)
                {
                    AddRoomItemToList(room);
                }
                Debug.Log($"[RoomUIControl] 查询到 {pack.Roomlistpack.Rooms.Count} 个房间");
            }
            else
            {
                Debug.Log("[RoomUIControl] 当前没有可加入的房间");
            }
        }
    }

    // ★重连后房间校验状态
    private bool verifyingRoomAfterReconnect = false;
    private int verifyingRoomId = -1;

    /// <summary>★断线重连成功后由 GameFace 调用：按ID查一次自己的房间还在不在。
    ///   在（软断线，服务器没重启）→ 刷新信息留在等待面板；
    ///   不在（服务器重启过）→ 自动退回房间搜索界面。</summary>
    public void VerifyRoomAfterReconnect()
    {
        // 不在房间等待状态就无需校验
        if (currentRoom == null) return;
        if (房间等待面板 == null || !房间等待面板.activeSelf) return;

        EnsureRoomRequest();
        if (roomRequest == null) return;

        verifyingRoomAfterReconnect = true;
        verifyingRoomId = currentRoom.RoomId;
        Debug.Log($"[RoomUIControl] 重连成功，校验房间 {verifyingRoomId} 是否仍存在...");
        roomRequest.SendQueryRoom(verifyingRoomId);
    }

    private void HandleLeaveRoomResponse(MainPack pack)
    {
        if (pack.Returncode != ReturnCode.Success)
            return;

        // ★修复问题3/6：区分三种情况
        if (本地发起离开)
        {
            // ── 情况A：我自己发起的离开，服务端确认成功 ──
            本地发起离开 = false;
            Debug.Log("[RoomUIControl] 已离开房间（主动离开）");

            // ★修复：主动离开后必须回到房间搜索界面。
            //   原来这里只打日志，等待面板一直挂着 → "退不回列表&操作面板"
            if (房间等待面板脚本 != null)
            {
                房间等待面板脚本.ResetState();
            }
            ShowPanel(房间列表面板);
            RefreshRoomList();
        }
        else if (pack.Roompack != null && pack.Roompack.HostName == LoginUIControl.CurrentAccount)
        {
            // ── 情况B：我是房主，加入者离开/掉线 → 房间仍在，留在房间 ──
            Debug.Log("[RoomUIControl] 加入者离开/掉线，房间保持等待状态");
            // ★音效：对手离开
            AudioManager.PlaySfx("leave");
            currentRoom = pack.Roompack;

            // 重置等待面板（停止倒计时、重置准备状态）
            if (房间等待面板脚本 != null)
            {
                房间等待面板脚本.ResetState();
                房间等待面板脚本.UpdateRoomInfo(currentRoom);
            }
            // 留在等待面板，不回大厅
            ShowPanel(房间等待面板);
        }
        else
        {
            // ── 情况C：我是加入者，房主离开/掉线 → 房间销毁，回大厅 ──
            Debug.Log("[RoomUIControl] 房主离开/掉线，房间已销毁，返回大厅");
            // ★音效：对手离开
            AudioManager.PlaySfx("leave");
            currentRoom = null;

            if (房间等待面板脚本 != null)
            {
                房间等待面板脚本.ResetState();
            }
            ShowPanel(房间操作面板);

            if (OnReturnToLobby != null)
            {
                OnReturnToLobby.Invoke();
            }
        }
    }

    // ★v4.2：Ready / StartGame 响应处理
    private void HandleReadyResponse(MainPack pack)
    {
        if (pack.Returncode == ReturnCode.Success && pack.Roompack != null)
        {
            currentRoom = pack.Roompack;
            Debug.Log($"[RoomUIControl] 收到准备状态更新: {pack.Roompack.HostName} ready={pack.Roompack.HostReady}, {pack.Roompack.GuestName} ready={pack.Roompack.GuestReady}");

            if (EnsureWaitPanelScript())
            {
                房间等待面板脚本.UpdateRoomInfo(currentRoom);
            }
            else
            {
                Debug.LogError("[RoomUIControl] HandleReadyResponse: 房间等待面板脚本为 null，无法更新准备状态！");
            }
        }
        else
        {
            Debug.LogWarning($"[RoomUIControl] Ready 响应失败: ReturnCode={pack.Returncode}");
            // ★修复：准备失败=房间已不存在（典型：服务器重启后内存房间全丢）。
            //   原来只打日志 → 用户点准备毫无反应，永远卡在等待界面。
            //   现在提示 + 2.5秒后自动退回房间搜索界面
            if (!roomGoneFlowRunning)   // 防重入：连点准备只触发一次兜底流程
            {
                roomGoneFlowRunning = true;
                StartCoroutine(RoomGoneExitFlow());
            }
        }
    }

    /// <summary>★房间失效兜底防重入标志</summary>
    private bool roomGoneFlowRunning = false;

    /// <summary>★房间失效兜底：提示 → 2.5秒后自动退回房间搜索界面并刷新列表</summary>
    private System.Collections.IEnumerator RoomGoneExitFlow()
    {
        if (EnsureWaitPanelScript())
        {
            房间等待面板脚本.ShowRoomGoneAndExit("房间已不存在（服务器可能重启过），请重新创建/加入房间");
        }

        yield return new WaitForSecondsRealtime(2.5f);

        currentRoom = null;
        if (EnsureWaitPanelScript())
        {
            房间等待面板脚本.ResetState();
        }
        // ★退回房间搜索界面（操作+列表面板），并立刻刷新一次列表
        ShowPanel(房间列表面板);
        RefreshRoomList();
        roomGoneFlowRunning = false;
    }

    private void HandleStartGameResponse(MainPack pack)
    {
        if (pack.Returncode == ReturnCode.Success)
        {
            Debug.Log("[RoomUIControl] 收到开始游戏响应！");

            if (EnsureWaitPanelScript())
            {
                // 通知等待面板开始倒计时
                // RoomWaitUIControl 的 UpdateRoomInfo 会自动检测双方准备+人数满 → 倒计时
                if (pack.Roompack != null)
                {
                    currentRoom = pack.Roompack;
                    房间等待面板脚本.UpdateRoomInfo(currentRoom);
                }
            }
            else
            {
                Debug.LogError("[RoomUIControl] HandleStartGameResponse: 房间等待面板脚本为 null！");
            }
        }
        else
        {
            // ★v4.4：失败提示（典型：一方在开始游戏前退出导致房间失效）
            Debug.LogWarning($"[RoomUIControl] StartGame 响应失败: ReturnCode={pack.Returncode}");
            ShowOperationHint("开始游戏失败，房间可能已解散");
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  列表项管理
    // ════════════════════════════════════════════════════════════════

    /// <summary>向 Content 添加一个房间列表项</summary>
    private void AddRoomItemToList(RoomPack room)
    {
        if (房间列表项预制体 == null || 房间列表容器 == null)
        {
            Debug.LogWarning("[RoomUIControl] 房间列表项预制体或容器未设置");
            return;
        }

        GameObject item = Instantiate(房间列表项预制体, 房间列表容器);
        item.SetActive(true);

        RoomListItem listItem = item.GetComponent<RoomListItem>();
        if (listItem != null)
        {
            listItem.Setup(room, (roomId) =>
            {
                EnsureRoomRequest();
                if (roomRequest != null)
                {
                    roomRequest.SendJoinRoom(roomId);
                }
            });
        }

        spawnedItems.Add(item);
        Debug.Log($"[RoomUIControl] 已添加房间项: {room.RoomName} (ID={room.RoomId})");
    }

    /// <summary>清空列表</summary>
    private void ClearRoomList()
    {
        foreach (GameObject item in spawnedItems)
        {
            if (item != null) Destroy(item);
        }
        spawnedItems.Clear();
    }

    // ════════════════════════════════════════════════════════════════
    //  面板显示工具
    //  操作面板+列表面板同时显示，只有等待面板单独显示
    // ════════════════════════════════════════════════════════════════
    private void ShowPanel(GameObject panelToShow)
    {
        if (panelToShow == null)
        {
            if (房间操作面板 != null) 房间操作面板.SetActive(false);
            if (房间列表面板 != null) 房间列表面板.SetActive(false);
            if (房间等待面板 != null) 房间等待面板.SetActive(false);
        }
        else if (panelToShow == 房间等待面板)
        {
            if (房间操作面板 != null) 房间操作面板.SetActive(false);
            if (房间列表面板 != null) 房间列表面板.SetActive(false);
            if (房间等待面板 != null) 房间等待面板.SetActive(true);
        }
        else
        {
            if (房间操作面板 != null) 房间操作面板.SetActive(true);
            if (房间列表面板 != null) 房间列表面板.SetActive(true);
            if (房间等待面板 != null) 房间等待面板.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (roomRequest != null)
            roomRequest.OnResponseCallback = null;
    }
}
