using System;
using System.Collections;
using System.Collections.Generic;
using Skeletom.Essentials.IO;
using UnityEngine;

public class OAuthClient : ISaveable<OAuthClient.TokenData>
{
    public string FileFolder { get; private set; }

    public string FileName => "token.json";

    public void FromSaveData(TokenData data)
    {
        throw new NotImplementedException();
    }

    public TokenData ToSaveData()
    {
        throw new NotImplementedException();
    }

    [Serializable]
    public class TokenData : BaseSaveData
    {
        public string token;
    }
}
