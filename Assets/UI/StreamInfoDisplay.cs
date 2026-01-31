using System.Collections;
using System.Collections.Generic;
using Skeletom.BattleStation.Integrations;
using Skeletom.BattleStation.Integrations.Twitch;
using UnityEngine;

public class StreamInfoDisplay : MonoBehaviour
{

    [SerializeField]
    private TMPro.TMP_Text _title;

    // Start is called before the first frame update
    void Start()
    {
        TwitchIntegration.Instance.onStreamInfoUpdate.AddListener(OnInfoUpdate);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnInfoUpdate(StreamInfo info)
    {
        _title.text = info.title;
    }
}
