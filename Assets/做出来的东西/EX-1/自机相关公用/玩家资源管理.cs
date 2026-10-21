using UnityEngine;

public class 玩家资源管理 : MonoBehaviour
{
    public static 玩家资源管理 instance { get; private set; }

    public float Heart = 2;
    public float Power = 0;
    public float Spell = 3;
    public float Point = 0;
    public float MaxHeart = 8;
    public float MaxPower = 4;
    public float MaxSpell = 7;

    // Awake：对象创建时调用，用于初始化单例
    private void Awake()
    {

        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject); // 可选：保持对象在场景切换时不被销毁
        }
        else
        {
            Destroy(gameObject); // 若已存在单例，销毁当前对象（避免重复）
        }
    }


    private void Start()
    {
        Heart = 2;
        Spell = 3;
        Power = 1;
        Point = 0;
    }

    private void Update()
    {
    }
}