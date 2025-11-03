using System;

namespace DFIN.VoiceAgent.OpenAI
{
    /// <summary>
    /// Marks a method as an event publisher that will send a narrative message to the realtime agent.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class RealtimeEventAttribute : Attribute
    {
        public string Name { get; }
        public string Description { get; }
        public string Message { get; }
        public bool InterruptResponse { get; }
        public bool RequestResponse { get; }

        public RealtimeEventAttribute(
            string message,
            string name = null,
            string description = null,
            bool interruptResponse = true,
            bool requestResponse = true)
        {
            Message = message;
            Name = name;
            Description = description;
            InterruptResponse = interruptResponse;
            RequestResponse = requestResponse;
        }
    }
}
