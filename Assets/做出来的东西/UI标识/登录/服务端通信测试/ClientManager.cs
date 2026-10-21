using SocketGameProtocol;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using System.IO;
/// <summary>
/// 网络层管理器：负责 socket 连接、发送、接收。
/// 接收回调在非主线程执行，通过 GameFace 队列转发到主线程。
/// ★新增：TCP 心跳 — 每10秒发 ActionNone 包，服务端30秒没收到则断开。
/// </summary>
public class ClientManager : BaseManager
{
    public ClientManager(GameFace face) : base(face) { }

    private Socket soc;
    private Message msg;

    // ★新增：心跳定时器（★改5秒：服务端30秒超时，客户端5秒探测一次，
    //   服务器重启后最快5秒就能发现断线并开始重连，原来10秒白等一半）
    private Timer heartbeatTimer;
    private const int HEARTBEAT_INTERVAL_MS = 5000;

    // ★新增：重连状态
    private bool isReconnecting = false;
    private Timer reconnectTimer;
    private const int RECONNECT_INTERVAL_MS = 5000;  // 每5秒尝试重连

    // ★新增：存储登录凭据用于重连（内存中，不持久化）
    private string savedUsername;
    private string savedPassword;
    private volatile bool isConnecting = false;   // ★异步连接进行中（防止重复发起/发送撞上）

    // ★新增：服务器IP列表 — txt里用逗号或换行分隔多个地址，连接时依次尝试
    //   成功的那个会被记住（currentIPIndex），之后重连优先试它
    private readonly List<string> serverIPs = new List<string>();
    private int currentIPIndex = 0;

    public override void OnInit()
    {
        base.OnInit();
        msg = new Message();
        SocketInit();
    }

    public override void OnDistory()
    {
        base.OnDistory();
        heartbeatTimer?.Dispose();
        reconnectTimer?.Dispose();
        Close();
    }

    /// <summary>★连接改为异步 + 多IP依次尝试。
    ///   server_ip.txt 里逗号/换行分隔多个IP → 本方法只发起当前IP的连接，
    ///   失败由 OnConnectFailed 自动切下一个；6秒超时看门狗兜底。
    ///   全程不阻塞主线程，游戏打开永不"未响应"。</summary>
    public void SocketInit()
    {
        if (serverIPs.Count == 0) LoadServerIPs();

        int myIndex = currentIPIndex % serverIPs.Count;
        string ip = serverIPs[myIndex];

        soc = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        try
        {
            isConnecting = true;
            Debug.Log($"[ClientManager] 连接 {ip}:8090（第 {myIndex + 1}/{serverIPs.Count} 个地址）");
            soc.BeginConnect(ip, 8090, ConnectCallback, myIndex);

            // 6秒超时看门狗：还没连上就放弃本地址，自动切下一个
            // （系统默认TCP超时约20秒，太久）。attemptSocket锁定本次的socket，
            //   防止过期看门狗误伤下一轮连接
            var attemptSocket = soc;
            var watchTimer = new System.Threading.Timer(_ =>
            {
                try
                {
                    if (attemptSocket == soc && isConnecting && currentIPIndex == myIndex && !soc.Connected)
                    {
                        try { soc.Close(); } catch { }
                        OnConnectFailed(myIndex, "连接超时（6秒）");
                    }
                }
                catch { }
            }, null, 6000, System.Threading.Timeout.Infinite);
        }
        catch (Exception e)
        {
            OnConnectFailed(myIndex, "发起失败: " + e.Message);
        }
    }

    /// <summary>★异步连接回调（socket后台线程，禁止直接调Unity API）。
    ///   myIndex 是本轮尝试的"轮次号"：如果回调触发时已经切到下一个IP，
    ///   说明这是过期回调，直接忽略——这是异步多IP链最容易踩的坑。</summary>
    private void ConnectCallback(IAsyncResult ar)
    {
        int myIndex = (int)ar.AsyncState;
        try
        {
            soc.EndConnect(ar);
            if (myIndex != currentIPIndex) return;   // 过期回调，忽略
            isConnecting = false;
            Debug.Log($"[ClientManager] 服务器连接成功: {serverIPs[myIndex]}");
            StartReceive();
            StartHeartbeat();
        }
        catch (Exception e)
        {
            OnConnectFailed(myIndex, e.Message);
        }
    }

