using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Skeletom.BattleStation.Integrations.Discord.IPC;
using Skeletom.Essentials.IO;
using Skeletom.Essentials.Utils;
using UnityEngine;

namespace Skeletom.BattleStation.Integrations.Discord
{
    public class DiscordIntegration : Integration<DiscordIntegration, DiscordIntegration.IntegrationData>
    {
        public override string FileName => "discord.json";

        private const string CLIENT_ID = "1524610169516986429";
        private string USER_TOKEN = "NO_TOKEN_SET";

        private IpcSocket _sock;
        private readonly Dictionary<string, Action<string>> REQUEST_RESPONSE_HANDLERS = new();
        private readonly Dictionary<string, Action<ErrorResponse>> REQUEST_RESPONSE_ERROR_HANDLERS = new();
        private readonly Dictionary<string, Action<string>> EVENT_SUBSCRIPTION_HANDLERS = new();
        private string LAST_MESSAGE_RECEIVED;

        #region Lifecycle

        public override void Disable()
        {
            _sock?.Disconnect();
        }

        public override void Enable()
        {
            if(_sock == null){
                string[] names = new string[10];
                for(int i = 0; i < 10; i++)
                {
                    names[i] = $"discord-ipc-{i}";
                }
                _sock = new IpcSocket(names);
            }
            FromSaveData(SaveDataManager.Instance.ReadSaveData(this));
        }

        public override void Initialize()
        {
            Enable();
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

        private void ProcessSocketMessage(string data)
        {
            LAST_MESSAGE_RECEIVED = data;
            DataResponse<string> header = JsonUtility.FromJson<DataResponse<string>>(data);
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

        public void ConnectToIPC()
        {
            _sock.Connect(() =>
            {
                Handshake((s) =>
                {
                    Debug.Log($"Discord IPC Handshake complete for user: {s.data.user.global_name}");
                    ValidateToken(USER_TOKEN, 
                    (s) =>
                    {
                        Debug.Log($"Discord IPC Authentication successful with existing token!");
                    },
                    (err) =>
                    {
                        Authenticate(
                        (s) =>
                        {
                            Debug.Log($"Discord Authentication successful with new token!");
                            SetToken(s.data.access_token);
                        },
                        (err) =>
                        {
                            Debug.LogError($"Discord IPC unable to authenticate: {err.data.message}");
                        });
                    });
                },
                (err) =>
                {
                    Debug.LogError($"IPC Handshake failed with error: {err.data.message}");
                });
            },
            () =>
            {
                // Debug.LogWarning("Disconnected!");
            },
            (e) =>
            {
                Debug.LogError("Discord IPC Error: " + e);
            });
        }

        #endregion

        #region Send

        private void Send(int opCode, string message)
        {
            byte[] payload = Encoding.UTF8.GetBytes(message);
            byte[] header = new byte[8 + payload.Length];
            BitConverter.GetBytes(opCode).CopyTo(header, 0);
            BitConverter.GetBytes(payload.Length).CopyTo(header, 4);
            payload.CopyTo(header, 8);
            _sock.Send(header);
        }

        private void SendRequest<I, O>(I request, Action<O> onSuccess, Action<ErrorResponse> onError) where I : IpcMessage, IArgRequest
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

        #endregion

        #region Handshake

        public void Handshake(Action<HandshakeResponse> onSuccess, Action<ErrorResponse> onError)
        {
            // Build in a timeout 
            // since there's no real easy way 
            // to detect a message as being the handshake failure.
            CancellationTokenSource cts = new();
            Task.Run(async () =>
            {
                await Task.Delay(1000, cts.Token);
                ErrorResponseData data = JsonUtility.FromJson<ErrorResponseData>(LAST_MESSAGE_RECEIVED);
                ErrorResponse err = new()
                {
                    data = new()
                    {
                        code = data != null ? data.code : 0,
                        message = data != null ? data.message : "Handshake timeout."
                    }
                };
                onError(err);
            }, cts.Token);
            EVENT_SUBSCRIPTION_HANDLERS["READY"] = (str) =>
            {
                cts.Cancel();
                cts.Dispose();
                onSuccess(JsonUtility.FromJson<HandshakeResponse>(str)); 
            };
            Send(0, JsonUtility.ToJson(
                new HandshakeRequest
                {
                    v = 1,
                    client_id = CLIENT_ID,
                }
            ));
        }

        #endregion

        #region Tokens

        public void SetToken(string token)
        {
            // Save our updated token
            USER_TOKEN = token;
            SaveDataManager.Instance.WriteSaveData(this);
        }

        private void Authenticate(Action<AuthenticateResponse> onSuccess, Action<ErrorResponse> onError)
        {
            string codeVerifier = GenerateCodeVerifier();
            string codeChallenge = GenerateCodeChallenge(codeVerifier);
            AuthorizeRequest request = new()
            {
                args = new AuthorizeRequestArgs
                {
                    client_id = CLIENT_ID,
                    scopes = new[] { "rpc", "rpc.voice.read", "identify" },
                    code_challenge = codeChallenge,
                    code_challenge_method = "S256"
                }
            };
            SendRequest<AuthorizeRequest, AuthorizeResponse>(request, 
            (res) =>
            {
                RequestToken(res.data.code, codeVerifier, (res) =>
                {
                    ValidateToken(res.access_token, onSuccess, onError);
                }, onError);
            }, onError);
        }

        private void RequestToken(string code, string codeVerifier, Action<TokenHttpResponse> onSuccess, Action<ErrorResponse> onError)
        {
            string body = $"client_id={Uri.EscapeDataString(CLIENT_ID)}" +
                $"&grant_type=authorization_code" +
                $"&code={Uri.EscapeDataString(code)}" +
                $"&code_verifier={Uri.EscapeDataString(codeVerifier)}";
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

        #region File IO

        public override void FromSaveData(IntegrationData data)
        {
            USER_TOKEN = data.token;
            // Once we load our settings, 
            // we can attempt to establish a connection.
            ConnectToIPC();
        }

        public override IntegrationData ToSaveData()
        {
            return new IntegrationData()
            {
                token = USER_TOKEN
            };
        }

        [SerializeField]
        public class IntegrationData : BaseSaveData
        {
            public string token;
        }

        #endregion
    }
}
