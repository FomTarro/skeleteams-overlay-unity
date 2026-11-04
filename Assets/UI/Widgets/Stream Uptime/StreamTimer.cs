using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StreamTimer : MonoBehaviour
{
    [SerializeField]
    private TMPro.TMP_Text _text;
    // Start is called before the first frame update

    private DateTime _lastCheck = DateTime.Now;

    private void Start()
    {
        LayoutManager.Instance.onStreamTimeChecked.AddListener((time) =>
        {
            _lastCheck = time;
        });
    }

    private void Update()
    {
        DateTime now = DateTime.Now;
        TimeSpan diff = now - _lastCheck;
        if (now < _lastCheck)
        {
            diff = TimeSpan.Zero;
        }
        string str = diff.ToString(@"hh\:mm\:ss");
        _text.text = $"{str}";
    }

}
