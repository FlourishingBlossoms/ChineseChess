using Mono.Data.Sqlite;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ============================================================
/// 数据库操作工具类 —— 提供完整的增删改查功能
/// 继承自 SQLiteConnect，基于 Mono.Data.Sqlite 实现
/// ============================================================
/// 
/// 数据库关系模型（V3 精简版）：
/// 
///   PlayerAccount(1) ──< GameRecord(N)   RedUserId → PlayerAccount.Id
///   PlayerAccount(1) ──| PlayerStats(1)  UserId → PlayerAccount.Id
/// 
/// 三张表：
///   PlayerAccount  - 玩家账号表（Id, Account, Password, NickName）
///   GameRecord     - 对局表（Id, RedUserId, Result, Difficulty）
///   PlayerStats    - 玩家统计表（Id, UserId, TotalGames, Wins, Losses, Draws）
/// 
/// ============================================================
/// 快速上手：
///   string dbPath = Application.dataPath + "/../Save/SaveData.db";
///   SQLDBCommand db = new SQLDBCommand(dbPath);  // 自动建表
///   int userId = db.RegisterUser("zhangsan", "123456");
///   db.Dispose();  // 用完必须释放
/// ============================================================
/// </summary>
public class SQLDBCommand : SQLiteConnect
{
    private SqliteCommand cmd;

    // ==================== 构造函数 ====================

    /// <summary>
    /// 初始化数据库连接，并自动创建所有业务表
    /// 用法：var db = new SQLDBCommand(Application.dataPath + "/../Save/SaveData.db");
    /// </summary>
    /// <param name="DBPath">数据库文件路径</param>
    public SQLDBCommand(string DBPath) : base(DBPath)
    {
        cmd = con.CreateCommand();
        CreateAllTables();
    }

    // ==================== 建表 ====================

    /// <summary>
    /// 创建所有业务表（如果不存在）
    /// 由构造函数自动调用，无需手动调用
    /// </summary>
    private void CreateAllTables()
    {
        CreatePlayerAccountTable();
        CreateGameRecordTable();
        CreatePlayerStatsTable();
    }

    /// <summary>
    /// 创建玩家账号表
    /// 等价SQL：
    ///   CREATE TABLE IF NOT EXISTS PlayerAccount (
    ///     Id       INTEGER PRIMARY KEY AUTOINCREMENT,
    ///     Account  TEXT UNIQUE NOT NULL,
    ///     Password TEXT NOT NULL,
    ///     NickName TEXT DEFAULT ''
    ///   );
    /// </summary>
    private void CreatePlayerAccountTable()
    {
        try
        {
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS PlayerAccount (
                    Id       INTEGER PRIMARY KEY AUTOINCREMENT,
                    Account  TEXT UNIQUE NOT NULL,
                    Password TEXT NOT NULL,
                    NickName TEXT DEFAULT ''
                );";
            cmd.Parameters.Clear();
            cmd.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            Debug.LogError($"[SQLDBCommand] 创建PlayerAccount表失败: {e.Message}");
        }
    }

    /// <summary>
    /// 创建对局表
    /// 等价SQL：
    ///   CREATE TABLE IF NOT EXISTS GameRecord (
    ///     Id         INTEGER PRIMARY KEY AUTOINCREMENT,
    ///     RedUserId  INTEGER NOT NULL,
    ///     Result     INTEGER DEFAULT -1,
    ///     Difficulty TEXT DEFAULT 'Normal'
    ///   );
    /// </summary>
    private void CreateGameRecordTable()
    {
        try
        {
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS GameRecord (
                    Id         INTEGER PRIMARY KEY AUTOINCREMENT,
                    RedUserId  INTEGER NOT NULL,
                    Result     INTEGER DEFAULT -1,
                    Difficulty TEXT DEFAULT 'Normal'
                );";
            cmd.Parameters.Clear();
            cmd.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            Debug.LogError($"[SQLDBCommand] 创建GameRecord表失败: {e.Message}");
        }
    }

