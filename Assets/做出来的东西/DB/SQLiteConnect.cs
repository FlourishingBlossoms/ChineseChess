using Mono.Data.Sqlite;
using System;
using System.IO;
using UnityEngine;

/// <summary>
/// SQLite 数据库连接基类
/// 负责创建数据库文件和建立连接，供 SQLDBCommand 继承使用
/// 
/// 用法：不需要手动使用此类，直接使用 SQLDBCommand 即可
///       SQLDBCommand 会在构造函数中自动调用基类完成连接
/// 
/// 内部流程：
///   1. 构造时检查数据库文件是否存在
///   2. 不存在则调用 SqliteConnection.CreateFile 创建
///   3. 调用 con.Open() 建立连接
///   4. 子类通过 protected SqliteConnection con 访问连接
/// </summary>
public class SQLiteConnect
{
    /// <summary>
    /// 数据库连接对象，子类可直接访问
    /// </summary>
    protected SqliteConnection con;

    /// <summary>
    /// 创建 SQLite 数据库文件
    /// 如果目录不存在会自动创建目录
    /// </summary>
    /// <param name="DBpath">数据库文件路径，如 "C:/Save/SaveData.db"</param>
    /// <returns>创建成功返回 true</returns>
    private bool CreateDbSqlite(string DBpath)
    {
        try
        {
            // 确保目录存在
            if (!Directory.Exists(new FileInfo(DBpath).Directory.FullName))
            {
                Directory.CreateDirectory(new FileInfo(DBpath).Directory.FullName);
            }

            // 创建数据库文件
            SqliteConnection.CreateFile(DBpath);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"数据库创建异常: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 连接到 SQLite 数据库
    /// </summary>
    /// <param name="DBpath">数据库文件路径</param>
    /// <returns>连接成功返回 true</returns>
    private bool ConnectDbSqlite(string DBpath)
    {
        try
        {
            // 构建连接字符串并创建连接对象
            con = new SqliteConnection(
                new SqliteConnectionStringBuilder() { DataSource = DBpath }.ToString()
            );
            // 打开数据库连接
            con.Open();
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"数据库连接异常: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 释放数据库连接资源
    /// 用法：sqlDbConnect.Dispose();
    /// </summary>
    public void Dispose()
    {
        con.Dispose();
    }

    /// <summary>
    /// 构造函数：如果数据库文件不存在则创建，然后建立连接
    /// 用法：var conn = new SQLiteConnect(Application.dataPath + "/../Save/SaveData.db");
    /// </summary>
    /// <param name="dbPath">数据库文件路径</param>
    public SQLiteConnect(string dbPath)
    {
        if (!File.Exists(dbPath))
        {
            CreateDbSqlite(dbPath);
        }
        ConnectDbSqlite(dbPath);
    }
}
