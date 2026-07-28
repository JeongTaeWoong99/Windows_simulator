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

    // 가챠로 뽑힌 결과 1건 (인벤토리 누적 수량이 아닌 "이번에 획득한 것")
    [MemoryPackable]
    public partial class GachaRewardInfo
    {
        public int ItemId { get; set; }
        public int Count  { get; set; }        // 이번에 획득한 수량
        public EItemRarity Rarity { get; set; } // 연출용 등급
    }

    /// <summary>
    /// 작업슬롯 한 칸의 상태.
    /// <c>LastTickAtUnix</c>는 클라이언트가 <b>다음 채취까지 남은 시간을 로컬에서 계산</b>하라고 준다.
    /// 그 카운트다운은 연출일 뿐이고, 실제로 몇 개가 나왔는지는 서버가 정한다.
    /// </summary>
    [MemoryPackable]
    public partial class WorkStationSlotInfo
    {
        public int  SlotIndex      { get; set; }
        public byte Industry       { get; set; }  // GameData.ItemType (0=미지정)
        public long CharacterId    { get; set; }  // 0=비어 있음 (채취하지 않는다)
        public long LastTickAtUnix { get; set; }  // 마지막 정산 시각 (Unix epoch 초, UTC)
    }
}
