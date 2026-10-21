using UnityEngine;

public class 子机发射子弹 : MonoBehaviour
{
    public GameObject Greenbullet;
    public GameObject Frogbullet;
    private Rigidbody2D rb;
    private Vector3 OriginalLocalPosition;
    public float ShiftMoveSpeed = 10f;

    private void Start()
    {
        OriginalLocalPosition = transform.localPosition;
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        Vector3 targetPosition = OriginalLocalPosition;

        if (Input.GetKey(KeyCode.LeftShift))
        {
            targetPosition = OriginalLocalPosition * 0.75f;
        }

        transform.localPosition = Vector3.Lerp(transform.localPosition, targetPosition, Time.deltaTime * ShiftMoveSpeed);
    }

    // --- 新增：由外部调用的发射函数 ---

    public void 发射青蛙弹()
    {
        if (Frogbullet != null)
        {
            Instantiate(Frogbullet, transform.position - new Vector3(0.1f, 0, 0), Quaternion.Euler(0, 0, 90));
            Instantiate(Frogbullet, transform.position + new Vector3(0.1f, 0, 0), Quaternion.Euler(0, 0, 90));
        }
    }

    public void 发射绿弹()
    {
        if (Greenbullet != null)
        {
            Vector2 direction60 = Quaternion.Euler(0, 0, 60) * this.transform.rotation * Vector2.right;
            Instantiate(Greenbullet, transform.position, Quaternion.FromToRotation(Vector2.right, direction60));

            Vector2 direction110 = Quaternion.Euler(0, 0, 110) * this.transform.rotation * Vector2.right;
            Instantiate(Greenbullet, transform.position, Quaternion.FromToRotation(Vector2.right, direction110));
        }
    }
}