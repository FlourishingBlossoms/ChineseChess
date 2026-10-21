using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public class 会跟踪某物体的脚本 : MonoBehaviour
{
    public GameObject Target;
    public string Tag = "敌人";
    public float Force = 10f;
    private Vector3 dir;
    private Rigidbody2D rb;
    // Start is called before the first frame update
    void Start()
    {
        rb=GetComponent<Rigidbody2D>();
        Target = GameObject.FindGameObjectWithTag(Tag);

    }

    // Update is called once per frame
    void Update()
    {
        if (Target == null)
        {
            Target = GameObject.FindGameObjectWithTag(Tag);

        }
        else
        {
            dir = Target.transform.position - transform.position;
            rb.AddForce(dir.normalized * Force);

        }
    }
}
