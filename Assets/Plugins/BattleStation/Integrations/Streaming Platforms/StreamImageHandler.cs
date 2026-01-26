using System.Linq;
using UnityEngine;

namespace Skeletom.BattleStation.Integrations
{
    public class StreamImageHandler : CacheHandler<StreamImage>
    {
        protected override void CleanUncachedData(StreamImage data)
        {
            foreach (var frame in data.frames)
            {
                Destroy(frame.image);
            }
        }

        protected override ContextResolution HandleData(string key, byte[] data)
        {
            var format = GetImageFormat(data);
            if (format == ImageFormat.Gif)
            {
                var frames = GifToTextureDecoder.Decode(data).Select(
                    frame => new StreamImage.Frame(frame.texture, frame.delay)
                );
                Debug.Log($"Resolved animated image: {key}");
                return new ContextResolution(new StreamImage(key, frames), null);
            }
            else
            {
                var tex = new Texture2D(2, 2);
                if (tex.LoadImage(data))
                {
                    Debug.Log($"Resolved static image: {key}");
                    return new ContextResolution(new StreamImage(key, tex), null);
                }
                else
                {
                    return new ContextResolution(new StreamImage(key, tex), new StreamError(
                    StreamError.ErrorCode.ApplicationError,
                    $"Unable to convert image to Texture2D for {key}"));
                }
            }
        }

        private enum ImageFormat
        { Unknown, Jpeg, Png, Gif };

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
