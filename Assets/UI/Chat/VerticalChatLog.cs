using System;
using System.Collections;
using System.Collections.Generic;
using Skeletom.BattleStation.Integrations.Twitch;
using UnityEngine;
using UnityEngine.UI;

public class VerticalChatLog : ChatLogGameObject<VerticalChatMessage>
{
    [SerializeField]
    private ScrollRect _scroll;
    [SerializeField]
    private RectTransform _logParent;

    // Start is called before the first frame update
    void Start()
    {
        TwitchIntegration.Instance.onChatMessage.AddListener((msg) =>
        {
            var obj = DisplayMessage(msg);
            obj.transform.SetParent(_logParent);
            CheckPriorMessages();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_logParent);
        });
        TwitchIntegration.Instance.onChatMessageDelete.AddListener((msg) =>
        {
            DisposeMessage(msg);
            CheckPriorMessages();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_logParent);
        });
    }

    private void CheckPriorMessages()
    {
        List<VerticalChatMessage> sortedMessages = new List<VerticalChatMessage>(Messages.Values);
        sortedMessages.Sort((a, b) => DateTime.Compare(a.Timestamp, b.Timestamp));
        if (sortedMessages.Count > 0)
        {
            sortedMessages[0].ToggleUserInfo(true);
            for (int i = 1; i < sortedMessages.Count; i++)
            {
                if ((sortedMessages[i].Timestamp.Minute == sortedMessages[i - 1].Timestamp.Minute)
                && (sortedMessages[i].Timestamp.Hour == sortedMessages[i - 1].Timestamp.Hour)
                && sortedMessages[i].ChatterId.Equals(sortedMessages[i - 1].ChatterId))
                {
                    sortedMessages[i].ToggleUserInfo(false);
                }
                else
                {
                    sortedMessages[i].ToggleUserInfo(true);
                }
            }
        }
    }

    // Update is called once per frame
    void Update()
    {

    }

    void LateUpdate()
    {
        _scroll.verticalNormalizedPosition = 0f;
    }
}
