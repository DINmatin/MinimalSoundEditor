using MinimalSoundEditor;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

internal static class VideoMuxer
{
    public static async Task SaveMp4WithNewAudioAsync(
        string inputMp4Path,
        string outputMp4Path,
        float[] editedSamples,
        int sampleRate,
        double trimStartSeconds,
        CancellationToken ct)
    {
        string ffmpeg = GetFfmpegPath();

        // 1) write edited audio to temp WAV
        string tempWav = Path.Combine(Path.GetTempPath(), "mse_batch_audio_" + Guid.NewGuid().ToString("N") + ".wav");
        try
        {
            await AudioIo.SaveWavAsync(tempWav, editedSamples, sampleRate, ct).ConfigureAwait(false);

            // 2) mux: video from original + audio from temp wav
            // If we trimmed silence at start, we trim video start too via -ss.
            string ss = trimStartSeconds.ToString("0.###", CultureInfo.InvariantCulture);

            string args = trimStartSeconds > 0.0001
                ? $"-y -hide_banner -loglevel error -nostdin -ss {ss} -i \"{inputMp4Path}\" -i \"{tempWav}\" " +
                  $"-map 0:v:0 -map 1:a:0 -c:v copy -c:a aac -b:a 192k -shortest \"{outputMp4Path}\""
                : $"-y -hide_banner -loglevel error -nostdin -i \"{inputMp4Path}\" -i \"{tempWav}\" " +
                  $"-map 0:v:0 -map 1:a:0 -c:v copy -c:a aac -b:a 192k -shortest \"{outputMp4Path}\"";

            await RunProcessAsync(ffmpeg, args, ct).ConfigureAwait(false);

            if (!File.Exists(outputMp4Path))
                throw new Exception("ffmpeg finished but output file is missing: " + outputMp4Path);
        }
        finally
        {
            try { if (File.Exists(tempWav)) File.Delete(tempWav); } catch { }
        }
    }

    private static string GetFfmpegPath()
    {
        string ffmpeg = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "ffmpeg.exe");
        if (!File.Exists(ffmpeg))
            throw new FileNotFoundException("ffmpeg.exe not found: " + ffmpeg);
        return ffmpeg;
    }

    private static async Task RunProcessAsync(string exe, string args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        using var p = Process.Start(psi);
        if (p == null) throw new Exception("Could not start process: " + exe);

        string stderr = await p.StandardError.ReadToEndAsync().ConfigureAwait(false);
        string stdout = await p.StandardOutput.ReadToEndAsync().ConfigureAwait(false);

        await p.WaitForExitAsync(ct).ConfigureAwait(false);

        if (p.ExitCode != 0)
            throw new Exception($"ffmpeg failed (ExitCode {p.ExitCode}).\n{(string.IsNullOrWhiteSpace(stderr) ? stdout : stderr)}");
    }
}
