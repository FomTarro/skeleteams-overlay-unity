using Skeletom.BattleStation.Integrations;
using UnityEngine;

public class RaidAlertLog : StreamEventLogGameObject<GenericStreamAlert>
{
    [SerializeField]
    private RectTransform _logParent;

    // Start is called before the first frame update
    void Start()
    {
        IntegrationManager.Instance.Twitch.onChannelRaid.AddListener((raid) =>
        {
            StreamEvent content = new(
                raid.raider,
                "Raid Alert",
                $"{raid.raider.displayName}",
                $"is raiding you with {raid.viewers} viewers",
                $"UNUSED"
            );
            var obj = Display(content);
            obj.SetLog(this);
            obj.transform.SetParent(_logParent);
        });
    }
}
