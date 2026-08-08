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
            StreamEvent content = new(raid.raider, "Incoming Channel Raid", $"With {raid.viewers} raiders");
            var obj = Display(content);
            obj.SetLog(this);
            obj.transform.SetParent(_logParent);
        });
    }
}
