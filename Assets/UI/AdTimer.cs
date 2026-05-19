using System;
using UnityEngine;

public class AdTimer : MonoBehaviour
{
    [SerializeField]
    private TMPro.TMP_Text _text;

    private DateTime _lastCheck = DateTime.Now;

    private void Start()
    {
        LayoutManager.Instance.onNextAdTimeChecked.AddListener((time) =>
        {
            _lastCheck = time;
        });
    }

    private void Update()
    {
        DateTime now = DateTime.Now.ToUniversalTime();
        TimeSpan diff = _lastCheck - now;
        if (now > _lastCheck)
        {
            diff = TimeSpan.Zero;
        }
        string str = diff.ToString(@"hh\:mm\:ss");
        _text.text = $"Next ad break: {str}";
    }
}
