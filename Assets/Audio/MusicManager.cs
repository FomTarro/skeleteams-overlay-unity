using System;
using System.Collections;
using System.Collections.Generic;
using Skeletom.BattleStation.Server;
using Skeletom.Essentials.IO;
using Skeletom.Essentials.Lifecycle;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;
using UnityEngine.Networking;

public class MusicManager : Singleton<MusicManager>, ISaveable<MusicManager.PlaylistMetadata>
{
    public string FileFolder => "Music";

    public string FileName => "metadata.json";

    [Serializable]
    public class PlaylistMetadata : BaseSaveData
    {
        public float fadeTime;
        public List<TrackMetadata> tracks;

        [Serializable]
        public struct TrackMetadata
        {
            public string file;
            public string id;
            public string name;
            public string artist;
            public int year;
            public int bpm;
        }
    }

    [Serializable]
    public class MusicTrack
    {
        public string id;
        public string name;
        public string artist;
        public int year;
        public int bpm;
        public SaveDataManager.FileData file;
        public AudioClip clip;
    }

    public enum VolumeGroup : int
    {
        MUSIC_MASTER = 100,
        MUSIC_RELATIVE = 101,
        MUSIC_SCENE = 102
    }

    [SerializeField]
    private WebServer _webServer;

    [SerializeField]
    private List<MusicTrack> _tracks = new();

    [SerializeField]
    private float _fadeTime = 0.5f;

    [SerializeField]
    private AudioSource _source;

    [SerializeField]
    private AudioMixer _mixer;

    [SerializeField]
    private Metronome _metronome;
    public Metronome.Beat Beat
    {
        get { return _metronome.Sync; }
    }

    private Coroutine _changingClip;

    [Serializable]
    public class VolumeChangeEvent : UnityEvent<VolumeGroup, float> { }
    public VolumeChangeEvent onVolumeChanged = new();

    [Serializable]
    public class MusicTrackChangeEvent : UnityEvent<MusicTrack> { }
    public MusicTrackChangeEvent onMusicTrackChanged = new();

    private List<Endpoint> WebServerEndpoints
    {
        get
        {
            return new()
            {
                new Endpoint("/music/song", (req) =>
                {
                    var queryParam = new List<QueryParameter>(req.queryParameters).Find(param => param.key.Equals("title"));
                    if (queryParam != null)
                    {
                        PlayTrack(queryParam.value);
                    }
                    return new EndpointResponse(200, "");
                }),
                new Endpoint("/music/volume", (req) =>
                {
                    var queryParam = new List<QueryParameter>(req.queryParameters).Find(param => param.key.Equals("increment"));
                    if (queryParam != null)
                    {
                        float.TryParse(queryParam.value, out float increment);
                        SetVolume(VolumeGroup.MUSIC_MASTER, GetVolume(VolumeGroup.MUSIC_MASTER) + increment);
                    }
                    return new EndpointResponse(200, "");
                }),
                new Endpoint("/music/reload", (req) =>
                {
                    FromSaveData(SaveDataManager.Instance.ReadSaveData(this));
                    return new EndpointResponse(200, "");
                })
            };
        }
    }

    public override void Initialize()
    {
        SaveDataManager.Instance.OpenDataFolder();
        FromSaveData(SaveDataManager.Instance.ReadSaveData(this));
        // Music controls via HTTP
        foreach (Endpoint endpoint in WebServerEndpoints)
        {
            _webServer.RegisterEndpoint(endpoint);
        }
    }

    public void PlayTrack(string id, float volume = 1f)
    {
        if (_changingClip != null)
        {
            StopCoroutine(_changingClip);
        }
        MusicTrack track = _tracks.Find(t => t.id.Equals(id));
        if(track != null)
        {
            StartCoroutine(GetAudioClipFromFile(track, (t) =>
            {
                _changingClip = StartCoroutine(ChangeTrack(track, volume, _fadeTime));
            }));
        }
    }

