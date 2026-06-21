namespace CrowRx.Json
{
    /// <summary>Creates an instance that the deserializer will populate.</summary>
    /// <typeparam name="T">The exact C# type created by the activator.</typeparam>
    public interface IJsonActivator<T>
    {
        T Create();
    }
}
