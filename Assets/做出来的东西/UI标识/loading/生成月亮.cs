using UnityEngine;

public class 生成月亮 : MonoBehaviour
{
    public bool 蓝月亮;
    public bool 红月亮;
    public GameObject _蓝月亮;
    public GameObject _红月亮;
    public float 生成时间;
    public float 计时器;

    private void Start()
    {
        计时器 = 0;
    }

    private void Update()
    {
        计时器 += Time.unscaledDeltaTime;
        if (计时器 >= 生成时间)
        {
            if (蓝月亮 && _蓝月亮 != null)
            {
                // ★修复：生成后作为自身子物体，确保在 Canvas 内正确渲染
                // 原来用 Instantiate(prefab, transform.position, Quaternion.identity)
                // 生成的是无父级的孤立对象，UI 模式下不在 Canvas 内 → 不渲染
                GameObject moon = Instantiate(_蓝月亮, transform);
                moon.transform.localPosition = Vector3.zero;
                moon.transform.localRotation = Quaternion.identity;
                计时器 = 0;
            }
            if (红月亮 && _红月亮 != null)
            {
                GameObject moon = Instantiate(_红月亮, transform);
                moon.transform.localPosition = Vector3.zero;
                moon.transform.localRotation = Quaternion.identity;
                计时器 = 0;
            }
        }
    }
}
