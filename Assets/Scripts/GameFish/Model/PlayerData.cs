using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using QFramework;
using UnityEngine;

[JsonObject(MemberSerialization.OptIn)]
public class PlayerData
{
    [JsonProperty("PlayerId")]public int _PlayerId;
    [JsonProperty("Score")]public BindableProperty<int> Score { get; } = new BindableProperty<int>(0);
    [JsonProperty("CurrentCannonId")]public BindableProperty<int> CurrentCannonId { get; } = new BindableProperty<int>(0);
    [JsonProperty("PlayerPrompt")]public BitFlag<PLAYER_PROMPT> PlayerPrompt { get; } = new BitFlag<PLAYER_PROMPT>();
    public PlayerData(int playerId)
    {
        _PlayerId = playerId;
    }
}
[System.Flags]
public enum PLAYER_PROMPT
{
    None,
    急停按钮已按下      = 1 << 0,
    未系安全带          = 1 << 1,
    正在关机中          = 1 << 2,
    正在重启中          = 1 << 3,
    正在下载影片        = 1 << 4,
    正在升级程序        = 1 << 5,
    头盔未放好          = 1 << 6,
    退票中              = 1 << 7,
    退票故障            = 1 << 8,
}