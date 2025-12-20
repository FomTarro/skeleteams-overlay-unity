using System;

namespace Skeletom.BattleStation.Server
{
    public class Endpoint : IEndpoint
    {
        public string Path { get; private set; }

        private readonly Func<EndpointRequest, EndpointResponse> _handler;

        public Endpoint(string path, Func<EndpointRequest, EndpointResponse> handler)
        {
            _handler = handler;
            Path = path;
        }
        public EndpointResponse ProcessRequest(EndpointRequest request)
        {
            return _handler(request);
        }
    }
}
