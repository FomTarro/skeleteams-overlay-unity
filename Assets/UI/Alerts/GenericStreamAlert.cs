using System.Collections;
using System.Collections.Generic;
using Skeletom.BattleStation.Graphics.Animations;
using Skeletom.BattleStation.Integrations;
using UnityEngine;

public struct GenericStreamAlertContent
{
    public StreamUser user;
    public string description;
    public string details;
    public GenericStreamAlertContent(StreamUser user, string description, string details)
    {
        this.user = user;
        this.description = description;
        this.details = details;
    }
}

public class GenericStreamAlert : StreamAlertGameObject<GenericStreamAlertContent>
{
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

    public override void DisplayAlert(GenericStreamAlertContent message)
    {
        _userName.text = message.user.displayName;
        _title.text = message.description;
        _details.text = message.details;
        if (message.user.avatar != null)
        {
            Debug.Log(message.user.avatar.name);
            _avatarDisplay.DisplayTexture(message.user.avatar);
        }
        _jingle.Play();
        _animator.FadeTo(1, 1f);
    }

    public override void DisposeAlert()
    {
        _animator.FadeTo(0, 0.5f);
    }
}
