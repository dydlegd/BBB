using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using QFramework;
using UnityEngine.UI;

namespace GameFish
{
    public class GameFishController : BaseController
    {
        public Text ScoreText;
        // Start is called before the first frame update
        void Start()
        {
            this.GetModel<PlayerModel>().playerDatas[0].Score.RegisterWithInitValue((value) =>
            {
                Debug.Log($"Score: {value}");
                ScoreText.text = $"Score: {value}";
            }).UnRegisterWhenGameObjectDestroyed(gameObject);
            this.GetModel<PlayerModel>().playerDatas[0].PlayerPrompt
                .Subscribe(evt =>
                {
                    if (evt.Sender is PlayerData player)
                    {
                        Debug.Log($"PlayerPrompt: {evt.Flag}  {evt.OperateType} {evt.NewMask}  {evt.OldMask}  {player._PlayerId}");
                    }
                    else
                    {
                        Debug.Log($"PlayerPrompt: {evt.Flag}  {evt.OperateType} {evt.NewMask}  {evt.OldMask}  {evt.Sender}");
                    }
                })
                .UnRegisterWhenGameObjectDestroyed(gameObject);
        }

        // Update is called once per frame
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.I))
            {
                this.GetModel<PlayerModel>().playerDatas[0].Score.Value++;
                string json = JsonUtil.ToJson(this.GetModel<PlayerModel>().playerDatas);
                Debug.Log(json);
            }

            if (Input.GetKeyDown(KeyCode.I))
            {
                this.GetModel<PlayerModel>().playerDatas[0].PlayerPrompt.Add(PLAYER_PROMPT.未系安全带, this.GetModel<PlayerModel>().playerDatas[1]);
            }
            if (Input.GetKeyDown(KeyCode.O))
            {
                this.GetModel<PlayerModel>().playerDatas[0].PlayerPrompt.Remove(PLAYER_PROMPT.未系安全带, this.GetModel<PlayerModel>().playerDatas[1]);
            }
        }
    }
}