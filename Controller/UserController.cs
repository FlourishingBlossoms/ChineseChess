using System;
using SocketGameProtocol;
using Surver.Surver;

using GameServer = Surver.Surver.Surver;

namespace Surver.Controller
{
    // ════════════════════════════════════════════════════════════════
    //  UserController —— 用户请求控制器 (登录/注册)
    //
    //  ★本次改动：Login() 新增踢旧连接逻辑
    //      验证账号密码成功后：
    //        1. 查 server.GetClientByUserName(client.UserName)
    //        2. 如果存在旧连接且不是自己 → 先发 AlreadyLogin 通知 → 再 Close()
    //        3. server.RegisterOnlineUser(client.UserName, client)
    //        4. 返回 Success
    //
    //  被踢的旧连接会收到一个 ReturnCode = AlreadyLogin 的 MainPack，
    //  客户端据此提示"你的账号在其他设备登录"。
    // ════════════════════════════════════════════════════════════════

    internal class UserController : BaseController
    {
        public UserController()
        {
            RequestCode = RequestCode.User;
        }

        // 注册
        public MainPack Logon(GameServer server, MainPack mainPack, Client client)
        {
            if (client.Logon(mainPack) == true)
            {
                mainPack.Returncode = ReturnCode.Success;
                Console.WriteLine("[UserController] 注册成功");
            }
            else
            {
                mainPack.Returncode = ReturnCode.Fail;
                Console.WriteLine("[UserController] 注册失败");
            }
            return mainPack;
        }

        // 登录
        public MainPack Login(GameServer server, MainPack mainPack, Client client)
        {
            // 1. 验证账号密码
            if (client.Login(mainPack) == true)
            {
                // 2. ★新增：踢旧连接
                Client existingClient = server.GetClientByUserName(client.UserName);
                if (existingClient != null && existingClient != client && !existingClient.IsClosed)
                {
                    // ★新增：如果旧连接是软断线（重连场景），转移状态不踢人
                    if (existingClient.IsDisconnected)
                    {
                        Console.WriteLine($"[UserController] 用户 {client.UserName} 重连成功，转移房间状态");
                        existingClient.TransferStateTo(client);
                        client.Reconnect();  // 清除断线标记 + 通知对手已重连
                        // 不需要踢旧连接，旧连接的Socket已经断了
                        // 从客户端列表移除旧Client
                        server.RemoveClient(existingClient);
                    }
                    else
                    {
                        // 旧连接还活着 → 踢旧（原有逻辑）
                        Console.WriteLine($"[UserController] 用户 {client.UserName} 重复登录，踢旧连接");

                        MainPack kickNotify = new MainPack
                        {
                            Requestcode = RequestCode.User,
                            Actioncode = ActionCode.Login,
                            Returncode = ReturnCode.AlreadyLogin
                        };
                        existingClient.Send(kickNotify);
                        System.Threading.Thread.Sleep(100);
                        existingClient.Close();
                    }
                }

                // 3. 注册新连接到在线字典
                server.RegisterOnlineUser(client.UserName, client);

                // 4. 返回成功
                mainPack.Returncode = ReturnCode.Success;
                if (mainPack.Loginpack != null)
                {
                    mainPack.Loginpack.UserId = client.UserId;
                }
                Console.WriteLine($"[UserController] 用户 {client.UserName} 登录成功");
            }
            else
            {
                mainPack.Returncode = ReturnCode.Fail;
                Console.WriteLine("[UserController] 登录失败");
            }
            return mainPack;
        }
    }
}
