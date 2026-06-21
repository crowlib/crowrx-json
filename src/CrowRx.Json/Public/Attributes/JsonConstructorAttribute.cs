using System;

namespace CrowRx.Json
{
    [AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
    public sealed class JsonConstructorAttribute : Attribute
    {
    }
}
