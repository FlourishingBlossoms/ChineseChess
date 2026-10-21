using UnityEngine;

public class 收点与子机与自机的发射和子机数控制 : MonoBehaviour
{
    public GameObject[] 子机 = new GameObject[5];

    // --- 新增：全局射击计时器 ---
    private float greenBulletTimer = 0f;

    private float frogBulletTimer = 0f;
    private float blueBulletTimer = 0f;
    public GameObject Bluebullet;
    public GameObject 自机炮位1;
    public GameObject 自机炮位2;

    // 对应原脚本中的间隔时间常量
    private const float GreenInterval = 1.0f;

    private const float FrogInterval = 0.1f;
    private const float BlueInterval = 0.05f;

    private void SCAN()
    {
        if (玩家资源管理.instance.Power < 2)
        {
            子机[0].SetActive(true);
            子机[1].SetActive(false);
            子机[2].SetActive(false);
            子机[3].SetActive(false);
            子机[4].SetActive(false);
        }
        else if (玩家资源管理.instance.Power < 3 && 玩家资源管理.instance.Power >= 2)
        {
            子机[0].SetActive(false);
            子机[1].SetActive(true);
            子机[2].SetActive(true);
            子机[3].SetActive(false);
            子机[4].SetActive(false);
        }
        else if (玩家资源管理.instance.Power < 4 && 玩家资源管理.instance.Power >= 3)
        {
            子机[0].SetActive(true);
            子机[1].SetActive(false);
            子机[2].SetActive(false);
            子机[3].SetActive(true);
            子机[4].SetActive(true);
        }
        else
        {
            子机[0].SetActive(false);
            子机[1].SetActive(true);
            子机[2].SetActive(true);
            子机[3].SetActive(true);
            子机[4].SetActive(true);
        }
    }

    private void Start()
    {
    }

    private void Update()
    {
        SCAN();
        if (Input.GetKey(KeyCode.Z))
        {
            控制子机同步射击();
            控制自机发射();
        }
    }

    private void 控制自机发射()
    {
        blueBulletTimer -= Time.deltaTime;
        if (blueBulletTimer <= 0)
        {
            Instantiate(Bluebullet, 自机炮位1.transform.position, Quaternion.identity);
            Instantiate(Bluebullet, 自机炮位2.transform.position, Quaternion.identity);
            blueBulletTimer = BlueInterval;
        }
    }

    private void 控制子机同步射击()
    {
        bool isShift = Input.GetKey(KeyCode.LeftShift);

        if (isShift)
        {
            // 青蛙弹计时逻辑
            frogBulletTimer -= Time.deltaTime;
            if (frogBulletTimer <= 0)
            {
                // 命令所有激活的子机发射青蛙弹
                foreach (GameObject op in 子机)
                {
                    if (op != null && op.activeSelf)
                    {
                        子机发射子弹 shooter = op.GetComponent<子机发射子弹>();
                        if (shooter != null) shooter.发射青蛙弹();
                    }
                }
                frogBulletTimer = FrogInterval;
            }
        }
        else
        {
            // 绿弹计时逻辑
            greenBulletTimer -= Time.deltaTime;
            if (greenBulletTimer <= 0)
            {
                // 命令所有激活的子机发射绿弹
                foreach (GameObject op in 子机)
                {
                    if (op != null && op.activeSelf)
                    {
                        子机发射子弹 shooter = op.GetComponent<子机发射子弹>();
                        if (shooter != null) shooter.发射绿弹();
                    }
                }
                greenBulletTimer = GreenInterval;
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.tag == "收点线")
        {
            资源下落[] 所有资源 = FindObjectsByType<资源下落>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Rigidbody2D[] 所有刚体 = new Rigidbody2D[所有资源.Length];
            foreach (Rigidbody2D rb in 所有刚体)
            {
                rb.AddForce(rb.transform.position - this.transform.position * 10);
            }
        }

        if (other.tag == "小P")
        {
            玩家资源管理.instance.Power += 0.01f;
            Destroy(other.gameObject);
        }
        else if (other.tag == "大P")
        {
            玩家资源管理.instance.Power += 0.5f;
            Destroy(other.gameObject);
        }
        else if (other.tag == "满P")
        {
            玩家资源管理.instance.Power = 玩家资源管理.instance.MaxPower;
            Destroy(other.gameObject);
        }
        else if (other.tag == "小残")
        {
            玩家资源管理.instance.Heart += 0.2f;
            Destroy(other.gameObject);
        }
        else if (other.tag == "大残")
        {
            玩家资源管理.instance.Heart += 1f;
            Destroy(other.gameObject);
        }
        else if (other.tag == "小B")
        {
            玩家资源管理.instance.Spell += 0.2f;
            Destroy(other.gameObject);
        }
        else if (other.tag == "大B")
        {
            玩家资源管理.instance.Spell += 1f;
            Destroy(other.gameObject);
        }
    }
}