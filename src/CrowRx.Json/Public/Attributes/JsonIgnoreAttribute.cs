using System;

namespace CrowRx.Json
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class JsonIgnoreAttribute : Attribute
    {
    }
}
