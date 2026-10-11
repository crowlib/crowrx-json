using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CrowRx.Json
{
    internal static class TypeContractBuilder
    {
        private const BindingFlags DeclaredMembers =
            BindingFlags.Instance |
            BindingFlags.Static |
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.DeclaredOnly;

        private static readonly HashSet<Type> ScalarTypes = new()
        {
            typeof(bool),
            typeof(string),
            typeof(char),
            typeof(sbyte),
            typeof(byte),
            typeof(short),
            typeof(ushort),
            typeof(int),
            typeof(uint),
            typeof(long),
            typeof(ulong),
            typeof(float),
            typeof(double),
            typeof(decimal),
            typeof(Guid),
            typeof(DateTime),
            typeof(DateTimeOffset),
        };

        internal static TypeContract Build(Type type, bool validateReferencedTypes = true)
        {
            ValidateTypeShape(type);

            if (ScalarTypes.Contains(type))
            {
                return new TypeContract(type, TypeContractKind.Scalar);
            }

            if (type.IsEnum)
            {
                return new TypeContract(type, TypeContractKind.Enum, underlyingType: Enum.GetUnderlyingType(type));
            }

            Type? nullableType = Nullable.GetUnderlyingType(type);
            if (nullableType is not null)
            {
                if (validateReferencedTypes) ValidateReferencedType(nullableType);
                return new TypeContract(type, TypeContractKind.Nullable, underlyingType: nullableType);
            }

            if (type.IsArray)
            {
                if (type.GetArrayRank() != 1)
                {
                    throw Unsupported(type, "Only one-dimensional arrays are supported.");
                }

                Type arrayElementType = type.GetElementType()!;
                if (validateReferencedTypes) ValidateReferencedType(arrayElementType);
                return new TypeContract(type, TypeContractKind.Collection, elementType: arrayElementType);
            }

            if (TryGetDictionaryArguments(type, out Type? keyType, out Type? valueType))
            {
                if (keyType != typeof(string))
                {
                    throw Unsupported(type, "Dictionary keys must be strings unless a converter is registered.");
                }

                if (validateReferencedTypes) ValidateReferencedType(valueType!);
                return new TypeContract(type, TypeContractKind.Dictionary, keyType: keyType, valueType: valueType);
            }

            if (TryGetListElementType(type, out Type? elementType))
            {
                if (validateReferencedTypes) ValidateReferencedType(elementType!);
                return new TypeContract(type, TypeContractKind.Collection, elementType: elementType);
            }

            if (typeof(IEnumerable).IsAssignableFrom(type))
            {
                throw Unsupported(type, "The collection shape is not supported by the core serializer.");
            }

            if (type == typeof(object) || type.IsInterface || type.IsAbstract)
            {
                throw Unsupported(type, "Interfaces, abstract classes, and System.Object require an explicit converter or concrete type.");
            }

            ConstructorInfo? explicitConstructor = FindExplicitConstructor(type);
            ConstructorInfo? selectedConstructor = SelectConstructor(type, explicitConstructor);
            HashSet<string> constructorParameterNames = GetConstructorParameterNames(type, selectedConstructor);
            IReadOnlyList<MemberContract> members = BuildMembers(type, constructorParameterNames, validateReferencedTypes);
            ConstructorContract? constructor = BuildConstructorContract(type, selectedConstructor, members);
            return new TypeContract(
                type,
                TypeContractKind.Dto,
                members: members,
                explicitConstructor: explicitConstructor,
                constructor: constructor);
        }

        private static IReadOnlyList<MemberContract> BuildMembers(
            Type type,
            HashSet<string> constructorParameterNames,
            bool validateReferencedTypes)
        {
            var candidates = new List<MemberCandidate>();
            var overriddenAccessors = new HashSet<MethodInfo>();
            List<Type> hierarchy = GetTypeHierarchy(type);

            for (int typeIndex = hierarchy.Count - 1; typeIndex >= 0; typeIndex--)
            {
                Type declaringType = hierarchy[typeIndex];
                foreach (PropertyInfo property in declaringType.GetProperties(DeclaredMembers))
                {
                    MethodInfo? accessor = property.GetMethod ?? property.SetMethod;
                    if (accessor is not null && accessor.IsVirtual)
                    {
                        MethodInfo baseDefinition = accessor.GetBaseDefinition();
                        if (!overriddenAccessors.Add(baseDefinition))
                        {
                            continue;
                        }
                    }

                    if (TryCreatePropertyCandidate(property, typeIndex, constructorParameterNames, validateReferencedTypes, out MemberCandidate candidate))
                    {
                        candidates.Add(candidate);
                    }
                }
            }

            for (int typeIndex = 0; typeIndex < hierarchy.Count; typeIndex++)
            {
                Type declaringType = hierarchy[typeIndex];
                foreach (FieldInfo field in declaringType.GetFields(DeclaredMembers))
                {
                    if (TryCreateFieldCandidate(field, typeIndex, constructorParameterNames, validateReferencedTypes, out MemberCandidate candidate))
                    {
                        candidates.Add(candidate);
                    }
                }
            }

            candidates.Sort(static (left, right) =>
            {
                int declaringTypeOrder = left.DeclaringTypeOrder.CompareTo(right.DeclaringTypeOrder);
                return declaringTypeOrder != 0
                    ? declaringTypeOrder
                    : left.MemberInfo.MetadataToken.CompareTo(right.MemberInfo.MetadataToken);
            });

            var names = new Dictionary<string, MemberInfo>(StringComparer.Ordinal);
            var members = new List<MemberContract>(candidates.Count);
            foreach (MemberCandidate candidate in candidates)
            {
                if (names.TryGetValue(candidate.JsonName, out MemberInfo? conflictingMember))
                {
                    throw new TypeContractException(
                        type,
                        $"Members '{conflictingMember.DeclaringType}.{conflictingMember.Name}' and " +
                        $"'{candidate.MemberInfo.DeclaringType}.{candidate.MemberInfo.Name}' resolve to JSON name '{candidate.JsonName}'.");
                }

                names.Add(candidate.JsonName, candidate.MemberInfo);
                members.Add(new MemberContract(
                    candidate.JsonName,
                    candidate.MemberType,
                    candidate.MemberInfo,
                    candidate.CanSerialize,
                    candidate.CanDeserialize));
            }

            return members.AsReadOnly();
        }

        private static bool TryCreateFieldCandidate(
            FieldInfo field,
            int typeOrder,
            HashSet<string> constructorParameterNames,
            bool validateReferencedTypes,
            out MemberCandidate candidate)
        {
            candidate = null!;
            if (field.IsStatic || field.IsLiteral || IsIgnored(field) || IsDelegate(field.FieldType))
            {
                return false;
            }

            bool explicitlyIncluded = IsIncluded(field);
            string jsonName = GetJsonName(field);
            bool constructorBound = field.IsPublic && field.IsInitOnly && constructorParameterNames.Contains(jsonName);
            if (!explicitlyIncluded && (!field.IsPublic || (field.IsInitOnly && !constructorBound)))
            {
                return false;
            }

            if (validateReferencedTypes) ValidateReferencedType(field.FieldType);
            candidate = new MemberCandidate(
                jsonName,
                field.FieldType,
                field,
                canSerialize: true,
                canDeserialize: !field.IsInitOnly,
                typeOrder);
            return true;
        }

        private static bool TryCreatePropertyCandidate(
            PropertyInfo property,
            int typeOrder,
            HashSet<string> constructorParameterNames,
            bool validateReferencedTypes,
            out MemberCandidate candidate)
        {
            candidate = null!;
            MethodInfo? getter = property.GetGetMethod(nonPublic: true);
            MethodInfo? setter = property.GetSetMethod(nonPublic: true);

            if ((getter?.IsStatic ?? false) ||
                (setter?.IsStatic ?? false) ||
                property.GetIndexParameters().Length != 0 ||
                IsIgnored(property) ||
                IsDelegate(property.PropertyType))
            {
                return false;
            }

            bool explicitlyIncluded = IsIncluded(property);
            bool hasPublicGetter = getter?.IsPublic == true;
            bool hasPublicSetter = setter?.IsPublic == true;
            bool isComputedOrReadOnly = setter is null;
            string jsonName = GetJsonName(property);
            bool constructorBound = hasPublicGetter && isComputedOrReadOnly && constructorParameterNames.Contains(jsonName);

            if (!explicitlyIncluded)
            {
                if ((!hasPublicGetter && !hasPublicSetter) || (isComputedOrReadOnly && !constructorBound))
                {
                    return false;
                }
            }

            bool canSerialize = explicitlyIncluded ? getter is not null : hasPublicGetter;
            bool canDeserialize = explicitlyIncluded ? setter is not null : hasPublicSetter;
            if (!canSerialize && !canDeserialize)
            {
                return false;
            }

            if (validateReferencedTypes) ValidateReferencedType(property.PropertyType);
            candidate = new MemberCandidate(
                jsonName,
                property.PropertyType,
                property,
                canSerialize,
                canDeserialize,
                typeOrder);
            return true;
        }

        private static ConstructorInfo? FindExplicitConstructor(Type type)
        {
            ConstructorInfo[] constructors = type.GetConstructors(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            ConstructorInfo[] explicitConstructors = constructors
                .Where(static constructor => constructor.IsDefined(typeof(JsonConstructorAttribute), inherit: false))
                .ToArray();

            if (explicitConstructors.Length > 1)
            {
                throw new TypeContractException(
                    type,
                    "A type cannot declare more than one JsonConstructor.",
                    JsonErrorCategory.AmbiguousConstructor);
            }

            return explicitConstructors.Length == 1 ? explicitConstructors[0] : null;
        }

        private static ConstructorInfo? SelectConstructor(Type type, ConstructorInfo? explicitConstructor)
        {
            if (explicitConstructor is not null)
            {
                return explicitConstructor;
            }

            ConstructorInfo[] publicConstructors = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public);
            if (publicConstructors.Length == 1)
            {
                return publicConstructors[0];
            }

            if (publicConstructors.Length > 1)
            {
                ConstructorInfo? parameterless = publicConstructors.SingleOrDefault(
                    static constructor => constructor.GetParameters().Length == 0);
                if (parameterless is not null)
                {
                    return parameterless;
                }

                throw new TypeContractException(
                    type,
                    $"Type '{type}' has multiple public constructors and no unambiguous parameterless constructor.",
                    JsonErrorCategory.AmbiguousConstructor);
            }

            return null;
        }

        private static HashSet<string> GetConstructorParameterNames(Type type, ConstructorInfo? constructor)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (constructor is null)
            {
                return names;
            }

            foreach (ParameterInfo parameter in constructor.GetParameters())
            {
                if (parameter.Name is null)
                {
                    throw new TypeContractException(
                        type,
                        $"Constructor parameter at position {parameter.Position} has no metadata name.",
                        JsonErrorCategory.ConstructorBindingFailed);
                }

                if (!names.Add(parameter.Name))
                {
                    throw new TypeContractException(
                        type,
                        $"Constructor parameters resolve to duplicate JSON name '{parameter.Name}'.",
                        JsonErrorCategory.ConstructorBindingFailed);
                }
            }

            return names;
        }

        private static ConstructorContract? BuildConstructorContract(
            Type type,
            ConstructorInfo? constructor,
            IReadOnlyList<MemberContract> members)
        {
            if (constructor is null)
            {
                return null;
            }

            var membersByName = new Dictionary<string, MemberContract>(StringComparer.OrdinalIgnoreCase);
            foreach (MemberContract member in members)
            {
                if (!membersByName.TryAdd(member.JsonName, member))
                {
                    throw new TypeContractException(
                        type,
                        $"Serializable members collide under case-insensitive constructor binding for JSON name '{member.JsonName}'.",
                        JsonErrorCategory.ConstructorBindingFailed);
                }
            }

            var parameters = new List<ConstructorParameterContract>();
            foreach (ParameterInfo parameter in constructor.GetParameters())
            {
                string parameterName = parameter.Name!;
                if (!membersByName.TryGetValue(parameterName, out MemberContract? member))
                {
                    throw new TypeContractException(
                        type,
                        $"Constructor parameter '{parameterName}' does not match a serializable JSON member name.",
                        JsonErrorCategory.ConstructorBindingFailed);
                }

                if (parameter.ParameterType != member.MemberType)
                {
                    throw new TypeContractException(
                        type,
                        $"Constructor parameter '{parameterName}' has type '{parameter.ParameterType}', but member '{member.MemberInfo.Name}' has type '{member.MemberType}'.",
                        JsonErrorCategory.ConstructorBindingFailed);
                }

                parameters.Add(new ConstructorParameterContract(member.JsonName, parameter, member));
            }

            return new ConstructorContract(constructor, parameters.AsReadOnly());
        }

        private static List<Type> GetTypeHierarchy(Type type)
        {
            var hierarchy = new List<Type>();
            for (Type? current = type; current is not null && current != typeof(object); current = current.BaseType)
            {
                hierarchy.Add(current);
            }

            hierarchy.Reverse();
            return hierarchy;
        }

        private static bool TryGetDictionaryArguments(Type type, out Type? keyType, out Type? valueType)
        {
            Type? dictionaryType = FindGenericType(type, typeof(IDictionary<,>));
            if (dictionaryType is null)
            {
                keyType = null;
                valueType = null;
                return false;
            }

            Type[] arguments = dictionaryType.GetGenericArguments();
            keyType = arguments[0];
            valueType = arguments[1];
            return true;
        }

        private static bool TryGetListElementType(Type type, out Type? elementType)
        {
            Type? listType = FindGenericType(type, typeof(IList<>));
            if (listType is null)
            {
                elementType = null;
                return false;
            }

            elementType = listType.GetGenericArguments()[0];
            return true;
        }

        private static Type? FindGenericType(Type type, Type genericDefinition)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == genericDefinition)
            {
                return type;
            }

            return type.GetInterfaces()
                .FirstOrDefault(interfaceType =>
                    interfaceType.IsGenericType &&
                    interfaceType.GetGenericTypeDefinition() == genericDefinition);
        }

        private static void ValidateTypeShape(Type type)
        {
            if (type.ContainsGenericParameters || type.IsPointer || type.IsByRef || type == typeof(void))
            {
                throw Unsupported(type, "Open generic, pointer, by-ref, and void types are not supported.");
            }

            if (IsDelegate(type) || IsTuple(type))
            {
                throw Unsupported(type, "Delegates and tuple types require an explicit converter.");
            }
        }

        private static void ValidateReferencedType(Type type)
        {
            ValidateTypeShape(type);

            if (ScalarTypes.Contains(type) || type.IsEnum)
            {
                return;
            }

            Type? nullableType = Nullable.GetUnderlyingType(type);
            if (nullableType is not null)
            {
                ValidateReferencedType(nullableType);
                return;
            }

            if (type.IsArray)
            {
                if (type.GetArrayRank() != 1)
                {
                    throw Unsupported(type, "Only one-dimensional arrays are supported.");
                }

                ValidateReferencedType(type.GetElementType()!);
                return;
            }

            if (TryGetDictionaryArguments(type, out Type? keyType, out Type? valueType))
            {
                if (keyType != typeof(string))
                {
                    throw Unsupported(type, "Dictionary keys must be strings unless a converter is registered.");
                }

                ValidateReferencedType(valueType!);
                return;
            }

            if (TryGetListElementType(type, out Type? elementType))
            {
                ValidateReferencedType(elementType!);
                return;
            }

            if (typeof(IEnumerable).IsAssignableFrom(type))
            {
                throw Unsupported(type, "The collection shape is not supported by the core serializer.");
            }

            if (type == typeof(object) || type.IsInterface || type.IsAbstract)
            {
                throw Unsupported(type, "Interfaces, abstract classes, and System.Object require an explicit converter or concrete type.");
            }
        }

        private static bool IsDelegate(Type type) => typeof(Delegate).IsAssignableFrom(type);

        private static bool IsTuple(Type type)
        {
            if (!type.IsGenericType)
            {
                return false;
            }

            string? name = type.GetGenericTypeDefinition().FullName;
            return name is not null &&
                   (name.StartsWith("System.Tuple`", StringComparison.Ordinal) ||
                    name.StartsWith("System.ValueTuple`", StringComparison.Ordinal));
        }

        private static bool IsIncluded(MemberInfo member) =>
            member.IsDefined(typeof(JsonIncludeAttribute), inherit: true);

        private static bool IsIgnored(MemberInfo member) =>
            member.IsDefined(typeof(JsonIgnoreAttribute), inherit: true);

        private static string GetJsonName(MemberInfo member) =>
            member.GetCustomAttribute<JsonPropertyNameAttribute>(inherit: true)?.Name ?? member.Name;

        private static TypeContractException Unsupported(Type type, string reason) =>
            new(type, $"Type '{type}' is not supported. {reason}");

        private sealed class MemberCandidate
        {
            internal string JsonName { get; }
            internal Type MemberType { get; }
            internal MemberInfo MemberInfo { get; }
            internal bool CanSerialize { get; }
            internal bool CanDeserialize { get; }
            internal int DeclaringTypeOrder { get; }

            internal MemberCandidate(
                string jsonName,
                Type memberType,
                MemberInfo memberInfo,
                bool canSerialize,
                bool canDeserialize,
                int declaringTypeOrder)
            {
                JsonName = jsonName;
                MemberType = memberType;
                MemberInfo = memberInfo;
                CanSerialize = canSerialize;
                CanDeserialize = canDeserialize;
                DeclaringTypeOrder = declaringTypeOrder;
            }
        }
    }
}
