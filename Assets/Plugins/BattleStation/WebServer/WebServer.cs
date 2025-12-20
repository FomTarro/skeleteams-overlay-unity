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

        private Dictionary<string, IEndpoint> _endpoints = new Dictionary<string, IEndpoint>();
        public List<string> Paths { get { return new List<string>(_endpoints.Keys); } }

        private HttpListener HTTP_LISTENER;
        private readonly LinkedList<HttpListenerContext> WAITING_CONTEXTS = new LinkedList<HttpListenerContext>();
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

        public void StartServer()
        {
            StopServer();
            Debug.Log(string.Format("HTTP Server starting on port: {0}...", this._port));
            CLOSE_THREAD_AND_CONTEXTS = false;
            LISTENER_THREAD = new Thread(ListenThread);
            LISTENER_THREAD.Start();
            Debug.Log(string.Format("HTTP Server started on port: {0}", this._port));
        }

        public void StopServer()
        {
            CLOSE_THREAD_AND_CONTEXTS = true;
            WAITING_CONTEXTS.Clear();
            if (LISTENER_THREAD != null)
            {
                HTTP_LISTENER.Close();
                LISTENER_THREAD.Abort();
                Debug.Log(string.Format("HTTP Server stopped on port: {0}", this._port));
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
                    //Debug.LogFormat("Recieved request from {0}.", context.Request.RemoteEndPoint.ToString());
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
                    Debug.Log("HTTP Server is aborting listener thread...");
                }
                else
                {
                    Debug.LogError(string.Format("HTTP Server error at {0}.", e.StackTrace));
                    Debug.LogError(e.Message, this);
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
                        using (StreamReader reader = new StreamReader(nextContext.Request.InputStream, nextContext.Request.ContentEncoding))
                        {
                            body = reader.ReadToEnd();
                        }
                        EndpointRequest args = new EndpointRequest(endpoint, parameters, body, nextContext.Request.UserHostName);
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
                Debug.LogError(string.Format("HTTP Server error: {0}", e));
                if (nextContext != null)
                {
                    nextContext.Response.StatusCode = 500;
                    nextContext.Response.Close();
                }
            }
        }
    }
}