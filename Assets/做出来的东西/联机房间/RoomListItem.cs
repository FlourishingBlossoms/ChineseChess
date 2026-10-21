/* ════════════════════════════════════════════════════════════════════
 *  RoomListItem.cs  —  房间列表项UI控制  ★新增
 * ════════════════════════════════════════════════════════════════════
 *
 *  【这个脚本干什么？】
 *      挂在"房间列表项预制体"上。每个预制体实例显示一个房间的信息。
 *      玩家点击"加入"按钮时，通知 RoomUIControl 发送加入请求。
 *
 *  【预制体结构】
 *      房间列表项 (GameObject，挂本脚本)
 *        ├─ 房间名Text (TMP_Text 或 Text)     → 显示房间名
 *        ├─ 房主名Text (TMP_Text 或 Text)     → 显示房主名
 *        ├─ 房间号Text (TMP_Text 或 Text)     → 显示房间号
 *        └─ 加入按钮 (Button)                 → 点击加入
 *
 *  【Inspector 拖拽】
 *      roomNameText  → 显示房间名的文本组件
 *      hostNameText  → 显示房主名的文本组件
 *      roomIdText    → 显示房间号的文本组件(可选)
 *      joinButton    → "加入"按钮
 * ════════════════════════════════════════════════════════════════════ */

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SocketGameProtocol;

public class RoomListItem : MonoBehaviour
{
    [Header("UI 组件引用")]
    public TMP_Text roomNameText;   // 显示房间名
    public TMP_Text hostNameText;   // 显示房主名
    public TMP_Text roomIdText;     // 显示房间号(可选)
    public Button joinButton;       // "加入"按钮

    // 当前房间数据
    private RoomPack roomData;
    // 加入房间回调
    private System.Action<int> onJoinClicked;

    /// <summary>
    /// 初始化列表项数据(由 RoomUIControl 调用)
    /// </summary>
    /// <param name="room">房间数据</param>
    /// <param name="joinCallback">点击加入时的回调，参数为 roomId</param>
    public void Setup(RoomPack room, System.Action<int> joinCallback)
    {
        roomData = room;
        onJoinClicked = joinCallback;

        // 填充 UI
        if (roomNameText != null) roomNameText.text = room.RoomName;
        if (hostNameText != null) hostNameText.text = "房主: " + room.HostName;
        if (roomIdText != null) roomIdText.text = "#" + room.RoomId;

        // 绑定按钮
        if (joinButton != null)
        {
            joinButton.onClick.RemoveAllListeners();
            joinButton.onClick.AddListener(OnJoinButtonClicked);
        }
    }

    private void OnJoinButtonClicked()
    {
        if (roomData != null && onJoinClicked != null)
        {
            Debug.Log($"[RoomListItem] 点击加入房间: {roomData.RoomName} (ID={roomData.RoomId})");
            onJoinClicked.Invoke(roomData.RoomId);
        }
    }
}
