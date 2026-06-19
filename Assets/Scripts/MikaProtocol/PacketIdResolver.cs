using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace MikaProtocol
{

    public static class PacketIdResolver
    {
        static readonly ConcurrentDictionary<Type, ushort> _cache = new();

        public static ushort Get(Type t) => _cache.GetOrAdd(t, type =>
        {
            var attr = type.GetCustomAttribute<PacketAttribute>()
                       ?? throw new InvalidOperationException($"{type.Name}에 [Packet]이 없습니다");
            return (ushort)attr.Id;
        });
    }
}