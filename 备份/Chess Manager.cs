using UnityEngine;
using static ChessManager.Chess;

public class ChessManager : MonoBehaviour
{
    public static ChessManager Instance { get; private set; }
    public GameObject 黑车;
    public GameObject 黑马;
    public GameObject 黑象;
    public GameObject 黑士;
    public GameObject 黑将;
    public GameObject 黑炮;
    public GameObject 黑卒;

    public GameObject 红车;
    public GameObject 红马;
    public GameObject 红相;
    public GameObject 红士;
    public GameObject 红帅;
    public GameObject 红炮;
    public GameObject 红兵;
    public GameObject 棋盘;

    public static Chess[] ChessArray = new Chess[32];

    public class Chess
    {
        public enum ChessType
        { 车, 马, 士, 象, 炮, 帅, 卒 };

        public bool Is_Red;
        public float Vec_X;
        public float Vec_Y;
        public ChessType Type;
        public int Id;
        public bool Is_Dead;
        public GameObject Obj;

        public Chess(int id, bool is_red, float vec_x, float vec_y, ChessType type, bool is_dead)
        {
            Id = id;
            Is_Red = is_red;
            Vec_X = vec_x;
            Vec_Y = vec_y;
            Type = type;
            Is_Dead = is_dead;
        }
    };

    public struct ChessPosition
    {
        public float x;
        public float y;
        public Chess.ChessType type;

        public ChessPosition(float _x, float _y, Chess.ChessType _type)
        {
            x = _x;
            y = _y;
            type = _type;
        }
    }

    private static Chess[] ChessArr = new Chess[32];

    public GameObject GetPrefab(int id, ChessManager.Chess.ChessType type)
    {
        if (id < 16) // 红方
        {
            switch (type)
            {
                case ChessManager.Chess.ChessType.车:
                    return 红车;

                case ChessManager.Chess.ChessType.马:
                    return 红马;

                case ChessManager.Chess.ChessType.象:
                    return 红相;

                case ChessManager.Chess.ChessType.士:
                    return 红士;

                case ChessManager.Chess.ChessType.炮:
                    return 红炮;

                case ChessManager.Chess.ChessType.帅:
                    return 红帅;

                case ChessManager.Chess.ChessType.卒:
                    return 红兵;
            }
        }
        else // 黑方
        {
            switch (type)
            {
                case ChessManager.Chess.ChessType.车:
                    return 黑车;

                case ChessManager.Chess.ChessType.马:
                    return 黑马;

                case ChessManager.Chess.ChessType.象:
                    return 黑象;

                case ChessManager.Chess.ChessType.士:
                    return 黑士;

                case ChessManager.Chess.ChessType.炮:
                    return 黑炮;

                case ChessManager.Chess.ChessType.帅:
                    return 黑将;

                case ChessManager.Chess.ChessType.卒:
                    return 黑卒;
            }
        }
        return null;
    }

    public void Init()
    {
        ChessPosition[] RedChessPositions =
        {
            new ChessPosition (-2.0f, -2.2f, Chess.ChessType.车),
            new ChessPosition (-1.5f, -2.2f, Chess.ChessType.马),
            new ChessPosition (-1.0f, -2.2f, Chess.ChessType.象),
            new ChessPosition (-0.5f, -2.2f, Chess.ChessType.士),
            new ChessPosition (0.0f, -2.2f, Chess.ChessType.帅),
            new ChessPosition (0.5f, -2.2f, Chess.ChessType.士),
            new ChessPosition (1.0f, -2.2f, Chess.ChessType.象),
            new ChessPosition (1.5f, -2.2f, Chess.ChessType.马),
            new ChessPosition (2.0f, -2.2f, Chess.ChessType.车),
            new ChessPosition (-1.5f, -1.2f, Chess.ChessType.炮),
            new ChessPosition (1.5f, -1.2f, Chess.ChessType.炮),
            new ChessPosition (-2.0f, -0.7f, Chess.ChessType.卒),
            new ChessPosition (-1.0f, -0.7f, Chess.ChessType.卒),
            new ChessPosition (1.0f, -0.7f, Chess.ChessType.卒),
            new ChessPosition (2.0f, -0.7f, Chess.ChessType.卒),
            new ChessPosition (0.0f, -0.7f, Chess.ChessType.卒),
        };
        for (int i = 0; i < 32; i++)
        {
            if (i < 16)
            {
                ChessArr[i] = new Chess(i, true, RedChessPositions[i].x, RedChessPositions[i].y, RedChessPositions[i].type, false);
            }
            else
            {
                ChessArr[i] = new Chess(i, false, RedChessPositions[i - 16].x, -RedChessPositions[i - 16].y, RedChessPositions[i - 16].type, false);
            }
        }
        for (int i = 0; i < 32; i++)
        {
            ChessArray[i] = ChessArr[i];
        }

        // 3. 实例化棋子（原有代码）
        GameObject temp;
        GameObject chessObj;
        for (int i = 0; i < 32; i++)
        {
            temp = GetPrefab(i, ChessArr[i].Type);
            chessObj = Instantiate(temp);
            chessObj.transform.SetParent(棋盘.transform);
            chessObj.transform.localPosition = new Vector3(ChessArr[i].Vec_X, ChessArr[i].Vec_Y, -2);
            chessObj.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
            ChessArr[i].Obj = chessObj;
            if (ChessArr[i].Is_Red)
            {
                chessObj.tag = "红棋";
            }
            else
            {
                chessObj.tag = "黑棋";
            }
            SpriteRenderer sr = chessObj.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.sortingOrder = 1;
            }
            for (int j = 0; j < 32; j++) { ChessArray[j] = ChessArr[j]; }
        }
    }


    private void Awake()
    {
        Init();
    }
}
