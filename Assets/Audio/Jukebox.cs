using System;
using System.Collections;
using System.Collections.Generic;
using Skeletom.Essentials.Lifecycle;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;

/// <summary>
/// A class that combines an audio track with its unique title.
/// </summary>
[Serializable]
public struct TrackListing
{
    public string title;
    public string displayName;
    public AudioClip track;
    public int BPM;
    public float vol;
}

/// <summary>
/// The manager for music tracks.
/// </summary>
public class Jukebox : Singleton<Jukebox>
{
    [SerializeField]
    private TrackListing[] _trackList;
    private readonly Dictionary<string, TrackListing> _trackLookupRegistry = new();

    [SerializeField]
    private float _fadeTime = 0.5f;

    [SerializeField]
    private AudioSource _trackDeck;

    [SerializeField]
    private AudioMixer _mixer;

    private Coroutine _changingClip;

    [Serializable]
    public class VolumeChangeEvent : UnityEvent<VolumeGroup, float> { }
    public VolumeChangeEvent onVolumeChanged = new();

    [Serializable]
    public class SongChangeEvent : UnityEvent<TrackListing> { }
    public SongChangeEvent onSongChanged = new();

    public override void Initialize()
    {
        foreach (TrackListing track in _trackList)
        {
            _trackLookupRegistry.Add(track.title, track);
        }
    }

    public void ChangeSong(string title, float targetVolume)
    {
        if (_changingClip != null)
        {
            StopCoroutine(_changingClip);
        }
        _changingClip = StartCoroutine(ChangeClip(title, targetVolume, _fadeTime));
    }

    private IEnumerator ChangeClip(string clipName, float targetVolume, float seconds = 1.0f)
    {
        float t = 0.0f;
        float initialVol = GetVolume(VolumeGroup.MUSIC_RELATIVE);
        if (_trackDeck.clip != null)
        {
            while (t <= 1.0)
            {
                t += Time.deltaTime / seconds;

                SetVolume(VolumeGroup.MUSIC_RELATIVE, Mathf.Lerp(initialVol, 0, Mathf.SmoothStep(0.0f, 1.0f, t)));
                yield return null;
            }
            SetVolume(VolumeGroup.MUSIC_RELATIVE, 0f);
            _trackDeck.Stop();
            yield return new WaitForSeconds(seconds);
        }
        _trackDeck.clip = _trackLookupRegistry[clipName].track;
        _bpm = _trackLookupRegistry[clipName].BPM;
        onSongChanged.Invoke(_trackLookupRegistry[clipName]);
        StartMetronome();
        t = 0.0f;
        if (!_trackDeck.isPlaying)
            _trackDeck.Play();

        while (t <= 1.0)
        {
            t += Time.deltaTime / seconds;

            SetVolume(VolumeGroup.MUSIC_RELATIVE, Mathf.Lerp(0, targetVolume, Mathf.SmoothStep(0.0f, 1.0f, t)));
            yield return null;
        }
        SetVolume(VolumeGroup.MUSIC_RELATIVE, targetVolume);
        yield return null;
    }

    // This is necessary because apparently you can't set mixer properties from frame 1? Why?
	private readonly Queue<Action> DEFERRED_VOLUME_SET = new();
	private void LateUpdate()
	{
		do
		{
			DEFERRED_VOLUME_SET.TryDequeue(out Action result);
			result?.Invoke();
		} while (DEFERRED_VOLUME_SET.Count > 0);
	}

    /// <summary>
	/// Sets the volume level of a given group, accounting for the nonlinear nature of decibels
	/// </summary>
	/// <param name="group">The group to adjust</param>
	/// <param name="newVolume">The desired volume level, from 0.0 to 1.0</param>
	public void SetVolume(VolumeGroup group, float newVolume)
	{
		if (newVolume >= 0)
		{
			DEFERRED_VOLUME_SET.Enqueue(() =>
			{
				string mixerGroup = VolumeGroupToFloatName(group);
				if (!mixerGroup.Equals(string.Empty))
				{
					float newVolumeDb = Mathf.Max(Mathf.Log10(newVolume) * 40, -80);
					_mixer.SetFloat(mixerGroup, newVolumeDb);
                    onVolumeChanged.Invoke(group, newVolume);
				}
			});
		}
	}

    /// <summary>
	/// Gets the volume level of a given group as a value from 0.0 to 1.0, after mapping from decibels
	/// </summary>
	/// <param name="group">The group to check</param>
	/// <returns></returns>
	public float GetVolume(VolumeGroup group)
	{
		string mixerGroup = VolumeGroupToFloatName(group);
		if (!mixerGroup.Equals(string.Empty))
		{
			_mixer.GetFloat(mixerGroup, out float currentVol);
			currentVol = Mathf.Pow(10, currentVol / 40f);
			if (currentVol < 0.11)
			{
				return 0;
			}
			return currentVol;
		}
		return 0.0f;
	}

	private static string VolumeGroupToFloatName(VolumeGroup group)
	{
		string floatName = string.Empty;
		switch (group)
		{
			case VolumeGroup.MUSIC_MASTER:
				floatName = "VOL_MASTER";
				break;
			case VolumeGroup.MUSIC_RELATIVE:
				floatName = "VOL_RELATIVE";
				break;
		}
		return floatName;
	}

	public enum VolumeGroup : int
	{
		MUSIC_MASTER = 10,
		MUSIC_RELATIVE = 101,
	}

    #region Metronome

    // beats per measure 
    private int _base = 4;
    private float _bpm = 120;
    private float _progression = 0f;
    public readonly struct SyncInfo
    {
        public readonly float currentBeat;
        public readonly int beatsPerMeasure;

        public SyncInfo(float beat, int perMeasure)
        {
            currentBeat = beat;
            beatsPerMeasure = perMeasure;
        }
    }

    /// <summary>
    /// The current lerp value along the current beat, sweeping from 0 to beatsPerMeasure to allow for effects every beat of the measure
    /// </summary>
    public SyncInfo Sync
    {
        get { return new SyncInfo(_progression, _base); }
    }

    private Coroutine _ticking;

    public void StartMetronome()
    {
        if (_ticking != null){
            StopCoroutine(_ticking);
        }
        _ticking = StartCoroutine(Tick());
    }

    private IEnumerator Tick()
    {
        var multiplier = _base / 4f;
        var tmpInterval = 60f / _bpm;
        var interval = tmpInterval / multiplier;
        var nextTime = Time.time; // set the relative time to now
        do
        {
            // do something with this beat
            nextTime += interval; // add interval to our relative time
            float nextDelta = nextTime - Time.time;
            do
            {
                _progression += (Time.deltaTime / interval);
                if (_progression >= _base)
                {
                    _progression = 0f;
                }
                yield return null;
            } while (nextDelta - Time.time > 0);

        } while (true);
    }

    #endregion
}