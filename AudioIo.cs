using System.Threading;
using System.Threading.Tasks;

namespace MinimalSoundEditor
{
    internal readonly record struct AudioData(float[] Samples, int SampleRate);

    internal static class AudioIo
    {
        public static Task<AudioData> LoadToSamplesAsync(string path, CancellationToken ct)
        {
            // TODO: hook into your existing decode:
            // - WAV/MP3: NAudio reader
            // - MP4: your existing ffmpeg extraction logic (you already do that for preview)
            //
            // Keep this method "UI-free". No MessageBox, no waveform.
            throw new System.NotImplementedException("Hook this to your existing load pipeline.");
        }

        public static Task SaveWavAsync(string outPath, float[] samples, int sampleRate, CancellationToken ct)
        {
            // TODO: call your existing ExportSamplesToFile(...) or WaveFileWriter logic.
            throw new System.NotImplementedException("Hook this to your existing WAV export.");
        }
    }
}
