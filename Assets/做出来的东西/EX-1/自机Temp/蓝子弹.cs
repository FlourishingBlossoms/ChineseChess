using UnityEngine;

public class 蓝子弹 : MonoBehaviour
{
    public float speed = 10f;
    private Rigidbody2D rb;

    // Start is called before the first frame update
    private void Start()
    {
        this.transform.rotation *= Quaternion.Euler(0, 0, 90);
        rb = GetComponent<Rigidbody2D>();
    }

    // Update is called once per frame
    private void Update()
    {
        rb.velocity = transform.right * speed;
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