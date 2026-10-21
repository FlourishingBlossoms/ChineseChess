using SocketGameProtocol;
using System.Collections.Generic;
using UnityEngine;

public class GameFace : MonoBehaviour
{
    public static GameFace instance;

    private ClientManager clientManager;
    public RequestManager requestManager;

    // 修正【关键】：添加响应队列，因为 socket 回调在非主线程执行，
    // 而 Unity API（如 Debug.Log、UI 更新）必须在主线程调用
    private readonly Queue<MainPack> responseQueue = new Queue<MainPack>();
    private readonly object queueLock = new object();

    // ★新增：重连状态
    public bool IsReconnecting { get; private set; } = false;

    // ★新增：重连UI引用（Inspector拖拽一个全屏遮罩面板）
    public GameObject reconnectPanel;

    // ★修复：用 static 替代 PlayerPrefs 存储玩家颜色
    //   PlayerPrefs 在 Windows 上存注册表，两个 .exe 共享同一份 → 后写的覆盖先写的 → 双开同色
    //   static 变量是进程级的，两个 .exe 各自独立 → 不会互相覆盖
    public static string MyColor { get; set; } = "Red";
    public static string OpponentName { get; set; } = "";

    // ★修复：重连UI操作不能在后台线程直接调Unity API
    //   OnReconnectStart 由 socket/Timer 后台线程调用 → 用标志位延迟到 Update 主线程执行
    private volatile bool pendingShowReconnect = false;
    private volatile bool pendingHideReconnect = false;

    private void Awake()
    {
        // ★修复：单例 + DontDestroyOnLoad，防止换场景时GameFace被销毁导致TCP断线
        //   原代码每次换场景都重新Awake → 新TCP连接 → 登录状态丢失 → 无法创建房间
        if (instance != null && instance != this)
        {
            Debug.Log("[GameFace] 已存在实例，销毁重复的GameFace");
            Destroy(gameObject);
            return;
        }
        instance = this;
        // 确保是根级GameObject，否则DontDestroyOnLoad不生效
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        // ★修复：确保后台运行，否则双开时非焦点窗口的协程（倒计时/加载）会冻结
        Application.runInBackground = true;

        requestManager = new RequestManager(this);
        clientManager = new ClientManager(this);
        clientManager.OnInit();
    }

    private void OnDestroy()
    {
        // ★只有在真正退出应用或实例被销毁时才关闭连接
        if (instance == this)
        {
            instance = null;
        }
        clientManager?.OnDistory();
        requestManager?.OnDistory();
    }

    private void Update()
    {
        // 修正【关键】：在主线程中处理响应队列
        // socket 回调在非主线程执行，Unity API 必须在主线程调用
        List<MainPack> toProcess = null;
        lock (queueLock)
        {
            if (responseQueue.Count > 0)
            {
                toProcess = new List<MainPack>(responseQueue.Count);
                while (responseQueue.Count > 0)
                {
                    toProcess.Add(responseQueue.Dequeue());
                }
            }
        }

        if (toProcess != null)
        {
            foreach (var pack in toProcess)
            {
                ProcessResponse(pack);
            }
        }

        // ★修复：在主线程处理重连UI变化（后台线程不能调Unity API）
        if (pendingShowReconnect)
        {
            pendingShowReconnect = false;
            if (reconnectPanel != null)
            {
                reconnectPanel.SetActive(true);
                // ★修复：将面板移到最上层，防止被其他UI遮挡
                reconnectPanel.transform.SetAsLastSibling();
            }
            else
            {
                // ★改为大声报错：遮罩没拖是配置错误，静默跳过会导致断连期间可随意点击
                Debug.LogError("[GameFace] reconnectPanel 未在 Inspector 赋值！断连期间无法阻挡误操作");
            }
            Debug.LogWarning("[GameFace] 进入重连状态，阻止所有操作");
        }
        if (pendingHideReconnect)
        {
            pendingHideReconnect = false;
            if (reconnectPanel != null)
                reconnectPanel.SetActive(false);
            Debug.Log("[GameFace] 重连成功，恢复操作");

            // ★新增：重连成功后校验房间是否还存在（主线程，Find在这里安全）。
            //   服务器没重启→房间在→留在等待面板；服务器重启→房间没了→自动退回搜索界面
            //   （不能直接假设房间没了：软断线重连时房间和人都还在，乱踢会把玩家踢出对局）
            var roomUI = FindObjectOfType<RoomUIControl>();
            if (roomUI != null) roomUI.VerifyRoomAfterReconnect();
        }

        // ★新增：清理超时的缓冲响应
        requestManager.UpdatePending();
    }

    public void Send(MainPack mainPack)
    {
        // ★修复：重连期间阻止所有发送
        if (IsReconnecting)
        {
            Debug.LogWarning("[GameFace] 重连中，阻止发送请求");
            return;
        }
        clientManager.Send(mainPack);
    }

    /// <summary>
    /// 修正【关键】：此方法由 socket 接收线程调用，不能直接调用 Unity API
    /// 改为将响应入队，在 Update 中由主线程处理
    /// </summary>
    public void HandleResponse(MainPack mainPack)
    {
        if (mainPack == null) return;
        lock (queueLock)
        {
            responseQueue.Enqueue(mainPack);
        }
    }

