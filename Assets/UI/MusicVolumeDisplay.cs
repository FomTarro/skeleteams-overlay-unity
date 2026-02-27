using UnityEngine;

public class MusicVolumeDisplay : MonoBehaviour
{
    [SerializeField]
    private TMPro.TMP_Text _volume;
    [SerializeField]
    private TMPro.TMP_Text _title;
    [SerializeField]
    private TMPro.TMP_Text _artist;

    void Start()
    {
        Jukebox.Instance.onVolumeChanged.AddListener((g, v) =>
        {
            if (_volume != null && g == Jukebox.VolumeGroup.MUSIC_MASTER)
            {
                _volume.text = $"{v * 100f:F0}%";
            }
        });
        Jukebox.Instance.onSongChanged.AddListener((song) =>
        {
            if (_title != null)
            {
                _title.text = $"\"{song.displayName}\"";
            }
            if (_artist != null)
            {
                _artist.text = $"({song.artist} / {song.year})";
            }
        });
    }
}
