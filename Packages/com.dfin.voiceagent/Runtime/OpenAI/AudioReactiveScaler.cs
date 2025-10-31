using DFIN.VoiceAgent.Audio;
using UnityEngine;

namespace DFIN.VoiceAgent.OpenAI
{
    /// <summary>
    /// Scales a target transform based on realtime audio energy coming from the StreamingAudioPlayer.
    /// </summary>
    [RequireComponent(typeof(StreamingAudioPlayer))]
    public class AudioReactiveScaler : MonoBehaviour
    {
        [SerializeField]
        private Transform target;

        [SerializeField]
        private float minimumScale = 0.9f;

        [SerializeField]
        private float maximumScale = 1.3f;

        [SerializeField]
        private float responseSpeed = 10f;

        [SerializeField]
        [Min(16)]
        private int sampleWindow = 64;

        [SerializeField]
        private float sensitivity = 12f;

        private StreamingAudioPlayer streamingPlayer;
        private AudioSource audioSource;
        private float[] sampleBuffer;
        private Vector3 baseScale;
        private float currentScaleFactor = 1f;

        private void Awake()
        {
            streamingPlayer = GetComponent<StreamingAudioPlayer>();
            audioSource = streamingPlayer != null ? streamingPlayer.GetComponent<AudioSource>() : GetComponent<AudioSource>();

            if (target == null)
            {
                target = transform;
            }

            baseScale = target != null ? target.localScale : Vector3.one;
            EnsureBuffer();
        }

        private void Update()
        {
            if (audioSource == null || target == null)
            {
                return;
            }

            EnsureBuffer();
            audioSource.GetOutputData(sampleBuffer, 0);

            var energy = CalculateRms(sampleBuffer);
            var normalized = Mathf.Clamp01(energy * sensitivity);
            var targetScaleFactor = Mathf.Lerp(minimumScale, maximumScale, normalized);

            currentScaleFactor = Mathf.Lerp(currentScaleFactor, targetScaleFactor, Time.deltaTime * responseSpeed);
            target.localScale = baseScale * currentScaleFactor;
        }

        private void EnsureBuffer()
        {
            if (sampleBuffer == null || sampleBuffer.Length != sampleWindow)
            {
                sampleWindow = Mathf.Max(16, sampleWindow);
                sampleBuffer = new float[sampleWindow];
            }
        }

        private static float CalculateRms(float[] samples)
        {
            if (samples == null || samples.Length == 0)
            {
                return 0f;
            }

            var sum = 0f;
            for (var i = 0; i < samples.Length; i++)
            {
                var value = samples[i];
                sum += value * value;
            }

            return Mathf.Sqrt(sum / samples.Length);
        }
    }
}
