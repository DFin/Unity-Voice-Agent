using System;
using UnityEngine;

namespace DFIN.VoiceAgent.Audio
{
    /// <summary>
    /// Simplified microphone capture wrapper. Streams audio samples via the SampleReady event.
    /// Full streaming pipeline will be fleshed out in Phase 1.
    /// </summary>
    [DisallowMultipleComponent]
    public class MicrophoneCapture : MonoBehaviour
    {
        /// <summary>
        /// Invoked whenever a new chunk of audio samples is captured. Buffer is reused; copy if you need to keep data.
        /// </summary>
        public event Action<float[]> SampleReady;

        [SerializeField]
        private int sampleRate = 16000;

        [SerializeField]
        private int bufferLengthMs = 100;

        [SerializeField]
        private bool autoStart = true;

        private AudioClip recordingClip;
        private string selectedDevice;
        private int lastSamplePosition;
        private float[] sampleBuffer;
        private float[] wrapHeadBuffer;
        private float[] wrapTailBuffer;

        public int SampleRate => sampleRate;

        private void Awake()
        {
            RecalculateSampleBuffer();
            selectedDevice = Microphone.devices.Length > 0 ? Microphone.devices[0] : null;
        }

        private void OnEnable()
        {
            if (autoStart)
            {
                StartCapture();
            }
        }

        private void OnDisable()
        {
            StopCapture();
        }

        private void Update()
        {
            if (recordingClip == null)
            {
                return;
            }

            var currentPosition = Microphone.GetPosition(selectedDevice);
            var samplesAvailable = currentPosition - lastSamplePosition;

            if (samplesAvailable < 0)
            {
                samplesAvailable += recordingClip.samples;
            }

            while (samplesAvailable >= sampleBuffer.Length)
            {
                ReadChunk(sampleBuffer.Length);
                SampleReady?.Invoke(sampleBuffer);

                lastSamplePosition = (lastSamplePosition + sampleBuffer.Length) % recordingClip.samples;
                samplesAvailable -= sampleBuffer.Length;
            }
        }

        public void StartCapture()
        {
            if (recordingClip != null)
            {
                return;
            }

            if (string.IsNullOrEmpty(selectedDevice))
            {
                Debug.LogWarning("No microphone devices available.");
                return;
            }

            recordingClip = Microphone.Start(selectedDevice, true, 1, sampleRate);
            lastSamplePosition = 0;
        }

        public void StopCapture()
        {
            if (recordingClip == null)
            {
                return;
            }

            Microphone.End(selectedDevice);
            recordingClip = null;
            lastSamplePosition = 0;
        }

        public void SetDevice(string deviceName)
        {
            if (recordingClip != null)
            {
                Debug.LogWarning("Stop capture before changing the microphone device.");
                return;
            }

            selectedDevice = deviceName;
        }

        public void SetSampleRate(int newSampleRate)
        {
            sampleRate = Mathf.Max(8000, newSampleRate);
            RecalculateSampleBuffer();
        }

        public void SetBufferLength(int milliseconds)
        {
            bufferLengthMs = Mathf.Clamp(milliseconds, 20, 500);
            RecalculateSampleBuffer();
        }

        private void ReadChunk(int chunkSize)
        {
            if (recordingClip == null)
            {
                return;
            }

            var clipSamples = recordingClip.samples;
            var samplesToEnd = clipSamples - lastSamplePosition;

            if (samplesToEnd >= chunkSize)
            {
                recordingClip.GetData(sampleBuffer, lastSamplePosition);
                return;
            }

            EnsureBuffer(ref wrapHeadBuffer, samplesToEnd);
            EnsureBuffer(ref wrapTailBuffer, chunkSize - samplesToEnd);

            if (samplesToEnd > 0)
            {
                recordingClip.GetData(wrapHeadBuffer, lastSamplePosition);
                Array.Copy(wrapHeadBuffer, 0, sampleBuffer, 0, samplesToEnd);
            }

            if (chunkSize - samplesToEnd > 0)
            {
                recordingClip.GetData(wrapTailBuffer, 0);
                Array.Copy(wrapTailBuffer, 0, sampleBuffer, samplesToEnd, chunkSize - samplesToEnd);
            }
        }

        private void RecalculateSampleBuffer()
        {
            var samplesPerChunk = Mathf.Max(1, (sampleRate * bufferLengthMs) / 1000);
            if (sampleBuffer == null || sampleBuffer.Length != samplesPerChunk)
            {
                sampleBuffer = new float[samplesPerChunk];
            }

            wrapHeadBuffer = null;
            wrapTailBuffer = null;
        }

        private static void EnsureBuffer(ref float[] buffer, int length)
        {
            if (length <= 0)
            {
                buffer = null;
                return;
            }

            if (buffer == null || buffer.Length != length)
            {
                buffer = new float[length];
            }
        }
    }
}
