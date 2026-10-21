/* ════════════════════════════════════════════════════════════════════
 *  FullscreenPanelFixer.cs  —  UI 面板全屏锚点自适应
 * ════════════════════════════════════════════════════════════════════
 *
 *  【作用】
 *      把自身 RectTransform 设为全屏 stretch 锚点，铺满父级 Canvas。
 *      解决固定锚点(0.5,0.5) + 写死 sizeDelta 在非 16:9 分辨率下偏移/溢出的问题。
 *
 *  【使用方法】
 *      1. 在 Unity Editor 里选中目标面板 GameObject（如结算面板、暂停面板、邀请弹窗）
 *      2. Add Component → FullscreenPanelFixer
 *      3. 确保该面板是 Canvas 的直接子物体（stretch 锚点是相对父节点 RectTransform 的）
 *
 *  【推荐挂载的面板】
 *      - 满身疮痍  1.prefab / 满身疮痍  2.prefab / 满身疮痍 .prefab（结算面板）
 *      - 暂停.prefab
 *      - 死了（多人）.prefab（多人结算，当前用 10000×10000 伪全屏，挂上后可去掉超大方块）
 *      - 邀请弹窗 / 拒绝弹窗（游戏结束面板的子物体）
 *
 *  【不适用】
 *      - 棋子/棋盘等世界空间 Sprite（无 RectTransform，不是 UI）
 *      - 本身就需要固定大小和位置的小按钮/小标签
 *
 *  【Editor 菜单】
 *      顶部菜单栏 → 辅助 → 适配全屏(选中对象)
 *      一键把选中 GameObject 设为全屏 stretch，无需挂脚本。
 * ════════════════════════════════════════════════════════════════════ */

using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class FullscreenPanelFixer : MonoBehaviour
{
    // ════════════════════════════════════════════════════════════════
    //  Reset / Awake — 每次挂载或启动时自动应用全屏锚点
    // ════════════════════════════════════════════════════════════════
    private void Reset()
    {
        ApplyStretch();
    }

    private void Awake()
    {
        ApplyStretch();
    }

    // ════════════════════════════════════════════════════════════════
    //  核心逻辑：把 RectTransform 设为全屏 stretch
    // ════════════════════════════════════════════════════════════════
    private void ApplyStretch()
    {
        RectTransform rect = transform as RectTransform;
        if (rect == null)
        {
            Debug.LogWarning("[FullscreenPanelFixer] 此 GameObject 没有 RectTransform，无法适配。");
            return;
        }

        rect.anchorMin = new Vector2(0f, 0f);       // 左下角锚到父节点左下
        rect.anchorMax = new Vector2(1f, 1f);       // 右上角锚到父节点右上
        rect.pivot = new Vector2(0.5f, 0.5f);      // 中心点
        rect.sizeDelta = new Vector2(0f, 0f);        // 宽高由锚点自动撑满
        rect.offsetMin = Vector2.zero;               // 左下偏移清零
        rect.offsetMax = Vector2.zero;               // 右上偏移清零

#if UNITY_EDITOR
        if (!Application.isPlaying)
            EditorUtility.SetDirty(rect);
#endif
    }

#if UNITY_EDITOR
    // ════════════════════════════════════════════════════════════════
    //  Editor 菜单：辅助 → 适配全屏(选中对象)
    //  一键应用，无需挂脚本组件（适合临时快速调整）
    // ════════════════════════════════════════════════════════════════
    [MenuItem("辅助/适配全屏(选中对象)")]
    static void MenuApplyStretchToSelection()
    {
        GameObject[] selection = Selection.gameObjects;
        int count = 0;

        foreach (GameObject go in selection)
        {
            RectTransform rect = go.transform as RectTransform;
            if (rect == null)
            {
                Debug.LogWarning($"[FullscreenPanelFixer] {go.name} 没有 RectTransform，已跳过。");
                continue;
            }

            Undo.RecordObject(rect, "适配全屏");

            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(0f, 0f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            EditorUtility.SetDirty(rect);
            count++;
        }

        if (count > 0)
            Debug.Log($"[FullscreenPanelFixer] 已为 {count} 个对象应用全屏适配。");
        else
            Debug.LogWarning("[FullscreenPanelFixer] 未选中任何带 RectTransform 的对象。");
    }

    // 菜单有效性检查：选中对象有 RectTransform 时才启用
    [MenuItem("辅助/适配全屏(选中对象)", true)]
    static bool MenuValidate()
    {
        foreach (GameObject go in Selection.gameObjects)
        {
            if (go.GetComponent<RectTransform>() != null)
                return true;
        }
        return false;
    }
#endif
}
