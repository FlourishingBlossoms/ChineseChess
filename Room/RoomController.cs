using System;
using SocketGameProtocol;
using Surver.Surver;
using Surver.Room;
using GameServer = Surver.Surver.Surver;

namespace Surver.Controller
{
    // ════════════════════════════════════════════════════════════════
    //  RoomController —— 房间请求控制器 (v2)
    // ════════════════════════════════════════════════════════════════
    //
    //  【v2 变更说明】
    //      新增 Ready() 方法 —— 处理准备/取消准备请求，转发给房间内所有人
    //      新增 StartGame() 方法 —— 检查双方准备，通知双方开始游戏
    //
    //  【职责】
    //      处理客户端发来的房间相关请求(创建/加入/查询/离开/准备/开始)。
    //      被 ControllerManager 通过反射调用。
    //
    //  【方法命名规则(重要！)】
    //      方法名必须与 ActionCode 枚举值完全一致！
    //      ControllerManager.HandleRequest() 会用 pack.Actioncode.ToString()
    //      作为方法名进行反射调用。
    //
    //      ActionCode.CreateRoom → 调用本类的 CreateRoom() 方法
    //      ActionCode.JoinRoom   → 调用本类的 JoinRoom() 方法
    //      ActionCode.QueryRoom  → 调用本类的 QueryRoom() 方法
    //      ActionCode.LeaveRoom  → 调用本类的 LeaveRoom() 方法
    //      ActionCode.Ready      → 调用本类的 Ready() 方法      ★v2
    //      ActionCode.StartGame  → 调用本类的 StartGame() 方法  ★v2
    //
    //  【方法签名(必须统一)】
    //      public MainPack XxxMethod(GameServer server, MainPack mainPack, Client client)
    //      返回 null 表示不回复(不常用)，返回 MainPack 会通过 client.Send() 发回客户端
    // ════════════════════════════════════════════════════════════════
    internal class RoomController : BaseController
    {
        // 房间管理器实例(由 ControllerManager 在构造时传入，或使用单例)
        private static readonly RoomManager roomManager = new RoomManager();

        public RoomController()
        {
            RequestCode = RequestCode.Room;
        }

        // ── 提供给外部访问的房间管理器 ──
        public static RoomManager GetRoomManager()
        {
            return roomManager;
        }

        // ════════════════════════════════════════════════════════════════
        //  CreateRoom —— 创建房间
        // ════════════════════════════════════════════════════════════════
        public MainPack CreateRoom(GameServer server, MainPack mainPack, Client client)
        {
            Console.WriteLine($"[RoomController] CreateRoom 请求来自用户: {client.UserName}");

            if (client.UserId == 0)
            {
                mainPack.Returncode = ReturnCode.Fail;
                Console.WriteLine("[RoomController] 创建房间失败: 用户未登录");
                return mainPack;
            }

            string roomName = "";
            if (mainPack.Roompack != null)
            {
                roomName = mainPack.Roompack.RoomName;
            }
            if (string.IsNullOrEmpty(roomName))
            {
                roomName = client.UserName + "的房间";
            }

            RoomManager.Room room = roomManager.CreateRoom(client, roomName);

            mainPack.Returncode = ReturnCode.Success;
            mainPack.Roompack = room.ToPack();

            Console.WriteLine($"[RoomController] 房间创建成功: ID={room.RoomId}");
            return mainPack;
        }

        // ════════════════════════════════════════════════════════════════
        //  JoinRoom —— 加入房间
        // ════════════════════════════════════════════════════════════════
        public MainPack JoinRoom(GameServer server, MainPack mainPack, Client client)
        {
            Console.WriteLine($"[RoomController] JoinRoom 请求来自用户: {client.UserName}");

            if (client.UserId == 0)
            {
                mainPack.Returncode = ReturnCode.Fail;
                return mainPack;
            }

            int roomId = -1;
            if (mainPack.Roompack != null)
            {
                roomId = mainPack.Roompack.RoomId;
            }

            if (roomId < 0)
            {
                mainPack.Returncode = ReturnCode.Fail;
                Console.WriteLine("[RoomController] 加入房间失败: 无效的房间ID");
                return mainPack;
            }

            var (code, room) = roomManager.JoinRoom(client, roomId);
            mainPack.Returncode = code;

            if (code == ReturnCode.Success && room != null)
            {
                mainPack.Roompack = room.ToPack();

                // 通知房主: 有人加入了你的房间
                if (room.Host != null && room.Host != client)
                {
                    MainPack notifyHost = new MainPack
                    {
                        Requestcode = RequestCode.Room,
                        Actioncode = ActionCode.JoinRoom,
                        Returncode = ReturnCode.Success,
                        Roompack = room.ToPack()
                    };
                    room.Host.Send(notifyHost);
                    Console.WriteLine($"[RoomController] 已通知房主 {room.Host.UserName}");
                }
            }

            return mainPack;
        }

