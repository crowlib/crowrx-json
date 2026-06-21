using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace CrowRx.Json
{
    internal sealed class JsonDeserializer
    {
        private readonly TypeContractCache _contractCache;
        private readonly JsonSerializerOptions _options;
        private JsonError _error = JsonError.NoError;

        internal JsonDeserializer(TypeContractCache contractCache, JsonSerializerOptions? options = null)
        {
            _contractCache = contractCache ?? throw new ArgumentNullException(nameof(contractCache));
            _options = options ?? JsonSerializerOptions.Default;
        }

        internal JsonDeserializer(TypeContractCache contractCache, bool rejectUnknownMembers)
            : this(contractCache, new JsonSerializerOptions(rejectUnknownMembers: rejectUnknownMembers))
        {
        }

        internal static bool TryDeserialize(
            string json,
            Type targetType,
            JsonSerializerOptions options,
            out object? value,
            out JsonError error)
        {
            if (targetType is null)
            {
                throw new ArgumentNullException(nameof(targetType));
            }

            if (!JsonReader.TryRead(json, out object? jsonValue, out error, preserveNumbers: true))
            {
                value = null;
                return false;
            }

            var deserializer = new JsonDeserializer(TypeContractCache.Shared, options);
            return deserializer.TryConvertRoot(jsonValue, targetType, out value, out error);
        }

        internal bool TryConvertRoot(object? jsonValue, Type targetType, out object? value, out JsonError error)
        {
            _error = JsonError.NoError;
            try
            {
                bool success = TryConvert(jsonValue, targetType, "$", out value);
                error = success ? JsonError.NoError : _error;
                return success;
            }
            catch (TypeContractException exception)
            {
                value = null;
                error = CreateError(
                    exception.Category,
                    exception.Message,
                    "$",
                    exception.TargetType,
                    GetJsonType(jsonValue));
                return false;
            }
        }

        internal JsonError Error => _error;

        internal void ResetError() => _error = JsonError.NoError;

        internal bool TryConvertValue(object? jsonValue, Type targetType, string path, out object? value) =>
            TryConvert(jsonValue, targetType, path, out value);

        internal bool SetError(
            JsonErrorCategory category,
            string message,
            string path,
            Type expectedType,
            string actualJsonType) =>
            Fail(category, message, path, expectedType, actualJsonType);

        private bool TryConvert(object? jsonValue, Type targetType, string path, out object? value)
        {
            if (jsonValue is null)
            {
                if (!targetType.IsValueType || Nullable.GetUnderlyingType(targetType) is not null)
                {
                    value = null;
                    return true;
                }

                value = null;
                return Fail(JsonErrorCategory.TypeMismatch, "JSON null cannot be assigned to a non-nullable value type.", path, targetType, "Null");
            }

            if (_options.TryGetConverter(targetType, out JsonConverterAdapter? converter))
            {
                try
                {
                    value = converter.Read(JsonValueFormatter.Format(jsonValue));
                    if (value is not null && targetType.IsInstanceOfType(value))
                    {
                        return true;
                    }

                    value = null;
                    return Fail(JsonErrorCategory.ConverterFailed, $"Converter for '{targetType}' returned an incompatible or null value.", path, targetType, GetJsonType(jsonValue));
                }
                catch (Exception exception)
                {
                    value = null;
                    return Fail(JsonErrorCategory.ConverterFailed, $"Converter for '{targetType}' failed: {exception.Message}", path, targetType, GetJsonType(jsonValue));
                }
            }

            TypeContract contract = _options.GetContract(_contractCache, targetType);
            switch (contract.Kind)
            {
                case TypeContractKind.Nullable:
                    return TryConvert(jsonValue, contract.UnderlyingType!, path, out value);
                case TypeContractKind.Scalar:
                    return TryConvertScalar(jsonValue, targetType, path, out value);
                case TypeContractKind.Enum:
                    return TryConvertEnum(jsonValue, targetType, path, out value);
                case TypeContractKind.Collection:
                    return TryConvertCollection(jsonValue, contract, path, out value);
                case TypeContractKind.Dictionary:
                    return TryConvertDictionary(jsonValue, contract, path, out value);
                case TypeContractKind.Dto:
                    return TryConvertDto(jsonValue, contract, path, out value);
                default:
                    value = null;
                    return Fail(JsonErrorCategory.UnsupportedType, $"Type '{targetType}' is not supported.", path, targetType, GetJsonType(jsonValue));
            }
        }

        private bool TryConvertScalar(object jsonValue, Type targetType, string path, out object? value)
        {
            if (targetType == typeof(string))
            {
                return TryExact<string>(jsonValue, targetType, path, out value);
            }

            if (targetType == typeof(bool))
            {
                return TryExact<bool>(jsonValue, targetType, path, out value);
            }

            if (targetType == typeof(char))
            {
                if (jsonValue is string text && text.Length == 1)
                {
                    value = text[0];
                    return true;
                }

                value = null;
                return Fail(JsonErrorCategory.TypeMismatch, "A char requires a JSON string containing exactly one UTF-16 code unit.", path, targetType, GetJsonType(jsonValue));
            }

            if (targetType == typeof(Guid))
            {
                if (jsonValue is not string text)
                {
                    value = null;
                    return Fail(JsonErrorCategory.TypeMismatch, "Expected a JSON string.", path, targetType, GetJsonType(jsonValue));
                }

                if (Guid.TryParseExact(text, "D", out Guid guid))
                {
                    value = guid;
                    return true;
                }

                value = null;
                return Fail(JsonErrorCategory.InvalidValue, "Expected a GUID string in D format.", path, targetType, GetJsonType(jsonValue));
            }

            if (targetType == typeof(DateTime))
            {
                if (jsonValue is not string text)
                {
                    value = null;
                    return Fail(JsonErrorCategory.TypeMismatch, "Expected a JSON string.", path, targetType, GetJsonType(jsonValue));
                }

                if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime dateTime))
                {
                    value = dateTime;
                    return true;
                }

                value = null;
                return Fail(JsonErrorCategory.InvalidValue, "Expected an invariant ISO 8601 DateTime string.", path, targetType, GetJsonType(jsonValue));
            }

            if (targetType == typeof(DateTimeOffset))
            {
                if (jsonValue is not string text)
                {
                    value = null;
                    return Fail(JsonErrorCategory.TypeMismatch, "Expected a JSON string.", path, targetType, GetJsonType(jsonValue));
                }

                if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset dateTimeOffset))
                {
                    value = dateTimeOffset;
                    return true;
                }

                value = null;
                return Fail(JsonErrorCategory.InvalidValue, "Expected an invariant ISO 8601 DateTimeOffset string.", path, targetType, GetJsonType(jsonValue));
            }

            if (jsonValue is not JsonNumber number)
            {
                value = null;
                return Fail(JsonErrorCategory.TypeMismatch, "Expected a JSON number.", path, targetType, GetJsonType(jsonValue));
            }

            return TryConvertNumber(number, targetType, path, out value);
        }

        private bool TryConvertNumber(JsonNumber number, Type targetType, string path, out object? value)
        {
            string text = number.Text;
            bool success;
            if (targetType == typeof(float))
            {
                success = float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float result) &&
                          !float.IsNaN(result) && !float.IsInfinity(result) &&
                          (result != 0 || IsJsonNumberZero(text));
                value = result;
            }
            else if (targetType == typeof(double))
            {
                success = double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double result) &&
                          !double.IsNaN(result) && !double.IsInfinity(result) &&
                          (result != 0 || IsJsonNumberZero(text));
                value = result;
            }
            else if (targetType == typeof(decimal))
            {
                success = decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal result) &&
                          (result != 0 || IsJsonNumberZero(text));
                value = result;
            }
            else if (!number.IsInteger)
            {
                value = null;
                return Fail(JsonErrorCategory.TypeMismatch, "A fractional or exponent JSON number cannot be assigned to an integer type.", path, targetType, "Number");
            }
            else if (targetType == typeof(sbyte)) { success = sbyte.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out sbyte result); value = result; }
            else if (targetType == typeof(byte)) { success = byte.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out byte result); value = result; }
            else if (targetType == typeof(short)) { success = short.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out short result); value = result; }
            else if (targetType == typeof(ushort)) { success = ushort.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out ushort result); value = result; }
            else if (targetType == typeof(int)) { success = int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int result); value = result; }
            else if (targetType == typeof(uint)) { success = uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out uint result); value = result; }
            else if (targetType == typeof(long)) { success = long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long result); value = result; }
            else if (targetType == typeof(ulong)) { success = ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out ulong result); value = result; }
            else
            {
                value = null;
                return Fail(JsonErrorCategory.UnsupportedType, $"Numeric type '{targetType}' is not supported.", path, targetType, "Number");
            }

            if (success)
            {
                return true;
            }

            value = null;
            return Fail(JsonErrorCategory.NumericOverflow, $"JSON number '{text}' is outside the range of '{targetType}'.", path, targetType, "Number");
        }

        private bool TryConvertEnum(object jsonValue, Type targetType, string path, out object? value)
        {
            if (jsonValue is string text &&
                Enum.TryParse(targetType, text, ignoreCase: false, out object? parsed) &&
                parsed is not null &&
                Enum.IsDefined(targetType, parsed))
            {
                value = parsed;
                return true;
            }

            if (_options.AllowNumericEnums && jsonValue is JsonNumber number && number.IsInteger)
            {
                Type underlyingType = Enum.GetUnderlyingType(targetType);
                if (TryConvertNumber(number, underlyingType, path, out object? numericValue) && numericValue is not null)
                {
                    object enumValue = Enum.ToObject(targetType, numericValue);
                    if (Enum.IsDefined(targetType, enumValue))
                    {
                        value = enumValue;
                        return true;
                    }
                }

                value = null;
                return Fail(JsonErrorCategory.InvalidValue, "Expected a numeric value defined by the target enum.", path, targetType, "Number");
            }

            value = null;
            return Fail(JsonErrorCategory.InvalidValue, "Expected the exact name of a defined enum value.", path, targetType, GetJsonType(jsonValue));
        }

        private bool TryConvertCollection(object jsonValue, TypeContract contract, string path, out object? value)
        {
            if (jsonValue is not List<object?> items)
            {
                value = null;
                return Fail(JsonErrorCategory.TypeMismatch, "Expected a JSON array.", path, contract.Type, GetJsonType(jsonValue));
            }

            Type elementType = contract.ElementType!;
            if (contract.Type.IsArray)
            {
                Array array = Array.CreateInstance(elementType, items.Count);
                for (int index = 0; index < items.Count; index++)
                {
                    if (!TryConvert(items[index], elementType, $"{path}[{index}]", out object? item))
                    {
                        value = null;
                        return false;
                    }

                    array.SetValue(item, index);
                }

                value = array;
                return true;
            }

            Type concreteType = contract.Type.IsInterface || contract.Type.IsAbstract
                ? typeof(List<>).MakeGenericType(elementType)
                : contract.Type;
            if (!TryCreate(concreteType, null, out object? instance) || instance is not IList list)
            {
                value = null;
                return Fail(JsonErrorCategory.ObjectCreationFailed, $"Collection type '{contract.Type}' requires an accessible parameterless constructor.", path, contract.Type, "Array");
            }

            for (int index = 0; index < items.Count; index++)
            {
                if (!TryConvert(items[index], elementType, $"{path}[{index}]", out object? item))
                {
                    value = null;
                    return false;
                }

                list.Add(item);
            }

            value = instance;
            return true;
        }

        private bool TryConvertDictionary(object jsonValue, TypeContract contract, string path, out object? value)
        {
            if (jsonValue is not Dictionary<string, object?> properties)
            {
                value = null;
                return Fail(JsonErrorCategory.TypeMismatch, "Expected a JSON object.", path, contract.Type, GetJsonType(jsonValue));
            }

            Type valueType = contract.ValueType!;
            Type concreteType = contract.Type.IsInterface || contract.Type.IsAbstract
                ? typeof(Dictionary<,>).MakeGenericType(typeof(string), valueType)
                : contract.Type;
            if (!TryCreate(concreteType, properties, out object? instance) || instance is not IDictionary dictionary)
            {
                value = null;
                return Fail(JsonErrorCategory.ObjectCreationFailed, $"Dictionary type '{contract.Type}' requires an accessible parameterless constructor.", path, contract.Type, "Object");
            }

            foreach (KeyValuePair<string, object?> property in properties)
            {
                string propertyPath = AppendPropertyPath(path, property.Key);
                if (!TryConvert(property.Value, valueType, propertyPath, out object? item))
                {
                    value = null;
                    return false;
                }

                dictionary.Add(property.Key, item);
            }

            value = instance;
            return true;
        }

        private bool TryConvertDto(object jsonValue, TypeContract contract, string path, out object? value)
        {
            if (jsonValue is not Dictionary<string, object?> properties)
            {
                value = null;
                return Fail(JsonErrorCategory.TypeMismatch, "Expected a JSON object.", path, contract.Type, GetJsonType(jsonValue));
            }

            StringComparer comparer = _options.PropertyNameCaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            var consumedConstructorMembers = new HashSet<string>(comparer);
            object? instance;
            if (_options.HasActivator(contract.Type))
            {
                if (!_options.TryCreate(contract.Type, properties, out instance))
                {
                    value = null;
                    return Fail(JsonErrorCategory.ObjectCreationFailed, $"The configured activator for '{contract.Type}' returned null or an incompatible type.", path, contract.Type, "Object");
                }
            }
            else if (contract.Constructor is not null)
            {
                ConstructorContract constructor = contract.Constructor;
                var arguments = new object?[constructor.Parameters.Count];
                for (int index = 0; index < constructor.Parameters.Count; index++)
                {
                    ConstructorParameterContract parameter = constructor.Parameters[index];
                    consumedConstructorMembers.Add(parameter.JsonName);
                    if (TryGetProperty(properties, parameter.JsonName, comparer, out object? argumentValue))
                    {
                        string parameterPath = AppendPropertyPath(path, parameter.JsonName);
                        if (!TryConvert(argumentValue, parameter.ParameterType, parameterPath, out arguments[index]))
                        {
                            value = null;
                            return false;
                        }
                    }
                    else if (parameter.HasDefaultValue)
                    {
                        arguments[index] = parameter.DefaultValue;
                    }
                    else
                    {
                        value = null;
                        string parameterPath = AppendPropertyPath(path, parameter.JsonName);
                        return Fail(
                            JsonErrorCategory.MissingRequiredMember,
                            $"Required constructor parameter '{parameter.JsonName}' is missing.",
                            parameterPath,
                            parameter.ParameterType,
                            "Missing");
                    }
                }

                try
                {
                    instance = constructor.Constructor.Invoke(arguments);
                }
                catch (Exception exception) when (exception is TargetInvocationException or ArgumentException or MethodAccessException)
                {
                    value = null;
                    string message = exception is TargetInvocationException { InnerException: not null } invocation
                        ? invocation.InnerException!.Message
                        : exception.Message;
                    return Fail(
                        JsonErrorCategory.ConstructorInvocationFailed,
                        $"Constructor for '{contract.Type}' failed: {message}",
                        path,
                        contract.Type,
                        "Object");
                }
            }
            else if (!TryCreate(contract.Type, properties, out instance))
            {
                value = null;
                return Fail(JsonErrorCategory.ObjectCreationFailed, $"DTO type '{contract.Type}' has no supported constructor.", path, contract.Type, "Object");
            }

            if (instance is null)
            {
                value = null;
                return Fail(JsonErrorCategory.ObjectCreationFailed, $"Constructor for '{contract.Type}' returned null.", path, contract.Type, "Object");
            }

            var members = new Dictionary<string, MemberContract>(comparer);
            foreach (MemberContract member in contract.Members)
            {
                if (member.CanDeserialize)
                {
                    try
                    {
                        members.Add(member.JsonName, member);
                    }
                    catch (ArgumentException)
                    {
                        value = null;
                        return Fail(JsonErrorCategory.ConstructorBindingFailed, $"Members of '{contract.Type}' collide under case-insensitive matching.", path, contract.Type, "Object");
                    }
                }
            }

            foreach (KeyValuePair<string, object?> property in properties)
            {
                string propertyPath = AppendPropertyPath(path, property.Key);
                if (consumedConstructorMembers.Contains(property.Key))
                {
                    continue;
                }

                if (!members.TryGetValue(property.Key, out MemberContract? member))
                {
                    if (_options.RejectUnknownMembers)
                    {
                        value = null;
                        return Fail(JsonErrorCategory.UnknownMember, $"JSON member '{property.Key}' does not map to a writable member of '{contract.Type}'.", propertyPath, contract.Type, GetJsonType(property.Value));
                    }

                    continue;
                }

                if (!TryConvert(property.Value, member.MemberType, propertyPath, out object? memberValue))
                {
                    value = null;
                    return false;
                }

                try
                {
                    switch (member.MemberInfo)
                    {
                        case FieldInfo field:
                            field.SetValue(instance, memberValue);
                            break;
                        case PropertyInfo propertyInfo:
                            propertyInfo.SetValue(instance, memberValue);
                            break;
                        default:
                            value = null;
                            return Fail(JsonErrorCategory.MemberAssignmentFailed, $"Member '{member.MemberInfo}' is not assignable.", propertyPath, member.MemberType, GetJsonType(property.Value));
                    }
                }
                catch (Exception exception) when (exception is ArgumentException or MethodAccessException or TargetInvocationException)
                {
                    value = null;
                    return Fail(JsonErrorCategory.MemberAssignmentFailed, $"Failed to assign member '{member.JsonName}': {exception.Message}", propertyPath, member.MemberType, GetJsonType(property.Value));
                }
            }

            value = instance;
            return true;
        }

        private bool TryExact<T>(object jsonValue, Type targetType, string path, out object? value)
        {
            if (jsonValue is T exact)
            {
                value = exact;
                return true;
            }

            value = null;
            return Fail(JsonErrorCategory.TypeMismatch, $"Expected a JSON {typeof(T).Name}.", path, targetType, GetJsonType(jsonValue));
        }

        private bool TryCreate(Type type, Dictionary<string, object?>? properties, out object? value)
        {
            if (_options.HasActivator(type))
            {
                return _options.TryCreate(type, properties, out value);
            }

            try
            {
                value = Activator.CreateInstance(type);
                return value is not null;
            }
            catch (Exception exception) when (exception is MissingMethodException or MemberAccessException or TargetInvocationException)
            {
                value = null;
                return false;
            }
        }

        private static bool TryGetProperty(
            Dictionary<string, object?> properties,
            string name,
            StringComparer comparer,
            out object? value)
        {
            if (comparer == StringComparer.Ordinal)
            {
                return properties.TryGetValue(name, out value);
            }

            foreach (KeyValuePair<string, object?> property in properties)
            {
                if (comparer.Equals(property.Key, name))
                {
                    value = property.Value;
                    return true;
                }
            }

            value = null;
            return false;
        }

        private bool Fail(JsonErrorCategory category, string message, string path, Type expectedType, string actualJsonType)
        {
            _error = CreateError(category, message, path, expectedType, actualJsonType);
            return false;
        }

        private static JsonError CreateError(JsonErrorCategory category, string message, string path, Type expectedType, string actualJsonType) =>
            new JsonError(category, message, path, 0, 1, 1, expectedType, actualJsonType);

        internal static string GetJsonType(object? value) => value switch
        {
            null => "Null",
            string => "String",
            bool => "Boolean",
            JsonNumber => "Number",
            List<object?> => "Array",
            Dictionary<string, object?> => "Object",
            _ => value.GetType().Name,
        };

        private static bool IsJsonNumberZero(string text)
        {
            foreach (char character in text)
            {
                if (character is 'e' or 'E')
                {
                    break;
                }

                if (character >= '1' && character <= '9')
                {
                    return false;
                }
            }

            return true;
        }

        internal static string AppendPropertyPath(string path, string propertyName)
        {
            if (propertyName.Length > 0 && IsIdentifierStart(propertyName[0]))
            {
                for (int index = 1; index < propertyName.Length; index++)
                {
                    if (!IsIdentifierPart(propertyName[index]))
                    {
                        return AppendQuotedPath(path, propertyName);
                    }
                }

                return $"{path}.{propertyName}";
            }

            return AppendQuotedPath(path, propertyName);
        }

        private static string AppendQuotedPath(string path, string propertyName) =>
            $"{path}[\"{propertyName.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"]";

        private static bool IsIdentifierStart(char value) => char.IsLetter(value) || value == '_';

        private static bool IsIdentifierPart(char value) => char.IsLetterOrDigit(value) || value == '_';
    }
}
