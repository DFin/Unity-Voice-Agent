using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace DFIN.VoiceAgent.Audio
{
    /// <summary>
    /// Streams PCM samples into an AudioSource in real time.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class StreamingAudioPlayer : MonoBehaviour
    {
        [SerializeField]
        private AudioSource audioSource;

        [SerializeField]
        private bool spatialize;

        [SerializeField]
        private float playbackVolume = 1f;

        [SerializeField]
        [Min(1)]
        private int bufferLengthSeconds = 4;

        private readonly ConcurrentQueue<float> sampleQueue = new();
        private AudioClip streamingClip;
        private int outputSampleRate;
        private float[] resampleBuffer;
        private bool isInitialized;
        private double lastSampleTimestamp;

        private const int MaxBufferedSeconds = 1800;

        private void Awake()
        {
            audioSource ??= GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            audioSource.playOnAwake = false;
            audioSource.loop = true;
            audioSource.spatialize = spatialize;
            audioSource.spatialBlend = spatialize ? 1f : 0f;
            audioSource.volume = playbackVolume;
        }

        private void Start()
        {
            outputSampleRate = AudioSettings.outputSampleRate;
            var bufferSamples = Mathf.Max(outputSampleRate * bufferLengthSeconds, outputSampleRate);
            streamingClip = AudioClip.Create("VoiceAgentStream", bufferSamples, 1, outputSampleRate, true, OnAudioRead, OnAudioSetPosition);
            audioSource.clip = streamingClip;
            audioSource.Play();
            isInitialized = true;
        }

        private void OnDestroy()
        {
            Clear();
            streamingClip = null;
            isInitialized = false;
        }

        public void EnqueuePcm16Samples(short[] samples, int sampleRate)
        {
            if (!isInitialized || samples == null || samples.Length == 0 || sampleRate <= 0)
            {
                return;
            }

            var ratio = (double)outputSampleRate / sampleRate;
            if (Math.Abs(ratio - 1.0) < 0.0001)
            {
                for (var i = 0; i < samples.Length; i++)
                {
                    sampleQueue.Enqueue(samples[i] / (float)short.MaxValue);
                }
            }
            else
            {
                var targetSamples = (int)Math.Ceiling(samples.Length * ratio);
                EnsureResampleBuffer(targetSamples);

                var inputLength = samples.Length;
                for (var i = 0; i < targetSamples; i++)
                {
                    var index = i / ratio;
                    var i0 = (int)Math.Floor(index);
                    var i1 = Math.Min(i0 + 1, inputLength - 1);
                    var frac = index - i0;

                    var sample0 = samples[Mathf.Clamp(i0, 0, inputLength - 1)] / (float)short.MaxValue;
                    var sample1 = samples[Mathf.Clamp(i1, 0, inputLength - 1)] / (float)short.MaxValue;

                    resampleBuffer[i] = Mathf.Lerp(sample0, sample1, (float)frac);
                }

                for (var i = 0; i < targetSamples; i++)
                {
                    sampleQueue.Enqueue(resampleBuffer[i]);
                }
            }

            TrimQueueIfNeeded();
            lastSampleTimestamp = AudioSettings.dspTime;
        }

        public void Clear()
        {
            while (sampleQueue.TryDequeue(out _))
            {
                // drain queue
            }
        }

        public bool HasRecentSamples(double toleranceSeconds = 0.25)
        {
            if (!isInitialized)
            {
                return false;
            }

            if (sampleQueue.IsEmpty && streamingClip != null && !audioSource.isPlaying)
            {
                return false;
            }

            return AudioSettings.dspTime - lastSampleTimestamp <= toleranceSeconds;
        }

        private void OnAudioRead(float[] data)
        {
            var length = data.Length;
            for (var i = 0; i < length; i++)
            {
                if (sampleQueue.TryDequeue(out var sample))
                {
                    data[i] = Mathf.Clamp(sample, -1f, 1f);
                }
                else
                {
                    data[i] = 0f;
                }
            }
        }

        private void OnAudioSetPosition(int position)
        {
            // no-op
        }

        private void EnsureResampleBuffer(int requiredSize)
        {
            if (resampleBuffer == null || resampleBuffer.Length < requiredSize)
            {
                resampleBuffer = new float[requiredSize];
            }
        }

        private void TrimQueueIfNeeded()
        {
            var maxSamples = outputSampleRate * MaxBufferedSeconds;
            while (sampleQueue.Count > maxSamples && sampleQueue.TryDequeue(out _))
            {
                // drop older samples to keep latency bounded
            }
        }
    }
}
