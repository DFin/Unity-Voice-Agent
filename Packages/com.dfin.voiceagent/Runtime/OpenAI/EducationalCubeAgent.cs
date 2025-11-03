using System;
using System.Collections.Generic;
using UnityEngine;

namespace DFIN.VoiceAgent.OpenAI
{
    /// <summary>
    /// Coordinates the educational cube prefab. Tracks colored buttons, forwards click events to the
    /// realtime controller, and exposes a reset tool for the agent.
    /// </summary>
    [RequireComponent(typeof(OpenAiRealtimeController))]
    public class EducationalCubeAgent : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Explicit realtime controller reference. Auto-populated at runtime if left empty.")]
        private OpenAiRealtimeController controller;

        private readonly List<EducationalCubeButton> registeredButtons = new();

        private void Awake()
        {
            controller ??= GetComponent<OpenAiRealtimeController>();
        }

        internal void RegisterButton(EducationalCubeButton button)
        {
            if (button != null && !registeredButtons.Contains(button))
            {
                registeredButtons.Add(button);
            }
        }

        internal void UnregisterButton(EducationalCubeButton button)
        {
            if (button != null)
            {
                registeredButtons.Remove(button);
            }
        }

        internal void HandleButtonClicked(EducationalCubeButton button)
        {
            if (button == null)
            {
                return;
            }

            button.SetHighlighted(true);

            if (controller != null)
            {
                controller.PublishEvent(button.EventName);
            }
        }

        [RealtimeTool("Returns all colored cubes to their default, non-emissive state.", name: "reset_cube_buttons")]
        public void ResetCubeButtons()
        {
            foreach (var button in registeredButtons)
            {
                if (button != null)
                {
                    button.SetHighlighted(false);
                }
            }
        }

        [RealtimeEvent("Event: user pressed red cube", name: "red_cube_pressed")]
        private void RedCubePressedEvent()
        {
            // Method intentionally left blank. The attribute carries the message sent to the model.
        }

        [RealtimeEvent("Event: user pressed green cube", name: "green_cube_pressed")]
        private void GreenCubePressedEvent()
        {
        }

        [RealtimeEvent("Event: user pressed blue cube", name: "blue_cube_pressed")]
        private void BlueCubePressedEvent()
        {
        }
    }
}
