using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace CrowRx.Json
{
    internal sealed class JsonOverwriter
    {
        private readonly TypeContractCache _contractCache;
        private readonly JsonDeserializer _deserializer;
        private readonly JsonSerializerOptions _options;

        private JsonOverwriter(TypeContractCache contractCache, JsonSerializerOptions options)
        {
            _contractCache = contractCache;
            _options = options;
            _deserializer = new JsonDeserializer(contractCache, options);
        }

        internal static bool TryOverwrite(
            string json,
            Type targetType,
            object target,
            JsonOverwriteMode mode,
            JsonSerializerOptions options,
            out JsonError error)
        {
            if (!JsonReader.TryRead(json, out object? jsonValue, out error, preserveNumbers: true))
            {
                return false;
            }

            var overwriter = new JsonOverwriter(TypeContractCache.Shared, options);
            return overwriter.TryOverwriteRoot(jsonValue, targetType, target, mode, out error);
        }

        private bool TryOverwriteRoot(
            object? jsonValue,
            Type targetType,
            object target,
            JsonOverwriteMode mode,
            out JsonError error)
        {
            _deserializer.ResetError();
            var mutations = new List<PendingMutation>();
            try
            {
                TypeContract contract = _options.GetContract(_contractCache, targetType);
                if (mode == JsonOverwriteMode.Append &&
                    contract.Kind is not TypeContractKind.Collection and not TypeContractKind.Dictionary)
                {
                    error = CreateError(
                        JsonErrorCategory.UnsupportedOverwriteTarget,
                        "Append mode is supported only for lists and dictionaries.",
                        "$",
                        targetType,
                        JsonDeserializer.GetJsonType(jsonValue));
                    return false;
                }

                if (!TryStage(jsonValue, contract, target, "$", mode, mutations))
                {
                    error = _deserializer.Error;
                    return false;
                }
            }
            catch (TypeContractException exception)
            {
                error = CreateError(
                    exception.Category,
                    exception.Message,
                    "$",
                    exception.TargetType,
                    JsonDeserializer.GetJsonType(jsonValue));
                return false;
            }

            foreach (PendingMutation mutation in mutations)
            {
                try
                {
                    mutation.Apply();
                }
                catch (Exception exception) when (
                    exception is ArgumentException or
                    InvalidOperationException or
                    NotSupportedException or
                    TargetInvocationException or
                    MethodAccessException)
                {
                    error = CreateError(
                        JsonErrorCategory.MutationFailed,
                        $"Overwrite mutation failed: {exception.Message}",
                        mutation.Path,
                        mutation.ExpectedType,
                        mutation.ActualJsonType);
                    return false;
                }
            }

            error = JsonError.NoError;
            return true;
        }

        private bool TryStage(
            object? jsonValue,
            TypeContract contract,
            object target,
            string path,
            JsonOverwriteMode mode,
            List<PendingMutation> mutations)
        {
            if (!contract.Type.IsInstanceOfType(target))
            {
                return _deserializer.SetError(
                    JsonErrorCategory.TypeMismatch,
                    $"Overwrite target is not an instance of '{contract.Type}'.",
                    path,
                    contract.Type,
                    target.GetType().Name);
            }

            return contract.Kind switch
            {
                TypeContractKind.Collection when target is IList list =>
                    TryStageCollection(jsonValue, contract, list, path, mode, mutations),
                TypeContractKind.Dictionary when target is IDictionary dictionary =>
                    TryStageDictionary(jsonValue, contract, dictionary, path, mode, mutations),
                TypeContractKind.Dto =>
                    TryStageDto(jsonValue, contract, target, path, mutations),
                TypeContractKind.Collection or TypeContractKind.Dictionary =>
                    _deserializer.SetError(
                        JsonErrorCategory.UnsupportedOverwriteTarget,
                        $"Collection type '{contract.Type}' does not expose the required non-generic mutation interface.",
                        path,
                        contract.Type,
                        JsonDeserializer.GetJsonType(jsonValue)),
                _ => _deserializer.SetError(
                    JsonErrorCategory.UnsupportedOverwriteTarget,
                    $"Type '{contract.Type}' is not a supported overwrite target.",
                    path,
                    contract.Type,
                    JsonDeserializer.GetJsonType(jsonValue)),
            };
        }

        private bool TryStageCollection(
            object? jsonValue,
            TypeContract contract,
            IList target,
            string path,
            JsonOverwriteMode mode,
            List<PendingMutation> mutations)
        {
            if (jsonValue is not List<object?> items)
            {
                return _deserializer.SetError(
                    JsonErrorCategory.TypeMismatch,
                    "Expected a JSON array.",
                    path,
                    contract.Type,
                    JsonDeserializer.GetJsonType(jsonValue));
            }

            Type elementType = contract.ElementType!;
            var converted = new object?[items.Count];
            for (int index = 0; index < items.Count; index++)
            {
                if (!_deserializer.TryConvertValue(items[index], elementType, $"{path}[{index}]", out converted[index]))
                {
                    return false;
                }
            }

            if (contract.Type.IsArray)
            {
                if (mode == JsonOverwriteMode.Append)
                {
                    return _deserializer.SetError(
                        JsonErrorCategory.UnsupportedOverwriteTarget,
                        "Append mode is not supported for arrays.",
                        path,
                        contract.Type,
                        "Array");
                }

                if (target.Count != converted.Length)
                {
                    return _deserializer.SetError(
                        JsonErrorCategory.UnsupportedOverwriteTarget,
                        "Root array overwrite requires the JSON array to have the same length as the target.",
                        path,
                        contract.Type,
                        "Array");
                }

                mutations.Add(new PendingMutation(
                    path,
                    contract.Type,
                    "Array",
                    () =>
                    {
                        for (int index = 0; index < converted.Length; index++)
                        {
                            target[index] = converted[index];
                        }
                    }));
                return true;
            }

            if (target.IsReadOnly || target.IsFixedSize)
            {
                return _deserializer.SetError(
                    JsonErrorCategory.UnsupportedOverwriteTarget,
                    $"Collection type '{contract.Type}' cannot be mutated.",
                    path,
                    contract.Type,
                    "Array");
            }

            mutations.Add(new PendingMutation(
                path,
                contract.Type,
                "Array",
                () =>
                {
                    if (mode == JsonOverwriteMode.Replace)
                    {
                        target.Clear();
                    }

                    foreach (object? item in converted)
                    {
                        target.Add(item);
                    }
                }));
            return true;
        }

        private bool TryStageDictionary(
            object? jsonValue,
            TypeContract contract,
            IDictionary target,
            string path,
            JsonOverwriteMode mode,
            List<PendingMutation> mutations)
        {
            if (jsonValue is not Dictionary<string, object?> properties)
            {
                return _deserializer.SetError(
                    JsonErrorCategory.TypeMismatch,
                    "Expected a JSON object.",
                    path,
                    contract.Type,
                    JsonDeserializer.GetJsonType(jsonValue));
            }

            if (target.IsReadOnly || target.IsFixedSize)
            {
                return _deserializer.SetError(
                    JsonErrorCategory.UnsupportedOverwriteTarget,
                    $"Dictionary type '{contract.Type}' cannot be mutated.",
                    path,
                    contract.Type,
                    "Object");
            }

            Type valueType = contract.ValueType!;
            var converted = new List<KeyValuePair<string, object?>>(properties.Count);
            foreach (KeyValuePair<string, object?> property in properties)
            {
                string propertyPath = JsonDeserializer.AppendPropertyPath(path, property.Key);
                if (mode == JsonOverwriteMode.Append && target.Contains(property.Key))
                {
                    return _deserializer.SetError(
                        JsonErrorCategory.DuplicateDictionaryKey,
                        $"Dictionary key '{property.Key}' already exists.",
                        propertyPath,
                        valueType,
                        JsonDeserializer.GetJsonType(property.Value));
                }

                if (!_deserializer.TryConvertValue(property.Value, valueType, propertyPath, out object? item))
                {
                    return false;
                }

                converted.Add(new KeyValuePair<string, object?>(property.Key, item));
            }

            mutations.Add(new PendingMutation(
                path,
                contract.Type,
                "Object",
                () =>
                {
                    if (mode == JsonOverwriteMode.Replace)
                    {
                        target.Clear();
                    }

                    foreach (KeyValuePair<string, object?> property in converted)
                    {
                        target.Add(property.Key, property.Value);
                    }
                }));
            return true;
        }

        private bool TryStageDto(
            object? jsonValue,
            TypeContract contract,
            object target,
            string path,
            List<PendingMutation> mutations)
        {
            if (jsonValue is not Dictionary<string, object?> properties)
            {
                return _deserializer.SetError(
                    JsonErrorCategory.TypeMismatch,
                    "Expected a JSON object.",
                    path,
                    contract.Type,
                    JsonDeserializer.GetJsonType(jsonValue));
            }

            StringComparer comparer = _options.PropertyNameCaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            var members = new Dictionary<string, MemberContract>(comparer);
            foreach (MemberContract member in contract.Members)
            {
                try
                {
                    members.Add(member.JsonName, member);
                }
                catch (ArgumentException)
                {
                    return _deserializer.SetError(
                        JsonErrorCategory.ConstructorBindingFailed,
                        $"Members of '{contract.Type}' collide under case-insensitive matching.",
                        path,
                        contract.Type,
                        "Object");
                }
            }

            var constructorMembers = new HashSet<string>(comparer);
            if (contract.Constructor is not null)
            {
                foreach (ConstructorParameterContract parameter in contract.Constructor.Parameters)
                {
                    constructorMembers.Add(parameter.JsonName);
                }
            }

            foreach (KeyValuePair<string, object?> property in properties)
            {
                string propertyPath = JsonDeserializer.AppendPropertyPath(path, property.Key);
                if (!members.TryGetValue(property.Key, out MemberContract? member))
                {
                    if (_options.RejectUnknownMembers)
                    {
                        return _deserializer.SetError(
                            JsonErrorCategory.UnknownMember,
                            $"JSON member '{property.Key}' does not map to a member of '{contract.Type}'.",
                            propertyPath,
                            contract.Type,
                            JsonDeserializer.GetJsonType(property.Value));
                    }

                    continue;
                }

                if (!member.CanDeserialize || constructorMembers.Contains(member.JsonName))
                {
                    return _deserializer.SetError(
                        JsonErrorCategory.UnsupportedOverwriteTarget,
                        $"Member '{member.JsonName}' cannot be overwritten after construction.",
                        propertyPath,
                        member.MemberType,
                        JsonDeserializer.GetJsonType(property.Value));
                }

                object? currentValue;
                try
                {
                    currentValue = member.MemberInfo switch
                    {
                        FieldInfo field => field.GetValue(target),
                        PropertyInfo propertyInfo => propertyInfo.GetValue(target),
                        _ => null,
                    };
                }
                catch (Exception exception) when (exception is TargetInvocationException or MethodAccessException)
                {
                    return _deserializer.SetError(
                        JsonErrorCategory.MutationFailed,
                        $"Failed to read member '{member.JsonName}': {exception.Message}",
                        propertyPath,
                        member.MemberType,
                        JsonDeserializer.GetJsonType(property.Value));
                }

                TypeContract? memberContract = _options.TryGetConverter(member.MemberType, out _)
                    ? null
                    : _options.GetContract(_contractCache, member.MemberType);
                bool stageNested =
                    property.Value is not null &&
                    currentValue is not null &&
                    memberContract is not null &&
                    (memberContract.Kind == TypeContractKind.Dto ||
                     memberContract.Kind == TypeContractKind.Dictionary ||
                     (memberContract.Kind == TypeContractKind.Collection && !member.MemberType.IsArray));

                if (stageNested)
                {
                    if (!TryStage(property.Value, memberContract!, currentValue!, propertyPath, JsonOverwriteMode.Replace, mutations))
                    {
                        return false;
                    }

                    continue;
                }

                if (!_deserializer.TryConvertValue(property.Value, member.MemberType, propertyPath, out object? memberValue))
                {
                    return false;
                }

                Action apply = member.MemberInfo switch
                {
                    FieldInfo field => () => field.SetValue(target, memberValue),
                    PropertyInfo propertyInfo => () => propertyInfo.SetValue(target, memberValue),
                    _ => throw new InvalidOperationException($"Member '{member.MemberInfo}' is not assignable."),
                };
                mutations.Add(new PendingMutation(
                    propertyPath,
                    member.MemberType,
                    JsonDeserializer.GetJsonType(property.Value),
                    apply));
            }

            return true;
        }

        private static JsonError CreateError(
            JsonErrorCategory category,
            string message,
            string path,
            Type expectedType,
            string actualJsonType) =>
            new JsonError(category, message, path, 0, 1, 1, expectedType, actualJsonType);

        private sealed class PendingMutation
        {
            internal string Path { get; }
            internal Type ExpectedType { get; }
            internal string ActualJsonType { get; }
            internal Action Apply { get; }

            internal PendingMutation(string path, Type expectedType, string actualJsonType, Action apply)
            {
                Path = path;
                ExpectedType = expectedType;
                ActualJsonType = actualJsonType;
                Apply = apply;
            }
        }
    }
}
