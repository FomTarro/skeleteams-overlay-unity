using System;
using System.Collections;
using System.Collections.Generic;
using Skeletom.BattleStation.Integrations.Twitch;
using UnityEngine;

public class AdTimer : MonoBehaviour
{
    [SerializeField]
    private int _pollingInterval = 5;
    [SerializeField]
    private TMPro.TMP_Text _text;

    private DateTime _nextAd = DateTime.Now;
    // Start is called before the first frame update

    // Update is called once per frame
    float delta = 0f;
    void Update()
    {
        delta += Time.deltaTime;
        if (delta > _pollingInterval)
        {
            TwitchIntegration.Instance.GetAdSchedule((schedule) =>
            {
                Debug.Log(JsonUtility.ToJson(schedule));
                _nextAd = DateTimeOffset.FromUnixTimeMilliseconds(schedule.next_ad_at).DateTime;
            }, (err) =>
            {
                Debug.LogError(err.message);
                _nextAd = DateTime.Now;
            });
            delta = 0f;
        }
        DateTime now = DateTime.Now;
        TimeSpan diff = _nextAd - now;
        if (now > _nextAd)
        {
            diff = TimeSpan.Zero;
        }
        string str = diff.ToString(@"hh\:mm\:ss");
        _text.text = $"Next ad break: {str}";
    }
}
