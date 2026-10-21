using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class 选择界面UI : MonoBehaviour
{
    [Header("角色图片")]
    public Image 妖梦图片;
    public Image 霖之助图片;

    [Header("介绍UI (Image或Text)")]
    public Graphic 妖梦介绍1;
    public Graphic 妖梦介绍2;
    public Graphic 霖之助介绍1;
    public Graphic 霖之助介绍2;

    [Header("动画参数")]
    [Range(1f, 20f)] public float lerpSpeed = 8f;    // 移动速度
    [Range(1f, 10f)] public float fadeSpeed = 5f;    // 颜色渐变速度
    [Range(1f, 15f)] public float flipSpeed = 10f;   // 翻转速度

    [Header("场景设置")] // 新增：用于在Inspector设置场景名称
    public string 妖梦场景名 = "GameScene_Youmu";
    public string 霖之助场景名 = "GameScene_Rin";

    [Header("加载动画引用")]
    public GameObject 少女分形; // 拖拽你的加载动画Prefab
    public GameObject 位置1;    // 拖拽生成的位置点

    // 位置常量
    private const float POS_LEFT = -487f;
    private const float POS_RIGHT = 487f;
    private const float HOME_YOUMU = -250f;
    private const float HOME_RIN = 250f;

    // 状态: 1=妖梦, 2=霖之助
    private int currentState = 0;
    private bool isShowingIntro1 = true;

    // 目标位置
    private Vector3 targetPosYoumu;
    private Vector3 targetPosRin;

    // 颜色定义
    private Color colorSelected = Color.white;
    private Color colorUnselected = new Color(50f / 255f, 50f / 255f, 50f / 255f, 0.5f);
    private Color colorHidden = new Color(0, 0, 0, 0);

    // 翻转动画控制
    private bool isFlipping = false;
    private float flipProgress = 0f;
    private Graphic flipOutGraphic;
    private Graphic flipInGraphic;

    // 加载状态锁
    private bool IsLoad = false;

    void Start()
    {
        // --- 1. 初始化位置 ---
        targetPosYoumu = new Vector3(HOME_YOUMU, 妖梦图片.rectTransform.anchoredPosition.y);
        targetPosRin = new Vector3(HOME_RIN, 霖之助图片.rectTransform.anchoredPosition.y);

        // --- 2. 初始化介绍栏状态 ---
        if (妖梦介绍1) { 妖梦介绍1.gameObject.SetActive(true); 妖梦介绍1.transform.localScale = Vector3.one; }
        if (妖梦介绍2) { 妖梦介绍2.gameObject.SetActive(true); 妖梦介绍2.transform.localScale = new Vector3(0, 1, 1); }

        if (霖之助介绍1) { 霖之助介绍1.gameObject.SetActive(true); 霖之助介绍1.transform.localScale = Vector3.one; }
        if (霖之助介绍2) { 霖之助介绍2.gameObject.SetActive(true); 霖之助介绍2.transform.localScale = new Vector3(0, 1, 1); }

        // --- 3. 自动播放妖梦初始动画 ---
        currentState = 1;
        UpdateTargetStates();
    }

    void Update()
    {
        HandleInput();
        UpdateAnimations();
        UpdateFlipAnimation();
    }

    void HandleInput()
    {
        // 如果正在加载，禁止所有输入操作
        if (IsLoad) return;

        // 左右键选择
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            currentState = 1;
            UpdateTargetStates();
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            currentState = 2;
            UpdateTargetStates();
        }

        // 上下键切换介绍 (翻转)
        if (currentState != 0 && !isFlipping)
        {
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                if (isShowingIntro1) StartFlip(true);
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                if (!isShowingIntro1) StartFlip(false);
            }
        }

        // --- Z键确认逻辑修改 ---
        if (Input.GetKeyDown(KeyCode.Z))
        {
            OnZKeyPressed();
        }
    }

    // 设定目标状态 (位置、层级、颜色)
    void UpdateTargetStates()
    {
        if (currentState == 1) // 选中妖梦
        {
            targetPosYoumu = new Vector3(POS_RIGHT, targetPosYoumu.y);
            妖梦图片.color = colorSelected;

            霖之助图片.transform.SetAsFirstSibling();
            妖梦图片.transform.SetAsLastSibling();
            SetGraphicLayer(妖梦介绍1, true); SetGraphicLayer(妖梦介绍2, true);
            SetGraphicLayer(霖之助介绍1, false); SetGraphicLayer(霖之助介绍2, false);

            targetPosRin = new Vector3(HOME_RIN, targetPosRin.y);
            霖之助图片.color = colorUnselected;
        }
        else if (currentState == 2) // 选中霖之助
        {
            targetPosRin = new Vector3(POS_LEFT, targetPosRin.y);
            霖之助图片.color = colorSelected;

            妖梦图片.transform.SetAsFirstSibling();
            霖之助图片.transform.SetAsLastSibling();
            SetGraphicLayer(霖之助介绍1, true); SetGraphicLayer(霖之助介绍2, true);
            SetGraphicLayer(妖梦介绍1, false); SetGraphicLayer(妖梦介绍2, false);

            targetPosYoumu = new Vector3(HOME_YOUMU, targetPosYoumu.y);
            妖梦图片.color = colorUnselected;
        }
    }

    void SetGraphicLayer(Graphic graphic, bool isSelected)
    {
        if (graphic != null)
        {
            if (isSelected) graphic.transform.SetAsLastSibling();
            else graphic.transform.SetAsFirstSibling();
        }
    }

    void UpdateAnimations()
    {
        float t = Time.deltaTime * lerpSpeed;
        float ct = Time.deltaTime * fadeSpeed;

        妖梦图片.rectTransform.anchoredPosition = Vector3.Lerp(妖梦图片.rectTransform.anchoredPosition, targetPosYoumu, t);
        霖之助图片.rectTransform.anchoredPosition = Vector3.Lerp(霖之助图片.rectTransform.anchoredPosition, targetPosRin, t);

        if (currentState == 1)
        {
            ProcessIntro(妖梦介绍1, POS_LEFT, true, ct, t);
            ProcessIntro(妖梦介绍2, POS_LEFT, true, ct, t);
            ProcessIntro(霖之助介绍1, 0, false, ct, t);
            ProcessIntro(霖之助介绍2, 0, false, ct, t);
        }
        else if (currentState == 2)
        {
            ProcessIntro(霖之助介绍1, POS_RIGHT, true, ct, t);
            ProcessIntro(霖之助介绍2, POS_RIGHT, true, ct, t);
            ProcessIntro(妖梦介绍1, 0, false, ct, t);
            ProcessIntro(妖梦介绍2, 0, false, ct, t);
        }
    }

    void ProcessIntro(Graphic intro, float targetX, bool isSelected, float colorT, float posT)
    {
        if (intro == null) return;

        if (isSelected)
        {
            Vector2 currentPos = intro.rectTransform.anchoredPosition;
            intro.rectTransform.anchoredPosition = Vector2.Lerp(currentPos, new Vector2(targetX, currentPos.y), posT);
        }

        if (isFlipping && (intro == flipInGraphic || intro == flipOutGraphic)) return;

        Color targetColor = isSelected ? colorSelected : colorHidden;
        intro.color = Color.Lerp(intro.color, targetColor, colorT);
    }

    // --- 翻转逻辑 ---
    void StartFlip(bool switchToIntro2)
    {
        isFlipping = true;
        flipProgress = 0f;
        isShowingIntro1 = !switchToIntro2;

        if (currentState == 1)
        {
            flipOutGraphic = switchToIntro2 ? 妖梦介绍1 : 妖梦介绍2;
            flipInGraphic = switchToIntro2 ? 妖梦介绍2 : 妖梦介绍1;
        }
        else
        {
            flipOutGraphic = switchToIntro2 ? 霖之助介绍1 : 霖之助介绍2;
            flipInGraphic = switchToIntro2 ? 霖之助介绍2 : 霖之助介绍1;
        }

        if (flipInGraphic)
        {
            flipInGraphic.gameObject.SetActive(true);
            flipInGraphic.transform.localScale = new Vector3(0, 1, 1);
            flipInGraphic.color = colorSelected;
        }
        if (flipOutGraphic)
        {
            flipOutGraphic.color = colorSelected;
        }
    }

    void UpdateFlipAnimation()
    {
        if (!isFlipping) return;

        flipProgress += Time.deltaTime * flipSpeed;

        if (flipProgress < 1f)
        {
            float scale = Mathf.Lerp(1f, 0f, flipProgress);
            if (flipOutGraphic) flipOutGraphic.transform.localScale = new Vector3(scale, 1, 1);
        }
        else
        {
            float scale = Mathf.Lerp(0f, 1f, flipProgress - 1f);
            if (flipInGraphic) flipInGraphic.transform.localScale = new Vector3(scale, 1, 1);

            if (flipProgress >= 2f)
            {
                isFlipping = false;
                if (flipOutGraphic) flipOutGraphic.gameObject.SetActive(false);
                if (flipInGraphic) flipInGraphic.transform.localScale = Vector3.one;
            }
        }
    }

    // --- 修改后的 Z键逻辑 ---
    public void OnZKeyPressed()
    {
        // 防止重复触发
        if (IsLoad) return;

        // 根据当前选择决定加载哪个场景
        string targetScene = "游戏界面";
        //if (currentState == 1)
        //{
        //    targetScene = 妖梦场景名;
        //    Debug.Log("确认选择：妖梦，准备加载场景：" + targetScene);
        //}
        //else if (currentState == 2)
        //{
        //    targetScene = 霖之助场景名;
        //    Debug.Log("确认选择：霖之助，准备加载场景：" + targetScene);
        //}

        // 如果场景名不为空，启动协程
        if (!string.IsNullOrEmpty(targetScene))
        {
            StartCoroutine(Loading(targetScene));
        }
    }

    // --- 您提供的加载协程 (逻辑保持不变，加入了状态锁控制) ---
    public IEnumerator Loading(string sceneName)
    {
        Debug.Log("开始加载流程...");
        if (IsLoad) yield break; // 双重保险
        IsLoad = true; // 锁定状态，禁止其他输入

        // 禁用UI交互，防止误触
        CanvasGroup canvas = GetComponent<CanvasGroup>();
        if (canvas != null)
        {
            canvas.blocksRaycasts = false;
            // 可选：如果你想淡出界面，可以在这里加代码
            // canvas.alpha = 0; 
        }

        Debug.Log("UI已隐藏，生成Loading动画...");
        if (少女分形 != null && 位置1 != null)
        {
            Instantiate(少女分形, 位置1.transform.position, Quaternion.identity);
        }
        else
        {
            Debug.LogWarning("少女分形 或 位置1 未赋值，跳过生成动画步骤。");
        }

        Debug.Log("等待2秒...");
        yield return new WaitForSecondsRealtime(2.0f);
        // ================= 新增：保存数据到本地 =================
        // 1. 保存选择的角色 (1代表妖梦，2代表霖之助)
        PlayerPrefs.SetInt("SelectedCharacter", currentState);
        // 2. 保存当前的介绍页 (true代表介绍1，false代表介绍2。这里转成int：1和0)
        PlayerPrefs.SetInt("SelectedIntro", isShowingIntro1 ? 1 : 0);
        PlayerPrefs.Save(); // 立即写入
                            // =======================================================
        Debug.Log("开始加载场景: " + sceneName);
        SceneManager.LoadScene(sceneName);

        // 场景加载后，当前物体会被销毁，IsLoad重置其实没必要，但为了规范保留
        IsLoad = false;
    }
}
