using UnityEngine;

public class 移动 : MonoBehaviour
{
    private float Speed = 5f;
    public const float DefaultSpeed = 0.1f;
    private Vector3 MoveInput;
    private Rigidbody2D rb;

    // Start is called before the first frame update
    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        Speed = DefaultSpeed;
    }

    // Update is called once per frame
    private void Update()
    {
        if (Input.GetKey(KeyCode.LeftShift))
        {
            Speed = DefaultSpeed / 1.5f;
        }
        else
        {
            Speed = DefaultSpeed;
        }
    }

    private void FixedUpdate()
    {
        MoveInput.x = Input.GetAxisRaw("Horizontal");
        MoveInput.y = Input.GetAxisRaw("Vertical");
        MoveInput.Normalize();
        rb.MovePosition(rb.position + (Vector2)MoveInput * Speed);
    }
}