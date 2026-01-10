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
        _emotes.Clear();
        foreach (ChatMessage.Fragment fragment in message.fragments)
        {
            if (fragment.type == ChatMessage.Fragment.Type.EMOTE)
            {
                _text.text += "<sprite index=0>";
                _text.ForceMeshUpdate();
                // float height = (_text.font.characterLookupTable['A'].glyph.metrics.height / _text.font.faceInfo.pointSize) * _text.fontSize;
                TMPro.TMP_TextInfo textInfo = _text.textInfo;
                Debug.Log(_text.font.faceInfo.descentLine + " : " + _text.font.faceInfo.baseline + " : " + _text.font.faceInfo.ascentLine + " : " + _text.font.faceInfo.pointSize);
                int charIndex = textInfo.characterCount - 1;
                // 2. Get character information
                TMPro.TMP_CharacterInfo charInfo = textInfo.characterInfo[charIndex];
                float height = charInfo.topLeft.y - charInfo.bottomLeft.y;
                // 3. Calculate the center of the character in local space
                Vector3 centerLocal = ((Vector2)charInfo.bottomLeft + (Vector2)charInfo.topRight) / 2;
                AnimatedTextureDisplay child = Instantiate(_imgPrefab);
                child.transform.SetParent(_text.transform);
                child.transform.localPosition = centerLocal;
                child.transform.localScale = Vector3.one * height;
                child.DisplayTexture(fragment.image);
                _emotes.Add(child);
            }
            else
            {
                _text.text += fragment.text.Trim();
            }
        }
    }

    private void Update()
    {

    }
}
