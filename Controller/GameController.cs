using System;
using SocketGameProtocol;
using Surver.Surver;
using Surver.Room;
using GameServer = Surver.Surver.Surver;

namespace Surver.Controller
{
    // ════════════════════════════════════════════════════════════════
    //  GameController —— 游戏对局控制器 (v1)
    // ════════════════════════════════════════════════════════════════
    //
    //  【职责】
    //      处理游戏对局中的走棋、游戏结束、投降请求。
    //      只做转发，不判规则。规则检测在客户端本地完成。
    //
    //  【方法命名规则】
    //      方法名 = ActionCode 枚举值.ToString()
    //      ActionCode.Move     → Move()
    //      ActionCode.GameOver → GameOver()
    //      ActionCode.Surrender → Surrender()  (预留接口，暂不实现)
    //
    //  【获取 RoomManager 的方式】
    //      RoomManager 不是单例，实例在 RoomController 内部:
    //          RoomController.GetRoomManager()
    //      Room 是 RoomManager 的嵌套类，类型名必须写 RoomManager.Room
    // ════════════════════════════════════════════════════════════════
    internal class GameController : BaseController
    {
        public GameController()
        {
            RequestCode = RequestCode.Game;
        }

        // ════════════════════════════════════════════════════════════════
        //  Move —— 转发走棋给对手
        // ════════════════════════════════════════════════════════════════
        public MainPack Move(GameServer server, MainPack mainPack, Client client)
        {
            if (mainPack.Movepack == null)
            {
                Console.WriteLine("[GameController] Move 失败: MovePack 为空");
                mainPack.Returncode = ReturnCode.Fail;
                return mainPack;
            }

            // 找到玩家所在房间
            RoomManager.Room room = FindRoomByClient(client);
            if (room == null)
            {
                Console.WriteLine($"[GameController] Move 失败: 用户 {client.UserName} 不在任何房间中");
                mainPack.Returncode = ReturnCode.Fail;
                return mainPack;
            }

            if (room.Status != RoomManager.STATUS_PLAYING)
            {
                Console.WriteLine($"[GameController] Move 失败: 房间 {room.RoomId} 不在游戏中");
                mainPack.Returncode = ReturnCode.Fail;
                return mainPack;
            }

            // 构造转发包，发给对手
            MainPack forwardPack = new MainPack
            {
                Requestcode = RequestCode.Game,
                Actioncode = ActionCode.Move,
                Returncode = ReturnCode.Success,
                Movepack = mainPack.Movepack
            };

            Client opponent = (room.Host == client) ? room.Guest : room.Host;
            if (opponent != null)
            {
                opponent.Send(forwardPack);
                Console.WriteLine($"[GameController] 转发走棋: {client.UserName} → {opponent.UserName} " +
                    $"棋子{mainPack.Movepack.ChessId} ({mainPack.Movepack.FromX},{mainPack.Movepack.FromY})→({mainPack.Movepack.ToX},{mainPack.Movepack.ToY})");
            }

            // ★新增：记录走棋到房间（用于对局记录）
            room.AddMove(mainPack.Movepack);

            // 不需要返回包给发送者（发送者本地已执行了移动）
            return null;
        }

        // ════════════════════════════════════════════════════════════════
        //  GameOver —— 转发游戏结束给对手，重置房间状态
        // ════════════════════════════════════════════════════════════════
        public MainPack GameOver(GameServer server, MainPack mainPack, Client client)
        {
            if (mainPack.Gameoverpack == null)
            {
                Console.WriteLine("[GameController] GameOver 失败: GameOverPack 为空");
                mainPack.Returncode = ReturnCode.Fail;
                return mainPack;
            }

            RoomManager.Room room = FindRoomByClient(client);
            if (room == null)
            {
                Console.WriteLine($"[GameController] GameOver 失败: 用户 {client.UserName} 不在任何房间中");
                mainPack.Returncode = ReturnCode.Fail;
                return mainPack;
            }

            // 转发给对手
            MainPack forwardPack = new MainPack
            {
                Requestcode = RequestCode.Game,
                Actioncode = ActionCode.GameOver,
                Returncode = ReturnCode.Success,
                Gameoverpack = mainPack.Gameoverpack
            };

            Client opponent = (room.Host == client) ? room.Guest : room.Host;
            if (opponent != null)
            {
                opponent.Send(forwardPack);
                Console.WriteLine($"[GameController] 转发游戏结束: {client.UserName} → {opponent.UserName} " +
                    $"winner={mainPack.Gameoverpack.Winner} reason={mainPack.Gameoverpack.Reason}");
            }

            // ★修复：防止重复保存记录（双方都可能发GameOver，只存第一次）
            if (!room.RecordSaved)
            {
                try
                {
                    client.getuserdata?.SaveGameRecord(
                        room.Host?.UserName ?? "",
                        room.Guest?.UserName ?? "",
                        mainPack.Gameoverpack.Winner,
                        mainPack.Gameoverpack.Reason,
                        room.GetMoveListString(),
                        room.StartTime
                    );
                    room.RecordSaved = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[GameController] 保存对局记录异常: " + ex.Message);
                }
            }
            else
            {
                Console.WriteLine($"[GameController] 房间 {room.RoomId} 记录已保存，跳过重复保存");
            }

            // 重置房间状态：游戏中 → 等待中
            room.Status = RoomManager.STATUS_WAITING;
            room.HostReady = false;
            room.GuestReady = false;
            Console.WriteLine($"[GameController] 房间 {room.RoomId} 状态重置为等待中");

            return null;
        }

