using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MinimalSoundEditor
{
    /// <summary>Duplicates each mono sample to left and right for stereo-only playback devices.</summary>
    public class MonoToStereoSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;

        /// <summary>Wraps a mono source while preserving its sample rate.</summary>
        public MonoToStereoSampleProvider(ISampleProvider source)
        {
            if (source.WaveFormat.Channels != 1)
                throw new ArgumentException("Source must be mono", nameof(source));

            _source = source;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(
                source.WaveFormat.SampleRate, 2);
        }

        public WaveFormat WaveFormat { get; }

        /// <summary>Reads mono frames and interleaves each value as an identical stereo pair.</summary>
        public int Read(float[] buffer, int offset, int count)
        {
            // count ist Stereo → halb so viele Monosamples lesen
            int samplesNeeded = count / 2;

            float[] mono = new float[samplesNeeded];
            int read = _source.Read(mono, 0, samplesNeeded);

            int outIndex = offset;
            for (int i = 0; i < read; i++)
            {
                float s = mono[i];
                buffer[outIndex++] = s; // Left
                buffer[outIndex++] = s; // Right
            }

            return read * 2;
        }
    }

}
