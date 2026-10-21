using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class 资源下落 : MonoBehaviour
{
    private float speed = 3f;
    private Rigidbody2D rb;
    // Start is called before the first frame update
    void Start()
    {
        
        rb = GetComponent<Rigidbody2D>();
    }

    // Update is called once per frame
    void Update()
    {
        transform .position += Vector3.down * speed * Time.deltaTime;
    }
    void OnBecameInvisible()
    {
        Destroy(gameObject);
    }
}