    /// <summary>★连接失败统一出口（幂等）：记录失败 → 自动切下一个IP → 全部失败才放弃。
    ///   myIndex != currentIPIndex 说明失败报告来自已过期的旧尝试，忽略。</summary>
    private void OnConnectFailed(int myIndex, string reason)
    {
        if (!isConnecting || myIndex != currentIPIndex) return;
        isConnecting = false;

        Debug.LogError($"[ClientManager] 连接 {serverIPs[myIndex]} 失败: {reason}");

        currentIPIndex = myIndex + 1;
        if (currentIPIndex >= serverIPs.Count)
        {
            Debug.LogError("[ClientManager] 所有服务器地址均连接失败，请检查 server_ip.txt 和服务器是否启动");
            currentIPIndex = 0;   // 归零，下次从头试
            return;
        }
        SocketInit();   // 自动尝试下一个IP
    }

    /// <summary>
    /// ★新增：重连 — 关闭旧连接并重新连接服务器
    /// 用于被踢下线后恢复 TCP 连接，使用户可以重新登录其他账号。
    /// </summary>
    public void Reconnect()
    {
        Debug.Log("[ClientManager] 重连服务器...");
        Close();
        msg = new Message();  // 新建缓冲区，避免旧数据残留
        SocketInit();
    }

    private void StartReceive()
    {
        try
        {
            if (soc == null || !soc.Connected) return;
            soc.BeginReceive(msg.Buffer, msg.StartIndex, msg.Remsize,
                SocketFlags.None, ReceiveCallback, null);
        }
        catch (Exception e)
        {
            Debug.LogError("StartReceive 异常: " + e.Message);
        }
    }

    private void ReceiveCallback(IAsyncResult ar)
    {
        try
        {
            if (soc == null || !soc.Connected) return;

            int len = soc.EndReceive(ar);
            if (len == 0)
            {
                // ★改为：服务器断开 → 启动自动重连
                Debug.LogWarning("[ClientManager] 服务器断开连接，启动重连...");
                StartReconnect();
                return;
            }

            msg.ReadBuffer(len, HandleResponse);
            StartReceive();
        }
        catch (Exception e)
        {
            Debug.LogError("ReceiveCallback 异常: " + e.Message);
            StartReconnect();  // ★异常也触发重连
        }
    }

    private void HandleResponse(MainPack mainPack)
    {
        face.HandleResponse(mainPack);
    }

    public void Send(MainPack mainPack)
    {
        // ★修复：重连期间阻止发送
        if (isReconnecting)
        {
            Debug.LogWarning("[ClientManager] 重连中，阻止发送");
            return;
        }
        if (soc == null || !soc.Connected)
        {
            // ★修复：被踢 Disconnect() 后 socket 已关闭、msg 已置空，
            //   用户回到登录界面重新登录时走到这里，原来只报错返回 → 登录包永远发不出去。
            if (isConnecting)
            {
                Debug.Log("[ClientManager] 正在连接服务器，本次发送跳过，稍候重试");
                return;
            }
            Debug.Log("[ClientManager] 连接失效，自动重建连接...");
            if (msg == null) msg = new Message();
            SocketInit();   // ★现在是异步发起，不再阻塞主线程
            return;         // 本次发送放弃，连接建立后的下一次发送正常
        }
        try
        {
            soc.Send(Message.PackData(mainPack));
        }
        catch (Exception e)
        {
            Debug.LogError("Send 异常: " + e.Message);
            // ★修复：发送异常说明连接断了 → 触发重连
            if (!isReconnecting) StartReconnect();
        }
    }

    public bool IsConnected()
    {
        return soc != null && soc.Connected;
    }

    // ════════════════════════════════════════════════════════════════
    //  ★新增：TCP 心跳
    // ════════════════════════════════════════════════════════════════

    /// <summary>启动心跳定时器（连接成功后调用）</summary>
    private void StartHeartbeat()
    {
        // 先销毁旧的（Reconnect 场景）
        heartbeatTimer?.Dispose();
        // 5秒后首次发送，之后每5秒一次（服务端30秒超时，5秒心跳留足余量）
        heartbeatTimer = new Timer(_ => SendHeartBeat(), null, HEARTBEAT_INTERVAL_MS, HEARTBEAT_INTERVAL_MS);
        Debug.Log($"[ClientManager] 心跳定时器已启动（每{HEARTBEAT_INTERVAL_MS / 1000}秒）");
    }

