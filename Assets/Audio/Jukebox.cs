using System;
using System.Collections;
using System.Collections.Generic;
using Skeletom.Essentials.Lifecycle;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// A class that combines an audio track with its unique title.
/// </summary>
[Serializable]
public struct TrackListing
{
    public string title;
    public AudioClip track;
    public int BPM;
    public float vol;

    public string GetRegistryKey()
    {
        return title;
    }
}

/// <summary>
/// The manager for music tracks.
/// </summary>
public class Jukebox : Singleton<Jukebox>
{
    [SerializeField]
    private TrackListing[] _trackList;
    private Dictionary<string, TrackListing> _trackLookupRegistry = new Dictionary<string, TrackListing>();

    [SerializeField]
    private float _fadeTime = 0.5f;
    public float FadeTime
    {
        get { return _fadeTime; }
        set { _fadeTime = Mathf.Max(0, value); }
    }

    [SerializeField]
    private AudioSource _trackDeck;

    private float _volumeModifier = 0.5f;
    public float VolumeModifier
    {
        get { return _volumeModifier; }
        set { _volumeModifier = Mathf.Clamp01(value); }
    }

    [SerializeField]
    private AudioMixer _mixer;

    private Coroutine _changingClip;

    /// <summary>
    /// Changes the currently playing song to the one with the specified title.
    /// </summary>
    /// <param name="title"></param>
    public void ChangeMusic(string title)
    {
        if (_changingClip != null)
        {
            StopCoroutine(_changingClip);
        }
        _changingClip = StartCoroutine(ChangeClip(title, _fadeTime));
    }

    /// <summary>
    /// Fades out the existing song and fades in the newly requested song
    /// </summary>
    /// <param name="clipName"></param>
    /// <param name="seconds"></param>
    /// <returns></returns>
    private IEnumerator ChangeClip(string clipName, float seconds = 1.0f)
    {
        float t = 0.0f;
        float initialVol = _trackDeck.volume;
        if (_trackDeck.clip != null)
        {
            while (t <= 1.0)
            {
                t += Time.deltaTime / seconds;

                _trackDeck.volume = Mathf.Lerp(initialVol, 0, Mathf.SmoothStep(0.0f, 1.0f, t));
                yield return null;
            }
            _trackDeck.volume = 0.0f;
            _trackDeck.Stop();
            yield return new WaitForSeconds(seconds);
        }
        _trackDeck.clip = _trackLookupRegistry[clipName].track;
        _bpm = _trackLookupRegistry[clipName].BPM;
        float newVol = _trackLookupRegistry[clipName].vol;
        StartMetronome();
        t = 0.0f;

        if (!_trackDeck.isPlaying)
            _trackDeck.Play();

        while (t <= 1.0)
        {
            t += Time.deltaTime / seconds;

            _trackDeck.volume = Mathf.Lerp(0, newVol, Mathf.SmoothStep(0.0f, 1.0f, t));
            yield return null;
        }
        _trackDeck.volume = newVol;
        yield return null;
    }

    #region Metronome

    //beats per measure 
    private int _base = 4;
    private float _bpm = 120;

    private float _interval;
    private float _nextTime;

    private float _progression = 0f;

    /// <summary>
    /// The current lerp value along the current beat, sweeping from 0 to beatsPerMeasure to allow for effects every beat of the measure
    /// </summary>
    public JukeboxSyncInfo SyncInfo
    {
        get { return new JukeboxSyncInfo(_progression, _base); }
    }

    private Coroutine _ticking;

    public void StartMetronome()
    {
        if (_ticking != null)
            StopCoroutine(_ticking);
        //_currentStep = 1;
        var multiplier = _base / 4f;
        var tmpInterval = 60f / _bpm;
        _interval = tmpInterval / multiplier;
        _nextTime = Time.time; // set the relative time to now
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

    public override void Initialize()
    {
        foreach (TrackListing track in _trackList)
        {
            _trackLookupRegistry.Add(track.title, track);
        }
    }
    #endregion
}

public struct JukeboxSyncInfo
{
    public float currentBeat;
    public int beatsPerMeasure;

    public JukeboxSyncInfo(float beat, int perMeasure)
    {
        currentBeat = beat;
        beatsPerMeasure = perMeasure;
    }
}