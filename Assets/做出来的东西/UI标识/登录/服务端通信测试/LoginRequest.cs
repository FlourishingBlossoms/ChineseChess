using SocketGameProtocol;
using UnityEngine;

/// <summary>
/// 登录请求：发送账号密码到服务器验证，接收登录结果。
/// 使用方式：
///   1. 挂载到场景中的 GameObject
///   2. UI 层通过 Inspector 引用此脚本
///   3. 调用 SendRequest(username, password) 发送请求
///   4. 通过 OnResponseCallback 接收响应
/// </summary>
public class LoginRequest : BaseRequest
{
    public override void Awake()
    {
        requestcode = RequestCode.User;
        actioncode = ActionCode.Login;
        base.Awake();
    }

    /// <summary>
    /// 发送登录请求
    /// </summary>
    public void SendRequest(string username, string password)
    {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            Debug.LogWarning("用户名或密码为空，取消发送登录请求");
            return;
        }

        MainPack mainPack = new MainPack();
        mainPack.Actioncode = actioncode;
        mainPack.Requestcode = requestcode;

        LoginPack loginPack = new LoginPack();
        loginPack.Username = username;
        loginPack.Password = password;
        mainPack.Loginpack = loginPack;

        Debug.Log($"发送登录请求: username={username}");
        Send(mainPack);
    }

    public override void OnResponse(MainPack mainPack)
    {
        switch (mainPack.Returncode)
        {
            case ReturnCode.Success:
                Debug.Log("登录成功 (Login successful)");
                break;
            case ReturnCode.Fail:
                Debug.Log("登录失败 (Login failed) - 用户名不存在或密码错误");
                break;
            default:
                Debug.Log("未知返回码: " + mainPack.Returncode);
                break;
        }

        base.OnResponse(mainPack);
    }
}
