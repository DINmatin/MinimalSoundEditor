using Microsoft.VisualBasic.Devices;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MinimalSoundEditor
{
    internal static class BatchProcessor
    {
        public static async Task ProcessOneAsync(
            string inputPath,
            BatchOptions opt,
            CancellationToken ct,
            Action<string> log)
        {
            ct.ThrowIfCancellationRequested();

            // 1) load samples (you already have decoding logic in your app)
            //    -> implement these helpers by calling your existing load/export code.
            var audio = await AudioIo.LoadToSamplesAsync(inputPath, ct).ConfigureAwait(false);

            float[] samples = audio.Samples;
            int sampleRate = audio.SampleRate;

            if (opt.normalize)
            {
                samples = AudioOps.Normalize(samples);
                log($"Normalize: {Path.GetFileName(inputPath)}");
            }

            if (opt.trimSilence)
            {
                samples = AudioOps.TrimSilence(samples, threshold: 0.0015f);
                log($"Trim: {Path.GetFileName(inputPath)}");
            }

            if (opt.saveUnattended)
            {
                string outPath = MakeOutputPath(inputPath, suffix: "_edited", ext: ".wav");
                await AudioIo.SaveWavAsync(outPath, samples, sampleRate, ct).ConfigureAwait(false);
                log($"Save: {Path.GetFileName(outPath)}");
            }
        }

        private static string MakeOutputPath(string inputPath, string suffix, string ext)
        {
            string dir = Path.GetDirectoryName(inputPath)!;
            string baseName = Path.GetFileNameWithoutExtension(inputPath);
            return Path.Combine(dir, baseName + suffix + ext);
        }
    }
}
