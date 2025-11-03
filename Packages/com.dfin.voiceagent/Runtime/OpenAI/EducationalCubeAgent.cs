using System.Collections.Generic;
using UnityEngine;
#if UNITY_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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

        [SerializeField]
        [Tooltip("Camera used for raycasting pointer interactions. Defaults to Camera.main if unset.")]
        private Camera raycastCamera;

        [SerializeField]
        [Tooltip("Maximum distance (in world units) for pointer raycasts.")]
        private float pointerMaxDistance = 10f;

        [SerializeField]
        [Tooltip("Physics layers considered when raycasting for the colored buttons.")]
        private LayerMask pointerLayerMask = Physics.DefaultRaycastLayers;

        private readonly List<EducationalCubeButton> registeredButtons = new();

        private void Awake()
        {
            controller ??= GetComponent<OpenAiRealtimeController>();
            raycastCamera ??= Camera.main;
        }

        private void Update()
        {
            if (TryGetPointerScreenPosition(out var screenPosition))
            {
                RaycastButton(screenPosition);
            }
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

            if (controller != null && !string.IsNullOrWhiteSpace(button.EventName))
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

        private bool TryGetPointerScreenPosition(out Vector2 position)
        {
#if UNITY_INPUT_SYSTEM
            var pointer = Mouse.current;
            if (pointer != null && pointer.leftButton.wasPressedThisFrame)
            {
                position = pointer.position.ReadValue();
                return true;
            }

            var pen = Pen.current;
            if (pen != null && pen.tip.wasPressedThisFrame)
            {
                position = pen.position.ReadValue();
                return true;
            }

            var touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                foreach (var touch in touchscreen.touches)
                {
                    if (touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began)
                    {
                        position = touch.position.ReadValue();
                        return true;
                    }
                }
            }

            var pointerDevice = Pointer.current;
            if (pointerDevice != null && pointerDevice.press.wasPressedThisFrame)
            {
                position = pointerDevice.position.ReadValue();
                return true;
            }

            position = default;
            return false;
#else
            if (Input.touchCount > 0)
            {
                for (var i = 0; i < Input.touchCount; i++)
                {
                    var touch = Input.GetTouch(i);
                    if (touch.phase == TouchPhase.Began)
                    {
                        position = touch.position;
                        return true;
                    }
                }
            }

            if (Input.GetMouseButtonDown(0))
            {
                position = Input.mousePosition;
                return true;
            }

            position = default;
            return false;
#endif
        }

        private void RaycastButton(Vector2 screenPosition)
        {
            var camera = raycastCamera != null ? raycastCamera : Camera.main;
            if (camera == null)
            {
                return;
            }

            var ray = camera.ScreenPointToRay(screenPosition);
            var maxDistance = pointerMaxDistance <= 0f ? Mathf.Infinity : pointerMaxDistance;

            if (!Physics.Raycast(ray, out var hit, maxDistance, pointerLayerMask, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            var button = hit.collider.GetComponent<EducationalCubeButton>() ?? hit.collider.GetComponentInParent<EducationalCubeButton>();
            button?.HandlePointerDown();
        }
    }
}
