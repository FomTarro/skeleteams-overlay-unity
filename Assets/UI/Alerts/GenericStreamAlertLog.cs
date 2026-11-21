using Skeletom.BattleStation.Integrations;
using UnityEngine;

public class GenericStreamAlertLog : StreamEventLogGameObject<GenericStreamAlert>
{
    [SerializeField]
    private RectTransform _logParent;

    // Start is called before the first frame update
    void Start()
    {
        IntegrationManager.Instance.Twitch.onChatRedeem.AddListener((redeem) =>
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
        IntegrationManager.Instance.Twitch.onChannelPaidSubscription.AddListener((redeem) =>
        {
            StreamEvent content = new(
                redeem.subscriber,
                "Paid Subscription",
                "What should go here?",
                $"{redeem.subscriber.displayName} has subscribed",
                $"for {redeem.streak} months"
            );
            var obj = Display(content);
            obj.SetLog(this);
            obj.transform.SetParent(_logParent);
        });
    }
}
