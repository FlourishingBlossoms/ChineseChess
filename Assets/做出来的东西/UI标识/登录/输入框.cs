/* ════════════════════════════════════════════════════════════════════
 *  输入框.cs (LoginUIControl)  —  登录/注册面板控制器
 * ════════════════════════════════════════════════════════════════════
 *
 *  【这个脚本干什么？】
 *      1. 管理登录/注册两个输入界面的翻转动画
 *      2. 通过网络(LoginRequest/LogonRequest)向服务端发送账号密码
 *      3. 接收服务端响应，更新登录状态
 *      4. 登录成功后触发 OnLoginSuccess 事件(开始界面UI 监听此事件)
 *      5. ★新增：处理 AlreadyLogin 返回码（同账号在其他设备登录时被踢）
 *
 *  【数据流向】
 *      用户输入 → 点击登录 → LoginRequest.SendRequest()
 *        → GameFace.Send() → TCP → 服务端 UserController.Login()
 *        → UserData.Login() 查SQL Server → 返回 Success/Fail
 *        → GameFace.Update() 主线程分发 → RequestManager.HandleResponse()
 *        → LoginRequest.OnResponse() → OnResponseCallback
 *        → 本类 OnLoginResponse() → 设置 CurrentUserId → 触发 OnLoginSuccess
 *
 *  【Inspector 需要拖拽的字段】
 *      输入框(4个 TMP_InputField):  inputAccount / inputPassWord / inputSingAccount / inputSignPassWord
 *      按钮(4个 Button):            登入 / 注册 / 忘记密码 / 想起密码
 *      面板(2个 GameObject):        登录界面 / 注册界面
 *      提示(2个):                   提示信息框 / 提示文本(TMP_Text)
 *      网络请求(2个):               loginRequest / logonRequest (拖入挂了对应脚本的GameObject)
 *
 *  【静态属性(外部脚本查询登录状态用)】
 *      LoginUIControl.CurrentUserId   → int, -1=未登录, >0=已登录
 *      LoginUIControl.CurrentAccount  → string, 当前用户名
 *      LoginUIControl.IsLoggedIn      → bool, 是否已登录
 *      LoginUIControl.Logout()        → 登出方法
 *
 *  【想加新功能从哪里改？】
 *      ▸ 加"记住密码":  在 Start() 里读 PlayerPrefs，填充 inputAccount/inputPassWord
 *      ▸ 加"自动登录":  在 Start() 里检查 PlayerPrefs 有没有保存的token，
 *                       有的话直接调 loginRequest.SendRequest()
 *      ▸ 加"第三方登录": 新建 XxxLoginRequest 继承 BaseRequest，在这里调用
 *      ▸ 修改密码长度限制: OnRegisterButtonClick() 里的 if 判断
 *
 *  【不要碰的地方】
 *      OnLoginResponse / OnLogonResponse 的逻辑结构——它们处理服务端返回
 *      UpdateFlipAnimation() 的动画算法——翻转效果靠这个
 * ════════════════════════════════════════════════════════════════════ */

using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SocketGameProtocol;

public class LoginUIControl : MonoBehaviour
{
    // ==================== 存储数据的变量 ====================
    private string userAccount;        // 登录账号
    private string userPassWord;       // 登录密码
    private string userSingAccount;    // 注册账号
    private string userSignPassWord;   // 注册密码

    // ==================== 输入框绑定 (Inspector 拖拽) ====================
    [SerializeField] private TMP_InputField inputAccount;       // 登录账号输入框
    [SerializeField] private TMP_InputField inputPassWord;     // 登录密码输入框
    [SerializeField] private TMP_InputField inputSingAccount;  // 注册账号输入框
    [SerializeField] private TMP_InputField inputSignPassWord; // 注册密码输入框

    // ==================== 按钮绑定 (Inspector 拖拽) ====================
    public Button 登入;       // 登录按钮
    public Button 注册;       // 注册按钮
    public Button 忘记密码;   // 切换到注册界面(翻转动画)
    public Button 想起密码;   // 切换回登录界面(翻转动画)

    // ==================== 界面翻转设置 (Inspector 拖拽) ====================
    [Header("翻转页面设置")]
    public GameObject 注册界面;
    public GameObject 登录界面;

    [Header("动画参数")]
    [Range(1f, 15f)]
    public float flipSpeed = 10f;  // 翻转速度，越大越快

