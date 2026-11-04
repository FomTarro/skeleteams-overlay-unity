using System;
using Skeletom.Essentials.Utils;
using UnityEngine;

public class AdTimer : MonoBehaviour
{
    [SerializeField]
    private TMPro.TMP_Text _text;

    private DateTime _adTime = DateTime.Now;

    [SerializeField]
    private Color _good;
    [SerializeField]
    private Color _bad;

    private void Start()
    {
        LayoutManager.Instance.onNextAdTimeChecked.AddListener((time) =>
        {
            _adTime = time;
        });
    }

    private void Update()
    {
        DateTime now = DateTime.Now.ToUniversalTime();
        TimeSpan diff = _adTime - now;
        if (now > _adTime)
        {
            diff = TimeSpan.Zero;
        }
        string str = diff.ToString(@"hh\:mm\:ss");
        float t = MathUtils.Normalize((diff.Ticks/10_000_000), 60*50, 0, 0, 1);
        string html = ColorUtility.ToHtmlStringRGBA(Color.Lerp(_bad, _good, t));
        _text.text = $"Next ad break: <color=#{html}>{str}</color>";
    }
}
