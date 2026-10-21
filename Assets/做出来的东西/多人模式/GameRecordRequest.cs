using SocketGameProtocol;
using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// ★新增：对局记录请求脚本（★调试增强版）
/// 
/// 发送 RequestCode.Game + ActionCode.QueryRoom → 路由到 GameController.QueryRoom()
/// 
/// GameOverPack.Reason 约定：
///   0  = 查询对局列表
///   >0 = 查询指定 game_id 的走棋详情（用于回放）
/// 
/// 响应数据在 GameOverPack.Message 里：
///   列表: "id|host|guest|winner|reason|date;id|host|guest|winner|reason|date;..."
///   详情: "host|guest|winner|reason|f3,4t5,4;f5,2t4,3;..."
/// 
/// 使用方式：
///   1. 挂到回放场景的空 GameObject 上
///   2. UI 脚本通过 Inspector 引用此脚本
///   3. 调用 QueryList() 查询列表，调用 QueryDetail(gameId) 查询回放
///   4. 通过 OnListResponse / OnDetailResponse 回调接收数据
/// </summary>
public class GameRecordRequest : BaseRequest
{
    public override void Awake()
    {
        Debug.Log("[GameRecordRequest] ========== Awake 执行 ==========");
        Debug.Log($"[GameRecordRequest] GameObject: {gameObject.name}, active: {gameObject.activeInHierarchy}");

        requestcode = RequestCode.Game;
        actioncode = ActionCode.QueryRoom;

        // ★调试：检查 GameFace
        if (GameFace.instance == null)
        {
            Debug.LogError("[GameRecordRequest] ★★★ GameFace.instance 为空！" +
                          " GameRecordRequest 依赖 GameFace 发送网络请求。" +
                          " 回放场景需要从有 GameFace 的场景进入（GameFace 有 DontDestroyOnLoad）。");
            // 仍然调用 base.Awake，它内部会再次检查并打印错误
        }
        else
        {
            Debug.Log("[GameRecordRequest] GameFace.instance 存在，准备注册");
        }

        base.Awake();
        Debug.Log($"[GameRecordRequest] 注册完成: requestcode={requestcode}, actioncode={actioncode}");
    }

    /// <summary>查询当前用户最近50条对局记录</summary>
    public void QueryList()
    {
        MainPack pack = new MainPack();
        pack.Requestcode = RequestCode.Game;
        pack.Actioncode = ActionCode.QueryRoom;
        pack.Gameoverpack = new GameOverPack { Reason = 0 };
        Send(pack);
        Debug.Log("[GameRecordRequest] 已发送查询对局列表请求");
    }

    /// <summary>查询单条对局的走棋详情（用于回放）</summary>
    public void QueryDetail(int gameId)
    {
        Debug.Log($"[GameRecordRequest] ========== QueryDetail({gameId}) ==========");

        MainPack pack = new MainPack();
        pack.Requestcode = RequestCode.Game;
        pack.Actioncode = ActionCode.QueryRoom;
        pack.Gameoverpack = new GameOverPack { Reason = gameId };

        // ★调试：检查 pack 是否构造正确
        Debug.Log($"[GameRecordRequest] pack 构造: RequestCode={pack.Requestcode}, ActionCode={pack.Actioncode}, " +
                  $"GameOverPack.Reason={pack.Gameoverpack?.Reason}");

        // ★调试：检查 face 是否可用
        if (face == null)
        {
            Debug.LogError($"[GameRecordRequest] ★★★ face 为空，无法发送！" +
                          " 可能原因：GameFace 不在场景中，或 BaseRequest.Awake() 未成功注册。");
            return;
        }

        Send(pack);
        Debug.Log($"[GameRecordRequest] Send() 已调用，请求已发送到服务端，等待响应...");
    }

    public override void OnResponse(MainPack mainPack)
    {
        Debug.Log("[GameRecordRequest] ========== OnResponse 收到服务端响应 ==========");
        Debug.Log($"[GameRecordRequest] ReturnCode={mainPack.Returncode}, ActionCode={mainPack.Actioncode}");

        if (mainPack.Returncode != ReturnCode.Success)
        {
            Debug.LogWarning($"[GameRecordRequest] ★ 查询失败！ReturnCode={mainPack.Returncode}");
            base.OnResponse(mainPack);
            return;
        }

        int reason = mainPack.Gameoverpack?.Reason ?? 0;
        string message = mainPack.Gameoverpack?.Message ?? "";

        Debug.Log($"[GameRecordRequest] reason={reason}, message.Length={message.Length}");
        Debug.Log($"[GameRecordRequest] message 内容（前200字符）: {message.Substring(0, Mathf.Min(200, message.Length))}");

        if (reason == 0)
        {
            // 列表响应
            List<GameRecordInfo> records = ParseRecordList(message);
            Debug.Log($"[GameRecordRequest] 解析列表完成: {records.Count} 条记录");
            OnListResponse?.Invoke(records);
        }
        else
        {
            // 详情响应
            GameRecordDetail detail = ParseRecordDetail(message);
            if (detail != null)
            {
                Debug.Log($"[GameRecordRequest] 解析详情完成: {detail.HostName} vs {detail.GuestName}, " +
                          $"{detail.Moves.Count} 步走棋");
                OnDetailResponse?.Invoke(detail);
            }
            else
            {
                Debug.LogError($"[GameRecordRequest] ★★★ ParseRecordDetail 返回 null！" +
                              $" message={message}");
                OnDetailResponse?.Invoke(null);
            }
        }

        base.OnResponse(mainPack);
    }

