using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using UnityEngine;

namespace Skeletom.BattleStation.Server
{
    public class WebServer : MonoBehaviour, IServer
    {
        private readonly Utils.Logger LOGGER = new("WebServer");

        [SerializeField]
        private int _port = 9000;
        public int Port
        {
            get { return _port; }
            set
            {
                _port = value;
                StartServer();
            }
        }

        private readonly Dictionary<string, IEndpoint> _endpoints = new();
        public List<string> Paths { get { return new List<string>(_endpoints.Keys); } }

        private HttpListener HTTP_LISTENER;
        private readonly LinkedList<HttpListenerContext> WAITING_CONTEXTS = new();
        private Thread LISTENER_THREAD;
        private bool CLOSE_THREAD_AND_CONTEXTS = false;

        private void OnEnable()
        {
            StartServer();
        }

        private void OnDisable()
        {
            StopServer();
        }

        public void RegisterEndpoint(IEndpoint endpoint)
        {
            _endpoints[endpoint.Path.ToLower()] = endpoint;
        }

        public void UnregisterEndpoint(IEndpoint endpoint)
        {
            if (_endpoints.ContainsKey(endpoint.Path.ToLower()))
            {
                _endpoints.Remove(endpoint.Path.ToLower());
            }
        }

        public void UnregisterEndpoint(string endpoint)
        {
            if (_endpoints.ContainsKey(endpoint.ToLower()))
            {
                _endpoints.Remove(endpoint.ToLower());
            }
        }

        public void StartServer()
        {
            StopServer();
            LOGGER.Log($"HTTP Server starting on port: {_port}...");
            CLOSE_THREAD_AND_CONTEXTS = false;
            LISTENER_THREAD = new Thread(ListenThread);
            LISTENER_THREAD.Start();
            LOGGER.Log($"HTTP Server started on port: {_port}");
        }

        public void StopServer()
        {
            CLOSE_THREAD_AND_CONTEXTS = true;
            WAITING_CONTEXTS.Clear();
            if (LISTENER_THREAD != null)
            {
                HTTP_LISTENER.Close();
                LISTENER_THREAD.Abort();
                LOGGER.Log($"HTTP Server stopped on port: {_port}");
            }
        }

        private void OnApplicationQuit()
        {
            StopServer();
        }

        private void ListenThread()
        {
            try
            {
                HTTP_LISTENER = new HttpListener();
                string host = string.Format("http://*:{0}/", this._port);
                HTTP_LISTENER.Prefixes.Add(host);
                HTTP_LISTENER.Start();
                while (!CLOSE_THREAD_AND_CONTEXTS)
                {
                    HttpListenerContext context = HTTP_LISTENER.GetContext();
                    context.Response.StatusCode = 200;
                    lock (WAITING_CONTEXTS)
                    {
                        WAITING_CONTEXTS.AddLast(context);
                    }
                }
            }
            catch (Exception e)
            {
                if (typeof(ThreadAbortException) == e.GetType())
                {
                    LOGGER.Log("HTTP Server is aborting listener thread...");
                }
                else
                {
                    LOGGER.LogError($"HTTP Server error at {e.StackTrace}.");
                    LOGGER.LogError($"{e.Message}");
                }
            }
        }

        private void Update()
        {
            HttpListenerContext nextContext = null;
            try
            {
                lock (WAITING_CONTEXTS)
                {
                    if (WAITING_CONTEXTS.Count > 0)
                    {
                        nextContext = WAITING_CONTEXTS.First.Value;
                        WAITING_CONTEXTS.RemoveFirst();
                    }
                }

                if (nextContext != null)
                {
                    string localPath = nextContext.Request.Url.LocalPath.ToLower();
                    if (_endpoints.ContainsKey(localPath))
                    {
                        IEndpoint endpoint = _endpoints[localPath];
                        var queryParams = System.Web.HttpUtility.ParseQueryString(nextContext.Request.Url.Query);
                        QueryParameter[] parameters = new QueryParameter[queryParams.Count];
                        for (int i = 0; i < queryParams.Count; i++)
                        {
                            string key = queryParams.AllKeys[i];
                            parameters[i] = new QueryParameter(key, queryParams[key]);
                        }
                        string body = "";
                        using (StreamReader reader = new(nextContext.Request.InputStream, nextContext.Request.ContentEncoding))
                        {
                            body = reader.ReadToEnd();
                        }
                        EndpointRequest args = new(endpoint, parameters, body, nextContext.Request.UserHostName);
                        EndpointResponse response = endpoint.ProcessRequest(args);
                        nextContext.Response.StatusCode = response.status;
                        // TODO: Content Type on Response
                        byte[] bytes = nextContext.Request.ContentEncoding.GetBytes(response.body);
                        nextContext.Response.OutputStream.Write(bytes, 0, bytes.Length);
                        nextContext.Response.Close();
                    }
                    else
                    {
                        nextContext.Response.StatusCode = 404;
                        nextContext.Response.Close();
                    }
                }
            }
            catch (Exception e)
            {
                LOGGER.LogError($"HTTP Server error: {e.Message}");
                if (nextContext != null)
                {
                    nextContext.Response.StatusCode = 500;
                    nextContext.Response.Close();
                }
            }
        }
    }
}