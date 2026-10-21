using System.Collections;
using UnityEngine;

// 自机血量与复活 类，用于管理自机的血量和复活逻辑
public class 自机血量与复活 : MonoBehaviour
{
    // 单例模式，确保游戏中只有一个自机血量与复活实例
    public static 自机血量与复活 Instance { get; private set; }

    // 自机预制体，用于实例化新的自机
    public GameObject 妖梦A;
    public GameObject 妖梦B;
    public GameObject 霖之助A;
    public GameObject 霖之助B;

    // 当前存在的自机实例
    private GameObject 自机实例;

    public GameObject 复活点;

    // 状态变量
    // 标记自机是否正在移动
    private bool 正在移动 = false;

    // 标记自机是否处于无敌状态
    private bool 处于无敌状态 = false;

    // 移动参数
    // 自机的移动速度
    private readonly float 移动速度 = 3f;

    // 自机移动的目标Y坐标
    private readonly float 目标Y = -4f;

    // 允许的移动误差范围
    private readonly float 容差 = 0.01f;

    // 无敌参数
    // 无敌状态持续时间
    private float 无敌时间 = 3f;

    // 每秒闪烁次数
    private float 闪烁频率 = 10f;

    // 引用
    // 自机实例的SpriteRenderer组件，用于改变自机的透明度
    private SpriteRenderer spriteRenderer;

    // 无敌状态协程引用，用于停止协程
    private Coroutine 无敌协程;

    // 游戏开始时调用，设置单例实例
    private int charID; 
    private int introID;
    private GameObject 场上现有的自机;
    private void Awake()
    {
        Instance = this;
         charID = PlayerPrefs.GetInt("SelectedCharacter", 0); // 0是默认值
        introID = PlayerPrefs.GetInt("SelectedIntro", 1);


    }

    private void Start()
    {
        // 1. 尝试在场景中寻找标签为"自机"的物体
         场上现有的自机 = GameObject.FindWithTag("自机");

        // 2. 如果找到了，就把它赋值给脚本管理的变量，这样脚本就不会再生成了
        if (场上现有的自机 != null)
        {
            Destroy(场上现有的自机); // 销毁现有的自机
        }
// 3. 实例化新的自机
        if(charID == 0 && introID == 0)
        {
            自机实例 = Instantiate(妖梦A, Vector3.zero, Quaternion.identity);
            场上现有的自机= 自机实例;
        }
        else if(charID == 0 && introID == 1)
        {
            自机实例 = Instantiate(妖梦B, Vector3.zero, Quaternion.identity);
            场上现有的自机 = 自机实例;

        }
        else if(charID == 1 && introID == 0)
        {
            自机实例 = Instantiate(霖之助A, Vector3.zero, Quaternion.identity);
            场上现有的自机 = 自机实例;

        }
        else if(charID == 1 && introID == 1)
        {
            自机实例 = Instantiate(霖之助B, Vector3.zero, Quaternion.identity);
            场上现有的自机 = 自机实例;

        }
    }

    // 每帧调用，管理自机实例和处理移动逻辑
    private void Update()
    {
        // 1. 自机实例管理
        管理自机实例();

        // 2. 处理移动逻辑
        if (正在移动 && 自机实例 != null)
        {
            处理移动();
        }
    }

    // 管理自机实例，判断自机是否需要重生
    private void 管理自机实例()
    {
        // 如果自机不存在且可以重生，并且当前不在移动状态
        if (自机实例 == null)
        {
            // 【判定点 1】如果残机大于0，就复活
            if (玩家资源管理.instance.Heart > 0 && !正在移动)
            {
                重生自机();
            }
            // 【判定点 2】如果残机归零了（刚才你说到的“停止生成”就是因为这里条件不满足）
            // 我们需要在这里手动触发死亡信号
            else if (玩家资源管理.instance.Heart == 0)
            {
                // 将残机设为 -1，以此去触发 死亡检查.cs 的逻辑
                玩家资源管理.instance.Heart = -1;
            }
        }
    }

    // 重生自机，实例化新的自机并设置相关属性
    private void 重生自机()
// 减少残机数
    {
        玩家资源管理.instance.Heart -= 1;
        // 创建新自机实例    
        自机实例 = Instantiate(场上现有的自机, 复活点.transform.position, Quaternion.identity);
        正在移动 = true;

        // 获取渲染器组件
        spriteRenderer = 自机实例.GetComponentInChildren<SpriteRenderer>();

        
    

        // 启动无敌状态协程
        if (无敌协程 != null)
        {
            StopCoroutine(无敌协程);
        }
        无敌协程 = StartCoroutine(无敌状态协程());
    }

    // 处理自机移动，使其向目标Y坐标移动
    private void 处理移动()
    {
        // 使用Mathf.MoveTowards函数将自机实例的Y坐标移动到目标Y坐标
        float 新Y = Mathf.MoveTowards(自机实例.transform.position.y, 目标Y, 移动速度 * Time.deltaTime);
        自机实例.transform.position = new Vector3(transform.position.x, 新Y, transform.position.z);

        // 如果自机实例的Y坐标接近目标Y坐标（在容差范围内），停止移动
        if (Mathf.Abs(自机实例.transform.position.y - 目标Y) < 容差)
        {
            正在移动 = false;
        }
    }

    // 无敌状态协程，处理自机无敌期间的闪烁效果
    private IEnumerator 无敌状态协程()
    {
        处于无敌状态 = true;

        // 安全获取SpriteRenderer引用
        if (自机实例 == null || spriteRenderer == null)
        {
            Debug.LogWarning("无敌状态启动失败：自机实例或渲染器为空");
            yield break; // 提前终止协程
        }

        // 设置自机实例为无敌状态（通过改变标签）
        自机实例.tag = "无敌";
        Debug.Log("进入无敌状态");

        float 计时器 = 0f;

        // 无敌期间闪烁（带安全检测）
        while (计时器 < 无敌时间)
        {
            // 关键：每次循环都检查对象是否有效
            if (自机实例 == null || spriteRenderer == null)
            {
                Debug.Log("自机已销毁，提前终止无敌状态");
                yield break; // 安全退出
            }

            // 计算闪烁效果，使用Mathf.PingPong函数在透明度0.2和1之间变化
            float alpha = Mathf.PingPong(Time.time * 闪烁频率, 1f);
            spriteRenderer.color = new Color(1, 1, 1, 0.2f + alpha * 0.8f);

            计时器 += Time.deltaTime;
            yield return null; // 等待下一帧
        }

        // 恢复状态（带安全检测）
        if (自机实例 != null && spriteRenderer != null)
        {
            spriteRenderer.color = Color.white; // 恢复白色
            自机实例.tag = "自机"; // 恢复自机标签
        }

        处于无敌状态 = false;
        Debug.Log("无敌状态结束");
    }

    // 自机被销毁时调用，重置相关状态和协程
    public void 自机被销毁()
    {
        // 停止所有正在运行的协程
        if (无敌协程 != null)
        {
            StopCoroutine(无敌协程);
            无敌协程 = null;
        }

        // 重置引用
        自机实例 = null;
        正在移动 = false;
        处于无敌状态 = false;

        // 可选：重置渲染器引用
        spriteRenderer = null;

        Debug.Log("自机销毁处理完成");
    }
}