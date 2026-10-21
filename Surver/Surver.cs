using SocketGameProtocol;
using Surver.Controller;
using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Threading;

namespace Surver.Surver
{
    internal class Surver
    {
        private Socket soc;
        private readonly List<Client> clients = new List<Client>();
        private readonly object clientsLock = new object();
        private ControllerManager controllermanager;

        // ★新增：在线用户字典（用户名 → Client）
        // 用于同账号重复登录时踢旧连接
        // 复用 clientsLock，不再新建锁
        private readonly Dictionary<string, Client> onlineUsers = new Dictionary<string, Client>();

        // ★新增：心跳巡检 — 每5秒检查一次，30秒无心跳的连接断开
        private Timer heartbeatChecker;
        private const int HEARTBEAT_CHECK_INTERVAL_MS = 5000;  // 巡检间隔
        private const int HEARTBEAT_TIMEOUT_SEC = 30;          // 超时阈值

        public Surver(int port)
        {
            controllermanager = new ControllerManager(this);
            soc = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            soc.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            soc.Bind(new IPEndPoint(IPAddress.Any, port));
            soc.Listen(0);
            StartAccept();

            // ★新增：启动心跳巡检线程
            heartbeatChecker = new Timer(CheckHeartbeats, null, HEARTBEAT_CHECK_INTERVAL_MS, HEARTBEAT_CHECK_INTERVAL_MS);
            Console.WriteLine("[Surver] 心跳巡检已启动（每5秒检查，30秒超时）");
        }

        private void StartAccept()
        {
            soc.BeginAccept(AcceptCallback, null);
        }

        private void AcceptCallback(IAsyncResult ar)
        {
            try
            {
                Socket client = soc.EndAccept(ar);
                Client c = new Client(client, this);
                lock (clientsLock)
                {
                    clients.Add(c);
                    Console.WriteLine($"客户端已连接，当前在线: {clients.Count}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("AcceptCallback 异常: " + ex.Message);
            }
            finally
            {
                StartAccept();
            }
        }

        public void HandleRequest(MainPack pack, Client client)
        {
            controllermanager.HandleRequest(pack, client);
        }

        public void RemoveClient(Client client)
        {
            if (client == null) return;
            lock (clientsLock)
            {
                clients.Remove(client);
                Console.WriteLine($"客户端已断开，当前在线: {clients.Count}");
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  ★新增：在线用户管理（用于踢旧连接）
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// 查询某用户名是否已在线，返回对应的 Client（不在则 null）
        /// </summary>
        public Client GetClientByUserName(string userName)
        {
            if (string.IsNullOrEmpty(userName)) return null;
            lock (clientsLock)
            {
                onlineUsers.TryGetValue(userName, out Client c);
                return c;
            }
        }

        /// <summary>
        /// 注册在线用户。如果该用户名已存在（理论上不会，因为 Login 会先踢旧的），
        /// 覆盖之。
        /// </summary>
        public void RegisterOnlineUser(string userName, Client client)
        {
            if (string.IsNullOrEmpty(userName)) return;
            lock (clientsLock)
            {
                onlineUsers[userName] = client;
            }
        }

        /// <summary>
        /// 从在线字典中移除用户。仅在 Client.Close() 中调用。
        /// 注意：只有当字典里的 Client 等于 currentClient 时才移除，
        /// 防止"旧连接被踢 → 新连接已注册 → 旧连接 Close 把新连接也移除了"。
        /// </summary>
        public void UnregisterOnlineUser(string userName, Client currentClient)
        {
            if (string.IsNullOrEmpty(userName)) return;
            lock (clientsLock)
            {
                onlineUsers.TryGetValue(userName, out Client existing);
                if (existing == currentClient)
                {
                    onlineUsers.Remove(userName);
                }
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  ★新增：心跳巡检
        //  每5秒遍历所有客户端，LastHeartbeat 超过30秒的 → Close
        //  注意：先复制列表再遍历，避免 Close 中修改 clients 列表导致异常
        // ════════════════════════════════════════════════════════════════
        private void CheckHeartbeats(object state)
        {
            List<Client> toClose = new List<Client>();
            DateTime now = DateTime.Now;

            lock (clientsLock)
            {
                foreach (Client c in clients)
                {
                    if (c.IsClosed) continue;

                    // ★新增：软断线超时检查
                    if (c.IsDisconnected)
                    {
                        if (c.IsDisconnectTimeout())
                            toClose.Add(c);  // 房间/游戏中断线超1分钟 → Close
                        continue;  // 软断线期间不检查心跳
                    }

                    // 心跳超时检查（未软断线的才检查）
                    if (!c.IsClosed && (now - c.LastHeartbeat).TotalSeconds > HEARTBEAT_TIMEOUT_SEC)
                    {
                        // ★改为：心跳超时也先走软断线，而不是直接Close
                        c.SoftDisconnect();
                    }
                }
            }

            foreach (Client c in toClose)
            {
                Console.WriteLine($"[Surver] 客户端 {c.UserName ?? "未登录"} 断线超时({HEARTBEAT_TIMEOUT_SEC}s心跳+{60}s重连窗口)，执行Close");
                c.Close();
            }
        }
    }
}
