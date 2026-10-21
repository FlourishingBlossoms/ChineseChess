using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 符卡 UI 类：改为 5 等分
[System.Serializable]
public class Spell_Star
{
    public Sprite Zero_Over_Five;  // 0/5
    public Sprite One_Over_Five;   // 1/5
    public Sprite Two_Over_Five;   // 2/5
    public Sprite Three_Over_Five;// 3/5
    public Sprite Four_Over_Five;  // 4/5
    public Sprite Five_Over_Five;  // 5/5
    public Image Table;

    public void Change_Star(int num)
    {
        if (num < 0) num = 0;
        if (num > 5) num = 5;
        switch (num)
        {
            case 0:
                Table.sprite = Zero_Over_Five;
                break;

            case 1:
                Table.sprite = One_Over_Five;
                break;

            case 2:
                Table.sprite = Two_Over_Five;
                break;

            case 3:
                Table.sprite = Three_Over_Five;
                break;

            case 4:
                Table.sprite = Four_Over_Five;
                break;

            case 5:
                Table.sprite = Five_Over_Five;
                break;
        }
    }
}

[System.Serializable]
public class Heart_Shaped
{
    public Sprite Zero_Over_Five;
    public Sprite One_Over_Five;
    public Sprite Two_Over_Five;
    public Sprite Three_Over_Five;
    public Sprite Four_Over_Five;
    public Sprite Five_Over_Five;
    public Image Table;

    public void Change_Star(int num)
    {
        if (num < 0) num = 0;
        if (num > 5) num = 5;
        switch (num)
        {
            case 0:
                Table.sprite = Zero_Over_Five;
                break;

            case 1:
                Table.sprite = One_Over_Five;
                break;

            case 2:
                Table.sprite = Two_Over_Five;
                break;

            case 3:
                Table.sprite = Three_Over_Five;
                break;

            case 4:
                Table.sprite = Four_Over_Five;
                break;

            case 5:
                Table.sprite = Five_Over_Five;
                break;
        }
    }
}

public class UI : MonoBehaviour
{
    public float Heart;
    public float Power;
    public float Spell;
    public float Point;

    public Heart_Shaped[] 残机 = new Heart_Shaped[8];
    public Spell_Star[] 符卡 = new Spell_Star[8];
    public TextMeshProUGUI PowerText;

    private void Scan()//这里不知道为什么反过来了，所有在SCAN里反过来接收
    {
        if (玩家资源管理.instance != null)
        {
            Heart = 玩家资源管理.instance.Spell;
            Power = 玩家资源管理.instance.Power;
            Spell = 玩家资源管理.instance.Heart;
            Point = 玩家资源管理.instance.Point;
        }
    }

    private void Change_Heart_Spell()
    {
        int Heart_Integer = (int)Heart;
        int Heart_Decimals = (int)((Heart - Heart_Integer) * 5f + 0.01f);

        for (int i = 0; i < 符卡.Length; i++)
        {
            if (i < Heart_Integer)
            {
                符卡[i].Change_Star(5);
            }
            else if (i == Heart_Integer)
            {
                符卡[i].Change_Star(Heart_Decimals);
            }
            else
            {
                符卡[i].Change_Star(0);
            }
        }

        int Spell_Integer = (int)Spell;
        int Spell_Decimals = (int)((Spell - Spell_Integer) * 5f + 0.01f);

        for (int i = 0; i < 残机.Length; i++)
        {
            if (i < Spell_Integer)
            {
                残机[i].Change_Star(5);
            }
            else if (i == Spell_Integer)
            {
                残机[i].Change_Star(Spell_Decimals);
            }
            else
            {
                残机[i].Change_Star(0);
            }
        }
    }

    private void Start()
    {
    }

    private void Update()
    {
        Scan();
        Change_Heart_Spell();
        PowerText.text = Power.ToString("F2") + "/" + 玩家资源管理.instance.MaxPower.ToString("F2");
    }
}