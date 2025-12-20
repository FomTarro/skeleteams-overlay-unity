using UnityEngine;

namespace Skeletom.BattleStation.Server
{
    public class TextAssetEndpoint : BaseUnityEndpoint
    {
        [SerializeField]
        private TextAsset _textAsset;
        public override EndpointResponse ProcessRequest(EndpointRequest request)
        {
            return new EndpointResponse(200, _textAsset.text);
        }
    }
}
