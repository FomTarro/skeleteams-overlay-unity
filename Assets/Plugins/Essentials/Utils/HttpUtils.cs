using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.Networking;

namespace Skeletom.Essentials.Utils
{

    public static class HttpUtils
    {

        // TODO: keep a dictionary of requests that map to their callbacks, such that two identical requests do not race, 
        // and instead the second awaits the first and shares the results. 

        private struct PendingWebRequest
        {
            public Action<UnityWebRequest> onSuccess;
            public Action<HttpError> onError;
            public PendingWebRequest(Action<UnityWebRequest> onSuccess, Action<HttpError> onError)
            {
                this.onSuccess = onSuccess;
                this.onError = onError;
            }
        }

        private static Dictionary<string, Queue<PendingWebRequest>> CONTEXTS = new Dictionary<string, Queue<PendingWebRequest>>();

        /// <summary>
        /// Makes a GET request to the given URL, executing the corresponding callback on completion.
        /// </summary>
        /// <param name="url">The URL to call.</param>
        /// <param name="headers">Optional request headers.</param>
        /// <param name="onError">The callback executed on an unsuccessful request.</param>
        /// <param name="onSuccess">The callback executed on a successful request.</param>
        /// <returns></returns>
        public static IEnumerator GetRequest(string url, HttpHeaders headers, Action<string> onSuccess, Action<HttpError> onError)
        {
            UnityWebRequest webRequest = UnityWebRequest.Get(url);
            yield return MakeWebRequest(webRequest, headers, (req) =>
            {
                onSuccess(req.downloadHandler.text);
            }, onError);
        }

        /// <summary>
        /// Makes a GET request to the given URL, executing the corresponding callback on completion.
        /// </summary>
        /// <param name="url">The URL to call.</param>
        /// <param name="headers">Optional request headers.</param>
        /// <param name="onError">The callback executed on an unsuccessful request.</param>
        /// <param name="onSuccess">The callback executed on a successful request.</param>
        /// <returns></returns>
        public static IEnumerator GetBytesRequest(string url, HttpHeaders headers, Action<byte[]> onSuccess, Action<HttpError> onError)
        {
            UnityWebRequest webRequest = UnityWebRequest.Get(url);
            yield return MakeWebRequest(webRequest, headers, (req) =>
            {
                onSuccess(req.downloadHandler.data);
            }, onError);
        }

        /// <summary>
        /// Makes a GET request to the given URL, executing the corresponding callback on completion.
        /// </summary>
        /// <param name="url">The URL to call.</param>
        /// <param name="headers">Optional request headers.</param>
        /// <param name="onError">The callback executed on an unsuccessful request.</param>
        /// <param name="onSuccess">The callback executed on a successful request.</param>
        /// <returns></returns>
        public static IEnumerator GetTextureRequest(string url, HttpHeaders headers, Action<Texture2D> onSuccess, Action<HttpError> onError)
        {
            UnityWebRequest webRequest = UnityWebRequestTexture.GetTexture(url);
            yield return MakeWebRequest(webRequest, headers, (req) =>
            {
                onSuccess(DownloadHandlerTexture.GetContent(req));
            }, onError);
        }

        /// <summary>
        /// Makes a POST request to the given URL with the given body, executing the corresponding callback on completion.
        /// </summary>
        /// <param name="url">The URL to call.</param>
        /// <param name="body">The body of the POST.</param>
        /// <param name="headers">Optional request headers.</param>
        /// <param name="onError">The callback executed on an unsuccessful request.</param>
        /// <param name="onSuccess">The callback executed on a successful request.</param>
        /// <returns></returns>
        public static IEnumerator PostRequest(string url, string body, HttpHeaders headers, Action<string> onSuccess, Action<HttpError> onError)
        {
            UnityWebRequest webRequest = new UnityWebRequest(url, "POST");
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(body);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            yield return MakeWebRequest(webRequest, headers, (req) =>
            {
                onSuccess(req.downloadHandler.text);
            }, onError);
        }

