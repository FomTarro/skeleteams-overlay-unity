using System;
using System.Collections.Generic;
using System.Linq;
using Skeletom.BattleStation.Integrations;
using Skeletom.Essentials.Utils;
using UnityEngine;

namespace Skeletom.BattleStation.Integrations
{
    public class StreamIntegrationImageHandler : MonoBehaviour
    {
        private readonly Dictionary<string, StreamingPlatformImage> IMAGE_CACHE = new Dictionary<string, StreamingPlatformImage>();
        private readonly Dictionary<string, Queue<ContextCallback>> PENDING_CONTEXTS = new Dictionary<string, Queue<ContextCallback>>();
        private struct ContextCallback
        {
            public Action<StreamingPlatformImage> onSuccess;
            public Action<HttpUtils.HttpError> onError;
            public ContextCallback(Action<StreamingPlatformImage> onSuccess, Action<HttpUtils.HttpError> onError)
            {
                this.onSuccess = onSuccess;
                this.onError = onError;
            }
        }

        private void ResolveContext(string key, StreamingPlatformImage img, HttpUtils.HttpError err)
        {
            if (PENDING_CONTEXTS.ContainsKey(key))
            {
                do
                {
                    if (PENDING_CONTEXTS[key].TryDequeue(out ContextCallback pair))
                    {
                        if (img != null)
                        {
                            try
                            {
                                pair.onSuccess(img);
                            }
                            catch (Exception e)
                            {
                                pair.onError(new HttpUtils.HttpError(500, e.Message));
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

        private void CacheChatImage(StreamingPlatformImage img)
        {
            if (img != null)
            {
                UncacheChatImage(img);
                IMAGE_CACHE[img.name] = img;
            }
        }

        private void UncacheChatImage(StreamingPlatformImage img)
        {
            if (img != null)
            {
                if (IMAGE_CACHE.ContainsKey(img.name))
                {
                    foreach (StreamingPlatformImage.Frame frame in IMAGE_CACHE[img.name].frames)
                    {
                        Destroy(frame.image);
                    }
                }
            }
        }

        /// <summary>
        /// Attempts to get an image from the cache.
        /// Used in cases where the URL is not known ad is not able to be derived.
        /// </summary>
        /// <param name="key">The key name of the image to search for.</param>
        /// <param name="onSuccess">Callback function that receives the image if it exists in the cache.</param>
        /// <param name="onError">Callback function to handle errors of any kind.</param>
        public void GetImageFromCache(string key, Action<StreamingPlatformImage> onSuccess, Action<HttpUtils.HttpError> onError)
        {
            if (IMAGE_CACHE.ContainsKey(key))
            {
                onSuccess(IMAGE_CACHE[key]);
            }
            else
            {
                onError(new HttpUtils.HttpError(500, $"Image with key: {key} does not exist in cache"));
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
        public void GetImageFromRemote(string url, HttpUtils.HttpHeaders headers, string key, Action<StreamingPlatformImage> onSuccess, Action<HttpUtils.HttpError> onError)
        {
            // If the emote is already cached, return it immediately.
            if (IMAGE_CACHE.ContainsKey(key))
            {
                onSuccess(IMAGE_CACHE[key]);
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
                            var format = GetImageFormat(bytes);
                            if (format == ImageFormat.Gif)
                            {
                                var frames = GifToTextureDecoder.Decode(bytes).Select(
                                    frame => new StreamingPlatformImage.Frame(frame.texture, frame.delay)
                                );
                                CacheChatImage(new StreamingPlatformImage(key, frames));
                                Debug.Log($"Resolved animated image: {key}");
                                ResolveContext(key, IMAGE_CACHE[key], null);
                            }
                            else
                            {
                                var tex = new Texture2D(2, 2);
                                if (tex.LoadImage(bytes))
                                {
                                    CacheChatImage(new StreamingPlatformImage(key, tex));
                                    Debug.Log($"Resolved static image: {key}");
                                    ResolveContext(key, IMAGE_CACHE[key], null);
                                }
                                else
                                {
                                    ResolveContext(key, null, new HttpUtils.HttpError(500, $"Unable to convert emote to Texture2D for {key}"));
                                }
                            }
                        },
                        (err) =>
                        {
                            Debug.LogError($"Error while resolving emote: {key} - {err}");
                            ResolveContext(key, null, err);
                        }
                    )
                );
            }
        }

        private enum ImageFormat { Unknown, Jpeg, Png, Gif };

        private static ImageFormat GetImageFormat(byte[] bytes)
        {
            // Check for JPEG (starts with FF D8, ends with FF D9)
            if (bytes.Length > 4 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[^2] == 0xFF && bytes[^1] == 0xD9)
            {
                return ImageFormat.Jpeg;
            }

            // Check for PNG (starts with 89 50 4E 47 0D 0A 1A 0A)
            if (bytes.Length > 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
                bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
            {
                return ImageFormat.Png;
            }

            // Check for GIF (starts with 47 49 46 38)
            if (bytes.Length > 4 && bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x38)
            {
                return ImageFormat.Gif;
            }

            return ImageFormat.Unknown;
        }
    }
}
