using System;

namespace CrowRx.Json
{
    public sealed class JsonException : Exception
    {
        public JsonError Error { get; }

        internal JsonException(JsonError error)
            : base(error.ToString())
        {
            Error = error;
        }
    }
}
