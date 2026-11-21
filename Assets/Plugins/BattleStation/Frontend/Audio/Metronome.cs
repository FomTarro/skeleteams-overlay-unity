using System;
using System.Collections;
using UnityEngine;

namespace Skeletom.BattleStation.Audio
{
    public class Metronome : MonoBehaviour
    {
        private const int _base = 4;
        private float _progression = 0f;
        private int _bpm = 120;
        private float _nextTime = 0f;
        private float _interval = 1f;

        private Coroutine _ticking;

        /// <summary>
        /// The current lerp value along the current beat, sweeping from 0 to beatsPerMeasure to allow for effects every beat of the measure
        /// </summary>
        public Beat Sync { get; private set; }

        [Serializable]
        public struct Beat
        {
            public float currentBeat;
            public int beatsPerMeasure;
            public int beatsPerMinute;

            public Beat(float beat, int perMeasure, int perMinute)
            {
                currentBeat = beat;
                beatsPerMeasure = perMeasure;
                beatsPerMinute = perMinute;
            }
        }

        public void SetBPM(int bpm)
        {
            _bpm = bpm;
            var multiplier = _base / 4f;
            var tmpInterval = 60f / _bpm;
            _interval = tmpInterval / multiplier;
            _nextTime = Time.time; // set the relative time to now
        }

        public void StartMetronome(int bpm)
        {
            if (_ticking != null)
                StopCoroutine(_ticking);
            SetBPM(bpm);
            _ticking = StartCoroutine(Tick());
        }

        IEnumerator Tick()
        {
            do
            {
                // do something with this beat
                _nextTime += _interval; // add interval to our relative time
                float nextDelta = _nextTime - Time.time;
                do
                {
                    _progression += (Time.deltaTime / _interval);
                    if (_progression >= _base)
                    {
                        _progression = 0f;
                    }
                    yield return null;
                } while (nextDelta - Time.time > 0);

            } while (true);
        }

        // Update is called once per frame
        void Update()
        {
            Sync = new Beat(_progression, _base, _bpm);
        }
    }
}
