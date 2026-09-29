using System;
using Skeletom.BattleStation.Integrations;
using Skeletom.BattleStation.Integrations.Twitch;
using UnityEngine;

public class GenericStreamAlertLog : StreamEventLogGameObject<GenericStreamAlert>
{
    [SerializeField]
    private RectTransform _logParent;

    // Start is called before the first frame update
    void Start()
    {
        TwitchIntegration.Instance.onChannelRaid.AddListener((raid) =>
        {
            StreamEvent content = new(
                raid.raider,
                "Raid Alert",
                "Incoming Channel Raid",
                $"from {raid.raider.displayName}",
                $"with {raid.viewers} raiders"
            );
            var obj = Display(content);
            obj.SetLog(this);
            obj.transform.SetParent(_logParent);
        });
        TwitchIntegration.Instance.onChatRedeem.AddListener((redeem) =>
        {
            Debug.LogWarning(redeem.rewardId);
            // TODO: this is hardcoded, can we change that later?
            if (redeem.rewardId.Equals("38ea65b7-81eb-4fc0-b27e-35b2e59bf751"))
            {
                StreamEvent content = new(
                    redeem.redeemer,
                    "Meeting Reminder",
                    redeem.message.Trim(),
                    $"scheduled by {redeem.redeemer.displayName}",
                    $"In {UnityEngine.Random.Range(5, 16)} minutes"
                );
                var obj = Display(content);
                obj.SetLog(this);
                obj.transform.SetParent(_logParent);
            }
        });
    }
}
