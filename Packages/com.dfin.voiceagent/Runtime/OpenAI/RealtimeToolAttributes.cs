using System;

namespace DFIN.VoiceAgent.OpenAI
{
    /// <summary>
    /// Marks a method as callable via the OpenAI realtime function-calling interface.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class RealtimeToolAttribute : Attribute
    {
        public string Name { get; }
        public string Description { get; }

        public RealtimeToolAttribute(string description, string name = null)
        {
            Description = description;
            Name = name;
        }
    }

    /// <summary>
    /// Provides metadata about a parameter exposed to the model.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
    public sealed class RealtimeToolParamAttribute : Attribute
    {
        public string Description { get; }
        public bool Required { get; }

        public RealtimeToolParamAttribute(string description, bool required = true)
        {
            Description = description;
            Required = required;
        }
    }
}