    /// <summary>
    /// 创建玩家统计表
    /// 等价SQL：
    ///   CREATE TABLE IF NOT EXISTS PlayerStats (
    ///     Id         INTEGER PRIMARY KEY AUTOINCREMENT,
    ///     UserId     INTEGER UNIQUE NOT NULL,
    ///     TotalGames INTEGER DEFAULT 0,
    ///     Wins       INTEGER DEFAULT 0,
    ///     Losses     INTEGER DEFAULT 0,
    ///     Draws      INTEGER DEFAULT 0
    ///   );
    /// </summary>
    private void CreatePlayerStatsTable()
    {
        try
        {
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS PlayerStats (
                    Id         INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId     INTEGER UNIQUE NOT NULL,
                    TotalGames INTEGER DEFAULT 0,
                    Wins       INTEGER DEFAULT 0,
                    Losses     INTEGER DEFAULT 0,
                    Draws      INTEGER DEFAULT 0
                );";
            cmd.Parameters.Clear();
            cmd.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            Debug.LogError($"[SQLDBCommand] 创建PlayerStats表失败: {e.Message}");
        }
    }

    // ================================================================
    // ==================== PlayerAccount 操作 ========================
    // ================================================================

    // ---------- INSERT ----------

    /// <summary>
    /// 插入新玩家账号，并自动创建对应的统计记录
    /// 用法：int id = InsertPlayerAccount("zhangsan", "123456", "张三");
    ///       int id = InsertPlayerAccount("lisi", "654321");  // 昵称默认与账号相同
    /// 等价SQL：INSERT INTO PlayerAccount (Account, Password, NickName) VALUES (@account, @password, @nickName);
    /// </summary>
    /// <param name="account">登录账号</param>
    /// <param name="password">登录密码</param>
    /// <param name="nickName">昵称（可选，默认与账号相同）</param>
    /// <returns>新用户Id，失败返回 -1</returns>
    public int InsertPlayerAccount(string account, string password, string nickName = "")
    {
        try
        {
            cmd.CommandText = @"
                INSERT INTO PlayerAccount (Account, Password, NickName)
                VALUES (@account, @password, @nickName);";
            cmd.Parameters.Clear();
            cmd.Parameters.Add(new SqliteParameter("@account", account));
            cmd.Parameters.Add(new SqliteParameter("@password", password));
            cmd.Parameters.Add(new SqliteParameter("@nickName", string.IsNullOrEmpty(nickName) ? account : nickName));
            cmd.ExecuteNonQuery();

            // 获取自增Id
            cmd.CommandText = "SELECT last_insert_rowid();";
            cmd.Parameters.Clear();
            int newId = Convert.ToInt32(cmd.ExecuteScalar());

            // 自动创建统计记录
            InsertPlayerStats(newId);

            return newId;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SQLDBCommand] 插入玩家账号失败: {e.Message}");
            return -1;
        }
    }

    // ---------- DELETE ----------

    /// <summary>
    /// 根据Id删除玩家账号（同时删除统计和对局记录）
    /// 用法：int affected = DeletePlayerAccount(5);
    /// 等价SQL：DELETE FROM PlayerAccount WHERE Id = @id;
    /// </summary>
    /// <param name="userId">玩家Id</param>
    /// <returns>受影响行数</returns>
    public int DeletePlayerAccount(int userId)
    {
        try
        {
            // 先删除关联的统计
            cmd.CommandText = "DELETE FROM PlayerStats WHERE UserId = @userId;";
            cmd.Parameters.Clear();
            cmd.Parameters.Add(new SqliteParameter("@userId", userId));
            cmd.ExecuteNonQuery();

            // 再删除关联的对局
            cmd.CommandText = "DELETE FROM GameRecord WHERE RedUserId = @userId;";
            cmd.Parameters.Clear();
            cmd.Parameters.Add(new SqliteParameter("@userId", userId));
            cmd.ExecuteNonQuery();

            // 最后删除账号
            cmd.CommandText = "DELETE FROM PlayerAccount WHERE Id = @id;";
            cmd.Parameters.Clear();
            cmd.Parameters.Add(new SqliteParameter("@id", userId));
            return cmd.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            Debug.LogError($"[SQLDBCommand] 删除玩家账号失败: {e.Message}");
            return -1;
        }
    }

    // ---------- UPDATE ----------

    /// <summary>
    /// 更新玩家昵称
    /// 用法：int affected = UpdatePlayerNickName(5, "新昵称");
    /// 等价SQL：UPDATE PlayerAccount SET NickName = @nickName WHERE Id = @id;
    /// </summary>
    /// <param name="userId">玩家Id</param>
    /// <param name="nickName">新昵称</param>
    /// <returns>受影响行数</returns>
    public int UpdatePlayerNickName(int userId, string nickName)
    {
        try
        {
            cmd.CommandText = "UPDATE PlayerAccount SET NickName = @nickName WHERE Id = @id;";
            cmd.Parameters.Clear();
            cmd.Parameters.Add(new SqliteParameter("@nickName", nickName));
            cmd.Parameters.Add(new SqliteParameter("@id", userId));
            return cmd.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            Debug.LogError($"[SQLDBCommand] 更新玩家昵称失败: {e.Message}");
            return -1;
        }
    }

    /// <summary>
    /// 更新玩家密码
    /// 用法：int affected = UpdatePlayerPassword(5, "新密码");
    /// 等价SQL：UPDATE PlayerAccount SET Password = @password WHERE Id = @id;
    /// </summary>
    /// <param name="userId">玩家Id</param>
    /// <param name="newPassword">新密码</param>
    /// <returns>受影响行数</returns>
    public int UpdatePlayerPassword(int userId, string newPassword)
    {
        try
        {
            cmd.CommandText = "UPDATE PlayerAccount SET Password = @password WHERE Id = @id;";
            cmd.Parameters.Clear();
            cmd.Parameters.Add(new SqliteParameter("@password", newPassword));
            cmd.Parameters.Add(new SqliteParameter("@id", userId));
            return cmd.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            Debug.LogError($"[SQLDBCommand] 更新玩家密码失败: {e.Message}");
            return -1;
        }
    }

    // ---------- SELECT ----------

    /// <summary>
    /// 根据Id查询玩家信息
    /// 用法：var info = QueryPlayerAccountById(5);
    ///       if (info != null) Debug.Log("账号: " + info["Account"]);
    /// 等价SQL：SELECT Id, Account, Password, NickName FROM PlayerAccount WHERE Id = @id;
    /// </summary>
    /// <param name="userId">玩家Id</param>
    /// <returns>玩家信息字典，未找到返回 null</returns>
    public Dictionary<string, object> QueryPlayerAccountById(int userId)
    {
        try
        {
            cmd.CommandText = "SELECT Id, Account, Password, NickName FROM PlayerAccount WHERE Id = @id;";
            cmd.Parameters.Clear();
            cmd.Parameters.Add(new SqliteParameter("@id", userId));

            using (SqliteDataReader reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    return new Dictionary<string, object>
                    {
                        { "Id",       reader["Id"]       },
                        { "Account",  reader["Account"]  },
                        { "Password", reader["Password"] },
                        { "NickName", reader["NickName"] }
                    };
                }
            }
            return null;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SQLDBCommand] 查询玩家信息失败: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 根据账号查询玩家信息
    /// 用法：var info = QueryPlayerAccountByAccount("zhangsan");
    /// 等价SQL：SELECT Id, Account, Password, NickName FROM PlayerAccount WHERE Account = @account;
    /// </summary>
    /// <param name="account">登录账号</param>
    /// <returns>玩家信息字典，未找到返回 null</returns>
    public Dictionary<string, object> QueryPlayerAccountByAccount(string account)
    {
        try
        {
            cmd.CommandText = "SELECT Id, Account, Password, NickName FROM PlayerAccount WHERE Account = @account;";
            cmd.Parameters.Clear();
            cmd.Parameters.Add(new SqliteParameter("@account", account));

            using (SqliteDataReader reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    return new Dictionary<string, object>
                    {
                        { "Id",       reader["Id"]       },
                        { "Account",  reader["Account"]  },
                        { "Password", reader["Password"] },
                        { "NickName", reader["NickName"] }
                    };
                }
            }
            return null;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SQLDBCommand] 根据账号查询玩家失败: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 检查账号是否已存在
    /// 用法：if (IsAccountExist("zhangsan")) Debug.Log("账号已存在");
    /// 等价SQL：SELECT COUNT(*) FROM PlayerAccount WHERE Account = @account;
    /// </summary>
    /// <param name="account">登录账号</param>
    /// <returns>存在返回 true，不存在返回 false</returns>
    public bool IsAccountExist(string account)
    {
        try
        {
            cmd.CommandText = "SELECT COUNT(*) FROM PlayerAccount WHERE Account = @account;";
            cmd.Parameters.Clear();
            cmd.Parameters.Add(new SqliteParameter("@account", account));
            long count = (long)cmd.ExecuteScalar();
            return count > 0;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SQLDBCommand] 检查账号是否存在失败: {e.Message}");
            return false;
        }
    }

    // ---------- 业务组合方法 ----------

    /// <summary>
    /// 注册新玩家（封装版）
    /// 用法：int userId = RegisterUser("zhangsan", "123456");
    ///       if (userId > 0) Debug.Log("注册成功，Id=" + userId);
    ///       if (userId == -2) Debug.Log("账号已存在");
    /// 流程：
    ///   1. 检查账号是否已存在 → 存在返回 -2
    ///   2. 插入 PlayerAccount 记录
    ///   3. 自动创建对应的 PlayerStats 记录
    ///   4. 返回新用户 Id
    /// </summary>
    /// <param name="account">登录账号</param>
    /// <param name="password">登录密码</param>
    /// <param name="nickName">昵称（可选，默认与账号相同）</param>
    /// <returns>新用户Id，账号已存在返回 -2，其他错误返回 -1</returns>
    public int RegisterUser(string account, string password, string nickName = "")
    {
        if (IsAccountExist(account))
        {
            Debug.LogWarning($"[SQLDBCommand] 注册失败：账号 {account} 已存在");
            return -2;
        }
        return InsertPlayerAccount(account, password, string.IsNullOrEmpty(nickName) ? account : nickName);
    }

    /// <summary>
    /// 玩家登录验证
    /// 用法：int userId = LoginUser("zhangsan", "123456");
    ///       if (userId > 0) Debug.Log("登录成功，Id=" + userId);
    ///       if (userId == -2) Debug.Log("账号不存在");
    ///       if (userId == -3) Debug.Log("密码错误");
    /// 流程：
    ///   1. 根据账号查询玩家信息
    ///   2. 账号不存在 → 返回 -2
    ///   3. 密码不匹配 → 返回 -3
    ///   4. 验证通过 → 返回用户 Id
    /// </summary>
    /// <param name="account">登录账号</param>
    /// <param name="password">登录密码</param>
    /// <returns>用户Id，账号不存在返回 -2，密码错误返回 -3</returns>
    public int LoginUser(string account, string password)
    {
        try
        {
            var info = QueryPlayerAccountByAccount(account);
            if (info == null) return -2; // 账号不存在

            string dbPassword = info["Password"].ToString();
            if (dbPassword != password) return -3; // 密码错误

            return Convert.ToInt32(info["Id"]); // 登录成功
        }
        catch (Exception e)
        {
            Debug.LogError($"[SQLDBCommand] 登录验证失败: {e.Message}");
            return -1;
        }
    }

    // ================================================================
    // ==================== GameRecord 操作 ===========================
    // ================================================================

    // ---------- INSERT ----------

    /// <summary>
    /// 插入新对局记录
    /// 用法：int gameId = InsertGameRecord(5, "Hard");
    /// 等价SQL：INSERT INTO GameRecord (RedUserId, Difficulty) VALUES (@redUserId, @difficulty);
    /// </summary>
    /// <param name="redUserId">红方玩家Id</param>
    /// <param name="difficulty">难度：Easy/Normal/Hard/Lunatic</param>
    /// <returns>新对局Id，失败返回 -1</returns>
    public int InsertGameRecord(int redUserId, string difficulty = "Normal")
    {
        try
        {
            cmd.CommandText = @"
                INSERT INTO GameRecord (RedUserId, Difficulty)
                VALUES (@redUserId, @difficulty);";
            cmd.Parameters.Clear();
            cmd.Parameters.Add(new SqliteParameter("@redUserId", redUserId));
            cmd.Parameters.Add(new SqliteParameter("@difficulty", difficulty));
            cmd.ExecuteNonQuery();

            cmd.CommandText = "SELECT last_insert_rowid();";
            cmd.Parameters.Clear();
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
        catch (Exception e)
        {
            Debug.LogError($"[SQLDBCommand] 插入对局记录失败: {e.Message}");
            return -1;
        }
    }

    // ---------- DELETE ----------

    /// <summary>
    /// 根据Id删除对局记录
    /// 用法：int affected = DeleteGameRecord(10);
    /// 等价SQL：DELETE FROM GameRecord WHERE Id = @id;
    /// </summary>
    /// <param name="gameId">对局Id</param>
    /// <returns>受影响行数</returns>
    public int DeleteGameRecord(int gameId)
    {
        try
        {
            cmd.CommandText = "DELETE FROM GameRecord WHERE Id = @id;";
            cmd.Parameters.Clear();
            cmd.Parameters.Add(new SqliteParameter("@id", gameId));
            return cmd.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            Debug.LogError($"[SQLDBCommand] 删除对局记录失败: {e.Message}");
            return -1;
        }
    }

    // ---------- UPDATE ----------

    /// <summary>
    /// 更新对局结果
    /// 用法：int affected = UpdateGameResult(10, 0);  // 0=红胜
    /// 等价SQL：UPDATE GameRecord SET Result = @result WHERE Id = @id;
    /// </summary>
    /// <param name="gameId">对局Id</param>
    /// <param name="result">结果：-1=未结束, 0=红胜, 1=黑胜, 2=和棋</param>
    /// <returns>受影响行数</returns>
    public int UpdateGameResult(int gameId, int result)
    {
        try
        {
            cmd.CommandText = "UPDATE GameRecord SET Result = @result WHERE Id = @id;";
            cmd.Parameters.Clear();
            cmd.Parameters.Add(new SqliteParameter("@result", result));
            cmd.Parameters.Add(new SqliteParameter("@id", gameId));
            return cmd.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            Debug.LogError($"[SQLDBCommand] 更新对局结果失败: {e.Message}");
            return -1;
        }
    }

    // ---------- SELECT ----------

    /// <summary>
    /// 根据Id查询对局记录
    /// 用法：var game = QueryGameRecordById(10);
    ///       if (game != null) Debug.Log("结果: " + game["Result"]);
    /// 等价SQL：SELECT * FROM GameRecord WHERE Id = @id;
    /// </summary>
    /// <param name="gameId">对局Id</param>
    /// <returns>对局信息字典，未找到返回 null</returns>
    public Dictionary<string, object> QueryGameRecordById(int gameId)
    {
        try
        {
            cmd.CommandText = "SELECT Id, RedUserId, Result, Difficulty FROM GameRecord WHERE Id = @id;";
            cmd.Parameters.Clear();
            cmd.Parameters.Add(new SqliteParameter("@id", gameId));

            using (SqliteDataReader reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    return new Dictionary<string, object>
                    {
                        { "Id",         reader["Id"]         },
                        { "RedUserId",  reader["RedUserId"]  },
                        { "Result",     reader["Result"]     },
                        { "Difficulty", reader["Difficulty"] }
                    };
                }
            }
            return null;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SQLDBCommand] 查询对局记录失败: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 查询某玩家的所有对局记录
    /// 用法：var list = QueryGameRecordsByUserId(5);
    /// 等价SQL：SELECT * FROM GameRecord WHERE RedUserId = @userId ORDER BY Id DESC;
    /// </summary>
    /// <param name="userId">玩家Id</param>
    /// <returns>对局记录列表</returns>
    public List<Dictionary<string, object>> QueryGameRecordsByUserId(int userId)
    {
        var list = new List<Dictionary<string, object>>();
        try
        {
            cmd.CommandText = @"
                SELECT Id, RedUserId, Result, Difficulty
                FROM GameRecord
                WHERE RedUserId = @userId
                ORDER BY Id DESC;";
            cmd.Parameters.Clear();
            cmd.Parameters.Add(new SqliteParameter("@userId", userId));

            using (SqliteDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    list.Add(new Dictionary<string, object>
                    {
                        { "Id",         reader["Id"]         },
                        { "RedUserId",  reader["RedUserId"]  },
                        { "Result",     reader["Result"]     },
                        { "Difficulty", reader["Difficulty"] }
                    });
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[SQLDBCommand] 查询玩家对局记录失败: {e.Message}");
        }
        return list;
    }

    // ================================================================
    // ==================== PlayerStats 操作 ==========================
    // ================================================================

    // ---------- INSERT ----------

    /// <summary>
    /// 为指定用户创建统计记录（初始值全为0）
    /// 用法：由 InsertPlayerAccount 自动调用，一般不需要手动调用
    /// 等价SQL：INSERT INTO PlayerStats (UserId) VALUES (@userId);
    /// </summary>
    /// <param name="userId">用户Id</param>
    /// <returns>新统计记录Id，失败返回 -1</returns>
    public int InsertPlayerStats(int userId)
    {
        try
        {
            cmd.CommandText = "INSERT INTO PlayerStats (UserId) VALUES (@userId);";
            cmd.Parameters.Clear();
            cmd.Parameters.Add(new SqliteParameter("@userId", userId));
            cmd.ExecuteNonQuery();

            cmd.CommandText = "SELECT last_insert_rowid();";
            cmd.Parameters.Clear();
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
        catch (Exception e)
        {
            Debug.LogError($"[SQLDBCommand] 创建玩家统计失败: {e.Message}");
            return -1;
        }
    }

    // ---------- DELETE ----------

    /// <summary>
    /// 删除指定用户的统计记录
    /// 用法：int affected = DeletePlayerStats(5);
    /// 等价SQL：DELETE FROM PlayerStats WHERE UserId = @userId;
    /// </summary>
    /// <param name="userId">用户Id</param>
    /// <returns>受影响行数</returns>
    public int DeletePlayerStats(int userId)
    {
        try
        {
            cmd.CommandText = "DELETE FROM PlayerStats WHERE UserId = @userId;";
            cmd.Parameters.Clear();
            cmd.Parameters.Add(new SqliteParameter("@userId", userId));
            return cmd.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            Debug.LogError($"[SQLDBCommand] 删除玩家统计失败: {e.Message}");
            return -1;
        }
    }

    // ---------- UPDATE ----------

    /// <summary>
    /// 更新玩家统计数据（直接设置值）
    /// 用法：UpdatePlayerStats(5, totalGames: 10, wins: 6, losses: 3, draws: 1);
    /// 等价SQL：UPDATE PlayerStats SET TotalGames=@totalGames, Wins=@wins, Losses=@losses, Draws=@draws WHERE UserId=@userId;
    /// </summary>
    /// <param name="userId">用户Id</param>
    /// <param name="totalGames">总局数</param>
    /// <param name="wins">胜场</param>
    /// <param name="losses">负场</param>
    /// <param name="draws">和棋场</param>
    /// <returns>受影响行数</returns>
    public int UpdatePlayerStats(int userId, int totalGames, int wins, int losses, int draws)
    {
        try
        {
            cmd.CommandText = @"
                UPDATE PlayerStats
                SET TotalGames = @totalGames, Wins = @wins, Losses = @losses, Draws = @draws
                WHERE UserId = @userId;";
            cmd.Parameters.Clear();
            cmd.Parameters.Add(new SqliteParameter("@totalGames", totalGames));
            cmd.Parameters.Add(new SqliteParameter("@wins", wins));
            cmd.Parameters.Add(new SqliteParameter("@losses", losses));
            cmd.Parameters.Add(new SqliteParameter("@draws", draws));
            cmd.Parameters.Add(new SqliteParameter("@userId", userId));
            return cmd.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            Debug.LogError($"[SQLDBCommand] 更新玩家统计失败: {e.Message}");
            return -1;
        }
    }

    /// <summary>
    /// 重置玩家统计（清零所有数据）
    /// 用法：int affected = ResetPlayerStats(5);
    /// 等价SQL：UPDATE PlayerStats SET TotalGames=0, Wins=0, Losses=0, Draws=0 WHERE UserId=@userId;
    /// </summary>
    /// <param name="userId">用户Id</param>
    /// <returns>受影响行数</returns>
    public int ResetPlayerStats(int userId)
    {
        try
        {
            cmd.CommandText = "UPDATE PlayerStats SET TotalGames = 0, Wins = 0, Losses = 0, Draws = 0 WHERE UserId = @userId;";
            cmd.Parameters.Clear();
            cmd.Parameters.Add(new SqliteParameter("@userId", userId));
            return cmd.ExecuteNonQuery();
        }
        catch (Exception e)
        {
            Debug.LogError($"[SQLDBCommand] 重置玩家统计失败: {e.Message}");
            return -1;
        }
    }

    // ---------- SELECT ----------

    /// <summary>
    /// 根据用户Id查询统计信息
    /// 用法：var stats = QueryPlayerStatsByUserId(5);
    ///       if (stats != null)
    ///           Debug.Log($"胜率: {(int)stats["Wins"] * 100 / (int)stats["TotalGames"]}%");
    /// 等价SQL：SELECT * FROM PlayerStats WHERE UserId = @userId;
    /// </summary>
    /// <param name="userId">用户Id</param>
    /// <returns>统计信息字典，未找到返回 null</returns>
    public Dictionary<string, object> QueryPlayerStatsByUserId(int userId)
    {
        try
        {
            cmd.CommandText = "SELECT Id, UserId, TotalGames, Wins, Losses, Draws FROM PlayerStats WHERE UserId = @userId;";
            cmd.Parameters.Clear();
            cmd.Parameters.Add(new SqliteParameter("@userId", userId));

            using (SqliteDataReader reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    int totalGames = Convert.ToInt32(reader["TotalGames"]);
                    int wins = Convert.ToInt32(reader["Wins"]);
                    double winRate = totalGames > 0 ? (double)wins / totalGames * 100 : 0;

                    return new Dictionary<string, object>
                    {
                        { "Id",         reader["Id"]         },
                        { "UserId",     reader["UserId"]     },
                        { "TotalGames", totalGames           },
                        { "Wins",       wins                 },
                        { "Losses",     Convert.ToInt32(reader["Losses"]) },
                        { "Draws",      Convert.ToInt32(reader["Draws"])  },
                        { "WinRate",    Math.Round(winRate, 1) }
                    };
                }
            }
            return null;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SQLDBCommand] 查询玩家统计失败: {e.Message}");
            return null;
        }
    }

    // ================================================================
    // ==================== 业务组合方法 ==============================
    // ================================================================

    /// <summary>
    /// 对局结束：更新对局结果 + 更新玩家统计（一步到位）
    /// 用法：bool ok = UpdateGameResultAndStats(10, 0, 5);
    ///       // 对局10红方胜利，玩家5是红方
    /// 流程：
    ///   1. UPDATE GameRecord SET Result = @result WHERE Id = @id
    ///   2. SELECT * FROM PlayerStats WHERE UserId = @userId
    ///   3. 重新计算并 UPDATE PlayerStats SET ...
    /// </summary>
    /// <param name="gameId">对局Id</param>
    /// <param name="result">结果：0=红胜, 1=黑胜, 2=和棋</param>
    /// <param name="redUserId">红方玩家Id</param>
    /// <returns>成功返回 true</returns>
    public bool UpdateGameResultAndStats(int gameId, int result, int redUserId)
    {
        // 1. 更新对局结果
        int affected = UpdateGameResult(gameId, result);
        if (affected <= 0) return false;

        // 2. 读取当前统计
        var stats = QueryPlayerStatsByUserId(redUserId);
        if (stats == null) return false;

        int totalGames = Convert.ToInt32(stats["TotalGames"]) + 1;
        int wins = Convert.ToInt32(stats["Wins"]);
        int losses = Convert.ToInt32(stats["Losses"]);
        int draws = Convert.ToInt32(stats["Draws"]);

        // 3. 根据结果更新统计
        if (result == 0) wins++;
        else if (result == 1) losses++;
        else if (result == 2) draws++;

        UpdatePlayerStats(redUserId, totalGames, wins, losses, draws);
        return true;
    }

    // ==================== 资源释放 ====================

    /// <summary>
    /// 释放数据库命令和连接资源
    /// 用法：db.Dispose();
    ///       或 using (var db = new SQLDBCommand(path)) { ... }
    /// </summary>
    public new void Dispose()
    {
        cmd?.Dispose();
        base.Dispose();
    }
}
