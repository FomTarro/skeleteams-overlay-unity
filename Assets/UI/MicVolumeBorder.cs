using System.Collections;
using System.Collections.Generic;
using Skeletom.BattleStation.Integrations.OBS;
using Skeletom.Essentials.Utils;
using UnityEngine;
using UnityEngine.UI;

public class MicVolumeBorder : MonoBehaviour
{

    [SerializeField]
    private string _micSource;
    [SerializeField]
    private Outline _outline;

    [SerializeField]
    private float _threshold = 0.05f;

    private readonly ShiftingAverage _avg = new(10);

    // Start is called before the first frame update
    void Start()
    {
        OBSIntegration.Instance.onVolume.AddListener((source, vol) =>
        {
            if (this.enabled)
            {
                if (_micSource.Equals(source))
                {
                    _avg.AddValue(vol);
                    _outline.enabled = _avg.Average > _threshold;
                }
            }
            else
            {
                _outline.enabled = false;
            }
        });
    }

    public void Configure(string sourceName, Color color)
    {
        _micSource = sourceName;
        _outline.effectColor = color;
    }
}
