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
    private readonly Dictionary<string, Action<ErrorResponse>> REQUEST_RESPONSE_ERROR_HANDLERS = new();
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
                Authenticate(
                    (s) =>
                    {
                        Debug.Log($"Authentication successful.");
                        _authToken = s.data.access_token;
                    },
                    (err) =>
                    {
                        Debug.LogError($"Unable to authenticate token: {err.data.message}");
                    }
                );
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
            if(!string.IsNullOrEmpty(header.nonce) && REQUEST_RESPONSE_HANDLERS.ContainsKey(header.nonce))
            {
                if (!string.IsNullOrEmpty(header.evt) && "ERROR".Equals(header.evt))
                {
                    ErrorResponse error = JsonUtility.FromJson<ErrorResponse>(data);
                    REQUEST_RESPONSE_ERROR_HANDLERS[header.nonce](error); 
                }
                else
                {
                    REQUEST_RESPONSE_HANDLERS[header.nonce](data); 
                }
                REQUEST_RESPONSE_ERROR_HANDLERS.Remove(header.nonce); 
                REQUEST_RESPONSE_HANDLERS.Remove(header.nonce);  
            }
        }
    }

    public void Handshake(Action<HandshakeResponseData> onSuccess)
    {
        EVENT_SUBSCRIPTION_HANDLERS["READY"] = (str) =>
        {
           onSuccess(JsonUtility.FromJson<HandshakeResponse>(str).data); 
        };
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

    private void SendRequest<I, O>(I request, Action<O> onSuccess, Action<ErrorResponse> onError) where I : DiscordRequest
    {
        string nonce = Guid.NewGuid().ToString();
        request.nonce = nonce;
        REQUEST_RESPONSE_HANDLERS[nonce] = (str) =>
        {
            O response = JsonUtility.FromJson<O>(str);
            onSuccess(response);
        };
        REQUEST_RESPONSE_ERROR_HANDLERS[nonce] = (err) =>
        {
            onError(err);
        };
        Send(1, JsonUtility.ToJson(request));
    }

    #region Authentication

    public void Authenticate(Action<AuthenticateResponse> onSuccess, Action<ErrorResponse> onError)
    {
        string codeVerifier = GenerateCodeVerifier();
        string codeChallenge = GenerateCodeChallenge(codeVerifier);
        AuthorizeRequest request = new()
        {
            args = new AuthorizeRequestArgs
            {
                client_id = applicationID,
                scopes = new[] { "rpc", "rpc.voice.read", "identify" },
                code_challenge = codeChallenge,
                code_challenge_method = "S256"
            }
        };
        SendRequest<AuthorizeRequest, AuthorizeResponse>(request, 
        (res) =>
        {
            ExchangeCodeForToken(res.data.code, codeVerifier, (res) =>
            {
                ValidateToken(res.access_token, onSuccess, onError);
            }, onError);
        }, onError);
    }

    private void ExchangeCodeForToken(string code, string codeVerifier, Action<TokenHttpResponse> onSuccess, Action<ErrorResponse> onError)
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
                    TokenHttpResponse res = JsonUtility.FromJson<TokenHttpResponse>(str);
                    onSuccess(res);
                },
                (err) =>
                {
                    Debug.LogError(err);
                    ErrorResponse res = new()
                    {
                        data = new()
                        {
                            code=(int)err.statusCode,
                            message=err.message
                        }
                    };
                    onError(res);
                }
            )
        );
    }

    private void ValidateToken(string token, Action<AuthenticateResponse> onSuccess, Action<ErrorResponse> onError)
    {
        AuthenticateRequest request = new()
        {
            args = new AuthenticateRequestArgs { access_token = token }
        };
        SendRequest(request, onSuccess, onError);
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