        private static IEnumerator MakeWebRequest(UnityWebRequest req, HttpHeaders headers, Action<UnityWebRequest> onSuccess, Action<HttpError> onError)
        {
            // Context system allows for intelligent pooling of requests and sharing of results
            string contextKey = $"{req.method}-${headers}-${req.url}";
            if (CONTEXTS.ContainsKey(contextKey))
            {
                CONTEXTS[contextKey].Enqueue(new PendingWebRequest(onSuccess, onError));
                Debug.Log($"Pending {req.method} Request to: {req.url}, adding response handler to queue...");
            }
            else
            {
                CONTEXTS[contextKey] = new Queue<PendingWebRequest>();
                CONTEXTS[contextKey].Enqueue(new PendingWebRequest(onSuccess, onError));
                Debug.Log($"Making {req.method} Request to: {req.url}");
                using (req)
                {
                    req.timeout = 30;
                    req.SetRequestHeader("Content-Type", headers.contentType);
                    if (headers.authorization != null)
                    {
                        req.SetRequestHeader("Authorization", $"Bearer {headers.authorization}");
                    }
                    foreach (string key in headers.customHeaders.Keys)
                    {
                        req.SetRequestHeader(key, headers.customHeaders[key]);
                    }
                    // Request and wait for the desired page.
                    yield return req.SendWebRequest();
                    do
                    {
                        if (CONTEXTS[contextKey].TryDequeue(out PendingWebRequest pair))
                        {
                            try
                            {
                                if (req.result != UnityWebRequest.Result.Success)
                                {
                                    string errorMessage = string.Format($"Error making request to URL: {req.url} : {req.error}");
                                    HttpError error = new HttpError(req.responseCode, errorMessage);
                                    pair.onError.Invoke(error);
                                }
                                else
                                {
                                    try
                                    {
                                        pair.onSuccess.Invoke(req);
                                    }
                                    catch (Exception e)
                                    {
                                        HttpError error = new HttpError(500, e.ToString());
                                        pair.onError.Invoke(error);
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                Debug.LogError(e);
                            }
                        }
                    } while (CONTEXTS[contextKey].Count > 0);
                    CONTEXTS.Remove(contextKey);
                }
            }
        }

        /// <summary>
        /// Returns the local IPv4 address of this machine.
        /// </summary>
        /// <returns>The local IPv4.</returns>
        public static IPAddress GetLocalIPAddress()
        {

            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("8.8.8.8", 65530);
                IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                return endPoint.Address;
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }

        /// <summary>
        /// Validates that a port is valid port number.
        /// </summary>
        /// <param name="value">The value to validate.</param>
        /// <param name="defaultPort">The value to use if the provided value is invalid.</param>
        /// <returns>A valid port number.</returns>
        public static int ValidatePortValue(int value, int defaultPort)
        {
            int port = value;
            if (port <= 0 || port > 65535)
            {
                port = defaultPort;
            }
            return port;
        }

        public class HttpHeaders
        {
            public string authorization;
            public string contentType = "application/json";
            public Dictionary<string, string> customHeaders = new Dictionary<string, string>();

            public bool Equals(HttpHeaders other)
            {
                // compare headers irrespective of order
                if (customHeaders.Keys.Count != other.customHeaders.Keys.Count)
                {
                    return false;
                }
                bool customEquals = true;
                foreach (var key in customHeaders.Keys)
                {
                    if (!other.customHeaders.ContainsKey(key))
                    {
                        return false;
                    }
                    else
                    {
                        customEquals &= customHeaders[key] == other.customHeaders[key];
                    }
                }
                // compare other properties
                bool authEqual = authorization == other.authorization && authorization != null;
                bool contentTypeEqual = contentType == other.contentType && contentType != null;
                return authEqual && contentTypeEqual;
            }

            public override string ToString()
            {
                List<string> pairs = new List<string>();
                foreach (string key in customHeaders.Keys)
                {
                    pairs.Add($"{key}::${customHeaders[key]}");
                }
                pairs.Sort();

                return $"{authorization}-${contentType}-${string.Join("-", pairs)}";
            }
        }


        public class HttpError
        {
            public long statusCode;
            public string message;

            public HttpError(long statusCode, string message)
            {
                this.statusCode = statusCode;
                this.message = message;
            }

            public override string ToString()
            {
                return statusCode + ": " + message;
            }
        }
    }
}