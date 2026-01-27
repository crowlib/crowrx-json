namespace CrowRx.Json
{
    public static class Serializer
    {
        internal static string ToJson(object obj) => MiniJSON.Json.Serialize(obj);
    }
}