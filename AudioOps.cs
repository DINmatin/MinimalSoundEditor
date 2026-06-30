using System;

namespace MinimalSoundEditor
{
    /// <summary>Pure sample-array operations used by the non-destructive batch workflow.</summary>
    internal static class AudioOps
    {
        /// <summary>Scales a copy of the signal so its highest absolute sample reaches just below 0 dBFS.</summary>
        public static float[] Normalize(float[] samples)
        {
            if (samples.Length == 0) return samples;

            float peak = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float a = Math.Abs(samples[i]);
                if (a > peak) peak = a;
            }

            if (peak <= 0.000001f) return samples;

            float gain = 0.999f / peak;

            var outS = new float[samples.Length];
            for (int i = 0; i < samples.Length; i++)
                outS[i] = samples[i] * gain;

            return outS;
        }
        /// <summary>Trims leading and trailing silence and reports the removed start offset for video synchronization.</summary>
        public static (float[] Trimmed, int RemovedStartSamples) TrimSilenceWithInfo(float[] samples, float threshold)
        {
            if (samples.Length == 0) return (samples, 0);

            int start = 0;
            int end = samples.Length - 1;

            while (start < samples.Length && Math.Abs(samples[start]) < threshold)
                start++;

            while (end > start && Math.Abs(samples[end]) < threshold)
                end--;

            int len = end - start + 1;
            if (len <= 0) return (Array.Empty<float>(), start);

            if (start == 0 && len == samples.Length)
                return (samples, 0);

            var outS = new float[len];
            Array.Copy(samples, start, outS, 0, len);
            return (outS, start);
        }

        /// <summary>Returns only the audible range when the caller does not need the removed start offset.</summary>
        public static float[] TrimSilence(float[] samples, float threshold)
        {
            if (samples.Length == 0) return samples;

            int start = 0;
            int end = samples.Length - 1;

            while (start < samples.Length && Math.Abs(samples[start]) < threshold)
                start++;

            while (end > start && Math.Abs(samples[end]) < threshold)
                end--;

            int len = end - start + 1;
            if (len <= 0) return Array.Empty<float>();
            if (start == 0 && len == samples.Length) return samples;

            var outS = new float[len];
            Array.Copy(samples, start, outS, 0, len);
            return outS;
        }
    }
}
