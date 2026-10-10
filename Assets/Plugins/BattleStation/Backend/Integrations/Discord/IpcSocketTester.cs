using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Skeletom.BattleStation.Integrations.Discord.IPC;
using Skeletom.Essentials.IO;
using Skeletom.Essentials.Utils;
using UnityEngine;

public class IpcSocketTester : MonoBehaviour
{
    public string applicationID;
    [SerializeField]
    private string _authToken;

    private IpcSocket _sock;
    private readonly Dictionary<string, Action<string>> REQUEST_RESPONSE_HANDLERS = new();
    private readonly Dictionary<string, Action<string>> REQUEST_RESPONSE_ERROR_HANDLERS = new();
    private readonly Dictionary<string, Action<string>> EVENT_SUBSCRIPTION_HANDLERS = new();

    private void Start()
    {
        string[] names = new string[10];
        for(int i = 0; i < 10; i++)
        {
            names[i] = $"discord-ipc-{i}";
        }
        _sock = new IpcSocket(names);
        Connect();
    }

    private void Update()
    {
        if(_sock != null)
        {
            _sock.Tick();
            string data;
            do
            {
                data = _sock.GetNextResponse();
                if (data != null)
                {
                    ProcessSocketMessage(data);
                }
            } while (data != null);
        }
    }

    public void Disconnect()
    {
        _sock.Disconnect();
    }

    public void Connect()
    {
        _sock.Connect(() =>
        {
            Debug.Log("Connected!");
            Handshake((s) =>
            {
                Debug.Log($"Handshake complete for user {s.user.global_name}");
                Authenticate((s) =>
                {
                    Debug.Log($"Authentication successful.");
                    _authToken = s.access_token;
                });
            });
        },
        () =>
        {
            Debug.LogWarning("Disconnected!");
        },
        (e) =>
        {
            Debug.LogError("Error: " + e);
        });
    }

    private void ProcessSocketMessage(string data)
    {
        FrameHeader header = JsonUtility.FromJson<FrameHeader>(data);
        if (header == null){
            return;
        }
        Debug.Log(data);
        // Subscribed Events
        if ("DISPATCH".Equals(header.cmd) && !string.IsNullOrEmpty(header.evt))
        {
            if (EVENT_SUBSCRIPTION_HANDLERS.ContainsKey(header.evt))
            {
                EVENT_SUBSCRIPTION_HANDLERS[header.evt](data);
            }
        }else
        // Commands
        {
            if(REQUEST_RESPONSE_HANDLERS.ContainsKey(header.nonce))
            {
                REQUEST_RESPONSE_HANDLERS[header.nonce](data);
                REQUEST_RESPONSE_HANDLERS.Remove(header.nonce);
            }
        }
    }

    public void Handshake(Action<HandshakeResponseData> onSuccess)
    {
        EVENT_SUBSCRIPTION_HANDLERS.Add("READY", (str) =>
        {
           onSuccess(JsonUtility.FromJson<HandshakeResponse>(str).data); 
        });
        Send(0, JsonUtility.ToJson(
            new HandshakeRequest
            {
                v = 1,
                client_id = applicationID,
            }
        ));
    }

    private void Send(int opCode, string message)
    {
        byte[] payload = Encoding.UTF8.GetBytes(message);
        byte[] header = new byte[8 + payload.Length];
        BitConverter.GetBytes(opCode).CopyTo(header, 0);
        BitConverter.GetBytes(payload.Length).CopyTo(header, 4);
        payload.CopyTo(header, 8);
        _sock.Send(header);
    }

    #region Authentication

    public void Authenticate(Action<TokenResponse> onSuccess)
    {
        string codeVerifier = GenerateCodeVerifier();
        string codeChallenge = GenerateCodeChallenge(codeVerifier);

        string nonce = Guid.NewGuid().ToString();
        REQUEST_RESPONSE_HANDLERS.Add(nonce, (str) =>
        {
            AuthorizeResponseData response = JsonUtility.FromJson<AuthorizeResponse>(str).data;
            ExchangeCodeForToken(response.code, codeVerifier, onSuccess);
        });
        Send(1, JsonUtility.ToJson(new AuthorizeRequest
        {
            cmd = "AUTHORIZE",
            nonce = nonce,
            args = new AuthorizeArgs
            {
                client_id = applicationID,
                scopes = new[] { "rpc", "rpc.voice.read", "identify" },
                code_challenge = codeChallenge,
                code_challenge_method = "S256"
            }
        }));
    }

    private void ExchangeCodeForToken(string code, string codeVerifier, Action<TokenResponse> onSuccess)
    {
        string body = $"client_id={Uri.EscapeDataString(applicationID)}" +
            $"&grant_type=authorization_code" +
            $"&code={Uri.EscapeDataString(code)}" +
            $"&code_verifier={Uri.EscapeDataString(codeVerifier)}";

        byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
        HttpUtils.HttpHeaders headers = new()
        {
            contentType="application/x-www-form-urlencoded",
        };
        StartCoroutine(
            HttpUtils.PostRequest(
                "https://discord.com/api/oauth2/token",
                body,
                headers,
                (str) =>
                {

                    TokenResponse response = JsonUtility.FromJson<TokenResponse>(str);
                    Authenticate(response.access_token, () =>
                    {
                        Debug.Log("Authenticated successfully");
                    });
                    
                },
                (err) =>
                {
                    Debug.LogError(err);
                }
            )
        );
    }

    private void Authenticate(string token, Action onSuccess)
    {
        string nonce = Guid.NewGuid().ToString();
        REQUEST_RESPONSE_HANDLERS.Add(nonce, (str) =>
        {
            Debug.Log(str);
            onSuccess();
        });
        Send(1, JsonUtility.ToJson(new AuthenticateRequest
        {
            cmd = "AUTHENTICATE",
            nonce = nonce,
            args = new AuthenticateArgs { access_token = token }
        }));
    }


    // PKCE helpers
    private static string GenerateCodeVerifier()
    {
        var bytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
            rng.GetBytes(bytes);
        return Base64UrlEncode(bytes);
    }

    private static string GenerateCodeChallenge(string verifier)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.ASCII.GetBytes(verifier));
        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    #endregion
}
