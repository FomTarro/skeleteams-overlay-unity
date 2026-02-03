using System.Collections;
using System.Collections.Generic;
using Skeletom.BattleStation.Integrations.Twitch;
using UnityEngine;
using UnityEngine.UI;

public class ChatLog : MonoBehaviour
{
    [SerializeField]
    private ScrollRect _scroll;
    [SerializeField]
    private RectTransform _prefabParent;

    [SerializeField]
    private ChatMessageDisplay _prefab;


    // Start is called before the first frame update
    void Start()
    {
        TwitchIntegration.Instance.onChatMessage.AddListener((message) =>
        {
            var chatObject = Instantiate(_prefab);
            chatObject.Display(message);
            chatObject.transform.SetParent(_prefabParent);
        });
    }

    // Update is called once per frame
    void LateUpdate()
    {
        _scroll.verticalNormalizedPosition = 0f;
    }
}
