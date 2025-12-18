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

        private IEndpoint[] _endpoints = new IEndpoint[0];
        public List<string> Paths => GetPaths();

        private List<string> GetPaths()
        {
            List<string> paths = new List<string>();
            foreach (IEndpoint endpoint in this._endpoints)
            {
                paths.Add(endpoint.Path);
            }
            return paths;
        }

        private HttpListener HTTP_LISTENER;
        private readonly LinkedList<HttpListenerContext> WAITING_CONTEXTS = new LinkedList<HttpListenerContext>();
        private Thread LISTENER_THREAD;
        private bool CLOSE_THREAD_AND_CONTEXTS = false;

        public void Start()
        {
            StartServer();
        }

        public void StartServer()
        {
            StopServer();
            Debug.Log(string.Format("HTTP Server starting on port: {0}...", this._port));
            this._endpoints = GetComponents<IEndpoint>();
            CLOSE_THREAD_AND_CONTEXTS = false;
            LISTENER_THREAD = new Thread(ListenThread);
            LISTENER_THREAD.Start();
            Debug.Log(string.Format("HTTP Server started on port: {0}!", this._port));
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

        void OnApplicationQuit()
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
                    bool match = false;
                    foreach (IEndpoint endpoint in this._endpoints)
                    {
                        if (nextContext.Request.Url.LocalPath.ToLower().Equals(endpoint.Path.ToLower()))
                        {
                            var queryParams = System.Web.HttpUtility.ParseQueryString(nextContext.Request.Url.Query);
                            QueryParameter[] parameters = new QueryParameter[queryParams.Count];
                            for (int i = 0; i < queryParams.Count; i++)
                            {
                                string key = queryParams.AllKeys[i];
                                parameters[i] = new QueryParameter(key, queryParams[key]);
                            }
                            match = true;
                            string body = "";
                            using (StreamReader reader = new StreamReader(nextContext.Request.InputStream, nextContext.Request.ContentEncoding))
                            {
                                body = reader.ReadToEnd();
                            }
                            HttpRequestArgs args = new HttpRequestArgs(endpoint, parameters, body, nextContext.Request.UserHostName);
                            IResponseArgs response = endpoint.ProcessRequest(args);
                            nextContext.Response.StatusCode = response.Status;
                            // TODO: Content Type on Response
                            byte[] bytes = nextContext.Request.ContentEncoding.GetBytes(response.Body);
                            nextContext.Response.OutputStream.Write(bytes, 0, bytes.Length);
                            nextContext.Response.Close();
                        }
                    }
                    if (!match)
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

        private class QueryParameter : IQueryParameter
        {
            public string Key { get; private set; }
            public string Value { get; private set; }
            public QueryParameter(string key, string value)
            {
                Key = key;
                Value = value;
            }
        }

        private class HttpRequestArgs : IRequestArgs
        {
            public IEndpoint Endpoint { get; private set; }
            public IQueryParameter[] QueryParameters { get; private set; }
            public string Body { get; private set; }
            public string ClientID { get; private set; }

            public HttpRequestArgs(IEndpoint endpoint, IQueryParameter[] queryParameters, string body, string id)
            {
                Endpoint = endpoint;
                QueryParameters = queryParameters;
                Body = body;
                ClientID = id;
            }
        }
    }
}