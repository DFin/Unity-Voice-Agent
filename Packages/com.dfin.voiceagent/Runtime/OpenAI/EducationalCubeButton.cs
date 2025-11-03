using System;
using UnityEngine;

namespace DFIN.VoiceAgent.OpenAI
{
    /// <summary>
    /// Simple helper attached to each colored cube. It handles clicks, toggles emissive lighting, and
    /// delegates the conversation update to <see cref="EducationalCubeAgent"/>.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class EducationalCubeButton : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Identifier used when publishing the event to the realtime controller.")]
        private string eventName;

        [SerializeField]
        [Tooltip("Renderer whose material will be toggled between base and emissive states.")]
        private Renderer targetRenderer;

        [SerializeField]
        [Tooltip("Base color applied to the cube's material.")]
        private Color baseColor = Color.white;

        [SerializeField]
        [Tooltip("Emission color shown while the cube is active.")]
        private Color emissionColor = Color.white;

        [SerializeField]
        [Tooltip("Emission intensity multiplier used for the highlight state.")]
        private float emissionIntensity = 2.2f;

        private EducationalCubeAgent owner;
        private Material materialInstance;

        public string EventName => eventName;

        private void Awake()
        {
            owner = GetComponentInParent<EducationalCubeAgent>();
            targetRenderer ??= GetComponent<Renderer>();

            if (string.IsNullOrWhiteSpace(eventName))
            {
                Debug.LogWarning($"[EducationalCubeButton] Missing event name on {name}.", this);
            }

            if (targetRenderer != null)
            {
                materialInstance = targetRenderer.material;
                ApplyBaseAppearance();
            }

            owner?.RegisterButton(this);
        }

        private void OnDestroy()
        {
            owner?.UnregisterButton(this);
        }

        private void OnMouseDown()
        {
            // Desktop-friendly input: having a collider lets Unity forward left-clicks automatically.
            owner?.HandleButtonClicked(this);
        }

        public void SetHighlighted(bool highlighted)
        {
            if (materialInstance == null)
            {
                return;
            }

            if (highlighted)
            {
                ApplyEmission();
            }
            else
            {
                ApplyBaseAppearance();
            }
        }

        private void ApplyBaseAppearance()
        {
            if (materialInstance == null)
            {
                return;
            }

            var targetBase = baseColor;
            if (materialInstance.HasProperty("_BaseColor"))
            {
                materialInstance.SetColor("_BaseColor", targetBase);
            }
            else
            {
                materialInstance.color = targetBase;
            }

            if (materialInstance.HasProperty("_Color"))
            {
                materialInstance.SetColor("_Color", targetBase);
            }

            if (materialInstance.HasProperty("_EmissionColor"))
            {
                materialInstance.EnableKeyword("_EMISSION");
                materialInstance.SetColor("_EmissionColor", Color.black);
            }
        }

        private void ApplyEmission()
        {
            if (materialInstance == null)
            {
                return;
            }

            var targetBase = baseColor;
            if (materialInstance.HasProperty("_BaseColor"))
            {
                materialInstance.SetColor("_BaseColor", targetBase);
            }

            if (materialInstance.HasProperty("_Color"))
            {
                materialInstance.SetColor("_Color", targetBase);
            }

            if (materialInstance.HasProperty("_EmissionColor"))
            {
                materialInstance.EnableKeyword("_EMISSION");
                var emissive = emissionColor * Mathf.LinearToGammaSpace(emissionIntensity);
                materialInstance.SetColor("_EmissionColor", emissive);
            }
        }
    }
}
