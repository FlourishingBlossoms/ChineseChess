using GameServer = Surver.Surver.Surver;

namespace Surver
{
    // 修正：命名空间从 Surver.DataBase 改为 Surver，Program 不应放在 DataBase 命名空间下
    internal class Program
    {
        private static void Main(string[] args)
        {
            try
            {
                GameServer surver = new GameServer(8090);
                Console.WriteLine("服务器启动成功，监听端口8090");
                Console.ReadLine(); // 保持程序运行，以便监听客户端连接
            }
            catch (Exception ex)
            {
                // 修正：捕获启动异常，避免程序崩溃且无任何提示
                Console.WriteLine("服务器启动异常: " + ex.Message);
                Console.WriteLine("按任意键退出...");
                Console.ReadKey();
            }
        }
    }
}