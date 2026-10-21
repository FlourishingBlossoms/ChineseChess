using Surver.DataBase;
using Surver.ToolClass;
using System;
using System.Net.Sockets;
using SocketGameProtocol;
using Surver.Room;
using Surver.Controller;

namespace Surver.Surver
{
    // ════════════════════════════════════════════════════════════════
    //  Client —— 客户端连接封装
    // ════════════════════════════════════════════════════════════════
    //
    //  【职责】
    //      每个客户端连接对应一个 Client 实例。
    //      负责接收数据、解析协议、转发请求到 ControllerManager。
    //
    //  ★新增字段(相比原文件)
    //      UserId      → 用户ID(登录成功后设置)
    //      UserName    → 用户名(登录成功后设置)
    //      CurrentRoom → 当前所在房间(离开时设为null)
    //      IsClosed    → 防止重复 Close
    //
    //  ★Close() 变更
    //      finally 块新增：server.UnregisterOnlineUser(UserName, this)
    //      断开时从在线字典移除自己
    //
    //  ★接收方式：使用 Message 实例的 ReadBuffer（流式粘包处理）
    //      与客户端 ClientManager.cs 完全一致：
    //        soc.BeginReceive(message.Buffer, ...) → message.ReadBuffer(callback)
    //      不使用 headBytes/UnpackData（已废弃，有字节序 bug）
    // ════════════════════════════════════════════════════════════════

    internal class Client
    {
        private Socket soc;
        private Message message;    // ★修复：每个 Client 自带 Message 实例（线程安全）
        private UserData userdata;
        private Surver server;

        public int UserId { get; set; }
        public string UserName { get; set; }
        public RoomManager.Room CurrentRoom { get; set; }
        public bool IsClosed { get { return isClosed; } }
        private bool isClosed = false;

        // ★新增：心跳时间戳，服务端每5秒巡检，30秒无任何数据 → Close
        public System.DateTime LastHeartbeat { get; private set; } = System.DateTime.Now;

        // ★新增：软断线 — TCP断了但保留Client对象，等1分钟超时才真正Close
        // 房间等待/游戏中：1分钟超时 → 执行退出逻辑
        // 大厅：无限期等待重连
        public bool IsDisconnected { get; private set; } = false;
        public System.DateTime DisconnectTime { get; private set; }
        private static readonly double DISCONNECT_TIMEOUT_SEC = 60;  // 1分钟

        /// <summary>软断线：标记为已断线但不清理，等超时或重连</summary>
        public void SoftDisconnect()
        {
            if (isClosed || IsDisconnected) return;
            IsDisconnected = true;
            DisconnectTime = System.DateTime.Now;
            Console.WriteLine($"[Client] {UserName ?? "未登录"} 软断线，等待重连（{DISCONNECT_TIMEOUT_SEC}s超时）");

            // 通知对手"断线等待中"
            NotifyOpponentDisconnect();
        }

        /// <summary>重连成功：清除软断线标记</summary>
        public void Reconnect()
        {
            IsDisconnected = false;
            LastHeartbeat = System.DateTime.Now;
            Console.WriteLine($"[Client] {UserName} 重连成功");

            // 通知对手"已重连"
            NotifyOpponentReconnect();
        }

        /// <summary>检查是否断线超时（由Surver心跳巡检调用）</summary>
        public bool IsDisconnectTimeout()
        {
            if (!IsDisconnected || isClosed) return false;
            // ★改为：无论在不在房间，都60秒超时 → Close
            //   原来大厅不超时（无限等待），会导致Client对象泄漏
            return (System.DateTime.Now - DisconnectTime).TotalSeconds >= DISCONNECT_TIMEOUT_SEC;
        }

        public UserData getuserdata { get { return userdata; } }

        public Client(Socket soc, Surver server)
        {
            this.soc = soc;
            this.server = server;
            this.message = new Message();
            this.userdata = new UserData();
            StartReceive();
        }

