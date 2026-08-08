using System;
using Skeletom.BattleStation.Graphics.Animations;
using Skeletom.BattleStation.Integrations;
using Skeletom.BattleStation.Integrations.Twitch;
using UnityEngine;
using UnityEngine.UI;


public class GenericStreamAlert : StreamEventGameObject
{
    private GenericStreamAlertLog _log;
    private string _id;
    private string _userId;
    [SerializeField]
    private TMPro.TMP_Text _userName;
    [SerializeField]
    private TMPro.TMP_Text _title;
    [SerializeField]
    private TMPro.TMP_Text _details;
    [SerializeField]
    private AnimatedTextureDisplay _avatarDisplay;

    [SerializeField]
    private AudioSource _jingle;

    [SerializeField]
    private AnimatedFade _animator;

    [SerializeField]
    private Button _shoutoutButton;


    public void SetLog(GenericStreamAlertLog log)
    {
        _log = log;
    }
    public override void Display(StreamEvent data)
    {
        _id = data.ID;
        _userId = data.user.id;
        _userName.text = data.user.displayName;
        _title.text = data.description;
        _details.text = data.details;
        if (data.user.avatar != null)
        {
            Debug.Log(data.user.avatar.name);
            _avatarDisplay.DisplayTexture(data.user.avatar);
        }
        _jingle.Play();
        _animator.FadeTo(1, 1f);
    }

    public override void Dispose()
    {
        _animator.FadeTo(0, 0.5f, () => { _log.Dispose(_id); });
    }

    public void ShoutOutUser()
    {
        TwitchIntegration.Instance.SendShoutout(_userId, () => { }, (err) => { Debug.LogError(err); });
    }
}