    /// <summary>发送心跳包（ActionNone，服务端拦截不路由）</summary>
    private void SendHeartBeat()
    {
        if (soc == null || !soc.Connected)
        {
            // ★心跳发现连接断了 → 触发重连
            if (!isReconnecting) StartReconnect();
            return;
        }
        try
        {
            MainPack heartPack = new MainPack();
            heartPack.Actioncode = ActionCode.ActionNone;
            heartPack.Requestcode = RequestCode.RequestNone;
            // ★修复：Protobuf v3 不序列化默认值(0)，ActionNone=0 + RequestNone=0 → ToByteArray() 返回0字节
            //   包头 = BitConverter.GetBytes(0) = [0,0,0,0] → 服务端误判"非法包长度: 0"
            //   解决：Returncode 设非零值，强制 protobuf 序列化此字段 → body 至少2字节
            //   服务端只看 Actioncode==ActionNone 就拦截心跳，不看 Returncode
            heartPack.Returncode = ReturnCode.Fail;  // Fail=非0 → protobuf 会序列化
            byte[] data = Message.PackData(heartPack);
            soc.BeginSend(data, 0, data.Length, SocketFlags.None, null, null);
        }
        catch (Exception)
        {
            // ★心跳发送失败 → 触发重连
            if (!isReconnecting) StartReconnect();
        }
    }
    // ════════════════════════════════════════════════════════════════
    //  ★新增：自动重连系统
    //  触发条件：ReceiveCallback len==0 / 心跳发送失败
    //  行为：每5秒尝试重新连接+重新登录，成功后通知GameFace恢复UI
    //  期间GameFace.IsReconnecting=true，阻止所有用户操作
    // ════════════════════════════════════════════════════════════════

    /// <summary>保存登录凭据（LoginUIControl 在登录时调用）</summary>
    public void SaveCredentials(string username, string password)
    {
        savedUsername = username;
        savedPassword = password;
    }

    /// <summary>★新增：清除登录凭据（被踢下线时调用，防止自动重连后自动登录）</summary>
    public void ClearCredentials()
    {
        savedUsername = null;
        savedPassword = null;
        Debug.Log("[ClientManager] 凭据已清除");
    }

    /// <summary>★新增：断开连接（不触发自动重连，被踢时用）</summary>
    public void Disconnect()
    {
        // 先停止重连和心跳，防止旧连接的回调触发自动重连
        isReconnecting = false;
        reconnectTimer?.Dispose();
        reconnectTimer = null;
        heartbeatTimer?.Dispose();
        heartbeatTimer = null;
        // 关闭socket
        Close();
        msg = null;
        Debug.Log("[ClientManager] 已断开连接（不触发自动重连）");
    }

    /// <summary>是否正在重连中</summary>
    public bool IsReconnecting { get { return isReconnecting; } }

    /// <summary>启动自动重连</summary>
    private void StartReconnect()
    {
        if (isReconnecting) return;  // 已在重连中
        isReconnecting = true;

        // 通知GameFace进入重连状态（显示遮罩、阻止操作）
        face.OnReconnectStart();

        // 停止心跳（旧连接已断）
        heartbeatTimer?.Dispose();

        // 启动重连定时器
        reconnectTimer?.Dispose();
        reconnectTimer = new Timer(_ => AttemptReconnect(), null, 1000, RECONNECT_INTERVAL_MS);
        Debug.Log("[ClientManager] 重连定时器已启动（每5秒尝试）");
    }

