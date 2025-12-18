using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Skeletom.BattleStation.Server
{
    public class TextAssetEndpoint : BaseUnityEndpoint
    {
        [SerializeField]
        private TextAsset _textAsset;
        public override IResponseArgs ProcessRequest(IRequestArgs request)
        {
            return new TextAssetResponse(200, _textAsset);
        }

        [System.Serializable]
        public class TextAssetResponse : IResponseArgs
        {
            public string Body { get; private set; }

            public int Status { get; private set; }

            public TextAssetResponse(int status, TextAsset body)
            {
                Status = status;
                Body = body.text;
            }
        }
    }
}
