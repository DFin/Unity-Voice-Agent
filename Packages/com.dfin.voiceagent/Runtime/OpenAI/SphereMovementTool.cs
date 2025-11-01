using UnityEngine;

namespace DFIN.VoiceAgent.OpenAI
{
    /// <summary>
    /// Provides a realtime tool for positioning the agent along the X axis.
    /// The function just lerps 
    /// 
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

        [SerializeField]
        [Tooltip("Movement speed in meters per second.")]
        private float moveSpeed = 2f;

        private Transform Target => target != null ? target : transform;
        private float targetX;
        private bool hasTarget;

        private void Awake()
        {
            var current = Target.position;
            targetX = Mathf.Clamp(current.x, minX, maxX);
            hasTarget = true;
        }

        private void Update()
        {
            if (!hasTarget)
            {
                return;
            }

            var current = Target.position;
            var nextX = Mathf.MoveTowards(current.x, targetX, Mathf.Max(0f, moveSpeed) * Time.deltaTime);
            if (!Mathf.Approximately(current.x, nextX))
            {
                Target.position = new Vector3(nextX, current.y, current.z);
            }
        }

        [RealtimeTool("Move (roll) your world X position to a position between -1.0 and 1.0 meters.", "set_sphere_x_position")]
        public void SetSphereXPosition(
            [RealtimeToolParam("Absolute world X position in meters (range -1.0 to 1.0).", required: true)] float x)
        {
            var clamped = Mathf.Clamp(x, minX, maxX);
            targetX = clamped;
            hasTarget = true;
        }
    }
}