        // ════════════════════════════════════════════════════════════════
        //  Surrender —— 再来一局（Rematch）
        //
        //  复用预留的 Surrender ActionCode 实现再来一局功能。
        //  GameOverPack.Reason 约定：
        //    10 = 再来一局请求（A→B）
        //    11 = 再来一局接受（B→A），服务端重置房间并发 StartGame 给双方
        //    12 = 再来一局拒绝（B→A）
        // ════════════════════════════════════════════════════════════════
        public MainPack Surrender(GameServer server, MainPack mainPack, Client client)
        {
            if (mainPack.Gameoverpack == null)
            {
                Console.WriteLine("[GameController] Surrender: GameOverPack 为空");
                mainPack.Returncode = ReturnCode.Fail;
                return mainPack;
            }

            int reason = mainPack.Gameoverpack.Reason;
            RoomManager.Room room = FindRoomByClient(client);
            if (room == null)
            {
                Console.WriteLine($"[GameController] Surrender: 用户 {client.UserName} 不在房间中");
                mainPack.Returncode = ReturnCode.Fail;
                mainPack.Gameoverpack.Message = "不在房间中";
                return mainPack;
            }

            Client opponent = (room.Host == client) ? room.Guest : room.Host;

            // ===== Reason 10: 再来一局请求 → 转发给对手 =====
            if (reason == 10)
            {
                if (opponent == null || opponent.IsClosed)
                {
                    mainPack.Returncode = ReturnCode.Fail;
                    mainPack.Gameoverpack.Message = "对手已离开";
                    Console.WriteLine($"[GameController] 再来一局请求: {client.UserName} 的对手已离开");
                    return mainPack;
                }

                MainPack forward = new MainPack
                {
                    Requestcode = RequestCode.Game,
                    Actioncode = ActionCode.Surrender,
                    Returncode = ReturnCode.Success,
                    Gameoverpack = new GameOverPack
                    {
                        Winner = 0,
                        Reason = 10,
                        Message = client.UserName
                    }
                };
                opponent.Send(forward);
                Console.WriteLine($"[GameController] 再来一局请求: {client.UserName} → {opponent.UserName}");
                return null;
            }

            // ===== Reason 11: 再来一局接受 → 重置房间 + 通知双方重载场景 =====
            if (reason == 11)
            {
                if (opponent == null || opponent.IsClosed)
                {
                    mainPack.Returncode = ReturnCode.Fail;
                    mainPack.Gameoverpack.Message = "对手已离开";
                    return mainPack;  // 返回失败给接受者
                }

                // 重置房间状态为游戏中
                room.Status = RoomManager.STATUS_PLAYING;
                room.HostReady = true;
                room.GuestReady = true;
                room.ResetRecord();  // ★新增：重置对局记录，开始新一局

                // ★给双方都发 Reason=11 通知（双方都重载场景）
                MainPack acceptNotify = new MainPack
                {
                    Requestcode = RequestCode.Game,
                    Actioncode = ActionCode.Surrender,
                    Returncode = ReturnCode.Success,
                    Gameoverpack = new GameOverPack
                    {
                        Winner = 0,
                        Reason = 11,
                        Message = "对手接受了再来一局"
                    }
                };
                opponent.Send(acceptNotify);
                client.Send(acceptNotify);

                Console.WriteLine($"[GameController] 再来一局接受: 房间 {room.RoomId} 重新开始游戏");
                return null;  // 双方都已收到通知，不需要额外返回
            }

            // ===== Reason 12: 再来一局拒绝 → 转发给邀请者 =====
            if (reason == 12)
            {
                if (opponent == null || opponent.IsClosed)
                    return null;

                MainPack forward = new MainPack
                {
                    Requestcode = RequestCode.Game,
                    Actioncode = ActionCode.Surrender,
                    Returncode = ReturnCode.Success,
                    Gameoverpack = new GameOverPack
                    {
                        Winner = 0,
                        Reason = 12,
                        Message = "对手拒绝了再来一局"
                    }
                };
                opponent.Send(forward);
                Console.WriteLine($"[GameController] 再来一局拒绝: {client.UserName} → {opponent.UserName}");
                return null;
            }

            Console.WriteLine($"[GameController] Surrender: 未知 Reason={reason}");
            mainPack.Returncode = ReturnCode.Fail;
            return mainPack;
        }

