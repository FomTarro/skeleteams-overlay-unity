using System.Collections;
using System.Collections.Generic;
using Skeletom.BattleStation.Integrations.OBS;
using Skeletom.Essentials.Utils;
using UnityEngine;
using UnityEngine.UI;

public class MicVolumeBorder : MonoBehaviour
{

    [SerializeField]
    private Outline _outline;

    [SerializeField]
    private float _threshold = 0.05f;

    private ShiftingAverage _avg = new ShiftingAverage(10);

    // Start is called before the first frame update
    void Start()
    {
        OBSIntegration.Instance.onMicVolume.AddListener((vol) =>
        {
            if (this.enabled)
            {
                _avg.AddValue(vol);
                _outline.enabled = _avg.Average > _threshold;
            }
            else
            {
                _outline.enabled = false;
            }
        });
    }
}
