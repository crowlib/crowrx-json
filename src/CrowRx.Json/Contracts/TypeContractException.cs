using System;

namespace CrowRx.Json
{
    internal sealed class TypeContractException : InvalidOperationException
    {
        internal Type TargetType { get; }
        internal JsonErrorCategory Category { get; }

        internal TypeContractException(
            Type targetType,
            string message,
            JsonErrorCategory category = JsonErrorCategory.UnsupportedType)
            : base(message)
        {
            TargetType = targetType;
            Category = category;
        }
    }
}
