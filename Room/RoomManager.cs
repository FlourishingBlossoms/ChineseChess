using System;
using System.Collections.Generic;
using SocketGameProtocol;
using Surver.Surver;

namespace Surver.Room
{
    // ════════════════════════════════════════════════════════════════
    //  RoomManager —— 房间管理器 (v2)
    // ════════════════════════════════════════════════════════════════
    //
    //  【v2 变更说明】
    //      1. Room 内部类新增 HostReady / GuestReady 字段
    //      2. ToPack() 输出 host_ready / guest_ready
    //      3. 新增 SetReady() 方法 —— 设置玩家准备状态
    //      4. 新增 CheckAllReady() 方法 —— 检查双方是否都准备
    //
    //  【职责】
    //      管理所有房间的创建、加入、离开、查询、准备。
    //      每个房间最多2人(房主+加入者)。
    //
    //  【线程安全】
    //      所有方法都加了 lock(roomsLock)，因为多个 Client 线程会同时访问。
    //
    //  【房间ID分配】
    //      从1开始递增，永不复用。nextRoomId 原子递增。
    //
    //  【房间生命周期】
    //      创建 → 等待中(status=0) → 双人满 → 双方准备 → 游戏中(status=1) → 结束(status=2)
    //      房主离开 → 房间销毁
    //      加入者离开 → 房间回到等待中，重置准备状态
    //
    //  【调用关系】
    //      RoomController → RoomManager → Room (内部类)
    // ════════════════════════════════════════════════════════════════
    internal class RoomManager
    {
        // ── 房间状态常量 ──
        public const int STATUS_WAITING = 0;   // 等待对手加入
        public const int STATUS_PLAYING = 1;   // 游戏进行中
        public const int STATUS_ENDED = 2;     // 游戏已结束

        // ── 房间存储 ──
        private readonly Dictionary<int, Room> rooms = new Dictionary<int, Room>();
        private readonly object roomsLock = new object();
        private int nextRoomId = 1;

        // ════════════════════════════════════════════════════════════════
        //  Room —— 内部类，描述一个房间 (v2: 新增准备状态)
        // ════════════════════════════════════════════════════════════════
        internal class Room
        {
            public int RoomId;
            public string RoomName;
            public Client Host;       // 房主(红方)
            public Client Guest;      // 加入者(黑方)，null表示无人
            public int Status;

            // ★v2 新增：准备状态
            public bool HostReady;
            public bool GuestReady;

            // ★修复：防止对局记录重复保存（双方都可能发GameOver）
            public bool RecordSaved = false;

            // ★新增：对局记录 — 走棋序列 + 开始时间
            private readonly List<string> moveList = new List<string>();
            public System.DateTime StartTime { get; private set; } = System.DateTime.Now;

            /// <summary>记录一步走棋（从MovePack提取6个字段，用逗号拼接）</summary>
            public void AddMove(MovePack move)
            {
                moveList.Add($"{move.FromX},{move.FromY},{move.ToX},{move.ToY},{move.ChessId},{move.CaptureId},{move.PlayerColor}");
            }

            /// <summary>获取走棋序列字符串（分号分隔）</summary>
            public string GetMoveListString()
            {
                return string.Join(";", moveList);
            }

            /// <summary>重置对局记录（再来一局或新对局开始时调用）</summary>
            public void ResetRecord()
            {
                moveList.Clear();
                StartTime = System.DateTime.Now;
                RecordSaved = false;  // ★修复：重置保存标志
            }

            /// <summary>
            /// 将房间信息转换为 Protobuf 数据包，用于网络传输
            /// </summary>
            public RoomPack ToPack()
            {
                return new RoomPack
                {
                    RoomId = RoomId,
                    RoomName = RoomName ?? "",
                    HostId = Host?.UserId ?? 0,
                    HostName = Host?.UserName ?? "",
                    GuestId = Guest?.UserId ?? -1,
                    GuestName = Guest?.UserName ?? "",
                    Status = Status,
                    HostReady = HostReady,       // ★v2
                    GuestReady = GuestReady      // ★v2
                };
            }

