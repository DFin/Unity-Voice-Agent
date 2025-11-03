using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace DFIN.VoiceAgent.OpenAI
{
    public class RealtimeEventRegistry
    {
        private readonly Dictionary<string, RealtimeEventDefinition> definitions = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyCollection<RealtimeEventDefinition> Definitions => definitions.Values;

        public void DiscoverSceneEvents()
        {
            definitions.Clear();

            var behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var behaviour in behaviours)
            {
                if (behaviour == null)
                {
                    continue;
                }

                var type = behaviour.GetType();
                var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                foreach (var method in methods)
                {
                    var eventAttribute = method.GetCustomAttribute<RealtimeEventAttribute>();
                    if (eventAttribute == null)
                    {
                        continue;
                    }

                    if (!TryCreateDefinition(behaviour, method, eventAttribute, out var definition))
                    {
                        continue;
                    }

                    if (definitions.ContainsKey(definition.Name))
                    {
                        Debug.LogWarning($"[OpenAI Realtime] Duplicate event name '{definition.Name}' detected. Ignoring method {method.DeclaringType?.Name}.{method.Name}.", behaviour);
                        continue;
                    }

                    definitions.Add(definition.Name, definition);
                }
            }
        }

        public bool TryGetEvent(string name, out RealtimeEventDefinition definition)
        {
            return definitions.TryGetValue(name, out definition);
        }

        private static bool TryCreateDefinition(MonoBehaviour target, MethodInfo method, RealtimeEventAttribute attribute, out RealtimeEventDefinition definition)
        {
            definition = null;

            if (method.IsGenericMethod)
            {
                Debug.LogWarning($"[OpenAI Realtime] Event methods cannot be generic: {method.DeclaringType?.Name}.{method.Name}", target);
                return false;
            }

            if (method.GetParameters().Length > 0)
            {
                Debug.LogWarning($"[OpenAI Realtime] Event methods cannot declare parameters: {method.DeclaringType?.Name}.{method.Name}", target);
                return false;
            }

            var name = string.IsNullOrWhiteSpace(attribute.Name) ? method.Name : attribute.Name.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                Debug.LogWarning($"[OpenAI Realtime] Event method {method.DeclaringType?.Name}.{method.Name} must specify a non-empty name.", target);
                return false;
            }

            var message = string.IsNullOrWhiteSpace(attribute.Message) ? null : attribute.Message.Trim();
            if (string.IsNullOrEmpty(message))
            {
                Debug.LogWarning($"[OpenAI Realtime] Event '{name}' is missing a message payload. Specify one in the attribute.", target);
                return false;
            }

            definition = new RealtimeEventDefinition(name, attribute.Description ?? string.Empty, message, attribute.InterruptResponse, attribute.RequestResponse, target, method);
            return true;
        }
    }

    public sealed class RealtimeEventDefinition
    {
        public string Name { get; }
        public string Description { get; }
        public string DefaultMessage { get; }
        public bool InterruptResponse { get; }
        public bool RequestResponse { get; }
        public MonoBehaviour Target { get; }
        public MethodInfo Method { get; }

        public RealtimeEventDefinition(string name, string description, string message, bool interruptResponse, bool requestResponse, MonoBehaviour target, MethodInfo method)
        {
            Name = name;
            Description = description;
            DefaultMessage = message;
            InterruptResponse = interruptResponse;
            RequestResponse = requestResponse;
            Target = target;
            Method = method;
        }

        public bool TryInvoke(out string message, out string error)
        {
            message = DefaultMessage;
            error = null;

            try
            {
                var result = Method.Invoke(Target, Array.Empty<object>());
                if (result is string str && !string.IsNullOrWhiteSpace(str))
                {
                    message = str.Trim();
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.InnerException?.Message ?? ex.Message;
                return false;
            }
        }
    }
}
