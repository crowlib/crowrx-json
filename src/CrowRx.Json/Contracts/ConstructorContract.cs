using System;
using System.Collections.Generic;
using System.Reflection;

namespace CrowRx.Json
{
    internal sealed class ConstructorContract
    {
        internal ConstructorInfo Constructor { get; }
        internal IReadOnlyList<ConstructorParameterContract> Parameters { get; }

        internal ConstructorContract(ConstructorInfo constructor, IReadOnlyList<ConstructorParameterContract> parameters)
        {
            Constructor = constructor ?? throw new ArgumentNullException(nameof(constructor));
            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        }
    }

    internal sealed class ConstructorParameterContract
    {
        internal string JsonName { get; }
        internal Type ParameterType { get; }
        internal ParameterInfo ParameterInfo { get; }
        internal MemberContract Member { get; }
        internal bool HasDefaultValue { get; }
        internal object? DefaultValue { get; }

        internal ConstructorParameterContract(string jsonName, ParameterInfo parameterInfo, MemberContract member)
        {
            JsonName = jsonName;
            ParameterInfo = parameterInfo;
            ParameterType = parameterInfo.ParameterType;
            Member = member;
            HasDefaultValue = parameterInfo.HasDefaultValue;
            DefaultValue = parameterInfo.HasDefaultValue ? parameterInfo.DefaultValue : null;
        }
    }
}
