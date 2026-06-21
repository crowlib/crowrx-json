using System;
using System.Globalization;

namespace CrowRx.Json
{
    internal static class BuiltInJsonConverters
    {
        internal static bool TryGet(Type type, out JsonConverterAdapter? converter)
        {
            if (type == typeof(Guid))
            {
                converter = GuidConverter.Adapter;
                return true;
            }

            if (type == typeof(DateTime))
            {
                converter = DateTimeConverter.Adapter;
                return true;
            }

            if (type == typeof(DateTimeOffset))
            {
                converter = DateTimeOffsetConverter.Adapter;
                return true;
            }

            converter = null;
            return false;
        }

        private sealed class GuidConverter : IJsonConverter<Guid>
        {
            internal static JsonConverterAdapter Adapter { get; } = new JsonConverterAdapter<Guid>(new GuidConverter());

            public string Write(Guid value) => JsonValueFormatter.Quote(value.ToString("D"));

            public Guid Read(string json)
            {
                string text = ReadString(json, typeof(Guid));
                if (Guid.TryParseExact(text, "D", out Guid value))
                {
                    return value;
                }

                throw new FormatException("Expected a GUID string in D format.");
            }
        }

        private sealed class DateTimeConverter : IJsonConverter<DateTime>
        {
            internal static JsonConverterAdapter Adapter { get; } = new JsonConverterAdapter<DateTime>(new DateTimeConverter());

            public string Write(DateTime value) => JsonValueFormatter.Quote(value.ToString("O", CultureInfo.InvariantCulture));

            public DateTime Read(string json)
            {
                string text = ReadString(json, typeof(DateTime));
                if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime value))
                {
                    return value;
                }

                throw new FormatException("Expected an invariant ISO 8601 DateTime string.");
            }
        }

        private sealed class DateTimeOffsetConverter : IJsonConverter<DateTimeOffset>
        {
            internal static JsonConverterAdapter Adapter { get; } = new JsonConverterAdapter<DateTimeOffset>(new DateTimeOffsetConverter());

            public string Write(DateTimeOffset value) => JsonValueFormatter.Quote(value.ToString("O", CultureInfo.InvariantCulture));

            public DateTimeOffset Read(string json)
            {
                string text = ReadString(json, typeof(DateTimeOffset));
                if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset value))
                {
                    return value;
                }

                throw new FormatException("Expected an invariant ISO 8601 DateTimeOffset string.");
            }
        }

        private static string ReadString(string json, Type type)
        {
            if (JsonReader.TryRead(json, out object? value, out _) && value is string text)
            {
                return text;
            }

            throw new FormatException($"Expected a JSON string for '{type}'.");
        }
    }
}
