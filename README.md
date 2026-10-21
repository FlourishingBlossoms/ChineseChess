# 中国象棋（联机对战）

大创项目：Unity 客户端 + C# Socket 服务端的联机中国象棋游戏。

## 分支结构

| 分支 | 内容 | 说明 |
| --- | --- | --- |
| [`client`](https://github.com/FlourishingBlossoms/ChineseChess/tree/client) | Unity 客户端 | 象棋客户端，含网络模块、UI 与资源（大文件使用 Git LFS） |
| [`server`](https://github.com/FlourishingBlossoms/ChineseChess/tree/server) | C# 服务端 | Socket 服务器，Protobuf 协议，含房间、数据库等模块 |

## 获取代码

```bash
# 客户端（Unity 项目，用 Unity Hub 打开）
git clone -b client https://github.com/FlourishingBlossoms/ChineseChess.git

# 服务端（用 Visual Studio 打开 Surver.sln）
git clone -b server https://github.com/FlourishingBlossoms/ChineseChess.git
```

> 客户端分支包含 LFS 大文件，克隆前请先安装并初始化 [Git LFS](https://git-lfs.com/)（`git lfs install`）。
