using System;
using System.Reflection;

namespace CrowRx.Json
{
    internal sealed class MemberContract
    {
        internal string JsonName { get; }
        internal Type MemberType { get; }
        internal MemberInfo MemberInfo { get; }
        internal bool CanSerialize { get; }
        internal bool CanDeserialize { get; }

        internal MemberContract(
            string jsonName,
            Type memberType,
            MemberInfo memberInfo,
            bool canSerialize,
            bool canDeserialize)
        {
            JsonName = jsonName;
            MemberType = memberType;
            MemberInfo = memberInfo;
            CanSerialize = canSerialize;
            CanDeserialize = canDeserialize;
        }
    }
}
