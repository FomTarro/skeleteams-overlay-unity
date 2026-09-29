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
        MusicManager.Instance.onVolumeChanged.AddListener((g, v) =>
        {
            if (_volume != null && g == MusicManager.VolumeGroup.MUSIC_MASTER)
            {
                _volume.text = $"{v * 100f:F0}%";
            }
        });
        MusicManager.Instance.onMusicTrackChanged.AddListener((song) =>
        {
            if (_title != null)
            {
                _title.text = $"\"{song.name}\"";
            }
            if (_artist != null)
            {
                _artist.text = $"({song.artist} / {song.year})";
            }
        });
    }
}
