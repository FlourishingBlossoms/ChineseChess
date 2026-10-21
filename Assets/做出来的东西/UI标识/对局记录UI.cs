/* ════════════════════════════════════════════════════════════════════
 *  对局记录UI.cs  —  对局记录列表面板控制器（分场景版·仅列表）
 * ════════════════════════════════════════════════════════════════════
 *
 *  【这个脚本干什么？】
 *      查询当前用户的历史对局列表 → 显示在 Scroll View 里
 *      用户点击某条记录 → 存 gameId 到 PlayerPrefs → 加载回放场景
 *
 *  【★修复内容】
 *      去掉 OnEnable 自动查询（时序不可靠），改为 public RefreshList()
 *      由 开始界面UI.OnHistoryReplyClicked 显示面板后主动调用。
 *      去掉所有回放相关字段（回放面板/回放棋盘/上一步/下一步），
 *      回放逻辑移到回放场景的 回放控制UI.cs。
 *
 *  【Inspector 需要拖拽的字段】
 *      recordRequest (GameRecordRequest):  对局记录请求脚本
 *      记录列表容器 (Transform):              ScrollView/Viewport/Content
 *      记录条目预制体 (GameObject):           单条记录的UI预制体
 *
 *  【预制体要求】
 *      记录条目预制体需要有3个 TMP_Text 子物体:
 *        - 名为 "对手名" 的 TMP_Text
 *        - 名为 "结果" 的 TMP_Text  
 *        - 名为 "时间" 的 TMP_Text
 *      预制体自身需要 Button 组件（点击跳转回放）
 *
 *  【场景跳转】
 *      点击记录 → PlayerPrefs.SetInt("ReplayGameId", gameId)
 *               → SceneLoader.LoadScene(回放场景名)
 *
 *  【挂在哪？】
 *      开始界面场景里 "历史记录界面" 面板上
 * ════════════════════════════════════════════════════════════════════ */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class 对局记录UI : MonoBehaviour
{
    [Header("=== 网络请求 ===")]
    public GameRecordRequest recordRequest;

    [Header("=== 列表 UI ===")]
    public Transform 记录列表容器;
    public GameObject 记录条目预制体;

    [Header("=== 场景名 ===")]
    public string 回放场景名 = "回放场景";
    public string 返回场景名 = "开始界面";

    // 内部数据
    private List<GameRecordRequest.GameRecordInfo> recordList = new List<GameRecordRequest.GameRecordInfo>();

    // ════════════════════════════════════════════════════════════════
    //  Awake —— 绑定回调
    // ════════════════════════════════════════════════════════════════

    private void Awake()
    {
        if (recordRequest != null)
        {
            recordRequest.OnListResponse = OnListReceived;
        }
        else
        {
            // 兜底：Inspector 忘拖拽时自动查找
            recordRequest = FindFirstObjectByType<GameRecordRequest>();
            if (recordRequest != null)
            {
                recordRequest.OnListResponse = OnListReceived;
            }
            else
            {
                Debug.LogError("[对局记录UI] 找不到 GameRecordRequest 脚本！请确保场景中已挂载。");
            }
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  ★公开方法：由 开始界面UI 在显示面板后主动调用
    // ════════════════════════════════════════════════════════════════

    public void RefreshList()
    {
        if (recordRequest == null)
        {
            Debug.LogError("[对局记录UI] recordRequest 为空，无法查询");
            return;
        }

        if (GameFace.instance == null || !GameFace.instance.IsConnected())
        {
            Debug.LogWarning("[对局记录UI] 未连接服务器，0.5秒后重试");
            StartCoroutine(RetryQuery());
            return;
        }

        // 确保回调已绑定
        recordRequest.OnListResponse = OnListReceived;

        Debug.Log("[对局记录UI] 开始查询对局列表");
        recordRequest.QueryList();
    }

    private System.Collections.IEnumerator RetryQuery()
    {
        yield return new WaitForSeconds(0.5f);
        if (GameFace.instance != null && GameFace.instance.IsConnected())
        {
            Debug.Log("[对局记录UI] 重试查询对局列表");
            recordRequest.QueryList();
        }
        else
        {
            Debug.LogError("[对局记录UI] 服务器未连接，无法查询对局记录");
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  收到列表数据
    // ════════════════════════════════════════════════════════════════

    private void OnListReceived(List<GameRecordRequest.GameRecordInfo> records)
    {
        Debug.Log($"[对局记录UI] 收到 {records.Count} 条对局记录");

        recordList = records;
        PopulateList();
    }

    // ════════════════════════════════════════════════════════════════
    //  填充列表UI
    // ════════════════════════════════════════════════════════════════

    private void PopulateList()
    {
        // 清空旧条目
        if (记录列表容器 != null)
        {
            for (int i = 记录列表容器.childCount - 1; i >= 0; i--)
            {
                Destroy(记录列表容器.GetChild(i).gameObject);
            }
        }
        else
        {
            Debug.LogError("[对局记录UI] 记录列表容器 未赋值！");
            return;
        }

        if (记录条目预制体 == null)
        {
            Debug.LogError("[对局记录UI] 记录条目预制体 未赋值！");
            return;
        }

        if (recordList.Count == 0)
        {
            Debug.Log("[对局记录UI] 暂无对局记录");
            return;
        }

        // 获取预制体高度
        float itemHeight = 80f;
        RectTransform prefabRt = 记录条目预制体.GetComponent<RectTransform>();
        if (prefabRt != null && prefabRt.rect.height > 0)
            itemHeight = prefabRt.rect.height;
        float spacing = 10f; // 条目间距

        // 检查容器是否有 VerticalLayoutGroup
        bool hasLayout = 记录列表容器.GetComponent<UnityEngine.UI.VerticalLayoutGroup>() != null;

        // 逐条创建
        for (int i = 0; i < recordList.Count; i++)
        {
            var record = recordList[i];
            GameObject item = Instantiate(记录条目预制体, 记录列表容器);
            item.transform.localScale = Vector3.one;

            RectTransform rt = item.GetComponent<RectTransform>();
            if (rt == null) rt = item.AddComponent<RectTransform>();

            if (!hasLayout)
            {
                // 手动排列：顶部对齐，左对齐，占满宽度
                rt.anchorMin = new Vector2(0f, 1f);   // 左上角
                rt.anchorMax = new Vector2(1f, 1f);    // 右上角（占满宽度）
                rt.pivot = new Vector2(0.5f, 1f);      // 从顶部中心向下排列
                rt.anchoredPosition = new Vector2(0f, -((itemHeight + spacing) * i));
                rt.sizeDelta = new Vector2(0f, itemHeight); // 宽度由 anchor 控制，只设高度
                rt.offsetMin = new Vector2(0f, rt.offsetMin.y); // 左边贴齐
                rt.offsetMax = new Vector2(0f, rt.offsetMax.y); // 右边贴齐
            }
            else
            {
                // 有 VerticalLayoutGroup，自动排列
                rt.localPosition = Vector3.zero;
            }

            // ★自动收集预制体下所有 TMP_Text（含子物体），按 X 坐标排序（从左到右）
            var texts = new System.Collections.Generic.List<TMP_Text>();
            CollectTexts(item.transform, texts);
            texts.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));

            // 组装显示文本
            // 顺序：时间 | 对手名 | 结果
            // 如果预制体有3个文本，按从左到右依次填
            if (texts.Count >= 3)
            {
                texts[0].text = record.EndTime;                                    // 最左：时间
                texts[1].text = record.HostName + " vs " + record.GuestName;       // 中间：对手
                texts[2].text = record.WinnerText;                                  // 最右：结果
            }
            else if (texts.Count == 2)
            {
                texts[0].text = record.HostName + " vs " + record.GuestName;
                texts[1].text = record.WinnerText;
            }
            else if (texts.Count == 1)
            {
                texts[0].text = $"{record.EndTime}  {record.HostName} vs {record.GuestName}  {record.WinnerText}";
            }
            else
            {
                Debug.LogWarning($"[对局记录UI] 预制体上找不到 TMP_Text 子物体！条目 {i}");
            }

            // 绑定点击事件
            var btn = item.GetComponent<Button>();
            if (btn == null) btn = item.AddComponent<Button>();
            int capturedId = record.GameId;
            btn.onClick.AddListener(() => OnRecordClicked(capturedId));

            Debug.Log($"[对局记录UI] 创建条目 {i}: gameId={record.GameId}, " +
                      $"{record.HostName} vs {record.GuestName}, {record.WinnerText}, " +
                      $"文本数={texts.Count}");
        }

        Debug.Log($"[对局记录UI] 列表填充完成，共 {recordList.Count} 条" +
                  (hasLayout ? "（VerticalLayoutGroup）" : "（手动排列）"));
    }

    /// <summary>递归收集所有 TMP_Text（含子物体的子物体）</summary>
    private void CollectTexts(Transform parent, System.Collections.Generic.List<TMP_Text> list)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            var tmp = child.GetComponent<TMP_Text>();
            if (tmp != null) list.Add(tmp);
            // 递归查找子物体的子物体
            CollectTexts(child, list);
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  点击单条记录 → 存gameId → 加载回放场景
    // ════════════════════════════════════════════════════════════════

    private void OnRecordClicked(int gameId)
    {
        Debug.Log($"[对局记录UI] 点击记录 gameId={gameId}，加载回放场景: {回放场景名}");

        PlayerPrefs.SetInt("ReplayGameId", gameId);
        PlayerPrefs.Save();

        SceneLoader.LoadScene(回放场景名);
    }
}