    // ==================== 提示信息 UI (Inspector 拖拽) ====================
    [Header("提示信息")]
    public GameObject 提示信息框;   // 提示框背景
    public TMP_Text 提示文本;       // 提示文字
    public float 提示显示时长 = 2f; // 提示自动消失时间

    // ==================== 登录成功回调事件 ====================
    [Header("登录成功回调")]
    public UnityEngine.Events.UnityEvent OnLoginSuccess;  // 开始界面UI 会监听这个事件

    // ★修复：静态事件——不依赖 Inspector 拖拽，永远能收到
    public static event System.Action<int, string> OnLoginSuccessStatic;

    // ==================== 网络请求绑定 (Inspector 拖拽) ====================
    [Header("网络请求（拖入场景中挂载了对应脚本的 GameObject）")]
    [SerializeField] private LoginRequest loginRequest;  // 登录请求脚本
    [SerializeField] private LogonRequest logonRequest;  // 注册请求脚本

    // ==================== 内部状态 ====================
    private bool isFlipping = false;      // 是否正在播放翻转动画
    private float flipProgress = 0f;      // 翻转动画进度(0~1)
    private GameObject flipOutObj;        // 翻转中消失的面板
    private GameObject flipInObj;         // 翻转中出现的面板
    private bool isShowingPage1 = true;   // 当前是否显示登录页(true=登录, false=注册)

    private bool isWaiting = false;       // 等待服务器响应中(防止重复点击)

    // ==================== 当前登录用户（静态，供外部脚本查询） ====================
    /// <summary>当前登录用户 ID，-1 表示未登录</summary>
    public static int CurrentUserId { get; private set; } = -1;

    /// <summary>当前登录用户名</summary>
    public static string CurrentAccount { get; private set; } = "";

    /// <summary>是否已登录（便捷查询）</summary>
    public static bool IsLoggedIn => CurrentUserId != -1;

    /// <summary>登出：重置登录状态</summary>
    public static void Logout()
    {
        CurrentUserId = -1;
        CurrentAccount = "";
        Debug.Log("[LoginUIControl] 已登出");
    }

    // ════════════════════════════════════════════════════════════════
    //  生命周期：Start() —— 绑定所有输入框/按钮/网络回调
    // ════════════════════════════════════════════════════════════════
    void Start()
    {
        // ★修复：确保 OnLoginSuccess 不为 null
        if (OnLoginSuccess == null)
            OnLoginSuccess = new UnityEngine.Events.UnityEvent();

        // --- 绑定输入框回调（用户输入完毕时保存到变量） ---
        if (inputAccount != null) inputAccount.onEndEdit.AddListener(OnUserInputAccount);
        if (inputPassWord != null) inputPassWord.onEndEdit.AddListener(OnUserInputPassWord);
        if (inputSingAccount != null) inputSingAccount.onEndEdit.AddListener(OnSignInputAccount);
        if (inputSignPassWord != null) inputSignPassWord.onEndEdit.AddListener(OnSignInputPassWord);

        // --- 绑定按钮回调 ---
        if (登入 != null) 登入.onClick.AddListener(OnLoginButtonClick);
        if (注册 != null) 注册.onClick.AddListener(OnRegisterButtonClick);
        if (忘记密码 != null) 忘记密码.onClick.AddListener(Flip);   // 翻转到注册
        if (想起密码 != null) 想起密码.onClick.AddListener(Flip);   // 翻转回登录

        // ★音效：登录/注册按钮点击音（忘记密码/想起密码的翻转动画自带反馈，不加）
        if (登入 != null) 登入.onClick.AddListener(() => AudioManager.PlaySfx("click"));
        if (注册 != null) 注册.onClick.AddListener(() => AudioManager.PlaySfx("click"));

        // --- 初始显示登录界面，隐藏注册界面 ---
        if (登录界面 != null)
        {
            登录界面.SetActive(true);
            登录界面.transform.localScale = new Vector3(1, 1, 1);
        }
        if (注册界面 != null)
        {
            注册界面.SetActive(false);
            注册界面.transform.localScale = new Vector3(0, 1, 1);  // 压扁，准备翻转出现
        }

        // --- 隐藏提示信息框 ---
        if (提示信息框 != null) 提示信息框.SetActive(false);

        // --- 检查网络请求引用是否已赋值 ---
        if (loginRequest == null)
            Debug.LogError("[LoginUIControl] loginRequest 未在 Inspector 中赋值！");
        if (logonRequest == null)
            Debug.LogError("[LoginUIControl] logonRequest 未在 Inspector 中赋值！");

        // --- 注册网络响应回调 ---
        // 服务端返回结果时，LoginRequest.OnResponse 会调用 OnResponseCallback
        if (loginRequest != null)
            loginRequest.OnResponseCallback = OnLoginResponse;
        if (logonRequest != null)
            logonRequest.OnResponseCallback = OnLogonResponse;

        // ★新增：检查是否被踢下线回来的（GameFace 设了 KickedMessage）
        string kickedMsg = PlayerPrefs.GetString("KickedMessage", "");
        if (!string.IsNullOrEmpty(kickedMsg))
        {
            PlayerPrefs.DeleteKey("KickedMessage");
            PlayerPrefs.Save();
            ShowMessage(kickedMsg);
            Debug.Log("[LoginUIControl] 检测到被踢消息: " + kickedMsg);
        }
    }

