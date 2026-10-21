using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class 初始界面UI : MonoBehaviour
{
    public GameObject 初始界面;
    public GameObject 选项界面;
    public GameObject EX选项界面;
    public Button StartButten;
    public Button Quit;
    public Button EXtra;
    public Button Options;
    public GameObject 箭头;

    // --- 新增变量 ---
    [Header("动画设置")]
    public float moveSpeed = 10f; // 移动平滑速度，可在Inspector调整
    private Vector3 targetPosition; // 箭头的目标位置

    private bool B3Press = false;
    public int WhichButton = 0; // 记录当前按下的按钮

    public void SCANButton()
    {
        // 键盘输入检测
        if (Input.GetKeyDown("down"))
        {
            WhichButton += 1;
        }
        if (Input.GetKeyDown("up"))
        {
            WhichButton -= 1;
        }

        // 边界限制
        if (WhichButton < 0) WhichButton = 0;
        if (WhichButton > 3) WhichButton = 3;

        // --- 修改点：只计算目标位置，不直接改变位置 ---
        switch (WhichButton)
        {
            case 0:
                targetPosition = new Vector3(-550, -27, 0);
                break;
            case 1:
                targetPosition = new Vector3(-550, -138, 0);
                break;
            case 2:
                targetPosition = new Vector3(-550, -240, 0);
                break;
            case 3:
                targetPosition = new Vector3(-550, -345, 0);
                break;
        }
    }

    // 新增方法：平滑移动逻辑
    void SmoothMoveArrow()
    {
        // 如果当前箭头位置和目标位置距离非常小，就直接吸附（防止永远动不停）
        if (Vector3.Distance(箭头.transform.localPosition, targetPosition) < 0.01f)
        {
            箭头.transform.localPosition = targetPosition;
        }
        else
        {
            // Lerp插值移动：从当前位置，向目标位置，按速度靠近
            箭头.transform.localPosition = Vector3.Lerp(箭头.transform.localPosition, targetPosition, moveSpeed * Time.deltaTime);
        }
    }

    void SCANPress()
    {
        if (Input.GetKeyDown("z"))
        {
            if (WhichButton == 0)
            {
                初始界面.SetActive(false);
                选项界面.SetActive(true);
            }
            else if (WhichButton == 1)
            {
                初始界面.SetActive(false);
                EX选项界面.SetActive(true);
            }
            else if (WhichButton == 2)
            {
                B3Press = true;
            }
            else if (WhichButton == 3)
            {
                Application.Quit(); // 退出游戏
            }
        }
    }

    void Start()
    {
        // --- 初始化：确保一开始箭头就在第一个位置 ---
        targetPosition = new Vector3(-550, -27, 0);
        箭头.transform.localPosition = targetPosition;
    }

    void Update()
    {
        SCANButton();
        SCANPress();
        SmoothMoveArrow(); // --- 每帧执行平滑移动 ---
    }
}
