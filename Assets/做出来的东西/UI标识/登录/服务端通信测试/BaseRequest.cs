using SocketGameProtocol;
using UnityEngine;

/// <summary>
/// 请求基类：所有网络请求脚本继承此类。
/// 子类在 Awake 中设置 requestcode 和 actioncode，然后调用 base.Awake()。
/// 响应到达时，RequestManager 调用 OnResponse()。
/// 通过 OnResponseCallback 将结果回调给 UI 层。
/// </summary>
public class BaseRequest : MonoBehaviour
{
    protected RequestCode requestcode;
    protected ActionCode actioncode;
    protected GameFace face;

    public ActionCode getActionCode { get { return actioncode; } }

    /// <summary>
    /// UI 层注册此回调，在响应到达时收到通知
    /// </summary>
    public System.Action<MainPack> OnResponseCallback;

    public virtual void Awake()
    {
        face = GameFace.instance;

        if (face != null)
        {
            face.AddRequest(this);
        }
        else
        {
            Debug.LogError("场景中找不到 GameFace 实例！请确保 GameFace 已挂载并最先执行。");
        }
    }

    /// <summary>
    /// ★修复：注销时传 this，让 RequestManager 做实例比对
    /// 防止场景重载时旧实例的 OnDestroy 把字典里新实例的注册删了
    /// </summary>
    public virtual void OnDestroy()
    {
        if (face != null)
        {
            face.RemoveRequest(actioncode, this);
        }
    }

    public virtual void OnResponse(MainPack mainPack)
    {
        OnResponseCallback?.Invoke(mainPack);
    }

    public virtual void Send(MainPack mainPack)
    {
        if (face != null)
        {
            face.Send(mainPack);
        }
        else
        {
            Debug.LogError("Send 失败: face 为空");
        }
    }
}
