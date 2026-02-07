using System;
using System.Collections;
using System.Collections.Generic;
using Skeletom.BattleStation.Graphics.Animations;
using Skeletom.BattleStation.Integrations;
using Skeletom.Essentials.Utils;
using TMPro;
using UnityEngine;

public class VerticalChatMessage : ChatMessageGameObject
{
    [SerializeField]
    private GameObject _avatarMask;
    [SerializeField]
    private GameObject _userInfoBox;
    [SerializeField]
    private AnimatedTextureDisplay _badge;
    [SerializeField]
    private TMP_Text _username;
    [SerializeField]
    private TMP_Text _timestamp;
    [SerializeField]
    private TMP_Text _text;

    [SerializeField]
    private AnimatedTextureDisplay _imgPrefab;
    private readonly Dictionary<string, AnimatedTextureDisplay> _emotes = new Dictionary<string, AnimatedTextureDisplay>();

    [SerializeField]
    private StreamChatMessage message;

    public DateTime Timestamp { get { return message.timestamp; } }
    public string ChatterId { get { return message.chatter.id; } }


    public override void DisplayMessage(StreamChatMessage message)
    {
        this.message = message;
        if (message.badges.Count > 0)
        {
            _badge.gameObject.SetActive(true);
            _badge.DisplayTexture(message.badges[0].image);
        }
        else
        {
            _badge.gameObject.SetActive(false);
        }
        _username.text = message.chatter.displayName;
        _username.color = message.nameColor;
        _timestamp.text = message.timestamp.ToString("hh:mm") + (message.timestamp.Hour > 11 ? " PM" : " AM");
        _text.text = "";
        foreach (string emote in _emotes.Keys)
        {
            Destroy(_emotes[emote].gameObject);
        }
        _emotes.Clear();
        bool hasText = false;

        foreach (StreamChatMessage.Fragment fragment in message.message)
        {
            if (fragment.type == StreamChatMessage.Fragment.Type.EMOTE)
            {
                string id = Guid.NewGuid().ToString();
                // Sprite 0 is just a blank square, the link tags allow us to ID the sprite,
                // So that we can connect emote objects to positions in the text
                _text.text += $"<link=\"{id}\"><sprite index=0></link>";
                _text.ForceMeshUpdate();
                AnimatedTextureDisplay emote = Instantiate(_imgPrefab);
                emote.transform.SetParent(_text.transform);
                emote.DisplayTexture(fragment.image);
                _emotes.Add(id, emote);
            }
            else
            {
                string sanitized = TextUtils.RemoveConsecutiveWhitespace(fragment.text);
                _text.text += sanitized;
                hasText = true;
            }
        }
        if (!hasText)
        {
            _text.fontSize = 64;
        }
        else
        {
            _text.fontSize = 24;
        }
        _text.text += " ";
    }

    public override void DisposeMessage()
    {
        // no additional cleanup needed
    }

    private void Update()
    {
        foreach (TMP_LinkInfo linkInfo in _text.textInfo.linkInfo)
        {
            string id = linkInfo.GetLinkID();
            if (id != null && id.Length > 0 && _emotes.ContainsKey(id))
            {
                AnimatedTextureDisplay emote = _emotes[id];
                // Calculate the center of the character in local space
                TMP_CharacterInfo charInfo = _text.textInfo.characterInfo[linkInfo.linkTextfirstCharacterIndex];
                Vector3 centerLocal = ((Vector2)charInfo.bottomLeft + (Vector2)charInfo.topRight) / 2;
                // float height = (_text.font.characterLookupTable['A'].glyph.metrics.height / _text.font.faceInfo.pointSize) * _text.fontSize;
                float glyphHeight = charInfo.topLeft.y - charInfo.bottomLeft.y;
                emote.transform.localPosition = centerLocal;
                emote.transform.localScale = Vector3.one * glyphHeight;
            }
        }
    }

    public void ToggleUserInfo(bool toggle)
    {
        _userInfoBox.SetActive(toggle);
        _avatarMask.SetActive(toggle);
    }
}