    /// <summary>尝试重连（后台定时器线程，同步连接不阻塞UI）。
    ///   ★多IP版：从"上次成功的地址"开始遍历列表，全部失败才等下一轮。
    ///   isConnecting 防重入：同步Connect最长阻塞约20秒，定时器5秒一触发，
    ///   不加锁会多线程并发重连、多个socket互踩soc引用。</summary>
    private void AttemptReconnect()
    {
        if (!isReconnecting) return;
        if (isConnecting) return;
        if (serverIPs.Count == 0) LoadServerIPs();

        Debug.Log("[ClientManager] 尝试重连服务器...");
        msg = new Message();

        bool connected = false;
        for (int tryCount = 0; tryCount < serverIPs.Count; tryCount++)
        {
            if (!isReconnecting) return;   // 重连期间被手动停止
            int idx = (currentIPIndex + tryCount) % serverIPs.Count;
            string ip = serverIPs[idx];
            try
            {
                Close();
                soc = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                isConnecting = true;
                Debug.Log($"[ClientManager] 重连 {ip}:8090 ...");

                // ★真修：连接限时3秒。原来同步Connect对不可达IP要阻塞约20秒，
                //   2个IP一轮就要40秒+ → "重启服务端后要等很久才能重连"。
                //   BeginConnect+WaitOne 在后台线程限时等待，超时立刻换下一个IP
                var ar = soc.BeginConnect(ip, 8090, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(3000))
                {
                    isConnecting = false;
                    Debug.LogWarning($"[ClientManager] 重连 {ip} 超时(3秒)，试下一个地址");
                    try { soc.Close(); } catch { }
                    continue;
                }
                soc.EndConnect(ar);
                isConnecting = false;
                currentIPIndex = idx;      // ★记住成功的地址，下次优先它
                connected = true;
                break;
            }
            catch (Exception e)
            {
                isConnecting = false;
                Debug.LogWarning($"[ClientManager] 重连 {ip} 失败: {e.Message}");
                try { soc?.Close(); } catch { }
            }
        }

        if (!connected || soc == null || !soc.Connected)
        {
            Debug.LogWarning("[ClientManager] 所有地址重连失败，5秒后重试...");
            return;
        }

        Debug.Log("[ClientManager] TCP重连成功，开始重新登录...");
        StartReceive();
        StartHeartbeat();

        // 发送登录请求（复用已保存的凭据）
        if (!string.IsNullOrEmpty(savedUsername) && !string.IsNullOrEmpty(savedPassword))
        {
            MainPack loginPack = new MainPack();
            loginPack.Requestcode = RequestCode.User;
            loginPack.Actioncode = ActionCode.Login;
            loginPack.Loginpack = new LoginPack
            {
                Username = savedUsername,
                Password = savedPassword
            };
            // ★修复：isReconnecting 仍为 true，Send() 会被拦截
            //   重连登录包需要直接发，绕过 isReconnecting 检查
            soc.Send(Message.PackData(loginPack));
            // 服务端 UserController.Login() 会检测到旧连接是软断线 → 转移状态
            // 登录成功响应到达后由 GameFace.ProcessResponse 调用 OnReconnectSuccess
        }
        else
        {
            // 没有保存凭据（未登录状态断线）→ 直接恢复
            StopReconnect();
            face.OnReconnectSuccess();
        }
    }
    /// <summary>★解析 server_ip.txt 成IP列表：
    ///   逗号或换行都算分隔符，# 开头是备注，空行忽略。
    ///   示例（两种写法等价）：
    ///     10.53.55.153,10.94.82.253
    ///     或
    ///     10.53.55.153
    ///     # 10.94.82.253 校园网
    ///   连接时从 currentIPIndex 开始依次尝试，成功的地址被记住。
    ///   文件不存在或内容为空 → 只用 127.0.0.1（本机测试）。</summary>
    private void LoadServerIPs()
    {
        serverIPs.Clear();
        string path = Path.Combine(Application.streamingAssetsPath, "server_ip.txt");
        if (File.Exists(path))
        {
            string[] parts = File.ReadAllText(path)
                .Split(new[] { ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string raw in parts)
            {
                string ip = raw.Trim();
                if (ip.Length == 0 || ip.StartsWith("#")) continue;
                serverIPs.Add(ip);
            }
        }
        if (serverIPs.Count == 0) serverIPs.Add("127.0.0.1");
        Debug.Log($"[ClientManager] 服务器地址列表（{serverIPs.Count}个）: {string.Join(" → ", serverIPs.ToArray())}");
    }


    /// <summary>停止重连（重连成功后调用）</summary>
    public void StopReconnect()
    {
        isReconnecting = false;
        reconnectTimer?.Dispose();
        reconnectTimer = null;
        Debug.Log("[ClientManager] 重连结束");
    }

    private void Close()
    {
        try
        {
            if (soc != null)
            {
                if (soc.Connected)
                {
                    try { soc.Shutdown(SocketShutdown.Both); } catch { }
                }
                soc.Close();
                soc = null;
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Close 异常: " + e.Message);
        }
    }
}
