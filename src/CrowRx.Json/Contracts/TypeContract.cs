using System;
using System.Collections.Generic;
using System.Reflection;

namespace CrowRx.Json
{
    internal sealed class TypeContract
    {
        internal Type Type { get; }
        internal TypeContractKind Kind { get; }
        internal Type? UnderlyingType { get; }
        internal Type? ElementType { get; }
        internal Type? KeyType { get; }
        internal Type? ValueType { get; }
        internal IReadOnlyList<MemberContract> Members { get; }
        internal ConstructorInfo? ExplicitConstructor { get; }
        internal ConstructorContract? Constructor { get; }

        internal TypeContract(
            Type type,
            TypeContractKind kind,
            Type? underlyingType = null,
            Type? elementType = null,
            Type? keyType = null,
            Type? valueType = null,
            IReadOnlyList<MemberContract>? members = null,
            ConstructorInfo? explicitConstructor = null,
            ConstructorContract? constructor = null)
        {
            Type = type;
            Kind = kind;
            UnderlyingType = underlyingType;
            ElementType = elementType;
            KeyType = keyType;
            ValueType = valueType;
            Members = members ?? Array.Empty<MemberContract>();
            ExplicitConstructor = explicitConstructor;
            Constructor = constructor;
        }
    }
}
