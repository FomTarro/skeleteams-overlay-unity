using Klak.Spout;
using Skeletom.BattleStation.Graphics.Animations;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StreamParticipantDisplay : MonoBehaviour
{
    public string ID { get { return _id; } }
    [SerializeField]
    private string _id;
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

    public void Configure(ParticipantManager.StreamParticipant participant)
    {
        _id = participant.metadata.id;
        _nameCard.text = participant.metadata.name;
        _faceCamDisplay.sourceName = participant.metadata.faceSource;
        if (participant.icon != null)
        {
            _iconDisplay.DisplayTexture(participant.icon);
        }
        _micBorder.Configure(participant.metadata.micSource, participant.borderColor);
        _nameCardBackground.color = new Color(participant.borderColor.r * 0.5f, participant.borderColor.g * 0.5f, participant.borderColor.b * 0.5f, 1f);
    }

    private void Update()
    {
        if (_micBorder.OutlineEnabled && !_iconDisplay.IsAnimating)
        {
            _iconDisplay.StartAnimation();
        }
        else if (!_micBorder.OutlineEnabled)
        {
            _iconDisplay.StopAnimation();
            _iconDisplay.ResetDefaults();
        }
    }
}
