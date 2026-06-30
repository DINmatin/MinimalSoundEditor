using System;
using System.IO;

namespace MinimalSoundEditor
{
    /// <summary>Keeps drag-and-drop validation aligned with the formats the batch pipeline can actually process.</summary>
    internal static class BatchFileValidator
    {
        /// <summary>Recognizes supported extensions and supplies the short type label shown in the queue.</summary>
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