        // ════════════════════════════════════════════════════════════════
        //  QueryRoom —— 查询房间列表
        // ════════════════════════════════════════════════════════════════
        public MainPack QueryRoom(GameServer server, MainPack mainPack, Client client)
        {
            Console.WriteLine($"[RoomController] QueryRoom 请求来自用户: {client.UserName}");

            // ★新增：按ID查询单个房间（断线重连后校验"我的房间还在不在"用）。
            //   RoomId > 0 表示按ID查：不限房间状态（对局中也能查到），不存在返回 NotFound
            if (mainPack.Roompack != null && mainPack.Roompack.RoomId > 0)
            {
                int roomId = mainPack.Roompack.RoomId;
                var targetRoom = roomManager.GetRoom(roomId);
                if (targetRoom != null)
                {
                    mainPack.Returncode = ReturnCode.Success;
                    mainPack.Roompack = targetRoom.ToPack();
                    mainPack.Roomlistpack = null;
                    Console.WriteLine($"[RoomController] 按ID查询房间 {roomId}: 存在（状态={targetRoom.Status}）");
                }
                else
                {
                    mainPack.Returncode = ReturnCode.NotFound;
                    mainPack.Roompack = null;
                    Console.WriteLine($"[RoomController] 按ID查询房间 {roomId}: 不存在");
                }
                return mainPack;
            }

            // 原有逻辑：返回等待中的房间列表
            var roomList = roomManager.GetRoomList();

            RoomListPack listPack = new RoomListPack();
            listPack.Rooms.Add(roomList);

            mainPack.Returncode = ReturnCode.Success;
            mainPack.Roomlistpack = listPack;

            Console.WriteLine($"[RoomController] 返回 {roomList.Count} 个房间");
            return mainPack;
        }

        // ════════════════════════════════════════════════════════════════
        //  LeaveRoom —— 离开房间
        // ════════════════════════════════════════════════════════════════
        public MainPack LeaveRoom(GameServer server, MainPack mainPack, Client client)
        {
            Console.WriteLine($"[RoomController] LeaveRoom 请求来自用户: {client.UserName}");

            // ★修复：对局中一方主动退出（ESC暂停→返回大厅/退出游戏）时，
            //   给留守方发"判负结算"通知。原来只发 LeaveRoom 房间通知，
            //   游戏场景不处理该消息 → 对手永远卡在对局界面
            RoomManager.Room leavingRoom = client.CurrentRoom;
            if (leavingRoom != null && leavingRoom.Status == RoomManager.STATUS_PLAYING)
            {
                Client opponent = (leavingRoom.Host == client) ? leavingRoom.Guest : leavingRoom.Host;
                if (opponent != null && !opponent.IsClosed && !opponent.IsDisconnected)
                {
                    opponent.Send(new MainPack
                    {
                        Requestcode = RequestCode.Game,
                        Actioncode = ActionCode.GameOver,
                        Returncode = ReturnCode.Success,
                        Gameoverpack = new GameOverPack
                        {
                            Winner = (leavingRoom.Host == client) ? 2 : 1,  // 房主=红方，退出者输
                            Reason = 3,
                            Message = $"对手 {client.UserName} 退出对局，你赢了！"
                        }
                    });
                    Console.WriteLine($"[RoomController] 对局中退出：通知 {opponent.UserName} 获胜");
                }
            }

            RoomManager.Room affectedRoom = roomManager.LeaveRoom(client);

            mainPack.Returncode = ReturnCode.Success;

            // 通知房间内的另一方
            if (affectedRoom != null)
            {
                Client otherClient = null;
                if (affectedRoom.Host != null && affectedRoom.Host != client)
                {
                    otherClient = affectedRoom.Host;
                }
                else if (affectedRoom.Guest != null && affectedRoom.Guest != client)
                {
                    otherClient = affectedRoom.Guest;
                }

                if (otherClient != null)
                {
                    MainPack notify = new MainPack
                    {
                        Requestcode = RequestCode.Room,
                        Actioncode = ActionCode.LeaveRoom,
                        Returncode = ReturnCode.Success,
                        Roompack = affectedRoom.ToPack()
                    };
                    otherClient.Send(notify);
                    Console.WriteLine($"[RoomController] 已通知 {otherClient.UserName} 对手离开");
                }
            }

            return mainPack;
        }

