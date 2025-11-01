using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DFIN.VoiceAgent.OpenAI
{
    public class RealtimeToolRegistry
    {
        private readonly Dictionary<string, RealtimeToolDefinition> definitions = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyCollection<RealtimeToolDefinition> Definitions => definitions.Values;

        public void DiscoverSceneTools()
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
                    var toolAttribute = method.GetCustomAttribute<RealtimeToolAttribute>();
                    if (toolAttribute == null)
                    {
                        continue;
                    }

                    if (!TryCreateDefinition(behaviour, method, toolAttribute, out var definition))
                    {
                        continue;
                    }

                    if (definitions.ContainsKey(definition.Name))
                    {
                        Debug.LogWarning($"[OpenAI Realtime] Duplicate tool name '{definition.Name}' detected. Ignoring method {method.DeclaringType?.Name}.{method.Name}.", behaviour);
                        continue;
                    }

                    definitions.Add(definition.Name, definition);
                }
            }
        }

        public bool TryGetTool(string name, out RealtimeToolDefinition definition)
        {
            return definitions.TryGetValue(name, out definition);
        }

        public JArray BuildToolSchema()
        {
            if (definitions.Count == 0)
            {
                return null;
            }

            var tools = new JArray();
            foreach (var definition in definitions.Values)
            {
                tools.Add(definition.ToJson());
            }

            return tools;
        }

        private static bool TryCreateDefinition(MonoBehaviour target, MethodInfo method, RealtimeToolAttribute attribute, out RealtimeToolDefinition definition)
        {
            definition = null;

            if (method.IsGenericMethod)
            {
                Debug.LogWarning($"[OpenAI Realtime] Tool methods cannot be generic: {method.DeclaringType?.Name}.{method.Name}", target);
                return false;
            }

            var parameters = method.GetParameters();
            var parameterDefinitions = new List<RealtimeToolParameter>(parameters.Length);

            foreach (var parameter in parameters)
            {
                if (!TryCreateParameterDefinition(parameter, out var parameterDefinition))
                {
                    Debug.LogWarning($"[OpenAI Realtime] Unsupported parameter type '{parameter.ParameterType.Name}' on tool method {method.DeclaringType?.Name}.{method.Name}", target);
                    return false;
                }

                parameterDefinitions.Add(parameterDefinition);
            }

            var name = string.IsNullOrWhiteSpace(attribute.Name) ? method.Name : attribute.Name.Trim();
            definition = new RealtimeToolDefinition(name, attribute.Description ?? string.Empty, target, method, parameterDefinitions);
            return true;
        }

        private static bool TryCreateParameterDefinition(ParameterInfo parameter, out RealtimeToolParameter definition)
        {
            var attribute = parameter.GetCustomAttribute<RealtimeToolParamAttribute>();
            var required = attribute?.Required ?? !parameter.IsOptional;

            if (!TryGetJsonType(parameter.ParameterType, out var jsonType))
            {
                definition = null;
                return false;
            }

            definition = new RealtimeToolParameter(
                parameter.Name ?? parameter.Position.ToString(CultureInfo.InvariantCulture),
                attribute?.Description ?? string.Empty,
                jsonType,
                required,
                parameter);

            return true;
        }

        private static bool TryGetJsonType(Type type, out string jsonType)
        {
            if (type == typeof(string))
            {
                jsonType = "string";
                return true;
            }

            if (type == typeof(bool))
            {
                jsonType = "boolean";
                return true;
            }

            if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte) || type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort))
            {
                jsonType = "integer";
                return true;
            }

            if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
            {
                jsonType = "number";
                return true;
            }

            if (type.IsEnum)
            {
                jsonType = "string";
                return true;
            }

            jsonType = null;
            return false;
        }
    }

    public sealed class RealtimeToolDefinition
    {
        public string Name { get; }
        public string Description { get; }
        public MonoBehaviour Target { get; }
        public MethodInfo Method { get; }
        public IReadOnlyList<RealtimeToolParameter> Parameters { get; }

        public RealtimeToolDefinition(string name, string description, MonoBehaviour target, MethodInfo method, IReadOnlyList<RealtimeToolParameter> parameters)
        {
            Name = name;
            Description = description;
            Target = target;
            Method = method;
            Parameters = parameters;
        }

        public JObject ToJson()
        {
            var properties = new JObject();
            var required = new JArray();

            foreach (var parameter in Parameters)
            {
                var schema = new JObject
                {
                    ["type"] = parameter.JsonType
                };

                if (!string.IsNullOrWhiteSpace(parameter.Description))
                {
                    schema["description"] = parameter.Description;
                }

                if (parameter.Parameter.ParameterType.IsEnum)
                {
                    var names = Enum.GetNames(parameter.Parameter.ParameterType);
                    schema["enum"] = new JArray(names);
                }

                properties[parameter.Name] = schema;

                if (parameter.Required)
                {
                    required.Add(parameter.Name);
                }
            }

            var parametersObject = new JObject
            {
                ["type"] = "object",
                ["properties"] = properties
            };

            if (required.Count > 0)
            {
                parametersObject["required"] = required;
            }

            return new JObject
            {
                ["type"] = "function",
                ["name"] = Name,
                ["description"] = Description ?? string.Empty,
                ["parameters"] = parametersObject
            };
        }

        public object Invoke(JObject arguments, out string error)
        {
            error = null;
            object[] invocationArgs;

            try
            {
                invocationArgs = BuildInvocationArguments(arguments);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return null;
            }

            try
            {
                var result = Method.Invoke(Target, invocationArgs);
                if (result is System.Threading.Tasks.Task task)
                {
                    task.GetAwaiter().GetResult();
                    var taskType = task.GetType();
                    if (taskType.IsGenericType && taskType.GetGenericTypeDefinition() == typeof(System.Threading.Tasks.Task<>))
                    {
                        return taskType.GetProperty("Result")?.GetValue(task);
                    }

                    return null;
                }

                return result;
            }
            catch (Exception ex)
            {
                error = ex.InnerException?.Message ?? ex.Message;
                return null;
            }
        }

        private object[] BuildInvocationArguments(JObject arguments)
        {
            var args = new object[Parameters.Count];
            for (var i = 0; i < Parameters.Count; i++)
            {
                var parameter = Parameters[i];
                var parameterInfo = parameter.Parameter;
                JToken token = null;
                var hasValue = arguments != null && arguments.TryGetValue(parameter.Name, StringComparison.OrdinalIgnoreCase, out token);

                if (!hasValue || token == null || token.Type == JTokenType.Null)
                {
                    if (parameter.Required)
                    {
                        throw new InvalidOperationException($"Missing required parameter '{parameter.Name}'.");
                    }

                    args[i] = parameterInfo.HasDefaultValue ? parameterInfo.DefaultValue : GetDefault(parameterInfo.ParameterType);
                    continue;
                }

                args[i] = ConvertToken(token, parameterInfo.ParameterType, parameter.Name);
            }

            return args;
        }

        private static object ConvertToken(JToken token, Type targetType, string parameterName)
        {
            try
            {
                if (targetType == typeof(string))
                {
                    return token.Type == JTokenType.String ? token.Value<string>() : token.ToString(Formatting.None);
                }

                if (targetType == typeof(bool))
                {
                    return token.Value<bool>();
                }

                if (targetType == typeof(int))
                {
                    return token.Value<int>();
                }

                if (targetType == typeof(long))
                {
                    return token.Value<long>();
                }

                if (targetType == typeof(short))
                {
                    return token.Value<short>();
                }

                if (targetType == typeof(byte))
                {
                    return token.Value<byte>();
                }

                if (targetType == typeof(uint))
                {
                    return token.Value<uint>();
                }

                if (targetType == typeof(ulong))
                {
                    return token.Value<ulong>();
                }

                if (targetType == typeof(ushort))
                {
                    return token.Value<ushort>();
                }

                if (targetType == typeof(float))
                {
                    return token.Value<float>();
                }

                if (targetType == typeof(double))
                {
                    return token.Value<double>();
                }

                if (targetType == typeof(decimal))
                {
                    return token.Value<decimal>();
                }

                if (targetType.IsEnum)
                {
                    var value = token.Value<string>();
                    return Enum.Parse(targetType, value, true);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to convert parameter '{parameterName}' to type '{targetType.Name}': {ex.Message}");
            }

            throw new InvalidOperationException($"Unsupported parameter type '{targetType.Name}' for parameter '{parameterName}'.");
        }

        private static object GetDefault(Type type)
        {
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }
    }

    public sealed class RealtimeToolParameter
    {
        public string Name { get; }
        public string Description { get; }
        public string JsonType { get; }
        public bool Required { get; }
        public ParameterInfo Parameter { get; }

        public RealtimeToolParameter(string name, string description, string jsonType, bool required, ParameterInfo parameter)
        {
            Name = name;
            Description = description;
            JsonType = jsonType;
            Required = required;
            Parameter = parameter;
        }
    }
}
