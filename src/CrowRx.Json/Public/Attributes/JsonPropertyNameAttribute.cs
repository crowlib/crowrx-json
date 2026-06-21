using System;

namespace CrowRx.Json
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class JsonPropertyNameAttribute : Attribute
    {
        public string Name { get; }

        public JsonPropertyNameAttribute(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }
    }
}
