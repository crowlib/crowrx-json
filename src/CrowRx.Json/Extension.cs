using System;


namespace CrowRx.Json
{
    public static class StringExtension
    {
        public static bool TryConvertFromJson(this string json, Type objectType, out object result, ICustomActivator? customActivator)
        {
            if (customActivator is null)
            {
                bool success = JsonSerializer.TryDeserialize(json, objectType, out object? value, out _);
                result = value!;
                return success;
            }
            JsonSerializerOptions options = JsonSerializerOptions.Default.WithLegacyActivator(customActivator);
            bool converted = JsonSerializer.TryDeserialize(json, objectType, out object? typedValue, out _, options);
            result = typedValue!;
            return converted;
        }

        public static bool TryConvertFromJsonOverwrite(this string json, Type objectType, ref object result, bool isAppend, ICustomActivator? customActivator)
        {
            if (string.IsNullOrEmpty(json) || result is null)
            {
                return false;
            }

            if (customActivator is null)
            {
                JsonOverwriteMode mode = isAppend ? JsonOverwriteMode.Append : JsonOverwriteMode.Replace;
                return JsonSerializer.TryOverwrite(json, objectType, result, out _, mode);
            }

            JsonOverwriteMode typedMode = isAppend ? JsonOverwriteMode.Append : JsonOverwriteMode.Replace;
            JsonSerializerOptions options = JsonSerializerOptions.Default.WithLegacyActivator(customActivator);
            return JsonSerializer.TryOverwrite(json, objectType, result, out _, typedMode, options);
        }

        public static bool TryConvertFromJsonOverwrite(this string json, Type objectType, ref object result, ICustomActivator? customActivator) =>
            json.TryConvertFromJsonOverwrite(objectType, ref result, false, customActivator);

        public static bool TryConvertFromJsonOverwrite(this string json, ref object result, bool isAppend, ICustomActivator? customActivator) =>
            json.TryConvertFromJsonOverwrite(result.GetType(), ref result, isAppend, customActivator);

        public static bool TryConvertFromJsonOverwrite(this string json, ref object result, ICustomActivator? customActivator) =>
            json.TryConvertFromJsonOverwrite(result.GetType(), ref result, false, customActivator);

        public static object ConvertFromJson(this string json, Type objectType, ICustomActivator? customActivator) =>
            json.TryConvertFromJson(objectType, out object result, customActivator) ? result : null!;

        public static bool TryConvertFromJson<T>(this string json, out T result, ICustomActivator? customActivator)
        {
            if (string.IsNullOrEmpty(json) == false &&
                json.TryConvertFromJson(typeof(T), out object obj, customActivator))
            {
                result = (T)obj;

                return true;
            }

            result = default!;

            return false;
        }

        public static bool TryConvertFromJsonOverwrite<T>(this string json, ref T result, bool isAppend, ICustomActivator? customActivator)
        {
            object obj = result!;

            if (string.IsNullOrEmpty(json) == false &&
                json.TryConvertFromJsonOverwrite(typeof(T), ref obj, isAppend, customActivator))
            {
                result = (T)obj;

                return true;
            }

            return false;
        }

        public static bool TryConvertFromJsonOverwrite<T>(this string json, ref T result, ICustomActivator? customActivator) =>
            json.TryConvertFromJsonOverwrite<T>(ref result, false, customActivator);

        public static T ConvertFromJson<T>(this string json, ICustomActivator? customActivator) => (T)json.ConvertFromJson(typeof(T), customActivator);

        public static bool TryConvertFromJson(this string json, Type objectType, out object result) => json.TryConvertFromJson(objectType, out result, null);
        public static bool TryConvertFromJsonOverwrite(this string json, Type objectType, ref object result) => json.TryConvertFromJsonOverwrite(objectType, ref result, null);
        public static bool TryConvertFromJsonOverwrite(this string json, ref object result) => json.TryConvertFromJsonOverwrite(ref result, null);
        public static object ConvertFromJson(this string json, Type objectType) => json.ConvertFromJson(objectType, null);

        public static bool TryConvertFromJson<T>(this string json, out T result) => json.TryConvertFromJson(out result, null);
        public static bool TryConvertFromJsonOverwrite<T>(this string json, ref T result, bool isAppend) => json.TryConvertFromJsonOverwrite(ref result, isAppend, null);
        public static bool TryConvertFromJsonOverwrite<T>(this string json, ref T result) => json.TryConvertFromJsonOverwrite(ref result, false);
        public static T ConvertFromJson<T>(this string json) => json.ConvertFromJson<T>(null);

    }

    public static class ObjectExtension
    {
        public static string ToJson(this object obj) =>
            JsonWriter.Serialize(obj, obj?.GetType() ?? typeof(object), JsonSerializerOptions.Default);
    }
}