    // ★修复：每次面板激活时强制显示登录页（不显示注册页）
    void OnEnable()
    {
        isShowingPage1 = true;
        isFlipping = false;
        flipProgress = 0f;
        isWaiting = false;

        if (登录界面 != null)
        {
            登录界面.SetActive(true);
            登录界面.transform.localScale = new Vector3(1, 1, 1);
        }
        if (注册界面 != null)
        {
            注册界面.SetActive(false);
            注册界面.transform.localScale = new Vector3(0, 1, 1);
        }
    }

    void Update()
    {
        UpdateFlipAnimation();  // 每帧更新翻转动画
    }

    // ════════════════════════════════════════════════════════════════
    //  登录流程
    // ════════════════════════════════════════════════════════════════

    /// <summary>登录按钮回调</summary>
    private void OnLoginButtonClick()
    {
        // 1. 防止重复点击
        if (isWaiting) { ShowMessage("正在处理中，请稍候..."); return; }

        // 2. 输入校验
        if (string.IsNullOrEmpty(userAccount)) { ShowMessage("请输入账号！"); return; }
        if (string.IsNullOrEmpty(userPassWord)) { ShowMessage("请输入密码！"); return; }

        // 3. 检查网络连接
        if (GameFace.instance == null)
        {
            ShowMessage("系统未初始化，请重启游戏！");
            Debug.LogError("[LoginUIControl] GameFace.instance 为空");
            return;
        }
        if (!GameFace.instance.IsConnected())
        {
            // ★修复：被踢/断线后 socket 已关闭 → 发起重连，并自动等待连接建立后继续登录。
            //   ★同时显示重连遮罩（原来自动重连才显示，手动重登路径没有 → 断连期间可随意点击）
            Debug.Log("[LoginUIControl] 连接已断开，发起重连，连接建立后自动继续登录");
            GameFace.instance.ReconnectSocket();
            GameFace.instance.ShowReconnectMask(true);

            isWaiting = true;
            SetButtonsInteractable(false);
            ShowMessage("正在连接服务器，请稍候...");
            StartCoroutine(LoginAfterConnect(userAccount, userPassWord));
            return;
        }

        // 4. 发送登录请求
        isWaiting = true;
        SetButtonsInteractable(false);  // 禁用按钮防止重复点击
        ShowMessage("登录中...");

        // ★新增：保存凭据用于断线重连
        if (GameFace.instance != null)
            GameFace.instance.SaveReconnectCredentials(userAccount, userPassWord);

        loginRequest.SendRequest(userAccount, userPassWord);
    }

    /// <summary>★等待重连建立后自动发送登录（最多等8秒，每0.25秒查一次）</summary>
    private System.Collections.IEnumerator LoginAfterConnect(string acc, string pwd)
    {
        float waited = 0f;
        while (!GameFace.instance.IsConnected() && waited < 8f)
        {
            yield return new WaitForSecondsRealtime(0.25f);
            waited += 0.25f;
        }

        if (!GameFace.instance.IsConnected())
        {
            isWaiting = false;
            SetButtonsInteractable(true);
            GameFace.instance.ShowReconnectMask(false);   // ★隐藏重连遮罩
            ShowMessage("连接服务器失败，请确认服务器已启动");
            Debug.LogError("[LoginUIControl] 等待8秒仍未连上服务器，放弃本次登录");
            yield break;
        }

        Debug.Log("[LoginUIControl] 连接已建立，自动继续登录");
        GameFace.instance.ShowReconnectMask(false);       // ★隐藏重连遮罩（登录响应到达后会正常切换UI）
        ShowMessage("登录中...");
        if (GameFace.instance != null)
            GameFace.instance.SaveReconnectCredentials(acc, pwd);
        loginRequest.SendRequest(acc, pwd);
    }

