using UnityEngine;

public class Metronome : MonoBehaviour
{
    private const int _base = 4;
    private float _progression = 0f;
    private int _bpm = 120;
    private float _nextTime = 0f;
    private float _nextDelta = 0f;
    private float _interval = 1f;

    private bool _ticking = false;

    /// <summary>
    /// The current lerp value along the current beat, sweeping from 0 to beatsPerMeasure to allow for effects every beat of the measure
    /// </summary>
    public Beat Sync
    {
        get { return new Beat(_progression, _base); }
    }

    public readonly struct Beat
    {
        public readonly float currentBeat;
        public readonly int beatsPerMeasure;

        public Beat(float beat, int perMeasure)
        {
            currentBeat = beat;
            beatsPerMeasure = perMeasure;
        }
    }

    public void StartMetronome(int bpm)
    {
        _ticking = true;
        _bpm = bpm;
        var multiplier = _base / 4f;
        var tmpInterval = 60f / _bpm;
        _interval = tmpInterval / multiplier;
        _nextTime = Time.time;
    }

    public void StopMetronome()
    {
        _ticking = false;
    }

    // Update is called once per frame
    void Update()
    {
        if(_ticking){
            // do something with this beat
            if(_nextDelta - Time.time > 0){
                _progression += (Time.deltaTime / _interval);
                if (_progression >= _base)
                {
                    _progression = 0f;
                }
            }
            else
            {
                _nextTime += _interval; // add interval to our relative time
                _nextDelta = _nextTime - Time.time;
            }
        }
    }
}