        // ════════════════════════════════════════════════════════════════
        //  NotifyDisconnect —— 玩家断线时通知对手（供 Client.Close() 调用）
        // ════════════════════════════════════════════════════════════════
        public static void NotifyDisconnect(Client client)
        {
            try
            {
                // ★修复：RoomManager 不是单例，通过 RoomController.GetRoomManager() 获取
                RoomManager roomManager = RoomController.GetRoomManager();
                if (roomManager == null) return;

                RoomManager.Room room = roomManager.GetRoomByClient(client);
                if (room == null) return;
                if (room.Status != RoomManager.STATUS_PLAYING) return;

                Client opponent = (room.Host == client) ? room.Guest : room.Host;
                if (opponent == null) return;

                // 判断断线者的颜色：房主=红方(1)，加入者=黑方(2)
                int winnerColor = (room.Host == client) ? 2 : 1;

                MainPack notify = new MainPack
                {
                    Requestcode = RequestCode.Game,
                    Actioncode = ActionCode.GameOver,
                    Returncode = ReturnCode.Success,
                    Gameoverpack = new GameOverPack
                    {
                        Winner = winnerColor,
                        Reason = 3,  // 3=对方断线
                        Message = $"对手 {client.UserName} 断线，你赢了！"
                    }
                };

                opponent.Send(notify);
                Console.WriteLine($"[GameController] 通知 {opponent.UserName}: 对手 {client.UserName} 断线");

                // 重置房间状态
                room.Status = RoomManager.STATUS_WAITING;
                room.HostReady = false;
                room.GuestReady = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[GameController] NotifyDisconnect 异常: " + ex.Message);
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  ★新增：QueryRoom —— 查询对局记录
        //  复用 ActionCode.QueryRoom + RequestCode.Game → 路由到此处
        //
        //  GameOverPack.Reason 约定：
        //    0  = 查询对局列表（返回当前用户最近50条）
        //    >0 = 查询指定ID的走棋详情（用于回放）
        //
        //  返回数据放在 GameOverPack.Message 里：
        //    列表: "id|host|guest|winner|reason|date;id|host|guest|winner|reason|date;..."
        //    详情: "host|guest|winner|reason|f3,4t5,4;f5,2t4,3;..."
        // ════════════════════════════════════════════════════════════════
        public MainPack QueryRoom(GameServer server, MainPack mainPack, Client client)
        {
            if (mainPack.Gameoverpack == null)
                mainPack.Gameoverpack = new GameOverPack();

            int reason = mainPack.Gameoverpack.Reason;

            if (reason == 0)
            {
                // 查询对局列表
                string records = client.getuserdata?.QueryGameRecords(client.UserName) ?? "";
                mainPack.Gameoverpack.Message = records;
                Console.WriteLine($"[GameController] 查询 {client.UserName} 的对局记录列表");
            }
            else
            {
                // 查询指定ID的走棋详情
                string detail = client.getuserdata?.QueryGameRecordDetail(reason) ?? "";
                mainPack.Gameoverpack.Message = detail;
                Console.WriteLine($"[GameController] 查询对局 {reason} 的走棋详情");
            }

            mainPack.Returncode = ReturnCode.Success;
            return mainPack;
        }

        // ════════════════════════════════════════════════════════════════
        //  辅助：通过 Client 找到所在房间
        // ════════════════════════════════════════════════════════════════
        private RoomManager.Room FindRoomByClient(Client client)
        {
            // ★修复：RoomManager 不是单例，通过 RoomController.GetRoomManager() 获取
            return RoomController.GetRoomManager()?.GetRoomByClient(client);
        }
    }
}
