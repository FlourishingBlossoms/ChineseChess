using UnityEngine;

public class 判定点相关 : MonoBehaviour
{
    private Rigidbody2D rb;
    private SpriteRenderer 自机;
    private SpriteRenderer 判定点;

    // Start is called before the first frame update
    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        判定点 = GetComponent<SpriteRenderer>();
        自机 = transform.parent.gameObject.GetComponent<SpriteRenderer>();
        判定点.sortingOrder = 0;
        自机.sortingOrder = 10;

    }

    // Update is called once per frame
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.LeftShift))
        {
            int temp = 判定点.sortingOrder;
            判定点.sortingOrder = 自机.sortingOrder;
            自机.sortingOrder = temp;
            Debug.Log("排序");
        }
        else if (Input.GetKeyUp(KeyCode.LeftShift))
        {
            int temp = 自机.sortingOrder;
            自机.sortingOrder = 判定点.sortingOrder;
            判定点.sortingOrder = temp;
            Debug.Log("排序2");
        }
    }

    private void OnCollisionEnter2D(Collision2D other)
    {
        if (other.gameObject.tag == "敌人" || other.gameObject.tag == "敌人子弹")
        {
            Destroy(transform.parent.gameObject);//掉血在死亡检查里
        }
    }
}