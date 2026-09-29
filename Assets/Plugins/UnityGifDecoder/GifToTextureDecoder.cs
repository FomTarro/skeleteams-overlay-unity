using System;
using System.Collections;
using System.Collections.Generic;
using ThreeDISevenZeroR.UnityGifDecoder;
using UnityEngine;

public static class GifToTextureDecoder
{
    [Serializable]
    public struct Frame
    {
        public Texture2D texture;
        public float delay;
        public Frame(Texture2D texture, float delay)
        {
            this.texture = texture;
            this.delay = delay;
        }
    }
    public static List<Frame> Decode(byte[] bytes)
    {
        var frames = new List<Frame>();
        using (var gifStream = new GifStream(bytes))
        {
            while (gifStream.HasMoreData)
            {
                switch (gifStream.CurrentToken)
                {
                    case GifStream.Token.Image:
                        var image = gifStream.ReadImage();
                        var frame = new Texture2D(
                            gifStream.Header.width,
                            gifStream.Header.height,
                            TextureFormat.ARGB32, false);

                        frame.SetPixels32(image.colors);
                        frame.Apply();

                        frames.Add(new Frame(frame, image.SafeDelaySeconds));
                        break;

                    case GifStream.Token.Comment:
                        var commentText = gifStream.ReadComment();
                        Debug.Log(commentText);
                        break;

                    default:
                        gifStream.SkipToken(); // Other tokens
                        break;
                }
            }
        }
        return frames;
    }
}
