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

            double removedStartSeconds = 0;

            if (opt.trimSilence)
            {
                var trimmed = AudioOps.TrimSilenceWithInfo(samples, threshold: 0.0015f);
                samples = trimmed.Trimmed;

                if (trimmed.RemovedStartSamples > 0)
                    removedStartSeconds = trimmed.RemovedStartSamples / (double)sampleRate;

                log($"Trim: {Path.GetFileName(inputPath)} (start -{removedStartSeconds:0.###}s)");
            }


            // WAV
            if (opt.outputWav)
            {
                string outWav = MakeOutputPath(inputPath, "_edited", ".wav");
                await AudioIo.SaveWavAsync(outWav, samples, sampleRate, ct).ConfigureAwait(false);
                log($"Saved WAV: {Path.GetFileName(outWav)}");
            }

            // MP4 (only makes sense if input is MP4)
            if (opt.outputMp4)
            {
                string ext = Path.GetExtension(inputPath);
                if (!ext.Equals(".mp4", StringComparison.OrdinalIgnoreCase))
                {
                    log($"Skip MP4 output (input not mp4): {Path.GetFileName(inputPath)}");
                }
                else
                {
                    string outMp4 = MakeOutputPath(inputPath, "_edited", ".mp4");
                    await VideoMuxer.SaveMp4WithNewAudioAsync(
                        inputMp4Path: inputPath,
                        outputMp4Path: outMp4,
                        editedSamples: samples,
                        sampleRate: sampleRate,
                        trimStartSeconds: removedStartSeconds,
                        ct: ct
                    ).ConfigureAwait(false);

                    log($"Saved MP4: {Path.GetFileName(outMp4)}");
                }
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
