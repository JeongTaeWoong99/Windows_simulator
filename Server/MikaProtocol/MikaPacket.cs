using System;
using System.Collections.Generic;
using MemoryPack;

/// <summary>
/// 1. 패킷은 반드시 ushort인 id, size를 포함해야 함.
/// 2. ([id][size][---body---]) 이렇게 이루어진 byte array를 TCP로 송수신 함
/// 3. id, size는 먼저 body를 serialize한 후, size를 측정하여 앞 비트에 써넣는 방식을 사용하며 
/// 4. body부분은 MemoryPack 등으로 Serialize/Deserialize 한다.
/// </summary>
///

namespace MikaProtocol
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class PacketAttribute : Attribute
    {
        public PacketId Id { get;}
        public PacketAttribute(PacketId id)
        {
            Id = id;
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class PacketHandlerAttribute : Attribute { }


    public enum PacketId : ushort
    {
        None = 0,
        C_EchoRequest = 1,
        S_EchoResponse = 2,
        C_PingRequest = 3,
        S_PongResponse = 4,
        C_LoginRequest = 5,
        S_LoginResponse = 6,
        C_AddItemRequest = 7,
        S_UpdateItemResponse = 8,
    }

    [MemoryPackable, Packet(PacketId.C_EchoRequest)]
    public partial class C_EchoRequest : IPacket
    {
        public string Message { get; set; } = "";
    }

    [MemoryPackable, Packet(PacketId.S_EchoResponse)]
    public partial class S_EchoResponse : IPacket
    {
        public string Message { get; set; } = "";
    }

    [MemoryPackable, Packet(PacketId.C_PingRequest)]
    public partial class C_PingRequest : IPacket
    {
        
    }
    
    [MemoryPackable, Packet(PacketId.S_PongResponse)]
    public partial class S_PongResponse : IPacket
    {

    }

    [MemoryPackable, Packet(PacketId.C_LoginRequest)]
    public partial class C_LoginRequest : IPacket
    {
        public string Id { get; set; } = "";
    }

    [MemoryPackable, Packet(PacketId.S_LoginResponse)]
    public partial class S_LoginResponse : IPacket
    {
        public bool Success { get; set; }
        public long SessionId { get; set; }
    }

    [MemoryPackable, Packet(PacketId.C_AddItemRequest)]
    public partial class C_AddItemRequest : IPacket
    {
        public int ItemId { get; set; }
        public int Count { get; set; }
    }

    [MemoryPackable, Packet(PacketId.S_UpdateItemResponse)]
    public partial class S_UpdateItemResponse : IPacket
    {
        public List<ItemInfo>? Items { get; set; }
    }



}