using System.Collections;
using System.Collections.Generic;
using Skeletom.BattleStation.Integrations.Twitch;
using UnityEngine;

public class GenericStreamAlertLog : MonoBehaviour
{

    [SerializeField]
    private GenericStreamAlert _alertInstance;

    // Start is called before the first frame update
    void Start()
    {
        TwitchIntegration.Instance.onChannelRaid.AddListener((raid) =>
        {
            GenericStreamAlertContent content = new(raid.raider, "Incoming Channel Raid", $"With {raid.viewers} raiders");
            _alertInstance.DisplayAlert(content);
        });
    }

    // Update is called once per frame
    void Update()
    {

    }
}
