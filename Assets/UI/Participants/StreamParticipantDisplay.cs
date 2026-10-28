using Klak.Spout;
using Skeletom.BattleStation.Graphics.Animations;
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
    private AnimatedTextureDisplay _iconDisplay;
    [SerializeField]
    private MicVolumeBorder _micBorder;

    public string ID { get; private set; }

    public void Configure(ParticipantManager.StreamParticipant participant)
    {
        this.ID = participant.metadata.id;
        _nameCard.text = participant.metadata.name;
        _faceCamDisplay.sourceName = participant.metadata.faceSource;
        _iconDisplay.DisplayTexture(participant.icon);
        _micBorder.Configure(participant.metadata.micSource, participant.borderColor);
        _nameCardBackground.color = new Color(participant.borderColor.r, participant.borderColor.g, participant.borderColor.b, 0.5f);
    }
}
