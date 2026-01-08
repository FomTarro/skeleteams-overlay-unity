using System.Collections;
using System.Collections.Generic;
using Skeletom.BattleStation.Graphics.Animations;
using Skeletom.BattleStation.Integrations;
using UnityEngine;
using UnityEngine.UI;

public class ChatMessageDisplay : MonoBehaviour
{
    [SerializeField]
    private TMPro.TMP_Text _text;
    [SerializeField]
    private AnimatedTextureDisplay _imgPrefab;
    [SerializeField]
    private List<AnimatedTextureDisplay> _emotes = new List<AnimatedTextureDisplay>();
    [SerializeField]
    private ChatMessage message;

    public void Display(ChatMessage message)
    {
        this.message = message;
        _text.text = "";
        foreach (AnimatedTextureDisplay emote in _emotes)
        {
            Destroy(emote.gameObject);
        }
        foreach (ChatMessage.Fragment fragment in message.fragments)
        {

            if (fragment.type == ChatMessage.Fragment.Type.EMOTE)
            {
                _text.text += "   ";
                _text.ForceMeshUpdate();
                float height = (_text.font.characterLookupTable['A'].glyph.metrics.height / _text.font.faceInfo.pointSize) * _text.fontSize;
                TMPro.TMP_TextInfo textInfo = _text.textInfo;
                int charIndex = textInfo.characterCount - 1;
                // 2. Get character information
                TMPro.TMP_CharacterInfo charInfo = textInfo.characterInfo[charIndex];
                // 3. Calculate the center of the character in local space
                AnimatedTextureDisplay child = Instantiate(_imgPrefab);
                child.transform.SetParent(_text.transform);
                // child.transform.sizeDelta = new Vector2(_text.fontSize, _text.fontSize) * 0.75f;
                Vector3 centerLocal = ((Vector2)charInfo.bottomLeft + (Vector2)charInfo.topRight + new Vector2(0, height)) / 2;
                child.transform.localPosition = centerLocal;
                child.transform.localScale = Vector3.one * _text.fontSize;
                child.DisplayTexture(fragment.image);
                _emotes.Add(child);
            }
            else
            {
                _text.text += fragment.text.Trim();
            }
        }
    }
}
