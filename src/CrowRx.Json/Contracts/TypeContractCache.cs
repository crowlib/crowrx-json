using System;
using System.Collections.Concurrent;

namespace CrowRx.Json
{
    internal sealed class TypeContractCache
    {
        internal static TypeContractCache Shared { get; } = new TypeContractCache();

        private readonly ConcurrentDictionary<Type, TypeContract> _contracts = new();
        private readonly object _buildLock = new();

        internal TypeContract GetContract(Type type)
        {
            if (type is null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (_contracts.TryGetValue(type, out TypeContract? existing))
            {
                return existing;
            }

            lock (_buildLock)
            {
                if (_contracts.TryGetValue(type, out existing))
                {
                    return existing;
                }

                TypeContract contract = TypeContractBuilder.Build(type);
                _contracts[type] = contract;
                return contract;
            }
        }
    }
}
