using System;
using System.Collections.Generic;
using System.Reflection;
using SocketGameProtocol;
using Surver.Surver;
using GameServer = Surver.Surver.Surver;

namespace Surver.Controller
{
    // ════════════════════════════════════════════════════════════════
    //  ControllerManager —— 请求路由器
    // ════════════════════════════════════════════════════════════════
    //
    //  【职责】
    //      根据 MainPack.Requestcode 找到对应的 Controller，
    //      再根据 MainPack.Actioncode 用反射调用 Controller 上的同名方法。
    //
    //  【路由规则】
    //      RequestCode.User → UserController  (登录/注册)
    //      RequestCode.Room → RoomController  (创建/加入/查询/离开/准备/开始)
    //      RequestCode.Game → GameController  (走棋/游戏结束/投降) ★新增
    //
    //  【反射调用规则】
    //      方法名 = ActionCode 枚举值.ToString()
    //      例如: ActionCode.CreateRoom → 调用 "CreateRoom" 方法
    //      参数: (GameServer, MainPack, Client)
    //      返回值: MainPack (非null则通过 client.Send 发回客户端)
    //
    //  ★修改点(相比原文件)
    //      构造函数中新增注册 RoomController
    // ════════════════════════════════════════════════════════════════
    internal class ControllerManager
    {
        private GameServer gameServer;
        private Dictionary<RequestCode, BaseController> controllers = new Dictionary<RequestCode, BaseController>();

        public ControllerManager(GameServer gameServer)
        {
            this.gameServer = gameServer;

            // ── 注册 UserController ──
            UserController userController = new UserController();
            controllers.Add(userController.GetRequestCode, userController);

            // ── ★新增：注册 RoomController ──
            RoomController roomController = new RoomController();
            controllers.Add(roomController.GetRequestCode, roomController);

            // ── ★新增：注册 GameController ──
            GameController gameController = new GameController();
            controllers.Add(gameController.GetRequestCode, gameController);
        }

        // ════════════════════════════════════════════════════════════════
        //  HandleRequest —— 处理客户端请求
        //
        //  流程:
        //      1. 根据 pack.Requestcode 找到 Controller
        //      2. 根据 pack.Actioncode.ToString() 找到方法
        //      3. 反射调用方法，参数为 (gameServer, pack, client)
        //      4. 方法返回的 MainPack 通过 client.Send 发回客户端
        // ════════════════════════════════════════════════════════════════
        public void HandleRequest(MainPack pack, Client client)
        {
            if (pack == null || client == null)
            {
                Console.WriteLine("[ControllerManager] pack 或 client 为空");
                return;
            }

            BaseController controller;
            if (!controllers.TryGetValue(pack.Requestcode, out controller))
            {
                Console.WriteLine("[ControllerManager] 未找到 Controller for RequestCode: " + pack.Requestcode);
                return;
            }

            // 用 ActionCode 枚举名作为方法名
            string methodName = pack.Actioncode.ToString();
            MethodInfo methodInfo = controller.GetType().GetMethod(methodName);

            if (methodInfo == null)
            {
                Console.WriteLine("[ControllerManager] 未找到方法: " + methodName + " in " + controller.GetType().Name);
                return;
            }

            // 构造参数
            object[] parameters = new object[] { gameServer, pack, client };

            try
            {
                object result = methodInfo.Invoke(controller, parameters);
                if (result != null)
                {
                    client.Send(result as MainPack);
                }
            }
            catch (TargetInvocationException tie)
            {
                Console.WriteLine("[ControllerManager] 调用方法异常: " + (tie.InnerException?.Message ?? tie.Message));
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ControllerManager] 调用方法异常: " + ex.Message);
            }
        }
    }
}
