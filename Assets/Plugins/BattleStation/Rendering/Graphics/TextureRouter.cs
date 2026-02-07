using System;
using System.Collections;
using System.Collections.Generic;
using Skeletom.Essentials.IO;
using Skeletom.Essentials.Lifecycle;
using UnityEngine;

public class TextureRouter : Singleton<TextureRouter>
{
    [SerializeField]
    private List<TextureSource> _sources = new List<TextureSource>();
    public override void Initialize()
    {
        foreach (TextureSource source in _sources)
        {
            source.FromSaveData(SaveDataManager.Instance.ReadSaveData(source));
        }
    }

    /// <summary>
    /// Maps a source texture to target Texture Receivers.
    /// </summary>
    [Serializable]
    public class SerializedTextureRoute
    {
        public string source;
        public string[] targets = new string[0];
    }


    public class InternalTextureRoute : SerializedTextureRoute
    {
        /// <summary>
        /// The actual Texture which the source string represents.
        /// </summary>
        public Texture sourceTexture;
    }
}
