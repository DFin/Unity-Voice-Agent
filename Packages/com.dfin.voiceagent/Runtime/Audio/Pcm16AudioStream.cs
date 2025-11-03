using System;

namespace DFIN.VoiceAgent.Audio
{
    /// <summary>
    /// Aggregates base64 PCM16 audio deltas into short sample buffers.
    /// </summary>
    public class Pcm16AudioStream
    {
        public event Action<short[]> SamplesAvailable;
        public event Action SegmentCompleted;

        public void Reset()
        {
            // no buffered state is kept between segments
        }

        public void AppendDelta(string base64Payload)
        {
            if (string.IsNullOrEmpty(base64Payload))
            {
                return;
            }

            try
            {
                var bytes = Convert.FromBase64String(base64Payload);
                var sampleCount = bytes.Length / sizeof(short);
                if (sampleCount <= 0)
                {
                    return;
                }

                var samples = new short[sampleCount];
                Buffer.BlockCopy(bytes, 0, samples, 0, bytes.Length);
                SamplesAvailable?.Invoke(samples);
            }
            catch (FormatException)
            {
                // ignore invalid payloads
            }
        }

        public void MarkSegmentComplete()
        {
            SegmentCompleted?.Invoke();
        }
    }
}
