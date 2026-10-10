using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Skeletom.BattleStation.Integrations.Discord.IPC;
using UnityEngine;

/// <summary>
/// Monitors Discord voice activity via the local IPC pipe.
/// Only an Application ID is needed.
/// Create one at: https://discord.com/developers/applications.
/// </summary>
public class DiscordIpcVoiceMonitor : MonoBehaviour
{
    [Header("Discord Application ID")]
    [SerializeField] private string applicationID;

    [Header("OAuth2 Token (auto-populated on auth if not present)")]
    [SerializeField] private string accessToken;

    private Stream stream;
    private bool isWindows;
    private CancellationTokenSource cts;
    private string subscribedChannelId;
    private int nonceCounter;
    private readonly ConcurrentDictionary<string, string> userNames = new ConcurrentDictionary<string, string>();

    // ── Lifecycle ────────────────────────────────────────────────────

    private void OnEnable()
    {
        isWindows = Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor;
    }

    private void Start()
    {
        Invoke(nameof(Connect), 1.0f);
    }

    private void OnDisable()
    {
        Disconnect();
    }

    /// <summary>
    /// Call from a UI button to start the Discord IPC connection.
    /// </summary>
    public void Connect()
    {
        if (string.IsNullOrEmpty(applicationID))
        {
            logError("Application ID is not set.");
            return;
        }

        if (cts != null && !cts.IsCancellationRequested)
            return;

        cts = new CancellationTokenSource();
        Task.Run(() => runAsync());
    }

    /// <summary>
    /// Call to disconnect and clean up.
    /// </summary>
    public void Disconnect()
    {
        var wasConnected = stream != null;
        cts?.Cancel();
        try { stream?.Dispose(); } catch { /* ignored */ }
        stream = null;
        subscribedChannelId = null;
        userNames.Clear();
        if (wasConnected)
            log("Disconnected from Discord IPC.");
    }

    // ── Main flow ────────────────────────────────────────────────────

    private async Task runAsync()
    {
        try
        {
            if (!connectStream())
            {
                logError("Could not connect to Discord IPC. Is Discord running?");
                return;
            }

            // 1. Handshake
            await sendFrameAsync(0, JsonUtility.ToJson(new HandshakeRequest
            {
                v = 1,
                client_id = applicationID
            }));

            var ready = await readFrameAsync();
            if (ready == null || isError(ready))
            {
                logError("Handshake failed. Is the Application ID valid?");
                return;
            }
            Debug.Log(ready);

            // 2. Authenticate (authorize on first run, cached afterwards)
            if (!await authenticateAsync())
                return;

            log("Connected to Discord IPC.");

            // 3. Subscribe to voice-channel selection (fires on join/leave VC)
            await subscribeAsync("VOICE_CHANNEL_SELECT");

            // 4. Check if already in a voice channel right now
            await sendCommandAsync("GET_SELECTED_VOICE_CHANNEL");

            // 5. Event loop
            while (stream != null && !cts.IsCancellationRequested)
            {
                var frame = await readFrameAsync();
                if (frame == null) break;
                handleFrame(frame);
            }
        }
        catch (OperationCanceledException) { /* shutting down */ }
        catch (Exception ex) { logError($"IPC error: {ex.Message}"); }
        finally
        {
            try { stream?.Dispose(); } catch { /* ignored */ }
            stream = null;
            cts = null;
        }
    }

    // ── IPC connection (Windows: named pipe, macOS/Linux: Unix socket) ─

    private bool connectStream()
    {
        return isWindows ? connectWindowsPipe() : connectUnixSocket();
    }

    private bool connectWindowsPipe()
    {
        for (int i = 0; i < 10; i++)
        {
            try
            {
                var pipe = new NamedPipeClientStream(".", $"discord-ipc-{i}",
                    PipeDirection.InOut, PipeOptions.Asynchronous);
                pipe.Connect(1000);
                stream = pipe;
                return true;
            }
            catch { /* try next index */ }
        }
        return false;
    }

