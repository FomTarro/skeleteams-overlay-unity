using UnityEngine;

public class MusicVolumeDisplay : MonoBehaviour
{
    [SerializeField]
    private TMPro.TMP_Text _volume;
    [SerializeField]
    private TMPro.TMP_Text _title;

    void Start()
    {
        Jukebox.Instance.onVolumeChanged.AddListener((g, v) =>
        {
            if(g == Jukebox.VolumeGroup.MUSIC_MASTER)
            {
                _volume.text = $"{v*100f:F0}%";
            }
        });
        Jukebox.Instance.onSongChanged.AddListener((song) =>
        {
            _title.text = $"\"{song.displayName}\"";
        });
    }
}
