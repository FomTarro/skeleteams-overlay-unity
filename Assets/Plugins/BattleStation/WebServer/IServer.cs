using System.Collections.Generic;
namespace Skeletom.BattleStation.Server
{
    public interface IServer
    {
        int Port { get; set; }

        List<string> Paths { get; }

        void StartServer();
        void StopServer();

        void RegisterEndpoint(IEndpoint endpoint);
        void UnregisterEndpoint(IEndpoint endpoint);

    }
}
