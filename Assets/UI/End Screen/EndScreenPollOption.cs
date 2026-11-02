using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EndScreenPollOption : MonoBehaviour
{
    [SerializeField]
    private Image _fill;
    [SerializeField]
    private string _optionTitle;
    // Start is called before the first frame update
    void Start()
    {
        _fill.fillAmount = 0f;
        LayoutManager.Instance.onPollChecked.AddListener(votes =>
        {
            if(votes.ContainsKey(_optionTitle)){
                _fill.fillAmount = votes[_optionTitle];
            }
        });
    }
}
