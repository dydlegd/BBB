using System.Collections;
using System.Collections.Generic;
using QFramework;
using UnityEngine;

public class PlayerModel : AbstractModel
{
    public const int MAX_PLAYER_COUNT = 4;
    public PlayerData[] playerDatas{get; private set;}

    protected override void OnInit()
    {
        playerDatas = new PlayerData[MAX_PLAYER_COUNT];
        for (int i = 0; i < MAX_PLAYER_COUNT; i++)
        {
            playerDatas[i] = new PlayerData(i);
        }
        
    }
}
