using Skeletom.BattleStation.Integrations;
using Skeletom.BattleStation.Integrations.Twitch;
using UnityEngine;

public class StreamTitleDisplay : MonoBehaviour
{
    [SerializeField]
    private TMPro.TMP_Text _title;

    // Start is called before the first frame update
    void Start()
    {
        TwitchIntegration.Instance.onStreamInfoUpdate.AddListener(OnInfoUpdate);
    }

    private void OnInfoUpdate(StreamInfo info)
    {
        _title.text = info.title;
    }
}
