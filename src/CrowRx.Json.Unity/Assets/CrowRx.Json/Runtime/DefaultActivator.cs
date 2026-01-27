using System;
using System.Collections.Generic;


namespace CrowRx.Json
{
    internal class DefaultActivator : ICustomActivator
    {
        public object CreateInstance(Type type) => Activator.CreateInstance(type);
        public object CreateInstance(Type type, Dictionary<string, object> jsonDictionary) => Activator.CreateInstance(type);
    }
}