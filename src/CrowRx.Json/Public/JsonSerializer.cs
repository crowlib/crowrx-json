using System;

namespace CrowRx.Json
{
    public static class JsonSerializer
    {
        public static string Serialize<T>(T value, JsonSerializerOptions? options = null) =>
            JsonWriter.Serialize(value, typeof(T), options ?? JsonSerializerOptions.Default);

        public static T? Deserialize<T>(string json, JsonSerializerOptions? options = null)
        {
            if (TryDeserialize(json, out T? value, out JsonError error, options))
            {
                return value;
            }

            throw new JsonException(error);
        }

        public static object? Deserialize(string json, Type targetType, JsonSerializerOptions? options = null)
        {
            if (TryDeserialize(json, targetType, out object? value, out JsonError error, options))
            {
                return value;
            }

            throw new JsonException(error);
        }

        public static bool TryDeserialize<T>(string json, out T? value, out JsonError error, JsonSerializerOptions? options = null)
        {
            if (TryDeserialize(json, typeof(T), out object? result, out error, options))
            {
                value = (T?)result;
                return true;
            }

            value = default;
            return false;
        }

        public static bool TryDeserialize(
            string json,
            Type targetType,
            out object? value,
            out JsonError error,
            JsonSerializerOptions? options = null)
        {
            if (json is null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            return JsonDeserializer.TryDeserialize(json, targetType, options ?? JsonSerializerOptions.Default, out value, out error);
        }

        public static bool TryOverwrite<T>(
            string json,
            T target,
            out JsonError error,
            JsonOverwriteMode mode = JsonOverwriteMode.Replace,
            JsonSerializerOptions? options = null)
        {
            if (target is null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            return TryOverwrite(json, typeof(T), target, out error, mode, options);
        }

        public static bool TryOverwrite(
            string json,
            Type targetType,
            object target,
            out JsonError error,
            JsonOverwriteMode mode = JsonOverwriteMode.Replace,
            JsonSerializerOptions? options = null)
        {
            if (json is null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            if (targetType is null)
            {
                throw new ArgumentNullException(nameof(targetType));
            }

            if (target is null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (!Enum.IsDefined(typeof(JsonOverwriteMode), mode))
            {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }

            return JsonOverwriter.TryOverwrite(json, targetType, target, mode, options ?? JsonSerializerOptions.Default, out error);
        }
    }
}
