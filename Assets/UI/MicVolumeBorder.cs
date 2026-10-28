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
    void Update()
    {
        if (this.enabled)
        {
            float vol = ParticipantManager.Instance.GetMicSourceVolume(_micSource);
            _avg.AddValue(vol);
            _outline.enabled = _avg.Average > _threshold;
        }
        else
        {
            _outline.enabled = false;
        }
    }

    public void Configure(string sourceName, Color color)
    {
        _micSource = sourceName;
        _outline.effectColor = color;
    }
}
