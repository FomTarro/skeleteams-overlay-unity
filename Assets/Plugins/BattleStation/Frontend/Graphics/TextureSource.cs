using System;
using System.Collections.Generic;
using Skeletom.Essentials.IO;
using UnityEngine;

public abstract class TextureSource : MonoBehaviour, ISaveable<TextureSource.RouteData>
{
    public string FileFolder => "texture_routes";

    [SerializeField]
    private string _fileName;
    public string FileName => _fileName + ".json";



    public abstract void FromSaveData(RouteData data);

    public abstract RouteData ToSaveData();


    [Serializable]
    public class RouteData : BaseSaveData
    {
        public List<TextureRouter.SerializedTextureRoute> routes = new List<TextureRouter.SerializedTextureRoute>();
    }
}