            /// <summary>★v2 重置准备状态（加入者离开时调用）</summary>
            public void ResetReady()
            {
                HostReady = false;
                GuestReady = false;
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  CreateRoom —— 创建房间
        // ════════════════════════════════════════════════════════════════
        public Room CreateRoom(Client host, string roomName)
        {
            lock (roomsLock)
            {
                // 如果已在房间中，先离开旧房间
                if (host.CurrentRoom != null)
                {
                    LeaveRoom(host);
                }

                Room room = new Room
                {
                    RoomId = nextRoomId++,
                    RoomName = roomName ?? ("Room " + nextRoomId),
                    Host = host,
                    Guest = null,
                    Status = STATUS_WAITING,
                    HostReady = false,      // ★v2
                    GuestReady = false      // ★v2
                };

                rooms[room.RoomId] = room;
                host.CurrentRoom = room;

                Console.WriteLine($"[RoomManager] 房间创建: ID={room.RoomId}, Name={room.RoomName}, Host={host.UserName}");
                return room;
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  JoinRoom —— 加入房间
        // ════════════════════════════════════════════════════════════════
        public (ReturnCode code, Room room) JoinRoom(Client guest, int roomId)
        {
            lock (roomsLock)
            {
                // 如果已在房间中，先离开旧房间
                if (guest.CurrentRoom != null)
                {
                    LeaveRoom(guest);
                }

                if (!rooms.TryGetValue(roomId, out Room room))
                {
                    Console.WriteLine($"[RoomManager] 加入失败: 房间 {roomId} 不存在");
                    return (ReturnCode.NotFound, null);
                }

                if (room.Guest != null)
                {
                    Console.WriteLine($"[RoomManager] 加入失败: 房间 {roomId} 已满");
                    return (ReturnCode.Full, null);
                }

                room.Guest = guest;
                guest.CurrentRoom = room;

                // ★修复问题2: 只重置加入者的准备状态，房主的准备不受影响
                room.GuestReady = false;

                Console.WriteLine($"[RoomManager] 加入成功: Room={roomId}, Guest={guest.UserName}");
                return (ReturnCode.Success, room);
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  LeaveRoom —— 离开房间
        // ════════════════════════════════════════════════════════════════
        public Room LeaveRoom(Client client)
        {
            lock (roomsLock)
            {
                Room room = client.CurrentRoom;
                if (room == null) return null;

                Room affectedRoom = null;

                if (room.Host == client)
                {
                    // 房主离开 → 销毁房间
                    rooms.Remove(room.RoomId);
                    Console.WriteLine($"[RoomManager] 房主 {client.UserName} 离开，房间 {room.RoomId} 销毁");

                    if (room.Guest != null)
                    {
                        room.Guest.CurrentRoom = null;
                        affectedRoom = room; // 通知加入者房间已销毁
                    }
                }
                else if (room.Guest == client)
                {
                    // 加入者离开 → 房间回到等待状态
                    room.Guest = null;
                    room.Status = STATUS_WAITING;
                    room.ResetReady();  // ★v2: 重置准备状态
                    Console.WriteLine($"[RoomManager] 加入者 {client.UserName} 离开房间 {room.RoomId}");
                    affectedRoom = room; // 通知房主
                }

                client.CurrentRoom = null;
                return affectedRoom;
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  DestroyRoom —— ★新增：强制销毁房间（游戏中断线时调用）
        //
        //  无论房主还是加入者断线，只要游戏在 PLAYING 状态，房间直接销毁。
        //  返回被通知的对手 Client（可能为 null）。
        // ════════════════════════════════════════════════════════════════
        public Client DestroyRoom(Client client)
        {
            lock (roomsLock)
            {
                Room room = client.CurrentRoom;
                if (room == null) return null;

                // 找到对手
                Client opponent = (room.Host == client) ? room.Guest : room.Host;

                // 从字典移除
                rooms.Remove(room.RoomId);
                Console.WriteLine($"[RoomManager] 游戏中断线，房间 {room.RoomId} 已销毁");

                // 清理双方引用
                client.CurrentRoom = null;
                if (opponent != null)
                {
                    opponent.CurrentRoom = null;
                }

                return opponent;
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  SetReady —— ★v2 新增：设置玩家准备状态
        //
        //  参数:
        //      client   → 要设置准备的玩家
        //      isReady  → true=准备, false=取消准备
        //  返回:
        //      (返回码, 房间对象)
        //      Success: 设置成功
        //      Fail: 不在任何房间中
        //
        //  逻辑:
        //      1. 找到玩家所在房间
        //      2. 判断是房主还是加入者，设置对应字段
        //      3. 返回房间对象(Controller 负责通知双方)
        // ════════════════════════════════════════════════════════════════
        public (ReturnCode code, Room room) SetReady(Client client, bool isReady)
        {
            lock (roomsLock)
            {
                Room room = client.CurrentRoom;
                if (room == null)
                {
                    Console.WriteLine($"[RoomManager] SetReady 失败: {client.UserName} 不在任何房间中");
                    return (ReturnCode.Fail, null);
                }

                if (room.Host == client)
                {
                    room.HostReady = isReady;
                    Console.WriteLine($"[RoomManager] 房主 {client.UserName} {(isReady ? "准备" : "取消准备")}");
                }
                else if (room.Guest == client)
                {
                    room.GuestReady = isReady;
                    Console.WriteLine($"[RoomManager] 加入者 {client.UserName} {(isReady ? "准备" : "取消准备")}");
                }
                else
                {
                    Console.WriteLine($"[RoomManager] SetReady 失败: {client.UserName} 不在房间 {room.RoomId} 中");
                    return (ReturnCode.Fail, null);
                }

                return (ReturnCode.Success, room);
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  CheckAllReady —— ★v2 新增：检查双方是否都准备
        //
        //  返回:
        //      true = 人数满且双方都准备
        //      false = 人数不满或有人未准备
        // ════════════════════════════════════════════════════════════════
        public bool CheckAllReady(Room room)
        {
            if (room == null) return false;
            if (room.Host == null || room.Guest == null) return false;
            return room.HostReady && room.GuestReady;
        }

        // ════════════════════════════════════════════════════════════════
        //  GetRoomList —— 获取所有等待中的房间列表
        // ════════════════════════════════════════════════════════════════
        public List<RoomPack> GetRoomList()
        {
            lock (roomsLock)
            {
                List<RoomPack> list = new List<RoomPack>();
                foreach (var kvp in rooms)
                {
                    Room room = kvp.Value;
                    // 只显示等待中且房主存在的房间（过滤幽灵房间）
                    if (room.Status == STATUS_WAITING && room.Host != null && room.Guest == null)
                    {
                        list.Add(room.ToPack());
                    }
                }
                return list;
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  GetRoom —— 根据房间ID获取房间对象
        // ════════════════════════════════════════════════════════════════
        public Room GetRoom(int roomId)
        {
            lock (roomsLock)
            {
                rooms.TryGetValue(roomId, out Room room);
                return room;
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  GetRoomByClient —— 根据Client引用找到所在房间 ★新增
        // ════════════════════════════════════════════════════════════════
        public Room GetRoomByClient(Client client)
        {
            lock (roomsLock)
            {
                foreach (var kvp in rooms)
                {
                    Room room = kvp.Value;
                    if (room.Host == client || room.Guest == client)
                        return room;
                }
                return null;
            }
        }
    }
}
