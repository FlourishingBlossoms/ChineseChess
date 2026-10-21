using SocketGameProtocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LogonPanle : MonoBehaviour
{
    public LogonRequest logonRequest;
    public TMP_InputField username;
    public TMP_InputField password;
    public Button logonButton;

    void Start()
    {
        logonButton.onClick.AddListener(OnLogonButtonClicked);
    }

    private void OnLogonButtonClicked()
    {
        // ★音效：注册按钮点击音
        AudioManager.PlaySfx("click");

        // 修正：使用 string.IsNullOrEmpty 更严谨
        if (string.IsNullOrEmpty(username.text) || string.IsNullOrEmpty(password.text))
        {
            Debug.LogWarning("用户名或密码不能为空");
            return;
        }

        // 修正：添加 logonRequest 空引用检查
        if (logonRequest == null)
        {
            Debug.LogError("logonRequest 未在 Inspector 中赋值！");
            return;
        }

        Debug.Log($"点击注册按钮: username={username.text}");
        logonRequest.SendRequest(username.text, password.text);  // Send logon request
    }
}