        // ════════════════════════════════════════════════════════════════
        //  接收：与客户端 ClientManager.cs 完全一致的流式接收
        // ════════════════════════════════════════════════════════════════
        private void StartReceive()
        {
            try
            {
                if (soc == null || !soc.Connected) return;
                soc.BeginReceive(message.Buffer, message.StartIndex, message.Remsize,
                    SocketFlags.None, ReceiveCallback, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine("StartReceive 异常: " + ex.Message);
            }
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                if (soc == null || isClosed) return;
                int len = soc.EndReceive(ar);
                if (len == 0)
                {
                    // ★改为软断线：不立即Close，标记等待重连
                    SoftDisconnect();
                    return;
                }

                message.ReadBuffer(len, (pack) =>
                {
                    // ★新增：心跳拦截 — ActionNone = 心跳包，不路由到 ControllerManager
                    LastHeartbeat = System.DateTime.Now;  // 任何包都算心跳
                    if (pack.Actioncode == ActionCode.ActionNone)
                    {
                        // 心跳包，静默处理
                        return;
                    }
                    server.HandleRequest(pack, this);
                });

                StartReceive();
            }
            catch (Exception ex)
            {
                Console.WriteLine("ReceiveCallback 异常: " + ex.Message + " → 软断线等待重连");
                // ★改为：异常也走软断线，给客户端60秒重连窗口，而不是直接Close
                SoftDisconnect();
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  Send —— 发送数据包给客户端
        // ════════════════════════════════════════════════════════════════
        public void Send(MainPack pack)
        {
            if (soc == null || !soc.Connected || isClosed) return;
            try
            {
                byte[] data = Message.PackData(pack);
                soc.BeginSend(data, 0, data.Length, SocketFlags.None, SendCallback, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Send 异常: " + ex.Message);
            }
        }

        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                if (soc != null && soc.Connected)
                {
                    soc.EndSend(ar);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("SendCallback 异常: " + ex.Message);
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  Logon —— 注册，委托给 UserData
        // ════════════════════════════════════════════════════════════════
        public bool Logon(MainPack pack)
        {
            return getuserdata.Logon(pack);
        }

        // ════════════════════════════════════════════════════════════════
        //  Login —— 登录，委托给 UserData
        //  登录成功后设置 UserId 和 UserName
        // ════════════════════════════════════════════════════════════════
        public bool Login(MainPack pack)
        {
            bool result = getuserdata.Login(pack);
            if (result)
            {
                UserId = getuserdata.GetUserId(pack.Loginpack.Username);
                UserName = pack.Loginpack.Username;
            }
            return result;
        }

        // ════════════════════════════════════════════════════════════════
        //  ★新增：断线通知对手 — Reason 30=断线等待, 31=已重连
        // ════════════════════════════════════════════════════════════════
        private void NotifyOpponentDisconnect()
        {
            if (CurrentRoom == null) return;
            Client opponent = (CurrentRoom.Host == this) ? CurrentRoom.Guest : CurrentRoom.Host;
            if (opponent == null || opponent.IsClosed || opponent.IsDisconnected) return;

            MainPack notify = new MainPack
            {
                Requestcode = RequestCode.Game,
                Actioncode = ActionCode.GameOver,
                Returncode = ReturnCode.Success,
                Gameoverpack = new GameOverPack
                {
                    Winner = 0, Reason = 30,
                    Message = $"对手 {UserName} 断线中，等待重连..."
                }
            };
            opponent.Send(notify);
        }

        private void NotifyOpponentReconnect()
        {
            if (CurrentRoom == null) return;
            Client opponent = (CurrentRoom.Host == this) ? CurrentRoom.Guest : CurrentRoom.Host;
            if (opponent == null || opponent.IsClosed || opponent.IsDisconnected) return;

            MainPack notify = new MainPack
            {
                Requestcode = RequestCode.Game,
                Actioncode = ActionCode.GameOver,
                Returncode = ReturnCode.Success,
                Gameoverpack = new GameOverPack
                {
                    Winner = 0, Reason = 31,
                    Message = $"对手 {UserName} 已重连"
                }
            };
            opponent.Send(notify);
        }

        // ════════════════════════════════════════════════════════════════
        //  ★新增：将房间状态转移到新Client（重连时调用）
        //  旧Client的CurrentRoom引用 → 新Client的CurrentRoom
        //  房间的Host/Guest指针也要更新
        // ════════════════════════════════════════════════════════════════
        public void TransferStateTo(Client newClient)
        {
            if (CurrentRoom == null) return;

            // 更新房间的Host/Guest指针
            if (CurrentRoom.Host == this)
                CurrentRoom.Host = newClient;
            else if (CurrentRoom.Guest == this)
                CurrentRoom.Guest = newClient;

            // 转移引用
            newClient.CurrentRoom = CurrentRoom;
            newClient.UserId = UserId;
            newClient.UserName = UserName;

            // 清空旧引用（防止旧Client Close时影响房间）
            CurrentRoom = null;

            Console.WriteLine($"[Client] 状态转移: {UserName} 旧Client→新Client");
        }

        /// <summary>
        /// 统一关闭逻辑：
        ///   1. 如果在房间里，自动离开 + 通知对手
        ///   2. 关闭 socket
        ///   3. 从在线字典移除
        ///   4. 从客户端列表移除
        /// </summary>
        public void Close()
        {
            if (isClosed) return;
            isClosed = true;

            try
            {
                // 1. 自动离开房间
                if (CurrentRoom != null)
                {
                    RoomManager roomManager = RoomController.GetRoomManager();
                    RoomManager.Room roomRef = CurrentRoom;  // 保存引用，DestroyRoom会清空CurrentRoom

                    try
                    {
                        if (roomRef.Status == RoomManager.STATUS_PLAYING)
                        {
                            // ★游戏中断线 → 销毁房间 + 通知对手获胜
                            Client opponent = roomManager.DestroyRoom(this);
                            if (opponent != null && !opponent.IsClosed)
                            {
                                MainPack winNotify = new MainPack
                                {
                                    Requestcode = RequestCode.Game,
                                    Actioncode = ActionCode.GameOver,
                                    Returncode = ReturnCode.Success,
                                    Gameoverpack = new GameOverPack
                                    {
                                        Winner = (roomRef.Host == this) ? 2 : 1,
                                        Reason = 3,
                                        Message = $"对手 {UserName} 断线，你赢了！"
                                    }
                                };
                                opponent.Send(winNotify);
                                Console.WriteLine($"[Client] 通知 {opponent.UserName}: 对手 {UserName} 游戏中断线");
                            }
                        }
                        else
                        {
                            // ★等待中断线 → 走正常离开房间逻辑（房主离开→销毁房间，加入者离开→房间回等待）
                            RoomManager.Room affectedRoom = roomManager.LeaveRoom(this);
                            if (affectedRoom != null)
                            {
                                // 通知房间内剩余的人
                                Client other = affectedRoom.Host ?? affectedRoom.Guest;
                                if (other != null && !other.IsClosed)
                                {
                                    MainPack notify = new MainPack
                                    {
                                        Requestcode = RequestCode.Room,
                                        Actioncode = ActionCode.LeaveRoom,
                                        Returncode = ReturnCode.Success,
                                        Roompack = affectedRoom.ToPack()
                                    };
                                    other.Send(notify);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[Client] 离开房间异常: " + ex.Message);
                    }
                }

                // 2. 关闭 socket
                if (soc != null)
                {
                    try { soc.Shutdown(SocketShutdown.Both); } catch { }
                    soc.Close();
                    soc = null;
                }
                userdata?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Close 异常: " + ex.Message);
            }
            finally
            {
                // ★新增：从在线字典移除（只在字典里存的是自己时才移除）
                if (!string.IsNullOrEmpty(UserName))
                {
                    server?.UnregisterOnlineUser(UserName, this);
                }
                // 从客户端列表移除
                server?.RemoveClient(this);
            }
        }
    }
}
