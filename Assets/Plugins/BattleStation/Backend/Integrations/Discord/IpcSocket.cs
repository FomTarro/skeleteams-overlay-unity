using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Skeletom.Essentials.IO
{
    /// <summary>
    /// A general-purpose IPC socket interface. 
    /// 
    /// The socket will attempt to reconnect automatically 
    /// if the connection is disrupted by any means 
    /// other than manually calling the Disconnect method.
    /// </summary>
    public class IpcSocket
    {
        private Stream _stream;
        private CancellationTokenSource _cts;

        private readonly ConcurrentQueue<string> _intakeQueue;
        private readonly ConcurrentQueue<Action> _responseQueue;
        private Action _onConnect = () => { };
        private Action _onDisconnect = () => { };
        private Action<Exception> _onError = (e) => { };

        private readonly string[] _nameRange = new string[0];
        private string _selectedName = "NO_IPC_ESTABLISHED";

        private bool _attemptReconnect = false;
        private readonly int _reconnectInterval;

        /// <summary>
        /// Creates an IPC socket, attempting to connect to an IPC within the given name range. 
        /// </summary>
        /// <param name="nameRange">The name range to attempt a connection within.</param>
        /// <param name="reconnectInterval">The number of milliseconds to wait before attempting an automatic reconnection.</param>
        public IpcSocket(string[] nameRange, int reconnectInterval = 5000)
        {
            _intakeQueue = new ConcurrentQueue<string>();
            _responseQueue = new ConcurrentQueue<Action>();
            _nameRange = nameRange;
            _reconnectInterval = Math.Max(100, reconnectInterval);
        }

        #region Lifecycle

        /// <summary>
        /// Connects to the given IPC socket, executing the provided callbacks at various stages in the lifecycle.
        /// </summary>
        /// <param name="onConnect">Execute upon a successful connection.</param>
        /// <param name="onDisconnect">Executed when the socket disconnects, either due to process closure or manual disconnection.</param>
        /// <param name="onError">Executed when an error occurs in the socket.</param>
        public void Connect(Action onConnect, Action onDisconnect, Action<Exception> onError)
        {
            Disconnect();
            _cts = new CancellationTokenSource();
            _onConnect = onConnect;
            _onDisconnect = onDisconnect;
            _onError = onError;
            _attemptReconnect = true;
            StartProcessingTask(_cts.Token);
        }

        /// <summary>
        /// Manually disconnects from the IPC socket.
        /// </summary>
        public void Disconnect()
        {
            _attemptReconnect = false;
            _cts?.Cancel();
            try { 
                _stream?.Dispose(); 
            } catch { 
                /* ignored */ 
            }
            _stream = null;
        }

        private bool ConnectStream()
        {
            bool isWindows = Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor;
            return isWindows ? ConnectWindowsPipe() : ConnectUnixSocket();
        }

        private bool ConnectWindowsPipe()
        {
            foreach(string name in _nameRange)
            {
               try
                {
                    NamedPipeClientStream pipe = new(
                        ".", 
                        name,
                        PipeDirection.InOut, 
                        PipeOptions.Asynchronous
                    );
                    pipe.Connect(1000);
                    _stream = pipe;
                    _selectedName = name;
                    return true;
                }
                catch { 
                    /* try next index */
                } 
            }
            return false;
        }
        
        private bool ConnectUnixSocket()
        {
            foreach(string name in _nameRange)
            {
                if (TryConnectUnixSocket(name, Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR")))
                    return true;
                if (TryConnectUnixSocket(name, Environment.GetEnvironmentVariable("TMPDIR")))
                    return true;
                if (TryConnectUnixSocket(name, "/tmp"))
                    return true;
            }
            return false;
        }
        
        private bool TryConnectUnixSocket(string name, string dir)
        {
            if (string.IsNullOrEmpty(dir)) {
                return false;
            }
            string path = Path.Combine(dir, name);
            Socket socket = null;
            try
            {
                socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                socket.Connect(new UnixSocketEndPoint(path));
                _stream = new NetworkStream(socket, true);
                _selectedName = name;
                return true;
            }
            catch
            {
                socket?.Dispose();
                return false;
            }
        }

        private async Task Reconnect(CancellationToken token)
        {
            try
            {
                await Task.Delay(_reconnectInterval, token);
                if (_attemptReconnect)
                {
                    Debug.LogError($"[{_selectedName}] - Attempting to reconnect to IPC...");
                    Connect(_onConnect, _onDisconnect, _onError);
                }
            }
            catch (OperationCanceledException)
            {
                // swallow this, it's fine
            }
        }

        #endregion

        #region Processing Loop

        /// <summary>
        /// Advances the socket one step. 
        /// This function can be called manually on the main thread to 
        /// ensure that the callbacks provided in the Connect method are safely executed,
        /// as many Unity API functions cannot be called on worker threads.
        /// </summary>
        public void Tick()
        {
            do
            {
                if (_responseQueue.IsEmpty || !_responseQueue.TryDequeue(out var action))
                {
                    continue;
                }
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[{_selectedName}] - IPC error: {e.StackTrace}");
                }
            } while (!_responseQueue.IsEmpty);
        }

        private void StartProcessingTask(CancellationToken token)
        {
            Task.Run<Task>(async () =>
            {
                if (!ConnectStream())
                {
                    Debug.LogError("Could not connect to IPC.");
                    await Reconnect(token);
                    return;
                }
                Debug.Log($"[{_selectedName}] - Connected to IPC!");
                _responseQueue.Enqueue(_onConnect);
                while (_stream != null && !token.IsCancellationRequested) {
                    try
                    {
                        // If attempting to write throws an exception,
                        // our connection is disrupted!
                        await _stream.WriteAsync(new byte[0], 0, 0, token);
                    }catch
                    {
                        Debug.LogError($"[{_selectedName}] - IPC disconnected...");
                        _responseQueue.Enqueue(async () =>
                        {
                            _onDisconnect();
                            await Reconnect(token);
                        });
                        return;
                    }
                    try{
                        string frame = await ReadFrameAsync();
                        if (frame != null){
                            _intakeQueue.Enqueue(frame);
                        }
                    }catch(Exception e)
                    {
                        _responseQueue.Enqueue(async () =>
                        {
                            Debug.LogError($"[{_selectedName}] - IPC error...");
                            Debug.LogError($"'{e.Message}', {e}");
                            _onError(e);
                            await Reconnect(token);
                        });
                    }
                }
                // Exiting task gracefully.
                _responseQueue.Enqueue(() => _onDisconnect());
            });
        }

        #endregion

        #region I/O

        private async Task<string> ReadFrameAsync()
        {
            var header = new byte[8];
            if (await ReadSegmentAsync(header, 8) < 8) return null;

            var length = BitConverter.ToInt32(header, 4);
            if (length <= 0 || length > 1024 * 1024) return null;

            var data = new byte[length];
            if (await ReadSegmentAsync(data, length) < length) return null;

            return Encoding.UTF8.GetString(data);
        }

        private async Task<int> ReadSegmentAsync(byte[] buffer, int count)
        {
            int total = 0;
            while (total < count)
            {
                var n = await _stream.ReadAsync(buffer, total, count - total, _cts.Token);
                if (n == 0) return total;
                total += n;
            }
            return total;
        }

        /// <summary>
        /// Gets the next message in the queue from the socket.
        /// </summary>
        /// <returns></returns>
        public string GetNextResponse()
        {
            _intakeQueue.TryDequeue(out var response);
            return response;
        }

        /// <summary>
        /// Sends a message over the socket.
        /// </summary>
        /// <param name="message">The message to send.</param>
        public void Send(byte[] message)
        {
            _ = _stream.WriteAsync(message, 0, message.Length, _cts.Token);
        }

        #endregion

        private class UnixSocketEndPoint : EndPoint
        {
            private readonly string _path;
            public UnixSocketEndPoint(string path) { 
                _path = path; 
            }

            public override AddressFamily AddressFamily => AddressFamily.Unix;

            public override SocketAddress Serialize()
            {
                var pathBytes = Encoding.UTF8.GetBytes(_path);
                var sa = new SocketAddress(AddressFamily.Unix, 2 + pathBytes.Length + 1);
                for (int i = 0; i < pathBytes.Length; i++)
                    sa[2 + i] = pathBytes[i];
                return sa;
            }

            public override EndPoint Create(SocketAddress socketAddress) => this;
        }
    }
}
