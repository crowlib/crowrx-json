using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CrowRx.Json
{
    internal static class JsonValueFormatter
    {
        internal static string Format(object? value)
        {
            var builder = new StringBuilder();
            Append(builder, value);
            return builder.ToString();
        }

        internal static string Quote(string value)
        {
            var builder = new StringBuilder();
            AppendString(builder, value);
            return builder.ToString();
        }

        private static void Append(StringBuilder builder, object? value)
        {
            switch (value)
            {
                case null:
                    builder.Append("null");
                    break;
                case string text:
                    AppendString(builder, text);
                    break;
                case bool boolean:
                    builder.Append(boolean ? "true" : "false");
                    break;
                case JsonNumber number:
                    builder.Append(number.Text);
                    break;
                case List<object?> items:
                    builder.Append('[');
                    for (int index = 0; index < items.Count; index++)
                    {
                        if (index > 0) builder.Append(',');
                        Append(builder, items[index]);
                    }
                    builder.Append(']');
                    break;
                case Dictionary<string, object?> properties:
                    builder.Append('{');
                    bool first = true;
                    foreach (KeyValuePair<string, object?> property in properties)
                    {
                        if (!first) builder.Append(',');
                        first = false;
                        AppendString(builder, property.Key);
                        builder.Append(':');
                        Append(builder, property.Value);
                    }
                    builder.Append('}');
                    break;
                default:
                    throw new InvalidOperationException($"Value of type '{value.GetType()}' is not a parsed JSON value.");
            }
        }

        private static void AppendString(StringBuilder builder, string value)
        {
            builder.Append('"');
            foreach (char character in value)
            {
                switch (character)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (character < ' ')
                        {
                            builder.Append("\\u");
                            builder.Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(character);
                        }
                        break;
                }
            }
            builder.Append('"');
        }
    }
}
