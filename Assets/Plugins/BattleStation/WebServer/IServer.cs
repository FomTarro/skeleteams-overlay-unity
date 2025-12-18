using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IServer {

    int Port { get; set; }

    List<string> Paths { get; }

    void StartServer();
    void StopServer();
}
