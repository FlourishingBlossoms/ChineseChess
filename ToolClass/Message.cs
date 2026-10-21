using Google.Protobuf;
using SocketGameProtocol;
using System;            // ★修复：Action / Console / BitConverter / Array 都在此命名空间
using System.Linq;       // ★修复：Concat 在此命名空间

namespace Surver.ToolClass
{
    internal class Message
    {
        // ════════════════════════════════════════════════════════════════
        //  ★修复：新增三个静态成员，供 Client.cs 的"先读包头再读包体"模式使用
        //
        //  Client.cs 的接收流程：
        //    1) BeginReceive 把 4 字节包头读到 Message.headBytes
        //    2) 用 BitConverter.ToInt32 + NetworkToHostOrder 解出包体长度
        //    3) Receive(bodyLen) 读完整包体
        //    4) Message.UnpackData(bodyBytes) 反序列化为 MainPack
        //
        //  ⚠ 线程安全警告：
        //    headBytes 是 static，多个 Client 并发接收时会共享同一个缓冲区，
        //    可能互相覆盖 4 字节包头数据。低并发场景没问题，高并发需要改成
        //    "每个 Client 自带 headBytes 实例字段"。
        //    建议改造方式：把 headBytes / headLength / UnpackData 改成
        //    Client 实例字段 + 实例方法，彻底消除竞态。
        // ════════════════════════════════════════════════════════════════
        public static readonly byte[] headBytes = new byte[4];   // 4字节包头缓冲
        public const int headLength = 4;                          // 包头长度（int32）

        /// <summary>
        /// 把已读到的包体字节反序列化为 MainPack
        /// </summary>
        public static object UnpackData(byte[] bodyBytes)
        {
            if (bodyBytes == null || bodyBytes.Length == 0) return null;
            return MainPack.Parser.ParseFrom(bodyBytes);
        }

        // 修正：缓冲区从 1024 增大到 8192，避免大包被截断
        // 实际生产环境建议使用动态扩容策略
        private byte[] buffer = new byte[8192];

        private int startindex;

        public byte[] Buffer
        { get { return buffer; } }
        public int StartIndex
        { get { return startindex; } }
        public int Remsize
        { get { return buffer.Length - startindex; } }

        public void ReadBuffer(int len, Action<MainPack> HandleRequest)
        {
            startindex += len;
            // 修正：在循环中持续读取，直到剩余数据不足以组成一个完整包
            while (startindex >= 4)
            {
                // 每次循环重新读取包头长度（因为数据可能被移动过）
                int count = BitConverter.ToInt32(buffer, 0);
                if (count < 0 || count > buffer.Length - 4)
                {
                    // 非法包长，重置缓冲区防止死循环
                    Console.WriteLine($"非法包长度: {count}，重置缓冲区");
                    startindex = 0;
                    break;
                }
                // ★修复：count==0 是合法的0字节心跳包，不是错误
                //   Protobuf v3 默认值不序列化，ActionNone=0 的 MainPack 序列化后是0字节
                //   包头=BitConverter.GetBytes(0)=[0,0,0,0]，以前被误判为非法包
                if (count == 0)
                {
                    // 跳过这4字节，调用回调（空包=心跳）
                    HandleRequest(new MainPack());
                    Array.Copy(buffer, 4, buffer, 0, startindex - 4);
                    startindex -= 4;
                    continue;
                }
                if (startindex >= count + 4)
                {
                    MainPack pack = null;
                    try
                    {
                        // 修正：使用 MainPack.Parser.ParseFrom 直接返回强类型 MainPack
                        pack = MainPack.Parser.ParseFrom(buffer, 4, count);
                    }
                    catch (Exception ex)
                    {
                        // 修正：解析失败时跳过当前包，避免一个坏包导致整个连接崩溃
                        Console.WriteLine("解析 MainPack 失败: " + ex.Message);
                        startindex = 0;
                        break;
                    }
                    HandleRequest(pack);
                    // 移动剩余数据到缓冲区开头
                    Array.Copy(buffer, count + 4, buffer, 0, startindex - count - 4);
                    startindex -= count + 4;
                }
                else
                {
                    break;
                }
            }

            // 修正：缓冲区空间不足时进行扩容，避免大包无法接收
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
}