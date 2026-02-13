using System;
using System.Collections.Generic;


namespace CrowRx.Json
{
    public interface ICustomActivator
    {
        object CreateInstance(Type type);
        object CreateInstance(Type type, Dictionary<string, object> jsonDictionary);
    }
}