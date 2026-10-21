using Google.Protobuf;
using SocketGameProtocol;
using System;
using System.Linq;

/// <summary>
/// 客户端消息处理：负责 TCP 粘包/半包处理和 protobuf 序列化/反序列化。
/// 与服务端 Message.cs 逻辑一致。
/// </summary>
public class Message
{
    private byte[] buffer = new byte[8192];
    private int startindex;

    public byte[] Buffer { get { return buffer; } }
    public int StartIndex { get { return startindex; } }
    public int Remsize { get { return buffer.Length - startindex; } }

    public void ReadBuffer(int len, Action<MainPack> HandleResponse)
    {
        startindex += len;

        while (startindex >= 4)
        {
            int count = BitConverter.ToInt32(buffer, 0);
            if (count <= 0 || count > buffer.Length - 4)
            {
                UnityEngine.Debug.LogWarning($"非法包长度: {count}，重置缓冲区");
                startindex = 0;
                break;
            }

            if (startindex >= count + 4)
            {
                MainPack pack = null;
                try
                {
                    pack = MainPack.Parser.ParseFrom(buffer, 4, count);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError("解析 MainPack 失败: " + ex.Message);
                    startindex = 0;
                    break;
                }
                HandleResponse(pack);

                // 移动剩余数据到缓冲区开头
                int remain = startindex - count - 4;
                if (remain > 0)
                {
                    Array.Copy(buffer, count + 4, buffer, 0, remain);
                }
                startindex = remain;
            }
            else
            {
                break;
            }
        }

        // 缓冲区空间不足时扩容
        if (Remsize < 1024 && startindex > 0)
        {
            byte[] newBuffer = new byte[buffer.Length * 2];
            Array.Copy(buffer, 0, newBuffer, 0, startindex);
            buffer = newBuffer;
        }
    }

    public static byte[] PackData(MainPack pack)
    {
        byte[] data = pack.ToByteArray();
        byte[] head = BitConverter.GetBytes(data.Length);
        return head.Concat(data).ToArray();
    }
}
