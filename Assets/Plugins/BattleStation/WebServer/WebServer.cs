using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using Skeletom.Essentials.Lifecycle;
using UnityEngine;

public class WebServer : Singleton<WebServer>, IServer
{

    [SerializeField]
    private int _port = 9000;
    public int Port => this._port;

    private IEndpoint[] _endpoints = new IEndpoint[0];
    [SerializeField]
    private string[] _suffixes = new string[] { "QuickServer" };
    public List<string> Paths => GetPaths();

    private List<string> GetPaths()
    {
        List<string> paths = new List<string>();
        foreach (string suffix in this._suffixes)
        {
            foreach (IEndpoint endpoint in this._endpoints)
            {
                paths.Add(string.Format("{0}{1}", suffix, endpoint.Path));
            }
        }
        return paths;
    }

    private HttpListener _listener;
    private LinkedList<HttpListenerContext> _waitingContexts = new LinkedList<HttpListenerContext>();
    private Thread _listenerThread;
    private bool _closeThreadAndContexts = false;

    public void SetPort(int port)
    {
        this._port = port;
    }

    public override void Initialize()
    {
        StartServer();
    }


    public void StartServer()
    {
        StopServer();
        Debug.Log(string.Format("HTTP Server starting on port: {0}...", this._port));
        this._endpoints = GetComponents<IEndpoint>();
        this._closeThreadAndContexts = false;
        this._listenerThread = new Thread(ListenThread);
        this._listenerThread.Start();
        Debug.Log(string.Format("HTTP Server started on port: {0}!", this._port));
    }

    public void StopServer()
    {
        this._closeThreadAndContexts = true;
        this._waitingContexts.Clear();
        if (this._listenerThread != null)
        {
            this._listener.Close();
            this._listenerThread.Abort();
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
            this._listener = new HttpListener();
            string host = string.Format("http://*:{0}/", this._port);
            foreach (string suffix in this._suffixes)
            {
                this._listener.Prefixes.Add(string.Format("{0}{1}/", host, suffix));
            }

            this._listener.Start();
            while (!this._closeThreadAndContexts)
            {
                HttpListenerContext context = this._listener.GetContext();
                //Debug.LogFormat("Recieved request from {0}.", context.Request.RemoteEndPoint.ToString());
                context.Response.StatusCode = 200;
                lock (this._waitingContexts)
                {
                    this._waitingContexts.AddLast(context);
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
            lock (this._waitingContexts)
            {
                if (this._waitingContexts.Count > 0)
                {
                    nextContext = this._waitingContexts.First.Value;
                    this._waitingContexts.RemoveFirst();
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
                        using (var reader = new StreamReader(nextContext.Request.InputStream, nextContext.Request.ContentEncoding))
                        {
                            body = reader.ReadToEnd();
                        }
                        HttpRequestArgs args = new HttpRequestArgs(endpoint, parameters, body, nextContext.Request.UserHostName);
                        var response = endpoint.ProcessRequest(args);
                        nextContext.Response.StatusCode = response.Status;
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