    /// <summary>
    /// 修正【关键】：在主线程中分发响应到对应的 Request
    /// 原代码只 Debug.Log，没有调用 requestManager.HandleResponse，导致 OnResponse 永远不会被调用
    ///
    /// ★新增：拦截 AlreadyLogin，不依赖 LoginRequest 是否注册
    ///   问题：被踢时如果在游戏/房间场景，LoginRequest 已销毁，响应被丢弃
    ///   解决：GameFace 跨场景存活，在这里统一处理被踢逻辑
    /// </summary>
    private void ProcessResponse(MainPack mainPack)
    {
        Debug.Log("收到响应: " + mainPack.ToString());

        // ★新增：拦截被踢下线响应，无论当前在哪个场景都能处理
        if (mainPack.Returncode == ReturnCode.AlreadyLogin)
        {
            HandleKickedByNewLogin();
            return;
        }

        // ★新增：重连后登录成功 → 恢复UI
        if (IsReconnecting && mainPack.Actioncode == ActionCode.Login
            && mainPack.Returncode == ReturnCode.Success)
        {
            clientManager.StopReconnect();
            OnReconnectSuccess();
            // 继续路由到 LoginRequest 处理登录逻辑
        }

        // ★新增：重连期间丢弃其他所有响应
        if (IsReconnecting)
        {
            return;
        }

        // 正常路由到 RequestManager
        requestManager.HandleResponse(mainPack);
    }

    // ════════════════════════════════════════════════════════════════
    //  ★新增：重连状态管理 — 由 ClientManager 调用
    // ════════════════════════════════════════════════════════════════

    /// <summary>重连开始：设置标志位，由Update在主线程显示遮罩</summary>
    public void OnReconnectStart()
    {
        IsReconnecting = true;
        pendingShowReconnect = true;  // 延迟到主线程执行
    }

    /// <summary>重连成功：设置标志位，由Update在主线程隐藏遮罩</summary>
    public void OnReconnectSuccess()
    {
        IsReconnecting = false;
        pendingHideReconnect = true;  // 延迟到主线程执行
    }

    /// <summary>★新增：手动显示/隐藏重连遮罩（主线程调用）。
    ///   自动重连走 StartReconnect→OnReconnectStart 自己会显示遮罩；
    ///   手动重登（登录按钮→ReconnectSocket）原来不触发遮罩 → 断连期间可随意点击。
    ///   现在登录按钮发起重连时调用 ShowReconnectMask(true)，连上/失败后传 false。</summary>
    public void ShowReconnectMask(bool show)
    {
        if (show) pendingShowReconnect = true;
        else pendingHideReconnect = true;
    }

    /// <summary>★新增：保存重连凭据（LoginUIControl 登录时调用）</summary>
    public void SaveReconnectCredentials(string username, string password)
    {
        clientManager?.SaveCredentials(username, password);
    }

    // ════════════════════════════════════════════════════════════════
    //  ★新增：被新设备踢下线的统一处理
    //
    //  流程：
    //    1. 重置登录状态（LoginUIControl.Logout）
    //    2. 关闭死掉的旧 TCP 连接 + 重连新连接（为重新登录做准备）
    //    3. 通过 PlayerPrefs 传递提示消息
    //    4. 加载"开始界面"场景（带加载动画）
    //
    //  这样无论被踢时在哪个场景，都会回到登录界面且能重新登录其他账号。
    // ════════════════════════════════════════════════════════════════
    /// <summary>
    /// ★修复：处理被踢下线（AlreadyLogin）
    ///   原bug：Reconnect() 开新TCP，但旧连接 ReceiveCallback 也触发 StartReconnect
    ///         → 用保存的凭据自动登录 → 又踢对方 → 对方也自动重连踢回来 → 死循环
    ///   修复：清除凭据 + Disconnect不触发自动重连 → 回登录界面让用户手动登录
    /// </summary>
    private void HandleKickedByNewLogin()
    {
        Debug.LogWarning("[GameFace] 该账号在其他设备登录，当前连接被踢下线");

        // 1. 重置登录状态
        LoginUIControl.Logout();

        // 2. ★修复：清除保存的凭据，防止旧连接的 ReceiveCallback 触发自动重连后自动登录
        clientManager.ClearCredentials();

        // 3. ★修复：用 Disconnect 替代 Reconnect，不触发自动重连
        clientManager.Disconnect();

        // 4. 传递提示消息给登录界面
        PlayerPrefs.SetString("KickedMessage", "该账号已在其他设备登录，请重新登录！");
        PlayerPrefs.Save();

        // 5. 返回开始界面（用户手动重新登录）
        SceneLoader.LoadScene("开始界面");
    }

    /// <summary>
    /// 检查是否已连接到服务器
    /// </summary>
    public bool IsConnected()
    {
        return clientManager != null && clientManager.IsConnected();
    }

    /// <summary>★修复：被踢下线后重新建立 TCP 连接</summary>
    public void ReconnectSocket()
    {
        if (clientManager != null)
        {
            clientManager.Reconnect();
        }
    }

    public void AddRequest(BaseRequest request)
    {
        requestManager.AddRequest(request);
    }

    /// <summary>
    /// ★修复：注销请求，传实例引用做比对
    /// 防止场景重载时旧 BaseRequest.OnDestroy 把新注册删了
    /// </summary>
    public void RemoveRequest(ActionCode actionCode, BaseRequest request)
    {
        requestManager.RemoveRequest(actionCode, request);
    }
}
