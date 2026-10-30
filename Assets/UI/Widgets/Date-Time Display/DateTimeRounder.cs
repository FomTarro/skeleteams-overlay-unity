using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DateTimeRounder : MonoBehaviour
{
    [SerializeField]
    private TMPro.TMP_Text _text;

    private void Start()
    {
        LayoutManager.Instance.onStreamTimeChecked.AddListener((time) =>
        {
            var rounded = RoundToNearestMinutes(time, 15);
            string format = "dddd, MMMM dd - hh:mm tt";
            _text.text = rounded.ToString(format) + " EST";
        });
    }

    public DateTime RoundToNearestMinutes(DateTime input, int minutes)
    {
        var intervalTicks = TimeSpan.FromMinutes(minutes).Ticks;
        var halfIntervalTicks = (intervalTicks + 1) >> 1; // Optimized division by 2

        // Add half the interval, then floor to the nearest full interval
        var roundedTicks = (input.Ticks + halfIntervalTicks) / intervalTicks * intervalTicks;

        return new DateTime(roundedTicks, input.Kind);
    }
}