    private bool connectUnixSocket()
    {
        for (int i = 0; i < 10; i++)
        {
            var name = $"discord-ipc-{i}";
            if (tryConnectUnixSocket(name, Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR")))
                return true;
            if (tryConnectUnixSocket(name, Environment.GetEnvironmentVariable("TMPDIR")))
                return true;
            if (tryConnectUnixSocket(name, "/tmp"))
                return true;
        }
        return false;
    }

    private bool tryConnectUnixSocket(string name, string dir)
    {
        if (string.IsNullOrEmpty(dir)) return false;
        var path = Path.Combine(dir, name);
        Socket socket = null;
        try
        {
            socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            socket.Connect(new UnixSocketEndPoint(path));
            stream = new NetworkStream(socket, ownsSocket: true);
            return true;
        }
        catch
        {
            socket?.Dispose();
            return false;
        }
    }

    // ── OAuth2 authentication (PKCE — no client_secret needed) ────────

    private async Task<bool> authenticateAsync()
    {
        // Try cached token first
        if (!string.IsNullOrEmpty(accessToken))
        {
            await sendAuthenticateAsync(accessToken);
            var resp = await readFrameAsync();
            if (resp != null && !isError(resp))
                return true;

            accessToken = null;
        }

        // PKCE: generate verifier + challenge
        var codeVerifier = generateCodeVerifier();
        var codeChallenge = generateCodeChallenge(codeVerifier);

        // Send AUTHORIZE over IPC — Discord shows its own in-app consent dialog
        await sendFrameAsync(1, JsonUtility.ToJson(new AuthorizeRequest
        {
            cmd = "AUTHORIZE",
            nonce = (++nonceCounter).ToString(),
            args = new AuthorizeArgs
            {
                
                client_id = applicationID,
                scopes = new[] { "rpc", "rpc.voice.read", "identify" },
                code_challenge = codeChallenge,
                code_challenge_method = "S256"
            }
        }));

        var authJson = await readFrameAsync();
        if (authJson == null || isError(authJson))
        {
            logError("IPC AUTHORIZE failed - " + authJson);
            return false;
        }

        var code = JsonUtility.FromJson<AuthorizeResponse>(authJson)?.data?.code;
        if (string.IsNullOrEmpty(code))
        {
            logError("Authorization failed — no code received from Discord.");
            return false;
        }

        // Exchange code → access_token using PKCE verifier (no client_secret)
        accessToken = await exchangeCodeForTokenAsync(code, codeVerifier);
        if (string.IsNullOrEmpty(accessToken))
        {
            logError("Token exchange failed.");
            return false;
        }

        // AUTHENTICATE with the IPC pipe
        await sendAuthenticateAsync(accessToken);
        var result = await readFrameAsync();
        if (result == null || isError(result))
        {
            logError("IPC AUTHENTICATE failed after token exchange.");
            accessToken = null;
            return false;
        }

        return true;
    }

    private Task sendAuthenticateAsync(string token)
    {
        return sendFrameAsync(1, JsonUtility.ToJson(new AuthenticateRequest
        {
            cmd = "AUTHENTICATE",
            nonce = (++nonceCounter).ToString(),
            args = new AuthenticateArgs { access_token = token }
        }));
    }

    // ── PKCE helpers ─────────────────────────────────────────────────

    private static string generateCodeVerifier()
    {
        var bytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
            rng.GetBytes(bytes);
        return base64UrlEncode(bytes);
    }

    private static string generateCodeChallenge(string verifier)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.ASCII.GetBytes(verifier));
        return base64UrlEncode(hash);
    }

    private static string base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    // ── Token exchange (PKCE — no client_secret) ──────────────────────

