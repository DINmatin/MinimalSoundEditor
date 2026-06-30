using NAudio.Wave;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Decoded mono sample data together with the sample rate required to interpret it.</summary>
internal readonly record struct AudioData(float[] Samples, int SampleRate);

/// <summary>Shared asynchronous file I/O used by the batch processor and video muxing workflow.</summary>
internal static class AudioIo
{
    /// <summary>Decodes any NAudio-supported input and averages all channels into the editor's mono format.</summary>
    public static Task<AudioData> LoadToSamplesAsync(string path, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            using var reader = new AudioFileReader(path);
            int sampleRate = reader.WaveFormat.SampleRate;
            int channels = reader.WaveFormat.Channels;

            var mono = new List<float>(capacity: (int)(reader.Length / 4));

            float[] buffer = new float[4096 * channels];
            int read;
            while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                ct.ThrowIfCancellationRequested();

                // downmix to mono
                for (int i = 0; i < read; i += channels)
                {
                    float sum = 0f;
                    for (int c = 0; c < channels; c++)
                        sum += buffer[i + c];
                    mono.Add(sum / channels);
                }
            }

            return new AudioData(mono.ToArray(), sampleRate);
        }, ct);
    }

    /// <summary>Writes mono floating-point samples to a temporary or final WAV file.</summary>
    public static Task SaveWavAsync(string outPath, float[] samples, int sampleRate, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            using var writer = new WaveFileWriter(outPath, WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1));
            writer.WriteSamples(samples, 0, samples.Length);
        }, ct);
    }
}
