using System;


namespace CrowRx.Json
{
    public static class StringExtension
    {
        public static bool TryConvertFromJson(this string json, Type objectType, out object result, ICustomActivator customActivator)
        {
            if (!string.IsNullOrEmpty(json) &&
                TryConvertFromJsonInternal(objectType, MiniJSON.Json.Deserialize(json), customActivator, out result))
            {
                return true;
            }

            result = null;

            return false;
        }

        public static bool TryConvertFromJsonOverwrite(this string json, Type objectType, ref object result, bool isAppend, ICustomActivator customActivator) =>
            string.IsNullOrEmpty(json) == false &&
            TryConvertFromJsonOverwriteInternal(objectType, MiniJSON.Json.Deserialize(json), customActivator, ref result, isAppend);

        public static bool TryConvertFromJsonOverwrite(this string json, Type objectType, ref object result, ICustomActivator customActivator) =>
            json.TryConvertFromJsonOverwrite(objectType, ref result, false, customActivator);

        public static bool TryConvertFromJsonOverwrite(this string json, ref object result, bool isAppend, ICustomActivator customActivator) =>
            json.TryConvertFromJsonOverwrite(result.GetType(), ref result, isAppend, customActivator);

        public static bool TryConvertFromJsonOverwrite(this string json, ref object result, ICustomActivator customActivator) =>
            json.TryConvertFromJsonOverwrite(result.GetType(), ref result, false, customActivator);

        public static object ConvertFromJson(this string json, Type objectType, ICustomActivator customActivator) =>
            json.TryConvertFromJson(objectType, out object result, customActivator) ? result : null;

        public static bool TryConvertFromJson<T>(this string json, out T result, ICustomActivator customActivator)
        {
            if (string.IsNullOrEmpty(json) == false &&
                json.TryConvertFromJson(typeof(T), out object obj, customActivator))
            {
                result = (T)obj;

                return true;
            }

            result = default;

            return false;
        }

        public static bool TryConvertFromJsonOverwrite<T>(this string json, ref T result, bool isAppend, ICustomActivator customActivator)
        {
            object obj = null;

            if (string.IsNullOrEmpty(json) == false &&
                json.TryConvertFromJsonOverwrite(typeof(T), ref obj, isAppend, customActivator))
            {
                result = (T)obj;

                return true;
            }

            return false;
        }

        public static bool TryConvertFromJsonOverwrite<T>(this string json, ref T result, ICustomActivator customActivator) =>
            json.TryConvertFromJsonOverwrite<T>(ref result, false, customActivator);

        public static T ConvertFromJson<T>(this string json, ICustomActivator customActivator) => (T)json.ConvertFromJson(typeof(T), customActivator);

        public static bool TryConvertFromJson(this string json, Type objectType, out object result) => json.TryConvertFromJson(objectType, out result, null);
        public static bool TryConvertFromJsonOverwrite(this string json, Type objectType, ref object result) => json.TryConvertFromJsonOverwrite(objectType, ref result, null);
        public static bool TryConvertFromJsonOverwrite(this string json, ref object result) => json.TryConvertFromJsonOverwrite(ref result, null);
        public static object ConvertFromJson(this string json, Type objectType) => json.ConvertFromJson(objectType, null);

        public static bool TryConvertFromJson<T>(this string json, out T result) => json.TryConvertFromJson(out result, null);
        public static bool TryConvertFromJsonOverwrite<T>(this string json, ref T result, bool isAppend) => json.TryConvertFromJsonOverwrite(ref result, isAppend, null);
        public static bool TryConvertFromJsonOverwrite<T>(this string json, ref T result) => json.TryConvertFromJsonOverwrite(ref result, false);
        public static T ConvertFromJson<T>(this string json) => json.ConvertFromJson<T>(null);

        private static bool TryConvertFromJsonInternal(Type objectType, object jsonObject, ICustomActivator customActivator, out object result)
        {
            if (jsonObject is not null && Deserializer.TryParse(out result, objectType, jsonObject, customActivator))
                return true;

            result = null;

            return false;
        }

        private static bool TryConvertFromJsonOverwriteInternal(Type objectType, object jsonObject, ICustomActivator customActivator, ref object result, bool isAppend) =>
            jsonObject is not null && Deserializer.TryParseOverwrite(ref result, objectType, jsonObject, customActivator, isAppend);
    }

    public static class ObjectExtension
    {
        public static string ToJson(this object obj) => Serializer.ToJson(obj);
    }
}