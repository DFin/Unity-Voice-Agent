using UnityEngine;

namespace DFIN.VoiceAgent.OpenAI
{
    /// <summary>
    /// Provides a realtime tool for positioning the agent along the X axis.
    /// </summary>
    public class SphereMovementTool : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Optional explicit transform to move. Defaults to this component's transform.")]
        private Transform target;

        [SerializeField]
        [Tooltip("Minimum allowed world X position (meters).")]
        private float minX = -1f;

        [SerializeField]
        [Tooltip("Maximum allowed world X position (meters).")]
        private float maxX = 1f;

        private Transform Target => target != null ? target : transform;

        [RealtimeTool("Set your world position between -1.0 and 1.0 meters. Use this at your own conveneince", "set_sphere_x_position")]
        public void SetSphereXPosition(
            [RealtimeToolParam("Absolute world X position in meters (range -1.0 to 1.0).")] float x)
        {
            var clamped = Mathf.Clamp(x, minX, maxX);
            var current = Target.position;
            Target.position = new Vector3(clamped, current.y, current.z);
        }
    }
}
