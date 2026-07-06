using MemoryPack;

/// <summary>
/// 패킷 바디 안에서 재사용되는 데이터 타입(Info)들을 모아두는 파일.
/// - 패킷이 아니므로 [Packet(...)]·IPacket 은 붙이지 않는다.
/// - 단, MemoryPack 이 직렬화하려면 반드시 [MemoryPackable] partial 이어야 한다.
/// </summary>

namespace MikaProtocol
{
    // 인벤토리 아이템 한 칸 (item_id, count)
    [MemoryPackable]
    public partial class ItemInfo
    {
        public int ItemId { get; set; }
        public int Count { get; set; }
    }
    
    [MemoryPackable]
    public partial class ItemChangeInfo   // 변경(델타) 전용
    {
        public int ItemId { get; set; }
        public int Count  { get; set; }
        public EItemChangeKind Kind { get; set; }
    }
}
