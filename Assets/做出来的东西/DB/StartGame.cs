using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StartGame : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        TestDb();
        string dbPath = Application.dataPath + "/../Save/SaveData.db";
        SQLDBCommand dbCmd = new SQLDBCommand(dbPath); // 初始化（自动连接数据库）

        // 定义表结构
//        Dictionary<string, string> playerColumns = new Dictionary<string, string>
//{
//    { "id", "INTEGER PRIMARY KEY AUTOINCREMENT" },
//    { "name", "TEXT NOT NULL" },
//    { "score", "INTEGER DEFAULT 0" }
//};

//        // 创建表
//        int result = dbCmd.CreateTable("Player", playerColumns);
//        if (result >= 0)
//        {
//            Debug.Log("表创建成功");
//        }
//        else
//        {
//            Debug.LogError("表创建失败");
//        }

        dbCmd.Dispose(); // 释放资源

    }

    // Update is called once per frame
    private void TestDb()
    {
        string dbPath = Application.dataPath + "/../Save/SaveData.db";
        SQLiteConnect sqlDbConnect = new SQLiteConnect(dbPath);
    }

}
