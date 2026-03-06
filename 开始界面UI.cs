using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class 开始界面UI : MonoBehaviour
{
    public GameObject UI; // 注意：如果脚本挂载在这个UI上，不要SetActive(false)它
    public GameObject 初始界面;
    public GameObject 单人;
    public GameObject 双人;
    public GameObject EX;

    public GameObject 少女分形;
    public GameObject 位置1;

    public Button StartButten;
    public Button Quit;
    public Button EXtra;
    public Button Practice;

    public Button EasyMode;
    public Button NormalMode;
    public Button HardMode;
    public Button LunaticMode;

    private bool isLoading = false;

    private void Start()
    {
        // 绑定按钮
        if (StartButten != null) StartButten.onClick.AddListener(StartSingle);
        if (Quit != null) Quit.onClick.AddListener(QuitGame);
        if (EXtra != null) EXtra.onClick.AddListener(EXtraGame);
        if (Practice != null) Practice.onClick.AddListener(PracticeGame);

        if (EasyMode != null) EasyMode.onClick.AddListener(Easy);
        if (NormalMode != null) NormalMode.onClick.AddListener(Normal);
        if (HardMode != null) HardMode.onClick.AddListener(Hard);
        if (LunaticMode != null) LunaticMode.onClick.AddListener(Lunatic);
    }

    public void StartSingle()
    {
        初始界面.SetActive(false);
        双人.SetActive(true);
    }

    public void PracticeGame()
    {
        初始界面.SetActive(false);
        单人.SetActive(true);
    }

    public void EXtraGame()
    {
        初始界面.SetActive(false);
        Debug.Log("Coming Soon");
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    public void Easy()
    {
        PlayerPrefs.SetString("Difficulty", "Easy");
        StartCoroutine(Loading("游戏界面"));
    }

    public void Normal()
    {
        PlayerPrefs.SetString("Difficulty", "Normal");
        StartCoroutine(Loading("游戏界面"));
    }

    public void Hard()
    {
        PlayerPrefs.SetString("Difficulty", "Hard");
        StartCoroutine(Loading("游戏界面"));
    }

    public void Lunatic()
    {
        PlayerPrefs.SetString("Difficulty", "Lunatic");
        StartCoroutine(Loading("游戏界面"));
    }

    public IEnumerator Loading(string sceneName)
    {
        Debug.Log("开始加载流程...");

        if (isLoading) yield break;
        isLoading = true;

        if (初始界面 != null) 初始界面.SetActive(false);
        if (单人 != null) 单人.SetActive(false);
        if (双人 != null) 双人.SetActive(false);
        if (EX != null) EX.SetActive(false);

        CanvasGroup canvas = GetComponent<CanvasGroup>();
        if (canvas != null) canvas.blocksRaycasts = false;

        Debug.Log("UI已隐藏，生成Loading动画...");

        if (少女分形 != null && 位置1 != null)
        {
            Instantiate(少女分形, 位置1.transform.position, Quaternion.identity);
        }
        Debug.Log("等待2秒...");
        yield return new WaitForSecondsRealtime(2.0f);

        Debug.Log("开始加载场景: " + sceneName);

        SceneManager.LoadScene(sceneName);

        isLoading = false;
    }
}