using UnityEngine;

public class 绿子弹 : MonoBehaviour
{
    private float speed = 10f;
    private GameObject Target;
    private Rigidbody2D rb;

    // Start is called before the first frame update
    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        // 在生成时寻找一次敌人，提升效率
        Target = GameObject.FindGameObjectWithTag("敌人");
    }

    // Update is called once per frame
    private void Update()
    {
        if (Target != null)
        {
            // 检查 Y 轴距离是否小于 0.1 (表示几乎在同一水平线)
            if (Mathf.Abs(transform.position.y - Target.transform.position.y) < 0.1f)
            {
                transform.rotation = Quaternion.Euler(0, 0, 0);
                Vector2 direction = (Vector2)(Target.transform.position - transform.position);
                direction.Normalize();
                rb.velocity = direction * speed;
            }
            else
            {
                // 不在同一行，向上飞
                rb.velocity = Vector2.up * speed;
            }
        }
        else
        {
            rb.velocity = Vector2.up * speed;
        }
    }

    private void OnBecameInvisible()
    {
        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.CompareTag("敌人"))
        {
            Destroy(gameObject);
        }
    }
}