using System;
using System.Collections.Generic;

namespace DFIN.VoiceAgent.OpenAI
{
    /// <summary>
    /// Aggregates OpenAI realtime audio deltas into PCM16 buffers.
    /// </summary>
    public class OpenAiAudioStream
    {
        private readonly List<byte> currentSegment = new();

        public event Action<short[]> SegmentReady;

        public void Reset()
        {
            currentSegment.Clear();
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
                currentSegment.AddRange(bytes);
            }
            catch (FormatException)
            {
                // ignore invalid payloads
            }
        }

        public void MarkSegmentComplete()
        {
            if (currentSegment.Count == 0)
            {
                return;
            }

            var data = currentSegment.ToArray();
            var sampleCount = data.Length / sizeof(short);
            var samples = new short[sampleCount];
            Buffer.BlockCopy(data, 0, samples, 0, data.Length);

            SegmentReady?.Invoke(samples);

            currentSegment.Clear();
        }
    }
}
