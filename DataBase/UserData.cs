using System;
using System.Linq;
using SocketGameProtocol;
using Google.Protobuf;
using Microsoft.Data.SqlClient;

namespace Surver.DataBase
{
    internal class UserData : IDisposable
    {
        private SqlConnection sqlConnection;
        private bool disposed = false;

        // ★ 修正：数据库名改为 sockettese（与 SSMS 中实际数据库名一致）
        // ★ 修正：实例名改为 SQL8080（与 SSMS 标签页 "二色蝶\SQL8080" 一致）
        private string con = "Server=localhost\\SQL8080;Database=sockettese;User Id=sa;Password=Ciallo;TrustServerCertificate=True;";

        public UserData()
        {
            try
            {
                sqlConnection = new SqlConnection(con);
                sqlConnection.Open();
                Console.WriteLine("[DB] SQL Server 数据库连接成功: " + sqlConnection.DataSource + " / " + sqlConnection.Database);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[DB] SQL Server 数据库连接失败: " + ex.Message);
                Console.WriteLine("[DB] 连接字符串: " + con);
                Console.WriteLine("[DB] 请检查:");
                Console.WriteLine("  1. SQL Server 服务是否已启动");
                Console.WriteLine("  2. 实例名是否正确（当前: " + con + ")");
                Console.WriteLine("  3. 账号密码是否正确");
                Console.WriteLine("  4. 是否允许 TCP/IP 连接（SQL Server 配置管理器 → 网络配置 → 协议）");

                try { sqlConnection?.Dispose(); } catch { }
                sqlConnection = null;
            }
        }

