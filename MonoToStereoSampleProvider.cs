using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MinimalSoundEditor
{
    public class MonoToStereoSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;

        public MonoToStereoSampleProvider(ISampleProvider source)
        {
            if (source.WaveFormat.Channels != 1)
                throw new ArgumentException("Source must be mono", nameof(source));

            _source = source;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(
                source.WaveFormat.SampleRate, 2);
        }

        public WaveFormat WaveFormat { get; }

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
