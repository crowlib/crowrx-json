using System;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace CrowRx.Json
{
    public sealed class JsonSerializerOptions
    {
        private readonly IReadOnlyDictionary<Type, JsonConverterAdapter> _converters;
        private readonly IReadOnlyDictionary<Type, JsonActivatorAdapter> _activators;
        private readonly ICustomActivator? _legacyActivator;
        private readonly ConcurrentDictionary<Type, TypeContract> _relaxedContracts = new();

        public static JsonSerializerOptions Default { get; } = new JsonSerializerOptions();

        public bool PropertyNameCaseInsensitive { get; }
        public bool RejectUnknownMembers { get; }
        public bool AllowNumericEnums { get; }

        public JsonSerializerOptions(
            bool propertyNameCaseInsensitive = false,
            bool rejectUnknownMembers = false,
            bool allowNumericEnums = false)
            : this(
                propertyNameCaseInsensitive,
                rejectUnknownMembers,
                allowNumericEnums,
                new Dictionary<Type, JsonConverterAdapter>(),
                new Dictionary<Type, JsonActivatorAdapter>(),
                null)
        {
        }

        private JsonSerializerOptions(
            bool propertyNameCaseInsensitive,
            bool rejectUnknownMembers,
            bool allowNumericEnums,
            IReadOnlyDictionary<Type, JsonConverterAdapter> converters,
            IReadOnlyDictionary<Type, JsonActivatorAdapter> activators,
            ICustomActivator? legacyActivator)
        {
            PropertyNameCaseInsensitive = propertyNameCaseInsensitive;
            RejectUnknownMembers = rejectUnknownMembers;
            AllowNumericEnums = allowNumericEnums;
            _converters = converters;
            _activators = activators;
            _legacyActivator = legacyActivator;
        }

        public JsonSerializerOptions WithConverter<T>(IJsonConverter<T> converter)
        {
            if (converter is null)
            {
                throw new ArgumentNullException(nameof(converter));
            }

            if (_converters.ContainsKey(typeof(T)))
            {
                throw new InvalidOperationException($"A converter for type '{typeof(T)}' is already registered in these options.");
            }

            var converters = new Dictionary<Type, JsonConverterAdapter>(_converters)
            {
                [typeof(T)] = new JsonConverterAdapter<T>(converter),
            };
            return Copy(converters: converters);
        }

        public JsonSerializerOptions WithActivator<T>(IJsonActivator<T> activator)
        {
            if (activator is null)
            {
                throw new ArgumentNullException(nameof(activator));
            }

            if (_activators.ContainsKey(typeof(T)))
            {
                throw new InvalidOperationException($"An activator for type '{typeof(T)}' is already registered in these options.");
            }

            var activators = new Dictionary<Type, JsonActivatorAdapter>(_activators)
            {
                [typeof(T)] = new JsonActivatorAdapter<T>(activator),
            };
            return Copy(activators: activators);
        }

        internal JsonSerializerOptions WithLegacyActivator(ICustomActivator activator) =>
            new JsonSerializerOptions(
                PropertyNameCaseInsensitive,
                RejectUnknownMembers,
                AllowNumericEnums,
                _converters,
                _activators,
                activator ?? throw new ArgumentNullException(nameof(activator)));

        internal bool TryGetConverter(Type type, out JsonConverterAdapter converter)
        {
            if (_converters.TryGetValue(type, out converter!))
            {
                return true;
            }

            return BuiltInJsonConverters.TryGet(type, out converter!);
        }

        internal TypeContract GetContract(TypeContractCache cache, Type type)
        {
            try
            {
                return cache.GetContract(type);
            }
            catch (TypeContractException) when (_converters.Count > 0)
            {
                return _relaxedContracts.GetOrAdd(
                    type,
                    static contractType => TypeContractBuilder.Build(contractType, validateReferencedTypes: false));
            }
        }

        internal bool TryCreate(Type type, Dictionary<string, object?>? properties, out object? value)
        {
            if (_activators.TryGetValue(type, out JsonActivatorAdapter? activator))
            {
                value = activator.Create();
                return value is not null && type.IsInstanceOfType(value);
            }

            if (_legacyActivator is not null)
            {
                var legacyProperties = new Dictionary<string, object>();
                if (properties is not null)
                {
                    foreach (KeyValuePair<string, object?> property in properties)
                    {
                        legacyProperties[property.Key] = property.Value!;
                    }
                }

                value = properties is null
                    ? _legacyActivator.CreateInstance(type)
                    : _legacyActivator.CreateInstance(type, legacyProperties);
                return value is not null && type.IsInstanceOfType(value);
            }

            value = null;
            return false;
        }

        internal bool HasActivator(Type type) =>
            _activators.ContainsKey(type) || _legacyActivator is not null;

        private JsonSerializerOptions Copy(
            IReadOnlyDictionary<Type, JsonConverterAdapter>? converters = null,
            IReadOnlyDictionary<Type, JsonActivatorAdapter>? activators = null) =>
            new JsonSerializerOptions(
                PropertyNameCaseInsensitive,
                RejectUnknownMembers,
                AllowNumericEnums,
                converters ?? _converters,
                activators ?? _activators,
                _legacyActivator);
    }

    internal abstract class JsonConverterAdapter
    {
        internal abstract string Write(object value);
        internal abstract object? Read(string json);
    }

    internal sealed class JsonConverterAdapter<T> : JsonConverterAdapter
    {
        private readonly IJsonConverter<T> _converter;

        internal JsonConverterAdapter(IJsonConverter<T> converter) => _converter = converter;

        internal override string Write(object value) => _converter.Write((T)value);

        internal override object? Read(string json) => _converter.Read(json);
    }

    internal abstract class JsonActivatorAdapter
    {
        internal abstract object? Create();
    }

    internal sealed class JsonActivatorAdapter<T> : JsonActivatorAdapter
    {
        private readonly IJsonActivator<T> _activator;

        internal JsonActivatorAdapter(IJsonActivator<T> activator) => _activator = activator;

        internal override object? Create() => _activator.Create();
    }
}
