/* ════════════════════════════════════════════════════════════════════
 *  RoomRequest.cs  —  客户端房间请求封装 (v4.1)
 * ════════════════════════════════════════════════════════════════════
 *
 *  【v4.1 变更说明（修复三个问题）】
 *      1. 新增 SendReady / SendStartGame 请求
 *      2. ROOM_ACTIONS 新增 Ready / StartGame
 *      3. SendCreateRoom / SendJoinRoom 新增登录态校验
 *      4. 日志增强：打印 UserId / Account
 *
 *  【v4 变更说明】
 *      新增 SendReady / SendStartGame 请求，支持准备状态同步和开始游戏。
 *      Awake() 注册 ActionCode 新增 Ready / StartGame。
 *
 *  【v3 变更说明（重要！修复响应无法到达的致命bug）】
 *      v2 版 RoomRequest 继承 MonoBehaviour，没有注册到 RequestManager，
 *      导致服务端返回的房间响应被 RequestManager 静默丢弃。
 *      v3 版改为继承 BaseRequest，并在 Awake() 中注册全部 ActionCode。
 *
 *  【数据流】
 *      RoomUIControl 调用 SendXxx()
 *        → 构建 MainPack { requestcode=Room, actioncode=Xxx }
 *        → GameFace.instance.Send(pack) → TCP → 服务端
 *        → 服务端 RoomController 处理 → 返回 MainPack
 *        → GameFace 接收 → RequestManager.HandleResponse()
 *        → 按 ActionCode 找到本类 → OnResponse() → OnResponseCallback
 *        → RoomUIControl.OnRoomResponse 处理
 * ════════════════════════════════════════════════════════════════════ */

using System;
using UnityEngine;
using SocketGameProtocol;

public class RoomRequest : BaseRequest
{
    // 房间系统需要处理的所有 ActionCode（v4.1 新增 Ready / StartGame）
    private static readonly ActionCode[] ROOM_ACTIONS = {
        ActionCode.CreateRoom,
        ActionCode.JoinRoom,
        ActionCode.QueryRoom,
        ActionCode.LeaveRoom,
        ActionCode.Ready,
        ActionCode.StartGame
    };

    // ════════════════════════════════════════════════════════════════
    //  Awake —— 注册全部 ActionCode 到 RequestManager
    // ════════════════════════════════════════════════════════════════
    public override void Awake()
    {
        requestcode = RequestCode.Room;
        face = GameFace.instance;

        if (face == null)
        {
            Debug.LogError("[RoomRequest] Awake: GameFace 不存在！请确保场景中有 GameFace。");
            return;
        }

        // 注册全部 ActionCode，让 RequestManager 把这些响应路由到本类
        foreach (ActionCode ac in ROOM_ACTIONS)
        {
            actioncode = ac;
            face.AddRequest(this);
        }

        Debug.Log("[RoomRequest] 已注册全部房间 ActionCode (6个)");
    }

    // ════════════════════════════════════════════════════════════════
    //  OnDestroy —— 注销全部 ActionCode
    // ════════════════════════════════════════════════════════════════
    public override void OnDestroy()
    {
        if (face != null)
        {
            foreach (ActionCode ac in ROOM_ACTIONS)
            {
                face.RemoveRequest(ac, this);
            }
        }
        OnResponseCallback = null;
    }

    // ════════════════════════════════════════════════════════════════
    //  OnResponse —— 被 RequestManager 调用，转发给 RoomUIControl
    // ════════════════════════════════════════════════════════════════
    public override void OnResponse(MainPack mainPack)
    {
        Debug.Log($"[RoomRequest] 收到响应: Action={mainPack.Actioncode}, Return={mainPack.Returncode}");
        OnResponseCallback?.Invoke(mainPack);
    }

    // ════════════════════════════════════════════════════════════════
    //  发送请求方法
    // ════════════════════════════════════════════════════════════════

