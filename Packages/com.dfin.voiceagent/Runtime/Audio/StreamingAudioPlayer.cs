using System.Collections.Generic;
using UnityEngine;

namespace DFIN.VoiceAgent.Audio
{
    /// <summary>
    /// Placeholder for a streaming audio player that will eventually feed model PCM output into an AudioSource.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class StreamingAudioPlayer : MonoBehaviour
    {
        [SerializeField]
        private AudioSource audioSource;

        private readonly Queue<AudioClip> pendingClips = new();

        private void Awake()
        {
            audioSource ??= GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
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

        /// <summary>
        /// Enqueue a clip to play. Used as a temporary stand-in for the future PCM streaming pipeline.
        /// </summary>
        public void EnqueueClip(AudioClip clip)
        {
            if (clip == null)
            {
                return;
            }

            pendingClips.Enqueue(clip);
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
