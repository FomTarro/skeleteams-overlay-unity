using System;
using System.Collections.Generic;
using Skeletom.BattleStation.Graphics.Animations;
using Skeletom.BattleStation.Integrations;
using Skeletom.Essentials.Utils;
using TMPro;
using UnityEngine;

public class ChatMessageDisplay : MonoBehaviour
{
    [SerializeField]
    private TMP_Text _text;

    [SerializeField]
    private AnimatedTextureDisplay _imgPrefab;
    private readonly Dictionary<string, AnimatedTextureDisplay> _emotes = new Dictionary<string, AnimatedTextureDisplay>();

    [SerializeField]
    private IntegrationChatMessage message;

    public void Display(IntegrationChatMessage message)
    {
        this.message = message;
        _text.text = "";
        foreach (string emote in _emotes.Keys)
        {
            Destroy(_emotes[emote].gameObject);
        }
        _emotes.Clear();
        foreach (IntegrationChatMessage.Fragment fragment in message.fragments)
        {
            if (fragment.type == IntegrationChatMessage.Fragment.Type.EMOTE)
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
                _text.text += TextUtils.RemoveConsecutiveWhitespace(fragment.text);
            }
        }
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
}