    private async Task<string> exchangeCodeForTokenAsync(string code, string codeVerifier)
    {
        try
        {
            var req = (HttpWebRequest)WebRequest.Create("https://discord.com/api/oauth2/token");
            req.Method = "POST";
            req.ContentType = "application/x-www-form-urlencoded";

            var body = $"client_id={Uri.EscapeDataString(applicationID)}" +
                       $"&grant_type=authorization_code" +
                       $"&code={Uri.EscapeDataString(code)}" +
                       $"&code_verifier={Uri.EscapeDataString(codeVerifier)}";

            var bodyBytes = Encoding.UTF8.GetBytes(body);
            req.ContentLength = bodyBytes.Length;

            using (var s = await req.GetRequestStreamAsync())
                await s.WriteAsync(bodyBytes, 0, bodyBytes.Length, cts.Token);

            using var res = (HttpWebResponse)await req.GetResponseAsync();
            using var reader = new StreamReader(res.GetResponseStream());
            var json = await reader.ReadToEndAsync();
            return JsonUtility.FromJson<TokenResponse>(json)?.access_token;
        }
        catch (Exception ex)
        {
            logError($"Token exchange error: {ex.Message}");
            return null;
        }
    }

    // ── Frame handler ────────────────────────────────────────────────

    private void handleFrame(string json)
    {
        var header = JsonUtility.FromJson<FrameHeader>(json);
        if (header == null) return;

        // GET_SELECTED_VOICE_CHANNEL response
        if (header.cmd == "GET_SELECTED_VOICE_CHANNEL" && header.evt != "ERROR")
        {
            handleSelectedVoiceChannel(json);
            return;
        }

        // Event dispatches (subscription confirmations are ignored)
        if (header.cmd == "DISPATCH" && !string.IsNullOrEmpty(header.evt))
            handleEvent(header.evt, json);
    }

    private void handleSelectedVoiceChannel(string json)
    {
        var data = JsonUtility.FromJson<SelectedChannelFrame>(json)?.data;
        if (data == null || string.IsNullOrEmpty(data.id))
            return;

        if (data.voice_states != null)
        {
            foreach (var vs in data.voice_states)
                cacheUser(vs?.user);
        }

        _ = Task.Run(() => subscribeToChannelEventsAsync(data.id));
    }

    // ── Event handling ───────────────────────────────────────────────

    private void handleEvent(string evt, string json)
    {
        switch (evt)
        {
            case "VOICE_CHANNEL_SELECT":
                {
                    var channelId = JsonUtility.FromJson<ChannelSelectFrame>(json)?.data?.channel_id;

                    if (string.IsNullOrEmpty(channelId))
                    {
                        if (subscribedChannelId != null)
                        {
                            var old = subscribedChannelId;
                            subscribedChannelId = null;
                            userNames.Clear();
                            _ = Task.Run(() => unsubscribeFromChannelEventsAsync(old));
                        }
                    }
                    else
                    {
                        userNames.Clear();
                        _ = Task.Run(async () =>
                        {
                            await subscribeToChannelEventsAsync(channelId);
                            await sendCommandAsync("GET_SELECTED_VOICE_CHANNEL");
                        });
                    }
                    break;
                }

            case "SPEAKING_START":
                {
                    var uid = JsonUtility.FromJson<SpeakingFrame>(json)?.data?.user_id;
                    log($"{getName(uid)} started speaking");
                    break;
                }

            case "SPEAKING_STOP":
                {
                    var uid = JsonUtility.FromJson<SpeakingFrame>(json)?.data?.user_id;
                    log($"{getName(uid)} stopped speaking");
                    break;
                }

            case "VOICE_STATE_CREATE":
                {
                    var user = JsonUtility.FromJson<VoiceStateFrame>(json)?.data?.user;
                    cacheUser(user);
                    log($"{getName(user?.id)} joined the channel");
                    break;
                }

            case "VOICE_STATE_DELETE":
                {
                    var user = JsonUtility.FromJson<VoiceStateFrame>(json)?.data?.user;
                    log($"{getName(user?.id)} left the channel");
                    if (!string.IsNullOrEmpty(user?.id))
                        userNames.TryRemove(user.id, out _);
                    break;
                }
        }
    }

    // ── Channel event subscriptions ──────────────────────────────────

    private async Task subscribeToChannelEventsAsync(string channelId)
    {
        if (!string.IsNullOrEmpty(subscribedChannelId) && subscribedChannelId != channelId)
            await unsubscribeFromChannelEventsAsync(subscribedChannelId);

        subscribedChannelId = channelId;

        await subscribeAsync("SPEAKING_START", channelId);
        await subscribeAsync("SPEAKING_STOP", channelId);
        await subscribeAsync("VOICE_STATE_CREATE", channelId);
        await subscribeAsync("VOICE_STATE_DELETE", channelId);
    }