    private IEnumerator ChangeTrack(MusicTrack track, float targetVolume, float seconds = 1.0f)
    {
        float t = 0.0f;
        float initialVol = GetVolume(VolumeGroup.MUSIC_RELATIVE);
        if (_source.clip != null)
        {
            while (t <= 1.0)
            {
                t += Time.deltaTime / seconds;
                SetVolume(VolumeGroup.MUSIC_RELATIVE, Mathf.Lerp(initialVol, 0, Mathf.SmoothStep(0.0f, 1.0f, t)));
                yield return null;
            }
            SetVolume(VolumeGroup.MUSIC_RELATIVE, 0f);
            _source.Stop();
            yield return new WaitForSeconds(seconds);
        }
        if (track == null)
        {
            yield break;
        }
        _source.clip = track.clip;
        onMusicTrackChanged.Invoke(track);
        _metronome.StartMetronome(track.bpm);
        t = 0.0f;
        if (!_source.isPlaying)
            _source.Play();

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
                    onVolumeChanged.Invoke(group, GetVolume(group));
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
        return group switch
        {
            VolumeGroup.MUSIC_MASTER => "VOL_MASTER",
            VolumeGroup.MUSIC_RELATIVE => "VOL_RELATIVE",
            VolumeGroup.MUSIC_SCENE => "VOL_SCENE",
            _ => string.Empty
        };
    }

    private IEnumerator GetAudioClipFromFile(MusicTrack track, Action<MusicTrack> onLoad)
    {
        if(track.clip != null)
        {
            onLoad.Invoke(track);
            yield break;
        }
        AudioType audioType = GetExtension(track.file);
        if (audioType == AudioType.ACC)
        {
            // Specific FLAC handling, which Unity doesn't really do.
            // byte[] bytes = System.IO.File.ReadAllBytes(song.filePath);
            // AudioClip myClip = FlacConverter.GetAudioClip(bytes);
            // myClip.name = song.name;
            // onLoad.Invoke(song, myClip);
            // yield return null;
        }
        if (audioType != AudioType.UNKNOWN)
        {
            Debug.LogError(SaveDataManager.Instance.MakeFileSystemURI(track.file.path));
            using UnityWebRequest webRequest = UnityWebRequestMultimedia.GetAudioClip(
                SaveDataManager.Instance.MakeFileSystemURI(track.file.path),
                audioType
            );
            yield return webRequest.SendWebRequest();
            if (webRequest.result == UnityWebRequest.Result.ConnectionError
            || webRequest.result == UnityWebRequest.Result.ProtocolError
            || webRequest.result == UnityWebRequest.Result.DataProcessingError)
            {
                Debug.LogError($"Error fetching clip {track.id}: {webRequest.error}");
            }
            else
            {
                AudioClip myClip = DownloadHandlerAudioClip.GetContent(webRequest);
                myClip.name = track.name;
                track.clip = myClip;
            }
            onLoad.Invoke(track);
        }
    }

    private AudioType GetExtension(SaveDataManager.FileData file)
    {
        return file.extension.ToLower() switch
        {
            ".wav" => AudioType.WAV,
            ".mp2" or ".mp3" => AudioType.MPEG,
            ".ogg" => AudioType.OGGVORBIS,
            ".flac" => AudioType.ACC,
            _ => AudioType.UNKNOWN,
        };
    }

    public void FromSaveData(PlaylistMetadata data)
    {
        // Hard reload
        foreach (MusicTrack track in _tracks)
        {
            if (track.clip != null)
            {
                Destroy(track.clip);
            }
        }
        _tracks = new();
        List<SaveDataManager.FileData> files = new();
        SaveDataManager.Instance.IterateFilesInDirectory(
            SaveDataManager.Instance.GetDirectory(this),
            (file) =>
            {
                files.Add(file);
            }
        );
        foreach (PlaylistMetadata.TrackMetadata track in data.tracks)
        {
            SaveDataManager.FileData file = files.Find(f => f.name == track.file);
            if (file.name != null && file.name.Length > 0)
            {
                MusicTrack newTrack = new()
                {
                    id = track.id,
                    name = track.name,
                    file = file,
                    bpm = track.bpm,
                    artist = track.artist,
                    year = track.year
                };
                _tracks.Add(newTrack);
            }
        }
    }

    public PlaylistMetadata ToSaveData()
    {
        throw new NotImplementedException();
    }
}