    /// <summary>创建房间</summary>
    public void SendCreateRoom(string roomName)
    {
        // ★v4.1：登录态校验
        if (!CheckLoginState("SendCreateRoom")) return;

        MainPack pack = new MainPack
        {
            Requestcode = RequestCode.Room,
            Actioncode = ActionCode.CreateRoom
        };

        RoomPack room = new RoomPack
        {
            RoomName = roomName,
            HostName = LoginUIControl.CurrentAccount,
            HostId = LoginUIControl.CurrentUserId
        };
        pack.Roompack = room;

        Debug.Log($"[RoomRequest] SendCreateRoom: roomName={roomName}, HostId={LoginUIControl.CurrentUserId}, HostName={LoginUIControl.CurrentAccount}");
        SendPack(pack);
    }

    /// <summary>加入房间</summary>
    public void SendJoinRoom(int roomId)
    {
        // ★v4.1：登录态校验
        if (!CheckLoginState("SendJoinRoom")) return;

        MainPack pack = new MainPack
        {
            Requestcode = RequestCode.Room,
            Actioncode = ActionCode.JoinRoom
        };

        RoomPack room = new RoomPack
        {
            RoomId = roomId,
            GuestName = LoginUIControl.CurrentAccount,
            GuestId = LoginUIControl.CurrentUserId
        };
        pack.Roompack = room;

        Debug.Log($"[RoomRequest] SendJoinRoom: roomId={roomId}, GuestId={LoginUIControl.CurrentUserId}, GuestName={LoginUIControl.CurrentAccount}");
        SendPack(pack);
    }

    /// <summary>查询房间列表</summary>
    public void SendQueryRoom()
    {
        MainPack pack = new MainPack
        {
            Requestcode = RequestCode.Room,
            Actioncode = ActionCode.QueryRoom
        };

        SendPack(pack);
    }

    /// <summary>★新增：按ID查询指定房间（断线重连后校验"我的房间还在不在"）。
    ///   服务端按 RoomId 查单个房间（不限状态），不存在返回 NotFound。</summary>
    public void SendQueryRoom(int roomId)
    {
        MainPack pack = new MainPack
        {
            Requestcode = RequestCode.Room,
            Actioncode = ActionCode.QueryRoom,
            Roompack = new RoomPack { RoomId = roomId }
        };

        SendPack(pack);
    }

    /// <summary>离开房间</summary>
    public void SendLeaveRoom()
    {
        MainPack pack = new MainPack
        {
            Requestcode = RequestCode.Room,
            Actioncode = ActionCode.LeaveRoom
        };

        SendPack(pack);
    }

    /// <summary>准备/取消准备</summary>
    public void SendReady(bool isReady)
    {
        MainPack pack = new MainPack
        {
            Requestcode = RequestCode.Room,
            Actioncode = ActionCode.Ready
        };

        RoomPack room = new RoomPack
        {
            HostId = LoginUIControl.CurrentUserId,
            HostName = LoginUIControl.CurrentAccount,
            HostReady = isReady
        };
        pack.Roompack = room;

        Debug.Log($"[RoomRequest] SendReady: isReady={isReady}, UserId={LoginUIControl.CurrentUserId}");
        SendPack(pack);
    }

    /// <summary>开始游戏（房主触发，服务端转发给房间内所有人）</summary>
    public void SendStartGame()
    {
        MainPack pack = new MainPack
        {
            Requestcode = RequestCode.Room,
            Actioncode = ActionCode.StartGame
        };

        SendPack(pack);
    }

    // ════════════════════════════════════════════════════════════════
    //  内部工具
    // ════════════════════════════════════════════════════════════════

    /// <summary>检查登录状态，未登录则拦截并打印错误</summary>
    private bool CheckLoginState(string methodName)
    {
        if (!LoginUIControl.IsLoggedIn || LoginUIControl.CurrentUserId <= 0)
        {
            Debug.LogError($"[RoomRequest] {methodName} 失败: 用户未登录！CurrentUserId={LoginUIControl.CurrentUserId}。请确保先登录再操作房间。");
            return false;
        }
        return true;
    }

    /// <summary>实际发送</summary>
    private void SendPack(MainPack pack)
    {
        if (GameFace.instance == null || !GameFace.instance.IsConnected())
        {
            Debug.LogError("[RoomRequest] 未连接到服务器!");
            return;
        }

        GameFace.instance.Send(pack);
        Debug.Log($"[RoomRequest] 已发送: {pack.Actioncode}");
    }
}
