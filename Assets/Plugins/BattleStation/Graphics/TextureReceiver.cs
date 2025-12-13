using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TextureReceiver : MonoBehaviour
{
    [SerializeField]
    private string _targetName;
    public string TargetName
    {
        get { return _targetName; }
    }

}
