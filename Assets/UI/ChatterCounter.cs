using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChatterCounter : MonoBehaviour
{
    [SerializeField]
    private TMPro.TMP_Text _text;
    // Start is called before the first frame update
    void Start()
    {
        LayoutManager.Instance.onChatUserCounted.AddListener((number) =>
        {
            _text.text = "" + number;
        });
    }
}