        public bool Logon(MainPack pack)
        {
            if (pack == null || pack.Loginpack == null)
            {
                Console.WriteLine("[DB] 注册失败: pack 或 Loginpack 为空");
                return false;
            }

            string username = pack.Loginpack.Username;
            string password = pack.Loginpack.Password;

            Console.WriteLine($"[DB] 开始注册: username={username}");

            try
            {
                if (sqlConnection == null)
                {
                    Console.WriteLine("[DB] 数据库连接未初始化，尝试重新连接...");
                    sqlConnection = new SqlConnection(con);
                }

                if (sqlConnection.State != System.Data.ConnectionState.Open)
                {
                    Console.WriteLine("[DB] 连接未打开，尝试打开...");
                    sqlConnection.Open();
                    Console.WriteLine("[DB] 连接打开成功");
                }

                string checkUserSql = "SELECT COUNT(*) FROM dbo.usertable WHERE username = @username";
                using (SqlCommand cmd = new SqlCommand(checkUserSql, sqlConnection))
                {
                    cmd.Parameters.AddWithValue("@username", username);
                    int count = Convert.ToInt32(cmd.ExecuteScalar());
                    Console.WriteLine($"[DB] 查询到 {count} 条同名记录");
                    if (count > 0)
                    {
                        Console.WriteLine($"[DB] 用户名 {username} 已被注册");
                        return false;
                    }
                }

                string insertSql = "INSERT INTO dbo.usertable (username, password) VALUES (@username, @password)";
                using (SqlCommand cmd = new SqlCommand(insertSql, sqlConnection))
                {
                    cmd.Parameters.AddWithValue("@username", username);
                    cmd.Parameters.AddWithValue("@password", int.Parse(password));
                    int result = cmd.ExecuteNonQuery();
                    Console.WriteLine($"[DB] INSERT 影响 {result} 行");
                    return result > 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[DB] 注册异常: " + ex.GetType().Name + " — " + ex.Message);
                Console.WriteLine("[DB] 堆栈: " + ex.StackTrace?.Substring(0, Math.Min(300, ex.StackTrace.Length)));
                return false;
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  ★新增：Login —— 登录验证
        //  逻辑: SELECT COUNT(*) 匹配用户名+密码
        //  返回: true=登录成功, false=登录失败
        // ════════════════════════════════════════════════════════════════
        public bool Login(MainPack pack)
        {
            if (pack == null || pack.Loginpack == null)
            {
                Console.WriteLine("[DB] 登录失败: pack 或 Loginpack 为空");
                return false;
            }

            string username = pack.Loginpack.Username;
            string password = pack.Loginpack.Password;

            Console.WriteLine($"[DB] 开始登录: username={username}");

            try
            {
                if (sqlConnection == null)
                {
                    Console.WriteLine("[DB] 数据库连接未初始化，尝试重新连接...");
                    sqlConnection = new SqlConnection(con);
                }

                if (sqlConnection.State != System.Data.ConnectionState.Open)
                {
                    Console.WriteLine("[DB] 连接未打开，尝试打开...");
                    sqlConnection.Open();
                    Console.WriteLine("[DB] 连接打开成功");
                }

                // 查询用户名+密码是否匹配
                string loginSql = "SELECT COUNT(*) FROM dbo.usertable WHERE username = @username AND password = @password";
                using (SqlCommand cmd = new SqlCommand(loginSql, sqlConnection))
                {
                    cmd.Parameters.AddWithValue("@username", username);
                    cmd.Parameters.AddWithValue("@password", int.Parse(password));
                    int count = Convert.ToInt32(cmd.ExecuteScalar());
                    Console.WriteLine($"[DB] 登录查询匹配 {count} 条记录");
                    if (count > 0)
                    {
                        Console.WriteLine($"[DB] 用户 {username} 登录成功");
                        return true;
                    }
                    else
                    {
                        Console.WriteLine($"[DB] 用户 {username} 登录失败: 用户名或密码错误");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[DB] 登录异常: " + ex.GetType().Name + " — " + ex.Message);
                return false;
            }
        }
        // ════════════════════════════════════════════════════════════════
        // ★新增：GetUserId —— 根据用户名获取用户ID
        // 供 Client.cs 在登录成功后设置 UserId 使用
        // 返回: 用户ID，失败返回0
        // ════════════════════════════════════════════════════════════════
        public int GetUserId(string username)
        {
            if (string.IsNullOrEmpty(username)) return 0;

            try
            {
                if (sqlConnection == null)
                {
                    sqlConnection = new SqlConnection(con);
                }
                if (sqlConnection.State != System.Data.ConnectionState.Open)
                {
                    sqlConnection.Open();
                }

                // 注意：如果您的数据库表中主键列名不是 "id"，请将 "id" 替换为实际的列名（例如 "user_id"）
                string sql = "SELECT id FROM dbo.usertable WHERE username = @username";
                using (SqlCommand cmd = new SqlCommand(sql, sqlConnection))
                {
                    cmd.Parameters.AddWithValue("@username", username);
                    object result = cmd.ExecuteScalar();
                    if (result != null && result != DBNull.Value)
                    {
                        return Convert.ToInt32(result);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[DB] GetUserId 异常: " + ex.GetType().Name + " — " + ex.Message);
            }
            return 0;
        }

        // ════════════════════════════════════════════════════════════════
        //  ★新增：对局记录 — 保存、查询列表、查询详情
        // ════════════════════════════════════════════════════════════════

        /// <summary>保存一局对局记录到数据库</summary>
        public bool SaveGameRecord(string hostName, string guestName, int winner, int endReason, string moveList, System.DateTime startTime)
        {
            if (sqlConnection == null || sqlConnection.State != System.Data.ConnectionState.Open) return false;
            try
            {
                string sql = @"INSERT INTO dbo.gamerecord 
                    (host_name, guest_name, winner, end_reason, move_list, start_time, end_time) 
                    VALUES (@host, @guest, @winner, @reason, @moves, @start, @end)";
                using (SqlCommand cmd = new SqlCommand(sql, sqlConnection))
                {
                    cmd.Parameters.AddWithValue("@host", hostName ?? "");
                    cmd.Parameters.AddWithValue("@guest", guestName ?? "");
                    cmd.Parameters.AddWithValue("@winner", winner);
                    cmd.Parameters.AddWithValue("@reason", endReason);
                    cmd.Parameters.AddWithValue("@moves", moveList ?? "");
                    cmd.Parameters.AddWithValue("@start", startTime);
                    cmd.Parameters.AddWithValue("@end", System.DateTime.Now);
                    int rows = cmd.ExecuteNonQuery();
                    Console.WriteLine($"[DB] 对局记录已保存: {hostName} vs {guestName}, winner={winner}");
                    return rows > 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[DB] SaveGameRecord 异常: " + ex.Message);
                return false;
            }
        }

        /// <summary>查询指定用户最近50条对局记录，返回编码字符串</summary>
        /// <returns>"id|host|guest|winner|reason|end_time;id|host|guest|winner|reason|end_time;..."</returns>
        public string QueryGameRecords(string userName)
        {
            if (sqlConnection == null || sqlConnection.State != System.Data.ConnectionState.Open) return "";
            try
            {
                string sql = @"SELECT TOP 50 id, host_name, guest_name, winner, end_reason, end_time 
                    FROM dbo.gamerecord 
                    WHERE host_name = @user OR guest_name = @user 
                    ORDER BY end_time DESC";
                using (SqlCommand cmd = new SqlCommand(sql, sqlConnection))
                {
                    cmd.Parameters.AddWithValue("@user", userName ?? "");
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        var sb = new System.Text.StringBuilder();
                        while (reader.Read())
                        {
                            int id = reader.GetInt32(0);
                            string host = reader.GetString(1);
                            string guest = reader.GetString(2);
                            int winner = reader.GetInt32(3);
                            int reason = reader.GetInt32(4);
                            string endTime = reader.IsDBNull(5) ? "" : reader.GetDateTime(5).ToString("yyyy-MM-dd HH:mm");
                            if (sb.Length > 0) sb.Append(";");
                            sb.Append($"{id}|{host}|{guest}|{winner}|{reason}|{endTime}");
                        }
                        Console.WriteLine($"[DB] 查询用户 {userName} 的对局记录: {sb.ToString().Split(';').Length} 条");
                        return sb.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[DB] QueryGameRecords 异常: " + ex.Message);
                return "";
            }
        }

        /// <summary>查询单条对局记录的走棋序列</summary>
        /// <returns>"f3,4t5,4;f5,2t4,3;..." 格式的走棋字符串</returns>
        public string QueryGameRecordDetail(int gameId)
        {
            if (sqlConnection == null || sqlConnection.State != System.Data.ConnectionState.Open) return "";
            try
            {
                string sql = "SELECT move_list, host_name, guest_name, winner, end_reason FROM dbo.gamerecord WHERE id = @id";
                using (SqlCommand cmd = new SqlCommand(sql, sqlConnection))
                {
                    cmd.Parameters.AddWithValue("@id", gameId);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string moves = reader.IsDBNull(0) ? "" : reader.GetString(0);
                            string host = reader.GetString(1);
                            string guest = reader.GetString(2);
                            int winner = reader.GetInt32(3);
                            int reason = reader.GetInt32(4);
                            // 返回格式: "host|guest|winner|reason|move_list"
                            return $"{host}|{guest}|{winner}|{reason}|{moves}";
                        }
                        return "";
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[DB] QueryGameRecordDetail 异常: " + ex.Message);
                return "";
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed) return;
            disposed = true;

            if (disposing)
            {
                if (sqlConnection != null)
                {
                    try
                    {
                        if (sqlConnection.State == System.Data.ConnectionState.Open)
                        {
                            sqlConnection.Close();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[DB] 关闭数据库连接异常: " + ex.Message);
                    }
                    finally
                    {
                        sqlConnection.Dispose();
                        sqlConnection = null;
                    }
                }
            }
        }

        ~UserData()
        {
            Dispose(false);
        }
    }
}
