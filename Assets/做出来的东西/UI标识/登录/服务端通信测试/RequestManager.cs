using SocketGameProtocol;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 请求管理器：根据 ActionCode 将响应路由到对应的 BaseRequest。
/// 每个 BaseRequest 在 Awake 时注册，在 OnDestroy 时注销。
///
/// ★修复：缓冲无处理器的响应
///   问题：GameOver 在加载界面到达时 GameRequest 还没注册 → 响应被丢弃 → 对手困在游戏场景
///   方案：未找到处理器时存入 pendingResponses，等处理器注册时重放
///
/// ★修复：RemoveRequest 实例比对，防止场景重载时旧实例误删新注册
///   问题：场景重载时，新 GameRecordRequest.Awake 注册后，
///         旧 GameRecordRequest.OnDestroy 调 RemoveRequest 把新的从字典里删了
///   原因：RemoveRequest 只按 ActionCode 盲删，不区分实例
///   方案：RemoveRequest 多收一个 BaseRequest 参数，删之前比 dict[actionCode] == request
/// </summary>
public class RequestManager : BaseManager
{
    public RequestManager(GameFace face) : base(face) { }

    private Dictionary<ActionCode, BaseRequest> requestDict = new Dictionary<ActionCode, BaseRequest>();

    // ★新增：缓冲列表，存尚未有处理器的响应（最多保留30秒）
    private readonly List<PendingResponse> pendingResponses = new List<PendingResponse>();
    private const float PENDING_TIMEOUT = 30f; // 超时自动丢弃

    private class PendingResponse
    {
        public MainPack Pack;
        public float Timestamp;
    }

    public void AddRequest(BaseRequest request)
    {
        if (requestDict.ContainsKey(request.getActionCode))
        {
            Debug.LogWarning($"ActionCode {request.getActionCode} 已注册，覆盖旧请求");
            requestDict[request.getActionCode] = request;
        }
        else
        {
            requestDict.Add(request.getActionCode, request);
        }

        // ★新增：重放所有等待此 ActionCode 的缓冲响应
        ActionCode code = request.getActionCode;
        for (int i = pendingResponses.Count - 1; i >= 0; i--)
        {
            if (pendingResponses[i].Pack.Actioncode == code)
            {
                Debug.Log($"[RequestManager] 重放缓冲响应: {code}（等待了 {Time.realtimeSinceStartup - pendingResponses[i].Timestamp:F1f}s）");
                request.OnResponse(pendingResponses[i].Pack);
                pendingResponses.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// ★修复：注销请求，只删自己（实例比对）
    /// 旧实现只按 ActionCode 盲删，场景重载时旧实例的 OnDestroy
    /// 会把字典里新实例的注册一并清除。现在先比 dict[actionCode] == request，
    /// 不是同一个实例就不删。
    /// </summary>
    public void RemoveRequest(ActionCode actionCode, BaseRequest request)
    {
        if (requestDict.TryGetValue(actionCode, out BaseRequest current))
        {
            if (current == request)
            {
                requestDict.Remove(actionCode);
            }
            // else: 字典里存的已经是新实例了，不删
        }
    }

    /// <summary>
    /// 根据 ActionCode 分发响应到对应的 BaseRequest.OnResponse
    /// ★修复：无处理器时缓冲而非丢弃
    /// </summary>
    public void HandleResponse(MainPack mainPack)
    {
        if (mainPack == null)
        {
            Debug.LogWarning("HandleResponse: mainPack 为空");
            return;
        }

        if (requestDict.TryGetValue(mainPack.Actioncode, out BaseRequest request))
        {
            request.OnResponse(mainPack);
        }
        else
        {
            // ★缓冲：等处理器注册后重放（典型场景：GameOver 在加载界面到达，GameRequest 还没注册）
            pendingResponses.Add(new PendingResponse { Pack = mainPack, Timestamp = Time.realtimeSinceStartup });
            Debug.LogWarning($"[RequestManager] ActionCode {mainPack.Actioncode} 暂无处理器，已缓冲（共{pendingResponses.Count}个待处理）");
        }
    }

    /// <summary>★新增：清理超时的缓冲响应，在 Update 中调用</summary>
    public void UpdatePending()
    {
        if (pendingResponses.Count == 0) return;
        float now = Time.realtimeSinceStartup;
        for (int i = pendingResponses.Count - 1; i >= 0; i--)
        {
            if (now - pendingResponses[i].Timestamp > PENDING_TIMEOUT)
            {
                Debug.LogWarning($"[RequestManager] 丢弃超时缓冲响应: {pendingResponses[i].Pack.Actioncode}");
                pendingResponses.RemoveAt(i);
            }
        }
    }
}