    // ════════════════════════════════════════════════════════════════
    //  回调（UI 脚本注册）
    // ════════════════════════════════════════════════════════════════

    /// <summary>对局列表响应回调</summary>
    public Action<List<GameRecordInfo>> OnListResponse;

    /// <summary>对局详情响应回调</summary>
    public Action<GameRecordDetail> OnDetailResponse;

    // ════════════════════════════════════════════════════════════════
    //  数据结构
    // ════════════════════════════════════════════════════════════════

    /// <summary>对局记录摘要（列表用）</summary>
    public class GameRecordInfo
    {
        public int GameId;
        public string HostName;    // 红方
        public string GuestName;   // 黑方
        public int Winner;         // 1=红方胜, 2=黑方胜, 0=平局
        public int EndReason;       // 1=将死, 2=困毙, 3=断线, 4=投降
        public string EndTime;     // 结束时间

        public string WinnerText
        {
            get
            {
                if (Winner == 1) return "红方胜";
                if (Winner == 2) return "黑方胜";
                return "平局";
            }
        }

        public string ReasonText
        {
            get
            {
                switch (EndReason)
                {
                    case 1: return "将死";
                    case 2: return "困毙";
                    case 3: return "断线";
                    case 4: return "投降";
                    default: return "未知";
                }
            }
        }
    }

    /// <summary>对局详情（回放用）</summary>
    public class GameRecordDetail
    {
        public string HostName;
        public string GuestName;
        public int Winner;
        public int EndReason;
        public List<MoveData> Moves = new List<MoveData>();
    }

    /// <summary>单步走棋数据</summary>
    public class MoveData
    {
        public int FromX, FromY, ToX, ToY, ChessId, CaptureId, PlayerColor;
    }

    // ════════════════════════════════════════════════════════════════
    //  解析方法
    // ════════════════════════════════════════════════════════════════

    /// <summary>解析对局列表: "id|host|guest|winner|reason|date;..."</summary>
    private List<GameRecordInfo> ParseRecordList(string data)
    {
        var result = new List<GameRecordInfo>();
        if (string.IsNullOrEmpty(data)) return result;

        string[] records = data.Split(';');
        foreach (string r in records)
        {
            if (string.IsNullOrEmpty(r)) continue;
            string[] f = r.Split('|');
            if (f.Length < 6) continue;

            int.TryParse(f[0], out int id);
            int.TryParse(f[3], out int winner);
            int.TryParse(f[4], out int reason);

            result.Add(new GameRecordInfo
            {
                GameId = id,
                HostName = f[1],
                GuestName = f[2],
                Winner = winner,
                EndReason = reason,
                EndTime = f[5]
            });
        }
        return result;
    }

    /// <summary>解析对局详情: "host|guest|winner|reason|f3,4t5,4;f5,2t4,3;..."</summary>
    private GameRecordDetail ParseRecordDetail(string data)
    {
        Debug.Log($"[GameRecordRequest] ParseRecordDetail 输入长度={data?.Length ?? -1}");

        if (string.IsNullOrEmpty(data))
        {
            Debug.LogError("[GameRecordRequest] ★ ParseRecordDetail: data 为空！");
            return null;
        }

        string[] parts = data.Split('|');
        Debug.Log($"[GameRecordRequest] Split('|') 得到 {parts.Length} 段");
        for (int i = 0; i < parts.Length; i++)
        {
            Debug.Log($"[GameRecordRequest]   parts[{i}]={parts[i].Substring(0, Mathf.Min(80, parts[i].Length))}");
        }

        if (parts.Length < 5)
        {
            Debug.LogError($"[GameRecordRequest] ★ ParseRecordDetail: parts.Length={parts.Length} < 5，格式不对！");
            return null;
        }

        var detail = new GameRecordDetail
        {
            HostName = parts[0],
            GuestName = parts[1],
            Winner = int.TryParse(parts[2], out int w) ? w : 0,
            EndReason = int.TryParse(parts[3], out int r) ? r : 0
        };

        // parts[4] = 走棋序列 "f3,4t5,4;f5,2t4,3;..."
        string moveStr = parts[4];
        string[] moves = moveStr.Split(';');
        Debug.Log($"[GameRecordRequest] 走棋序列 Split(';') 得到 {moves.Length} 段");

        int parsed = 0, skipped = 0;
        foreach (string m in moves)
        {
            if (string.IsNullOrEmpty(m)) { skipped++; continue; }
            string[] f = m.Split(',');
            if (f.Length < 7) { skipped++; continue; }

            detail.Moves.Add(new MoveData
            {
                FromX = int.TryParse(f[0], out int v0) ? v0 : 0,
                FromY = int.TryParse(f[1], out int v1) ? v1 : 0,
                ToX = int.TryParse(f[2], out int v2) ? v2 : 0,
                ToY = int.TryParse(f[3], out int v3) ? v3 : 0,
                ChessId = int.TryParse(f[4], out int v4) ? v4 : 0,
                CaptureId = int.TryParse(f[5], out int v5) ? v5 : -1,
                PlayerColor = int.TryParse(f[6], out int v6) ? v6 : 0
            });
            parsed++;
        }

        Debug.Log($"[GameRecordRequest] 解析走棋: 成功={parsed}, 跳过={skipped}");

        return detail;
    }
}
