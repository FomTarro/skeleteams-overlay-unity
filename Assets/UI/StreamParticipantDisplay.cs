using Klak.Spout;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StreamParticipantDisplay : MonoBehaviour
{
    [SerializeField]
    private TMP_Text _nameCard;
    [SerializeField]
    private Image _nameCardBackground;
    [SerializeField]
    private SpoutReceiver _faceCamDisplay;
    [SerializeField]
    private Image _iconDisplay;
    [SerializeField]
    private MicVolumeBorder _micBorder;

    public string Key { get; private set; }

    public void Configure(LayoutManager.StreamParticipant participant)
    {
        this.Key = participant.key;
        _nameCard.text = participant.name;
        _faceCamDisplay.sourceName = participant.faceSourceName;
        _iconDisplay.sprite = participant.icon;
        _micBorder.Configure(participant.micSourceName, participant.borderColor);
        _nameCardBackground.color = new Color(participant.borderColor.r, participant.borderColor.g, participant.borderColor.b, 0.5f);
    }
}