    /// <summary>登录响应回调（由 LoginRequest.OnResponse 在主线程触发）</summary>
    private void OnLoginResponse(MainPack mainPack)
    {
        isWaiting = false;
        SetButtonsInteractable(true);  // 恢复按钮

        if (mainPack.Returncode == ReturnCode.Success)
        {
            // ✅ 登录成功
            CurrentAccount = userAccount;
            CurrentUserId = mainPack.Loginpack.UserId; 
            Debug.Log($"[LoginUIControl] 登录成功！账号: {userAccount}, ID: {CurrentUserId}");
            ShowMessage("登录成功！");

            // ★修复：触发登录成功事件（两种方式）
            // 方式1: 实例事件（需要 Inspector 拖拽 loginControl）
            if (OnLoginSuccess != null)
            {
                Debug.Log("[LoginUIControl] 触发 OnLoginSuccess 实例事件");
                OnLoginSuccess.Invoke();
            }
            else
            {
                Debug.LogWarning("[LoginUIControl] OnLoginSuccess 事件为空！loginControl 可能未在 Inspector 中赋值");
            }

            // 方式2: 静态事件（不依赖 Inspector，保底机制）
            Debug.Log("[LoginUIControl] 触发 OnLoginSuccessStatic 静态事件");
            OnLoginSuccessStatic?.Invoke(CurrentUserId, CurrentAccount);
        }
        else if (mainPack.Returncode == ReturnCode.AlreadyLogin)
        {
            // ★已由 GameFace.ProcessResponse 拦截处理（回登录界面+重连）
            // 这里只是保底，正常不会走到
            Debug.LogWarning("[LoginUIControl] AlreadyLogin 响应（应由 GameFace 处理）");
        }
        else
        {
            // ❌ 登录失败
            Debug.LogWarning("[LoginUIControl] 登录失败：用户名不存在或密码错误");
            ShowMessage("登录失败，账号或密码错误！");
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  注册流程
    // ════════════════════════════════════════════════════════════════

    /// <summary>注册按钮回调</summary>
    private void OnRegisterButtonClick()
    {
        // 1. 防止重复点击
        if (isWaiting) { ShowMessage("正在处理中，请稍候..."); return; }

        // 2. 输入校验
        if (string.IsNullOrEmpty(userSingAccount)) { ShowMessage("请输入注册账号！"); return; }
        if (string.IsNullOrEmpty(userSignPassWord)) { ShowMessage("请输入注册密码！"); return; }
        if (userSingAccount.Length < 3) { ShowMessage("账号长度不能少于3位！"); return; }
        if (userSignPassWord.Length < 4) { ShowMessage("密码长度不能少于4位！"); return; }

        // 3. 检查网络连接
        if (GameFace.instance == null)
        {
            ShowMessage("系统未初始化，请重启游戏！");
            Debug.LogError("[LoginUIControl] GameFace.instance 为空");
            return;
        }
        if (!GameFace.instance.IsConnected())
        {
            // ★修复：被踢下线后 socket 已被 Disconnect() 关闭，走这里。
            //   GameFace.ReconnectSocket()（之前修过但调用点丢失）负责重建连接。
            //   ★连接已改异步：第一次点击发起连接（不卡界面），连上后再点一次即可登录
            Debug.Log("[LoginUIControl] 连接已断开，尝试重建连接...");
            GameFace.instance.ReconnectSocket();

            ShowMessage("正在连接服务器，请稍候后再次点击登录");
            Debug.LogError("[LoginUIControl] 连接未就绪，已发起重连，请稍候再点");
            return;
        }

        // 4. 发送注册请求
        isWaiting = true;
        SetButtonsInteractable(false);
        ShowMessage("注册中...");
        logonRequest.SendRequest(userSingAccount, userSignPassWord);
    }

    /// <summary>注册响应回调</summary>
    private void OnLogonResponse(MainPack mainPack)
    {
        isWaiting = false;
        SetButtonsInteractable(true);

        if (mainPack.Returncode == ReturnCode.Success)
        {
            // ✅ 注册成功
            Debug.Log($"[LoginUIControl] 注册成功！账号: {userSingAccount}");
            ShowMessage("注册成功！请登录");

            // 清空注册输入框
            if (inputSingAccount != null) inputSingAccount.text = "";
            if (inputSignPassWord != null) inputSignPassWord.text = "";
            userSingAccount = "";
            userSignPassWord = "";

            // 自动翻转回登录界面
            if (isShowingPage1) Flip();
        }
        else
        {
            // ❌ 注册失败
            Debug.LogWarning("[LoginUIControl] 注册失败：用户名可能已存在");
            ShowMessage("注册失败，账号已存在！");
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  UI 辅助方法
    // ════════════════════════════════════════════════════════════════

    /// <summary>启用/禁用登录和注册按钮</summary>
    private void SetButtonsInteractable(bool interactable)
    {
        if (登入 != null) 登入.interactable = interactable;
        if (注册 != null) 注册.interactable = interactable;
    }

    /// <summary>显示提示信息，N秒后自动隐藏</summary>
    private void ShowMessage(string message)
    {
        if (提示文本 != null) 提示文本.text = message;
        if (提示信息框 != null)
        {
            提示信息框.SetActive(true);
            StartCoroutine(HideMessageAfterDelay(提示显示时长));
        }
        Debug.Log($"[LoginUIControl] 提示: {message}");
    }

    private IEnumerator HideMessageAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (提示信息框 != null && !isWaiting) 提示信息框.SetActive(false);
    }

    // ════════════════════════════════════════════════════════════════
    //  翻转动画 —— 登录↔注册 界面切换效果
    // ════════════════════════════════════════════════════════════════

    /// <summary>触发翻转：登录→注册 或 注册→登录</summary>
    public void Flip()
    {
        if (isFlipping) return;  // 动画中不再触发
        isFlipping = true;
        flipProgress = 0f;

        if (isShowingPage1) { flipOutObj = 登录界面; flipInObj = 注册界面; }
        else                { flipOutObj = 注册界面; flipInObj = 登录界面; }

        if (flipOutObj != null) flipOutObj.SetActive(true);
        if (flipInObj != null)
        {
            flipInObj.SetActive(true);
            flipInObj.transform.localScale = new Vector3(0, 1, 1);
        }

        isShowingPage1 = !isShowingPage1;
    }

    /// <summary>每帧更新翻转动画（在 Update 中调用）</summary>
    private void UpdateFlipAnimation()
    {
        if (!isFlipping) return;

        flipProgress += Time.deltaTime * flipSpeed;

        if (flipProgress <= 0.5f)
        {
            // 阶段1：当前面板横向压扁消失
            float scaleX = 1f - flipProgress * 2f;
            if (flipOutObj != null)
                flipOutObj.transform.localScale = new Vector3(Mathf.Max(0, scaleX), 1, 1);
        }
        else
        {
            // 阶段1结束时完全隐藏旧面板
            if (flipOutObj != null && flipProgress >= 0.5f && flipOutObj.activeSelf)
                flipOutObj.transform.localScale = new Vector3(0, 1, 1);

            // 阶段2：新面板横向展开
            float scaleX = (flipProgress - 0.5f) * 2f;
            if (flipInObj != null)
                flipInObj.transform.localScale = new Vector3(Mathf.Min(1, scaleX), 1, 1);
        }

        // 动画完成
        if (flipProgress >= 1f)
        {
            isFlipping = false;
            if (flipOutObj != null) flipOutObj.transform.localScale = new Vector3(0, 1, 1);
            if (flipInObj != null) flipInObj.transform.localScale = new Vector3(1, 1, 1);
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  输入框回调 —— 用户结束编辑时把文本存到变量
    // ════════════════════════════════════════════════════════════════
    private void OnUserInputAccount(string account) { userAccount = account; }
    private void OnUserInputPassWord(string pwd)    { userPassWord = pwd; }
    private void OnSignInputAccount(string account) { userSingAccount = account; }
    private void OnSignInputPassWord(string pwd)    { userSignPassWord = pwd; }

    // ════════════════════════════════════════════════════════════════
    //  清理：OnDestroy() —— 解绑所有回调
    // ════════════════════════════════════════════════════════════════
    void OnDestroy()
    {
        // 解绑输入框回调
        if (inputAccount != null) inputAccount.onEndEdit.RemoveListener(OnUserInputAccount);
        if (inputPassWord != null) inputPassWord.onEndEdit.RemoveListener(OnUserInputPassWord);
        if (inputSingAccount != null) inputSingAccount.onEndEdit.RemoveListener(OnSignInputAccount);
        if (inputSignPassWord != null) inputSignPassWord.onEndEdit.RemoveListener(OnSignInputPassWord);

        // 清除请求回调引用
        if (loginRequest != null) loginRequest.OnResponseCallback = null;
        if (logonRequest != null) logonRequest.OnResponseCallback = null;
    }
}
