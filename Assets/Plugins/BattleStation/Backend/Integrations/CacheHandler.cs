using System;
using System.Collections.Generic;
using Skeletom.Essentials.Collections;
using Skeletom.Essentials.Utils;
using UnityEngine;

namespace Skeletom.BattleStation.Integrations
{
    public abstract class CacheHandler<T> : MonoBehaviour where T : class
    {
        [SerializeField]
        private int _maximumCachedItems = -1;
        private LRUDictionary<string, T> _cache;
        public LRUDictionary<string, T> CACHE => _cache ??= new LRUDictionary<string, T>(_maximumCachedItems < 0 ? int.MaxValue : _maximumCachedItems, CleanUncachedData);
        private readonly Dictionary<string, Queue<ContextCallback>> PENDING_CONTEXTS = new Dictionary<string, Queue<ContextCallback>>();
        private struct ContextCallback
        {
            public Action<T> onSuccess;
            public Action<StreamError> onError;
            public ContextCallback(Action<T> onSuccess, Action<StreamError> onError)
            {
                this.onSuccess = onSuccess;
                this.onError = onError;
            }
        }

        private void ResolveContext(string key, T data, StreamError err)
        {
            if (PENDING_CONTEXTS.ContainsKey(key))
            {
                do
                {
                    if (PENDING_CONTEXTS[key].TryDequeue(out ContextCallback pair))
                    {
                        if (data != null)
                        {
                            try
                            {
                                pair.onSuccess(data);
                            }
                            catch (Exception e)
                            {
                                pair.onError(new StreamError(StreamError.ErrorCode.ApplicationError, e.Message));
                            }
                        }
                        else
                        {
                            pair.onError(err);
                        }
                    }
                } while (PENDING_CONTEXTS[key].Count > 0);
                PENDING_CONTEXTS.Remove(key);
            }
        }

        private void Cache(string key, T data)
        {
            if (data != null)
            {
                Uncache(key);
                CACHE.Add(key, data);
            }
        }

        protected abstract void CleanUncachedData(T data);

        private void Uncache(string key)
        {
            if (CACHE.ContainsKey(key))
            {
                CleanUncachedData(CACHE.Get(key));
                CACHE.Remove(key);
            }

        }

        /// <summary>
        /// Attempts to get an image from the cache.
        /// Used in cases where the URL is not known ad is not able to be derived.
        /// </summary>
        /// <param name="key">The key name of the image to search for.</param>
        /// <param name="onSuccess">Callback function that receives the image if it exists in the cache.</param>
        /// <param name="onError">Callback function to handle errors of any kind.</param>
        public void GetFromCache(string key, Action<T> onSuccess, Action<HttpUtils.HttpError> onError)
        {
            if (CACHE.ContainsKey(key))
            {
                onSuccess(CACHE.Get(key));
            }
            else
            {
                onError(new HttpUtils.HttpError(500, $"Item with key: {key} does not exist in cache"));
            }
        }

        /// <summary>
        /// Attempts to get an image from a remote URL, storing it in the cache under the provided key name. 
        /// If the image is already cached, it will simply be returned without calling the URL.
        /// If a call to the remote URL is currently in progress, the result of that call will be shared with this request.
        /// </summary>
        /// <param name="url">The URL to get the image from.</param>
        /// <param name="headers">Any necessary HTTP headers for making the call.</param>
        /// <param name="key">The key name to store the image as.</param>
        /// <param name="onSuccess">Callback function that receives the image if successful.</param>
        /// <param name="onError">Callback function to handle errors of any kind.</param>
        public void GetFromRemote(string url, HttpUtils.HttpHeaders headers, string key, Action<T> onSuccess, Action<StreamError> onError)
        {
            // If the emote is already cached, return it immediately.
            if (CACHE.ContainsKey(key))
            {
                onSuccess(CACHE[key]);
            }
            // If another request is already processing this emote, wait for that.
            else if (PENDING_CONTEXTS.ContainsKey(key))
            {
                PENDING_CONTEXTS[key].Enqueue(new ContextCallback(onSuccess, onError));
            }
            // If nothing is processing this emote, make a request to get and process it.
            else
            {
                PENDING_CONTEXTS[key] = new Queue<ContextCallback>();
                PENDING_CONTEXTS[key].Enqueue(new ContextCallback(onSuccess, onError));
                StartCoroutine(
                    HttpUtils.GetBytesRequest(url, headers,
                        (bytes) =>
                        {
                            var result = HandleData(key, bytes);
                            if (result.data != null)
                            {
                                Cache(key, result.data);
                                ResolveContext(key, CACHE[key], null);
                            }
                            else
                            {
                                ResolveContext(key, null, result.error);
                            }
                        },
                        (err) =>
                        {
                            Debug.LogError($"Error while resolving item: {key} - {err}");
                            ResolveContext(key, null, new StreamError(err));
                        }
                    )
                );
            }
        }

        protected struct ContextResolution
        {
            public readonly T data;
            public readonly StreamError error;
            public ContextResolution(T data, StreamError error)
            {
                this.data = data;
                this.error = error;
            }
        }

        protected abstract ContextResolution HandleData(string key, byte[] data);
    }
}