    private async Task unsubscribeFromChannelEventsAsync(string channelId)
    {
        try
        {
            await unsubscribeAsync("SPEAKING_START", channelId);
            await unsubscribeAsync("SPEAKING_STOP", channelId);
            await unsubscribeAsync("VOICE_STATE_CREATE", channelId);
            await unsubscribeAsync("VOICE_STATE_DELETE", channelId);
        }
        catch { /* pipe may already be closed */ }
    }

    // ── IPC frame protocol ───────────────────────────────────────────
    //
    //  Frame: [opcode : uint32 LE] [length : uint32 LE] [json payload]
    //  Opcodes: 0 = HANDSHAKE, 1 = FRAME, 2 = CLOSE

    private Task sendCommandAsync(string cmd)
    {
        return sendFrameAsync(1, JsonUtility.ToJson(new EventRequest
        {
            cmd = cmd,
            nonce = (++nonceCounter).ToString(),
            args = new ChannelArgs()
        }));
    }

    private Task subscribeAsync(string evt, string channelId = null)
    {
        return sendFrameAsync(1, JsonUtility.ToJson(new EventRequest
        {
            cmd = "SUBSCRIBE",
            evt = evt,
            nonce = (++nonceCounter).ToString(),
            args = new ChannelArgs { channel_id = channelId }
        }));
    }

    private Task unsubscribeAsync(string evt, string channelId = null)
    {
        return sendFrameAsync(1, JsonUtility.ToJson(new EventRequest
        {
            cmd = "UNSUBSCRIBE",
            evt = evt,
            nonce = (++nonceCounter).ToString(),
            args = new ChannelArgs { channel_id = channelId }
        }));
    }

    private async Task sendFrameAsync(int opcode, string json)
    {
        if (stream == null) return;

        var data = Encoding.UTF8.GetBytes(json);
        var header = new byte[8];
        BitConverter.GetBytes(opcode).CopyTo(header, 0);
        BitConverter.GetBytes(data.Length).CopyTo(header, 4);

        await stream.WriteAsync(header, 0, 8, cts.Token);
        await stream.WriteAsync(data, 0, data.Length, cts.Token);
        await stream.FlushAsync(cts.Token);
    }

    private async Task<string> readFrameAsync()
    {
        var header = new byte[8];
        if (await readExactAsync(header, 8) < 8) return null;

        var length = BitConverter.ToInt32(header, 4);
        if (length <= 0 || length > 1024 * 1024) return null;

        var data = new byte[length];
        if (await readExactAsync(data, length) < length) return null;

        return Encoding.UTF8.GetString(data);
    }

    private async Task<int> readExactAsync(byte[] buffer, int count)
    {
        int total = 0;
        while (total < count)
        {
            var n = await stream.ReadAsync(buffer, total, count - total, cts.Token);
            if (n == 0) return total;
            total += n;
        }
        return total;
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static bool isError(string json)
    {
        return JsonUtility.FromJson<FrameHeader>(json)?.evt == "ERROR";
    }

    private void cacheUser(DiscordUser user)
    {
        if (user == null || string.IsNullOrEmpty(user.id)) return;
        var name = !string.IsNullOrEmpty(user.global_name) ? user.global_name
                 : !string.IsNullOrEmpty(user.username) ? user.username
                 : user.id;
        userNames[user.id] = name;
    }

    private string getName(string userId)
    {
        if (userId != null && userNames.TryGetValue(userId, out var n)) return n;
        return userId ?? "?";
    }

    private void log(string msg) => Debug.Log($"[DiscordIPC] {msg}");
    private void logError(string msg) => Debug.LogError($"[DiscordIPC] {msg}");

    // ── JSON payloads (UnityEngine.JsonUtility) ──────────────────────


    // ── Unix domain socket endpoint (macOS/Linux) ────────────────────

    private class UnixSocketEndPoint : EndPoint
    {
        private readonly string _path;

        public UnixSocketEndPoint(string path) { _path = path; }

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