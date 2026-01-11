using System;
using System.IO;

namespace MinimalSoundEditor
{
    internal static class BatchFileValidator
    {
        public static bool IsSupported(string path, out string type)
        {
            type = "";
            string ext = Path.GetExtension(path).ToLowerInvariant();

            // ✅ match what your app can actually open
            switch (ext)
            {
                case ".wav": type = "WAV"; return true;
                case ".mp3": type = "MP3"; return true;
                case ".mp4": type = "MP4"; return true;
                default: return false;
            }
        }
    }
}