        // ════════════════════════════════════════════════════════════════
        //  Ready —— ★v2 新增：准备/取消准备
        //
        //  客户端请求:
        //      MainPack { requestcode=Room, actioncode=Ready,
        //                 roompack={ host_ready=true/false } }
        //      (服务端通过 client 身份判断改的是 host_ready 还是 guest_ready)
        //
        //  服务端响应(给请求者):
        //      MainPack { returncode=Success,
        //                 roompack={ ...含最新准备状态... } }
        //
        //  通知(给房间内另一方):
        //      MainPack { requestcode=Room, actioncode=Ready,
        //                 returncode=Success,
        //                 roompack={ ...含最新准备状态... } }
        //
        //  逻辑:
        //      1. RoomManager.SetReady() 设置准备状态
        //      2. 返回更新后的 RoomPack 给请求者
        //      3. 通知房间内另一方准备状态变化
        //      4. 如果双方都准备了，触发 StartGame 通知双方
        // ════════════════════════════════════════════════════════════════
        public MainPack Ready(GameServer server, MainPack mainPack, Client client)
        {
            Console.WriteLine($"[RoomController] Ready 请求来自用户: {client.UserName}");

            // 从请求中获取准备状态
            bool isReady = false;
            if (mainPack.Roompack != null)
            {
                isReady = mainPack.Roompack.HostReady;
            }

            var (code, room) = roomManager.SetReady(client, isReady);
            mainPack.Returncode = code;
            mainPack.Roompack = room?.ToPack();

            if (code == ReturnCode.Success && room != null)
            {
                Console.WriteLine($"[RoomController] {client.UserName} 准备状态更新: {isReady}");

                // 通知房间内另一方准备状态变化
                Client otherClient = null;
                if (room.Host == client && room.Guest != null)
                {
                    otherClient = room.Guest;
                }
                else if (room.Guest == client && room.Host != null)
                {
                    otherClient = room.Host;
                }

                if (otherClient != null)
                {
                    MainPack notify = new MainPack
                    {
                        Requestcode = RequestCode.Room,
                        Actioncode = ActionCode.Ready,
                        Returncode = ReturnCode.Success,
                        Roompack = room.ToPack()
                    };
                    otherClient.Send(notify);
                    Console.WriteLine($"[RoomController] 已通知 {otherClient.UserName} 对手准备状态变化");
                }

                // 检查是否双方都准备了，如果是则触发 StartGame 通知
                if (roomManager.CheckAllReady(room))
                {
                    Console.WriteLine($"[RoomController] 房间 {room.RoomId} 双方已准备，触发开始游戏!");

                    // 设置房间状态为游戏中
                    room.Status = RoomManager.STATUS_PLAYING;
                    // ★修复：每局开始必须重置记录标志，否则上一局留下的 RecordSaved=true
                    //   会让本局 GameOver 被"防重复保存"机制跳过 → 第二局记录丢失
                    room.ResetRecord();

                    MainPack startNotify = new MainPack
                    {
                        Requestcode = RequestCode.Room,
                        Actioncode = ActionCode.StartGame,
                        Returncode = ReturnCode.Success,
                        Roompack = room.ToPack()
                    };

                    // 通知双方
                    if (room.Host != null)
                    {
                        room.Host.Send(startNotify);
                    }
                    if (room.Guest != null)
                    {
                        room.Guest.Send(startNotify);
                    }
                }
            }

            return mainPack;
        }

        // ════════════════════════════════════════════════════════════════
        //  StartGame —— ★v2 新增：开始游戏（手动触发，备用）
        //
        //  说明:
        //      客户端等待界面 v2 采用"双方准备自动开始"模式，
        //      实际开始游戏的触发在 Ready() 方法中完成。
        //      此方法保留作为备用入口，允许房主手动强制开始。
        //
        //  客户端请求:
        //      MainPack { requestcode=Room, actioncode=StartGame }
        //
        //  服务端响应:
        //      Success = 双方准备，已通知开始
        //      NotReady = 人数不满或有人未准备
        // ════════════════════════════════════════════════════════════════
        public MainPack StartGame(GameServer server, MainPack mainPack, Client client)
        {
            Console.WriteLine($"[RoomController] StartGame 请求来自用户: {client.UserName}");

            RoomManager.Room room = client.CurrentRoom;

            if (room == null)
            {
                mainPack.Returncode = ReturnCode.Fail;
                Console.WriteLine("[RoomController] StartGame 失败: 不在任何房间中");
                return mainPack;
            }

            if (!roomManager.CheckAllReady(room))
            {
                mainPack.Returncode = ReturnCode.NotReady;
                Console.WriteLine("[RoomController] StartGame 失败: 双方未全部准备");
                return mainPack;
            }

            // 设置房间状态为游戏中
            room.Status = RoomManager.STATUS_PLAYING;
            // ★修复：同上，开始新对局前重置记录标志
            room.ResetRecord();

            mainPack.Returncode = ReturnCode.Success;
            mainPack.Roompack = room.ToPack();

            // 通知房间内双方
            MainPack startNotify = new MainPack
            {
                Requestcode = RequestCode.Room,
                Actioncode = ActionCode.StartGame,
                Returncode = ReturnCode.Success,
                Roompack = room.ToPack()
            };

            if (room.Host != null && room.Host != client)
            {
                room.Host.Send(startNotify);
            }
            if (room.Guest != null && room.Guest != client)
            {
                room.Guest.Send(startNotify);
            }

            Console.WriteLine($"[RoomController] 房间 {room.RoomId} 游戏开始!");
            return mainPack;
        }
    }
}
