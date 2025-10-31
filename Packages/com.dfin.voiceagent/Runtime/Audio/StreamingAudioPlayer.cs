using System;
using System.Collections.Generic;
using UnityEngine;

namespace DFIN.VoiceAgent.Audio
{
    /// <summary>
    /// Takes PCM audio and plays it through an AudioSource in sequence.
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

        private readonly Queue<AudioClip> pendingClips = new();

        private void Awake()
        {
            audioSource ??= GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
            audioSource.playOnAwake = false;
            audioSource.spatialize = spatialize;
            audioSource.spatialBlend = spatialize ? 1f : 0f;
            audioSource.volume = playbackVolume;
        }

        private void Update()
        {
            if (!audioSource.isPlaying && pendingClips.Count > 0)
            {
                var nextClip = pendingClips.Dequeue();
                audioSource.clip = nextClip;
                audioSource.Play();
            }
        }

        public void EnqueueClip(AudioClip clip)
        {
            if (clip == null)
            {
                return;
            }

            pendingClips.Enqueue(clip);
        }

        public void EnqueuePcm16Samples(short[] samples, int sampleRate)
        {
            if (samples == null || samples.Length == 0)
            {
                return;
            }

            var clip = AudioClip.Create("OpenAI Response", samples.Length, 1, sampleRate, false);
            var floatBuffer = new float[samples.Length];
            for (var i = 0; i < samples.Length; i++)
            {
                floatBuffer[i] = samples[i] / (float)short.MaxValue;
            }

            clip.SetData(floatBuffer, 0);
            EnqueueClip(clip);
        }

        /// <summary>
        /// Stops playback and clears any queued clips.
        /// </summary>
        public void StopAll()
        {
            audioSource.Stop();
            audioSource.clip = null;
            pendingClips.Clear();
        }
    }
}
