/* ════════════════════════════════════════════════════════════════════
 *  GameRequest.cs  —  客户端游戏对局请求封装 (v1)
 * ════════════════════════════════════════════════════════════════════
 *
 *  【这个脚本干什么？】
 *      封装客户端到服务端的游戏对局请求（走棋/游戏结束/投降）。
 *      继承 BaseRequest，在 Awake 中注册到 RequestManager。
 *      收到服务端响应时，通过 OnResponseCallback 回调给 选择象棋.cs。
 *
 *  【数据流】
 *      选择象棋.cs 调用 SendMove()
 *        → 构建 MainPack { requestcode=Game, actioncode=Move, movepack=... }
 *        → GameFace.Send() → TCP → 服务端
 *        → 服务端 GameController.Move() → 转发给对手
 *
 *      收到对手走棋:
 *        → GameFace 接收 → RequestManager.HandleResponse()
 *        → GameRequest.OnResponse() → OnResponseCallback
 *        → 选择象棋.OnOpponentMove()
 *
 *  【挂载方式】
 *      不需要手动挂载。选择象棋.cs 在 Awake 中 AddComponent<GameRequest>()
 * ════════════════════════════════════════════════════════════════════ */

using SocketGameProtocol;
using UnityEngine;

public class GameRequest : BaseRequest
{
    // GameRequest 需要注册的3个 ActionCode
    private static readonly ActionCode[] GAME_ACTIONS = {
        ActionCode.Move,
        ActionCode.GameOver,
        ActionCode.Surrender,  // 预留
    };

    public override void Awake()
    {
        face = GameFace.instance;
        if (face == null)
        {
            Debug.LogError("[GameRequest] GameFace.instance 为 null！请确保场景中已挂载 GameFace。");
            return;
        }

        foreach (ActionCode ac in GAME_ACTIONS)
        {
            actioncode = ac;
            face.AddRequest(this);
        }
        Debug.Log("[GameRequest] 已注册到 RequestManager");
    }

    // ════════════════════════════════════════════════════════════════
    //  发送方法
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 发送走棋
    /// </summary>
    /// <param name="fromX">起点列(逻辑坐标)</param>
    /// <param name="fromY">起点行</param>
    /// <param name="toX">终点列</param>
    /// <param name="toY">终点行</param>
    /// <param name="chessId">移动的棋子ID(0-31)</param>
    /// <param name="captureId">被吃的棋子ID(-1=没吃)</param>
    /// <param name="playerColor">1=红,2=黑</param>
    public void SendMove(int fromX, int fromY, int toX, int toY,
                         int chessId, int captureId, int playerColor)
    {
        MainPack pack = new MainPack
        {
            Requestcode = RequestCode.Game,
            Actioncode = ActionCode.Move,
            Movepack = new MovePack
            {
                FromX = fromX,
                FromY = fromY,
                ToX = toX,
                ToY = toY,
                ChessId = chessId,
                CaptureId = captureId,
                PlayerColor = playerColor
            }
        };

        Debug.Log($"[GameRequest] 发送走棋: 棋子{chessId} ({fromX},{fromY})→({toX},{toY}) " +
                  $"吃子={captureId} 颜色={playerColor}");
        Send(pack);
    }

    /// <summary>
    /// 发送游戏结束
    /// </summary>
    /// <param name="winner">1=红胜,2=黑胜</param>
    /// <param name="reason">1=将帅被吃,2=投降,3=断线</param>
    /// <param name="message">可选描述</param>
    public void SendGameOver(int winner, int reason, string message = "")
    {
        MainPack pack = new MainPack
        {
            Requestcode = RequestCode.Game,
            Actioncode = ActionCode.GameOver,
            Gameoverpack = new GameOverPack
            {
                Winner = winner,
                Reason = reason,
                Message = message ?? ""
            }
        };

        Debug.Log($"[GameRequest] 发送游戏结束: winner={winner} reason={reason}");
        Send(pack);
    }

    /// <summary>
    /// ★新增：对局中主动退出（ESC暂停→返回大厅/退出游戏）时发送。
    /// LeaveRoom 归房间控制器管，所以 Requestcode 用 Room。
    /// 服务端检测到对局中退出 → 给对手发判负结算（reason=3）。
    /// </summary>
    public void SendLeaveRoomFromGame()
    {
        MainPack pack = new MainPack
        {
            Requestcode = RequestCode.Room,
            Actioncode = ActionCode.LeaveRoom,
            Returncode = ReturnCode.Success
        };
        Debug.Log("[GameRequest] 对局中退出，发送 LeaveRoom");
        Send(pack);
    }

    /// <summary>
    /// 发送再来一局请求（Reason=10）
    /// </summary>
    public void SendRematchRequest()
    {
        MainPack pack = new MainPack
        {
            Requestcode = RequestCode.Game,
            Actioncode = ActionCode.Surrender,
            Gameoverpack = new GameOverPack
            {
                Winner = 0,
                Reason = 10,
                Message = ""
            }
        };
        Debug.Log("[GameRequest] 发送再来一局请求");
        Send(pack);
    }

    /// <summary>
    /// 发送再来一局接受（Reason=11）
    /// </summary>
    public void SendRematchAccept()
    {
        MainPack pack = new MainPack
        {
            Requestcode = RequestCode.Game,
            Actioncode = ActionCode.Surrender,
            Gameoverpack = new GameOverPack
            {
                Winner = 0,
                Reason = 11,
                Message = ""
            }
        };
        Debug.Log("[GameRequest] 发送再来一局接受");
        Send(pack);
    }

    /// <summary>
    /// 发送再来一局拒绝（Reason=12）
    /// </summary>
    public void SendRematchReject()
    {
        MainPack pack = new MainPack
        {
            Requestcode = RequestCode.Game,
            Actioncode = ActionCode.Surrender,
            Gameoverpack = new GameOverPack
            {
                Winner = 0,
                Reason = 12,
                Message = ""
            }
        };
        Debug.Log("[GameRequest] 发送再来一局拒绝");
        Send(pack);
    }

    // ════════════════════════════════════════════════════════════════
    //  响应处理
    // ════════════════════════════════════════════════════════════════

    public override void OnResponse(MainPack mainPack)
    {
        Debug.Log($"[GameRequest] 收到响应: Action={mainPack.Actioncode}");

        switch (mainPack.Actioncode)
        {
            case ActionCode.Move:
                if (mainPack.Movepack != null)
                {
                    OnResponseCallback?.Invoke(mainPack);
                }
                break;

            case ActionCode.GameOver:
                if (mainPack.Gameoverpack != null)
                {
                    OnResponseCallback?.Invoke(mainPack);
                }
                break;

            case ActionCode.Surrender:
                // ★再来一局：转发给 选择象棋.OnGameResponse 处理
                if (mainPack.Gameoverpack != null)
                {
                    OnResponseCallback?.Invoke(mainPack);
                }
                break;
        }
    }

    public override void OnDestroy()
    {
        if (face != null)
        {
            foreach (ActionCode ac in GAME_ACTIONS)
            {
                face.RemoveRequest(ac, this);
            }
        }
        OnResponseCallback = null;
    }
}
