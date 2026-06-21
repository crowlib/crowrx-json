namespace CrowRx.Json
{
    /// <summary>Converts a value to and from one complete JSON value.</summary>
    /// <typeparam name="T">The exact C# type handled by the converter.</typeparam>
    public interface IJsonConverter<T>
    {
        string Write(T value);

        T? Read(string json);
    }
}
