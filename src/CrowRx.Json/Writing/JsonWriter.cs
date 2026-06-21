using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace CrowRx.Json
{
    internal sealed class JsonWriter
    {
        internal const int DefaultMaxDepth = 64;

        private readonly StringBuilder _builder;
        private readonly TypeContractCache _contractCache;
        private readonly JsonSerializerOptions _options;
        private readonly HashSet<object> _recursionPath;
        private readonly int _maxDepth;
        private bool _completed;

        internal JsonWriter(
            TypeContractCache contractCache,
            int maxDepth = DefaultMaxDepth,
            JsonSerializerOptions? options = null)
        {
            _contractCache = contractCache ?? throw new ArgumentNullException(nameof(contractCache));
            _options = options ?? JsonSerializerOptions.Default;
            if (maxDepth < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDepth));
            }

            _maxDepth = maxDepth;
            _builder = WriterStatePool.RentBuilder();
            _recursionPath = WriterStatePool.RentRecursionPath();
        }

        internal static string Serialize(object? value, Type declaredType, JsonSerializerOptions options) =>
            new JsonWriter(TypeContractCache.Shared, options: options).Write(value, declaredType);

        internal string Write(object? value, Type? declaredType = null)
        {
            if (_completed)
            {
                throw new InvalidOperationException("A JsonWriter instance can write only one JSON value.");
            }

            try
            {
                WriteValue(value, declaredType ?? value?.GetType() ?? typeof(object), 0);
                return _builder.ToString();
            }
            finally
            {
                _completed = true;
                WriterStatePool.Return(_builder, _recursionPath);
            }
        }

        private void WriteValue(object? value, Type declaredType, int depth)
        {
            if (value is null)
            {
                _builder.Append("null");
                return;
            }

            Type runtimeType = value.GetType();
            Type converterType = _options.TryGetConverter(declaredType, out JsonConverterAdapter? converter)
                ? declaredType
                : runtimeType;
            if ((converter is not null || _options.TryGetConverter(converterType, out converter!)) && converter is not null)
            {
                string json;
                try
                {
                    json = converter.Write(value);
                }
                catch (Exception exception)
                {
                    throw new InvalidOperationException($"Converter for '{converterType}' failed while writing JSON.", exception);
                }
                if (json is null)
                {
                    throw new InvalidOperationException($"Converter for '{converterType}' returned null instead of a JSON value.");
                }

                if (!JsonReader.TryRead(json, out _, out JsonError converterError))
                {
                    throw new InvalidOperationException($"Converter for '{converterType}' wrote invalid JSON: {converterError}");
                }

                _builder.Append(json);
                return;
            }

            Type type = runtimeType;
            TypeContract contract = _options.GetContract(_contractCache, type);
            if (contract.Kind is TypeContractKind.Scalar or TypeContractKind.Enum or TypeContractKind.Nullable)
            {
                WriteScalar(value, contract);
                return;
            }

            if (depth >= _maxDepth)
            {
                throw new InvalidOperationException($"The maximum serialization depth of {_maxDepth} was exceeded.");
            }

            bool trackReference = !type.IsValueType;
            if (trackReference && !_recursionPath.Add(value))
            {
                throw new InvalidOperationException($"A cyclic reference was detected while serializing type '{type}'.");
            }

            try
            {
                switch (contract.Kind)
                {
                    case TypeContractKind.Collection:
                        WriteCollection((IEnumerable)value, contract.ElementType!, depth);
                        break;
                    case TypeContractKind.Dictionary:
                        WriteDictionary((IEnumerable)value, contract.ValueType!, depth);
                        break;
                    case TypeContractKind.Dto:
                        WriteDto(value, contract, depth);
                        break;
                    default:
                        throw new InvalidOperationException($"Type '{type}' cannot be serialized by the JSON writer.");
                }
            }
            finally
            {
                if (trackReference)
                {
                    _recursionPath.Remove(value);
                }
            }
        }

        private void WriteScalar(object value, TypeContract contract)
        {
            Type type = contract.Kind == TypeContractKind.Nullable
                ? contract.UnderlyingType!
                : contract.Type;

            if (type.IsEnum)
            {
                string? name = Enum.GetName(type, value);
                if (name is null)
                {
                    throw new InvalidOperationException($"Enum value '{value}' is not a named value of '{type}'.");
                }

                WriteString(name);
                return;
            }

            if (value is string text)
            {
                WriteString(text);
            }
            else if (value is char character)
            {
                WriteString(character.ToString());
            }
            else if (value is bool boolean)
            {
                _builder.Append(boolean ? "true" : "false");
            }
            else if (value is float single)
            {
                if (float.IsNaN(single) || float.IsInfinity(single))
                {
                    throw new InvalidOperationException("NaN and infinity are not valid JSON numbers.");
                }

                _builder.Append(single.ToString("R", CultureInfo.InvariantCulture));
            }
            else if (value is double number)
            {
                if (double.IsNaN(number) || double.IsInfinity(number))
                {
                    throw new InvalidOperationException("NaN and infinity are not valid JSON numbers.");
                }

                _builder.Append(number.ToString("R", CultureInfo.InvariantCulture));
            }
            else if (value is decimal decimalNumber)
            {
                _builder.Append(decimalNumber.ToString(CultureInfo.InvariantCulture));
            }
            else if (value is Guid guid)
            {
                WriteString(guid.ToString("D"));
            }
            else if (value is DateTime dateTime)
            {
                WriteString(dateTime.ToString("O", CultureInfo.InvariantCulture));
            }
            else if (value is DateTimeOffset dateTimeOffset)
            {
                WriteString(dateTimeOffset.ToString("O", CultureInfo.InvariantCulture));
            }
            else if (value is IFormattable formattable)
            {
                _builder.Append(formattable.ToString(null, CultureInfo.InvariantCulture));
            }
            else
            {
                throw new InvalidOperationException($"Scalar type '{type}' is not supported by the JSON writer.");
            }
        }

        private void WriteCollection(IEnumerable collection, Type elementType, int depth)
        {
            _builder.Append('[');
            bool first = true;
            foreach (object? item in collection)
            {
                WriteSeparator(ref first);
                WriteValue(item, elementType, depth + 1);
            }

            _builder.Append(']');
        }

        private void WriteDictionary(IEnumerable dictionary, Type valueType, int depth)
        {
            var entries = new List<KeyValuePair<string, object?>>();
            foreach (object? entry in dictionary)
            {
                if (entry is null)
                {
                    throw new InvalidOperationException("A dictionary entry cannot be null.");
                }

                Type entryType = entry.GetType();
                PropertyInfo? keyProperty = entryType.GetProperty("Key");
                PropertyInfo? valueProperty = entryType.GetProperty("Value");
                object? key = keyProperty?.GetValue(entry);
                if (key is not string stringKey || valueProperty is null)
                {
                    throw new InvalidOperationException("Dictionary keys must be strings.");
                }

                entries.Add(new KeyValuePair<string, object?>(stringKey, valueProperty.GetValue(entry)));
            }

            entries.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.Key, right.Key));
            _builder.Append('{');
            bool first = true;
            foreach (KeyValuePair<string, object?> entry in entries)
            {
                WriteSeparator(ref first);
                WriteString(entry.Key);
                _builder.Append(':');
                WriteValue(entry.Value, valueType, depth + 1);
            }

            _builder.Append('}');
        }

        private void WriteDto(object value, TypeContract contract, int depth)
        {
            _builder.Append('{');
            bool first = true;
            foreach (MemberContract member in contract.Members)
            {
                if (!member.CanSerialize)
                {
                    continue;
                }

                object? memberValue = member.MemberInfo switch
                {
                    FieldInfo field => field.GetValue(value),
                    PropertyInfo property => property.GetValue(value),
                    _ => throw new InvalidOperationException($"Member '{member.MemberInfo}' cannot be serialized."),
                };

                WriteSeparator(ref first);
                WriteString(member.JsonName);
                _builder.Append(':');
                WriteValue(memberValue, member.MemberType, depth + 1);
            }

            _builder.Append('}');
        }

        private void WriteString(string value)
        {
            _builder.Append('"');
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                switch (character)
                {
                    case '"': _builder.Append("\\\""); break;
                    case '\\': _builder.Append("\\\\"); break;
                    case '\b': _builder.Append("\\b"); break;
                    case '\f': _builder.Append("\\f"); break;
                    case '\n': _builder.Append("\\n"); break;
                    case '\r': _builder.Append("\\r"); break;
                    case '\t': _builder.Append("\\t"); break;
                    default:
                        if (character < ' ')
                        {
                            _builder.Append("\\u");
                            _builder.Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else if (char.IsHighSurrogate(character))
                        {
                            if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1]))
                            {
                                throw new InvalidOperationException("Strings cannot contain an unpaired surrogate.");
                            }

                            _builder.Append(character);
                            _builder.Append(value[++index]);
                        }
                        else if (char.IsLowSurrogate(character))
                        {
                            throw new InvalidOperationException("Strings cannot contain an unpaired surrogate.");
                        }
                        else
                        {
                            _builder.Append(character);
                        }

                        break;
                }
            }

            _builder.Append('"');
        }

        private void WriteSeparator(ref bool first)
        {
            if (!first)
            {
                _builder.Append(',');
            }

            first = false;
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            internal static ReferenceComparer Instance { get; } = new ReferenceComparer();

            public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

            public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }

        private static class WriterStatePool
        {
            private const int MaximumRetainedBuilderCapacity = 32 * 1024;
            private const int MaximumRetainedStates = 32;
            private static readonly ConcurrentBag<StringBuilder> Builders = new();
            private static readonly ConcurrentBag<HashSet<object>> RecursionPaths = new();
            private static int _builderCount;
            private static int _recursionPathCount;

            internal static StringBuilder RentBuilder()
            {
                if (Builders.TryTake(out StringBuilder? builder))
                {
                    Interlocked.Decrement(ref _builderCount);
                    return builder;
                }

                return new StringBuilder();
            }

            internal static HashSet<object> RentRecursionPath()
            {
                if (RecursionPaths.TryTake(out HashSet<object>? path))
                {
                    Interlocked.Decrement(ref _recursionPathCount);
                    return path;
                }

                return new HashSet<object>(ReferenceComparer.Instance);
            }

            internal static void Return(StringBuilder builder, HashSet<object> recursionPath)
            {
                recursionPath.Clear();
                if (Interlocked.Increment(ref _recursionPathCount) <= MaximumRetainedStates)
                {
                    RecursionPaths.Add(recursionPath);
                }
                else
                {
                    Interlocked.Decrement(ref _recursionPathCount);
                }

                if (builder.Capacity <= MaximumRetainedBuilderCapacity &&
                    Interlocked.Increment(ref _builderCount) <= MaximumRetainedStates)
                {
                    builder.Clear();
                    Builders.Add(builder);
                }
                else if (builder.Capacity <= MaximumRetainedBuilderCapacity)
                {
                    Interlocked.Decrement(ref _builderCount);
                }
            }
        }
    }
}
