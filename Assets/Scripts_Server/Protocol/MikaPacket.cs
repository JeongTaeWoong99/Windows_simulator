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
        C_GachaDrawRequest = 9,
        S_GachaDrawResponse = 10,
        S_InventoryResponse = 11,
        C_WorkStationAssignRequest = 12,
        S_WorkStationAssignResponse = 13,
        S_WorkStationSlotsResponse = 14,
        S_GatherResultResponse = 15,
        S_CurrencyResponse = 16,
        S_CharacterListResponse = 17,
        S_WorkStationSlotSyncResponse = 18,
        C_ItemSellRequest = 19,
        S_ItemSellResponse = 20,
        S_CharacterSyncResponse = 21,
        C_CheatRequest = 22,
        S_CheatResponse = 23,
        C_UnlockRequest = 24,
        S_UnlockResponse = 25,
        S_UnlockListResponse = 26,
        S_EquipListResponse = 27,
        S_EquipSyncResponse = 28,
        C_EquipRequest = 29,
        C_UnequipRequest = 30,
        S_EquipResponse = 31,
        // 32·33은 옛 적성 찍기(C_AptitudeUpRequest·S_AptitudeUpResponse)의 결번이다 — 2026-10-06 철거(이슈 #35). 재사용하지 않는다
        C_UserTraitLearnRequest = 34,
        S_UserTraitLearnResponse = 35,
        S_AccountLevelResponse = 36,
        C_ItemUseRequest = 37,
        S_ItemUseResponse = 38,
        S_MailListResponse = 39,
        S_MailArrivedResponse = 40,
        C_MailClaimRequest = 41,
        S_MailClaimResponse = 42,
        C_MailDeleteRequest = 43,
        S_MailDeleteResponse = 44,
        C_EquipEnchantRequest = 45,
        S_EquipEnchantResponse = 46,
        C_AuctionSearchRequest = 47,
        S_AuctionSearchResponse = 48,
        C_AuctionRegisterRequest = 49,
        S_AuctionRegisterResponse = 50,
        C_AuctionBuyRequest = 51,
        S_AuctionBuyResponse = 52,
        C_AuctionCancelRequest = 53,
        S_AuctionCancelResponse = 54,
        C_AuctionMyListingsRequest = 55,
        S_AuctionMyListingsResponse = 56,
        C_MarketItemsRequest = 57,
        S_MarketItemsResponse = 58,
        C_MarketPriceRequest = 59,
        S_MarketPriceResponse = 60,
        C_MarketBuyRequest = 61,
        S_MarketBuyResponse = 62,
        C_EntitySellRequest = 63,
        S_EntitySellResponse = 64,
        C_StorageSortRequest = 65,
        C_StorageMoveSlotRequest = 66,
        S_StorageSlotsResponse = 67,
        S_UserTraitListResponse = 68,
        S_ServerTimeResponse = 69,
        C_CharacterCondenseRequest = 70,
        S_CharacterCondenseResponse = 71,
        C_EquipEnchantChooseRequest = 72,
        S_EquipEnchantChooseResponse = 73,
        S_ArtifactListResponse = 74,
        C_EntityLockRequest = 75,
        S_EntityLockResponse = 76,
        C_MarketPriceHistoryRequest = 77,
        S_MarketPriceHistoryResponse = 78,
        C_AuctionHistoryRequest = 79,
        S_AuctionHistoryResponse = 80,
        C_WarehouseTransferRequest = 81,
        S_WarehouseTransferResponse = 82,
        C_EquipDisassembleRequest = 83,
        S_EquipDisassembleResponse = 84,
        C_StorageMergeRequest = 85,
        C_WarehouseStackRequest = 86,
        S_ArtifactInventoryResponse = 87,
        C_ArtifactEquipRequest = 88,
        S_ArtifactEquipResponse = 89,
        C_ArtifactActiveRequest = 90,
        S_ArtifactActiveResponse = 91,
        C_ArtifactSellRequest = 92,
        S_ArtifactSellResponse = 93,
        C_ShopBuyRequest = 94,
        S_ShopBuyResponse = 95,
        S_BuildingListResponse = 96,
        C_BuildingUpgradeRequest = 97,
        S_BuildingUpgradeResponse = 98,
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
        public EResultCode Result { get; set; }
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
        public EResultCode Result { get; set; }
        public List<ItemChangeInfo>? ItemChangeInfos { get; set; }
    }

    [MemoryPackable, Packet(PacketId.C_GachaDrawRequest)]
    public partial class C_GachaDrawRequest : IPacket
    {
        public int GachaId { get; set; }    // 뽑을 풀 ID
        public int DrawCount { get; set; }  // 1(단차) 또는 10(10연차)
    }

    [MemoryPackable, Packet(PacketId.S_GachaDrawResponse)]
    public partial class S_GachaDrawResponse : IPacket
    {
        public EResultCode Result { get; set; }
        public List<GachaRewardInfo>? Rewards { get; set; }  // 연출용 — 뽑힌 순서대로 (델타)
        public List<ItemChangeInfo>? ItemChangeInfos { get; set; }  // 인벤토리 반영용 — 갱신 후 누적 총량
    }

    [MemoryPackable, Packet(PacketId.S_InventoryResponse)]
    public partial class S_InventoryResponse : IPacket
    {
        public List<ItemInfo>? Items { get; set; }  // 로그인 시 인벤토리 전체 스냅샷
        public List<int>? LockedItemTids { get; set; }  // 잠긴 자원 TID (T-159). 보유 0개인 종류도 실린다
    }

    // ───────────────────── 작업슬롯 (WorkStationSlot) ─────────────────────

    [MemoryPackable, Packet(PacketId.C_WorkStationAssignRequest)]
    public partial class C_WorkStationAssignRequest : IPacket
    {
        public int  SlotIndex     { get; set; }  // 배치할 슬롯 번호
        public EIndustryType Industry { get; set; }  // 지정 산업 (None=해제)
        public byte IndustryLevel { get; set; }  // 지정 산업 레벨 (1~). 서버가 해금 여부를 검증한다
        public long CharacterId   { get; set; }  // 배치할 캐릭터 (0=해제)
    }

    [MemoryPackable, Packet(PacketId.S_WorkStationAssignResponse)]
    public partial class S_WorkStationAssignResponse : IPacket
    {
        public EResultCode          Result { get; set; }
        public WorkStationSlotInfo? Slot   { get; set; }  // 변경된 슬롯의 최신 상태
    }

    [MemoryPackable, Packet(PacketId.S_WorkStationSlotsResponse)]
    public partial class S_WorkStationSlotsResponse : IPacket
    {
        public List<WorkStationSlotInfo>? Slots { get; set; }  // 로그인 시 슬롯 전체 스냅샷
    }

    /// <summary>
    /// 채취 결과 푸시. 클라이언트 요청 없이 <b>서버가 판정 후 밀어 준다</b>(서버 권위).
    ///
    /// <para>
    /// 도착 간격은 <b>슬롯마다 다르다</b> — 캐릭터 적성·버프가 슬롯별 채취 속도를 정하기 때문이다.
    /// 서버는 0.1초 해상도로 깨어나 그 시점까지 완성된 판정만 담아 보내므로, 이 패킷이 안 온다고
    /// 진행이 멈춘 것은 아니다. 슬롯 변경 시에는 그때까지의 구간을 한 번에 정산해 보낸다.
    /// </para>
    /// </summary>
    [MemoryPackable, Packet(PacketId.S_GatherResultResponse)]
    public partial class S_GatherResultResponse : IPacket
    {
        public int                   SlotIndex     { get; set; }  // 어느 슬롯에서 나왔는지
        public int                   JudgeCount    { get; set; }  // 이번에 정산된 판정 횟수(연출용)
        public List<ItemChangeInfo>? ItemChanges   { get; set; }  // 인벤토리 갱신분
        public bool                  InventoryFull { get; set; }  // 칸이 없거나 최대 수량이라 버린 몫이 있다 — "가득 차서 획득 불가" (T-157)
    }

    /// <summary>
    /// 슬롯 1칸의 최신 상태를 밀어 준다. 정산·속도 변경 등 <b>슬롯 상태가 바뀔 때마다</b> 보낸다.
    /// 클라이언트는 이 패킷으로 카운트다운 기준점을 매번 교정한다.
    /// </summary>
    [MemoryPackable, Packet(PacketId.S_WorkStationSlotSyncResponse)]
    public partial class S_WorkStationSlotSyncResponse : IPacket
    {
        public WorkStationSlotInfo? Slot { get; set; }
    }

    // ───────────────────────── 재화 (Currency) ─────────────────────────

    /// <summary>
    /// 재화 보유량 통지. <b>로그인 스냅샷과 변경 푸시가 같은 패킷을 쓴다.</b>
    ///
    /// <para>
    /// 아이템처럼 스냅샷/델타 패킷을 나누지 않는 이유는 값이 증감이 아니라
    /// <b>확정된 잔액</b>이기 때문이다. 클라이언트는 두 경우 모두 <b>덮어쓰기</b>만 하면 되므로
    /// 처리 경로가 하나로 끝난다. 재화는 지금 골드 하나다 — 다이아는 폐지했다(2026-09-28).
    /// </para>
    ///
    /// <para>
    /// 재화가 늘면 <b>패킷이 아니라 필드를 추가한다</b> — DB(t_user_currency)도 행이 아니라
    /// 컬럼으로 늘리는 구조라 축을 맞춘 것이다.
    /// </para>
    /// </summary>
    [MemoryPackable, Packet(PacketId.S_CurrencyResponse)]
    public partial class S_CurrencyResponse : IPacket
    {
        // 보유량. int로 받지 말 것 — 거래 경제에서 21억을 넘길 수 있다
        public long Gold { get; set; }
    }

    // ───────────────────────── 캐릭터 (Character) ─────────────────────────

    /// <summary>
    /// 보유 캐릭터 전체 스냅샷(로그인 시). 클라이언트는 여기서 받은 <c>CharacterId</c>로
    /// <see cref="C_WorkStationAssignRequest"/>의 배치 대상을 지정한다 —
    /// 이 패킷 없이는 클라이언트가 자기 캐릭터의 개체 PK를 알 방법이 없다.
    /// </summary>
    [MemoryPackable, Packet(PacketId.S_CharacterListResponse)]
    public partial class S_CharacterListResponse : IPacket
    {
        public List<CharacterInfo>? Characters { get; set; }
    }

    /// <summary>
    /// 캐릭터 1개체의 최신 상태를 밀어 준다. 판정 정산으로 레벨·경험치가 바뀔 때마다 보낸다.
    /// 목록 스냅샷과 같은 <c>CharacterInfo</c>라 클라이언트는 <c>CharacterId</c>로 찾아 덮어쓰면 된다.
    /// </summary>
    [MemoryPackable, Packet(PacketId.S_CharacterSyncResponse)]
    public partial class S_CharacterSyncResponse : IPacket
    {
        public CharacterInfo? Character { get; set; }
    }

    // ───────────────────────── 치트 (Cheat) ─────────────────────────

    /// <summary>
    /// 개발·운영 명령. <b>admin_level ≥ 1인 유저만</b> 통과한다.
    /// 명령별 인자 의미·거절 조건은 <c>Server/docs/치트.md</c> 2장. 결과는 기존 동기화 패킷(재화·인벤·캐릭터·슬롯)으로 온다.
    /// </summary>
    [MemoryPackable, Packet(PacketId.C_CheatRequest)]
    public partial class C_CheatRequest : IPacket
    {
        public ECheatCommand Command { get; set; }
        public long          Arg1    { get; set; }
        public long          Arg2    { get; set; }
    }

    [MemoryPackable, Packet(PacketId.S_CheatResponse)]
    public partial class S_CheatResponse : IPacket
    {
        public EResultCode   Result  { get; set; }
        public ECheatCommand Command { get; set; }
        public string        Message { get; set; } = "";   // 로그용 한 줄. 클라 로직이 읽지 않는다
    }

    /// <summary>
    /// 서버의 "지금"(게임 시계 기준). 로그인 직후와 시간 치트(<c>AdvanceTime</c>·<c>ResetTime</c>) 직후에 온다.
    /// 클라는 <c>ServerNowUnixMs - 내 PC 시각</c>을 들고 남은 시간·진행도를 그린다 — PC 시계가 틀려도 함께 맞는다.
    /// </summary>
    [MemoryPackable, Packet(PacketId.S_ServerTimeResponse)]
    public partial class S_ServerTimeResponse : IPacket
    {
        public long ServerNowUnixMs { get; set; }
    }

    // ───────────────────────── 해금 (Unlock) ─────────────────────────

    /// <summary>
    /// 해금 요청. <b>조건을 채웠어도 이 요청을 보내야 열린다</b> — 해금은 유저의 행동이다.
    /// 판정 순서·조건은 <c>GameDesign/design/unlock/README.md</c> 2.1. 조건 검사는 서버가 다시 한다.
    /// </summary>
    [MemoryPackable, Packet(PacketId.C_UnlockRequest)]
    public partial class C_UnlockRequest : IPacket
    {
        public int           UnlockTID { get; set; }  // UnlockTable 행
        public ECurrencyType Currency  { get; set; }  // 지불 재화. 지불 컬럼이 없는 해금은 무시한다
    }

    /// <summary>
    /// 해금 결과. 열린 뒤 무엇이 달라지는가는 <b>콘텐츠별 기존 패킷</b>이 따로 밀어 준다
    /// (작업슬롯이면 <see cref="S_WorkStationSlotSyncResponse"/>). 서버가 직접 연 경우(퀘스트·치트)도 같은 패킷이 온다.
    /// </summary>
    /// <summary>건물 트리 노드(특성)를 1레벨 올린다. 조건은 앞 노드 최대 레벨 + 그 건물의 남은 포인트다(T-183).</summary>
    [MemoryPackable, Packet(PacketId.C_UserTraitLearnRequest)]
    public partial class C_UserTraitLearnRequest : IPacket
    {
        public int UserTraitTID { get; set; }
    }

    /// <summary>
    /// 노드 레벨업 결과. 남은 포인트는 오지 않는다 — 클라가 건물 레벨 − 찍은 레벨 × <c>TraitPoint</c>로 센다.
    /// 속도 노드면 바뀐 슬롯이 <see cref="S_WorkStationSlotSyncResponse"/>로 따로 온다.
    /// </summary>
    [MemoryPackable, Packet(PacketId.S_UserTraitLearnResponse)]
    public partial class S_UserTraitLearnResponse : IPacket
    {
        public EResultCode Result       { get; set; }
        public int         UserTraitTID { get; set; }
        public int         Level        { get; set; }   // 성공이면 올린 뒤 레벨, 실패면 지금 레벨
    }

    /// <summary>특성 레벨 스냅샷(로그인 직후). 기본 레벨보다 올린 특성만 싣는다 — 없는 특성은 <c>UserTraitTable.BaseLevel</c>이다.</summary>
    [MemoryPackable, Packet(PacketId.S_UserTraitListResponse)]
    public partial class S_UserTraitListResponse : IPacket
    {
        public List<UserTraitInfo> Traits { get; set; } = new();
    }

    /// <summary>아티팩트 도감 스냅샷(T-181) — 장착한 번호와 켜짐 여부. 로그인 직후와 장착·켜고 끌 때마다 통째로 온다 — 클라는 덮어쓴다.</summary>
    [MemoryPackable, Packet(PacketId.S_ArtifactListResponse)]
    public partial class S_ArtifactListResponse : IPacket
    {
        public List<ArtifactCodexInfo> Artifacts { get; set; } = new();
    }

    /// <summary>
    /// 인벤토리 아티팩트 탭 스냅샷(T-181). 로그인 직후와 칸이 바뀔 때마다(감정 · 우편 수령 · 장착 · 판매 · 경매 등록 · 이동·정렬) 통째로 온다 — 클라는 덮어쓴다.
    /// <c>ItemInfo.ItemId</c>는 아티팩트 번호(ArtifactTID)다. 같은 번호가 <c>Constants.ArtifactMaxStack</c>을 넘으면 칸이 나뉜다.
    /// </summary>
    [MemoryPackable, Packet(PacketId.S_ArtifactInventoryResponse)]
    public partial class S_ArtifactInventoryResponse : IPacket
    {
        public List<ItemInfo> Stacks { get; set; } = new();
    }

    /// <summary>인벤토리의 아티팩트 1개를 도감에 장착한다(T-181). 번호마다 한 번 — 되돌릴 수 없고, 장착하면 켜진 채로 시작한다.</summary>
    [MemoryPackable, Packet(PacketId.C_ArtifactEquipRequest)]
    public partial class C_ArtifactEquipRequest : IPacket
    {
        public int ArtifactTid { get; set; }
    }

    /// <summary>장착 결과. Ok면 <see cref="S_ArtifactInventoryResponse"/>·<see cref="S_ArtifactListResponse"/>가 함께 온다.</summary>
    [MemoryPackable, Packet(PacketId.S_ArtifactEquipResponse)]
    public partial class S_ArtifactEquipResponse : IPacket
    {
        public EResultCode Result      { get; set; }
        public int         ArtifactTid { get; set; }
    }

    /// <summary>폐기 예정(#90) — 장착한 번호는 항상 켜져 있다. 서버는 바꾸지 않고 지금 상태로 답한다. 클라 전환 뒤 지운다.</summary>
    [MemoryPackable, Packet(PacketId.C_ArtifactActiveRequest)]
    public partial class C_ArtifactActiveRequest : IPacket
    {
        public int  ArtifactTid { get; set; }
        public bool IsActive    { get; set; }
    }

    /// <summary>폐기 예정(#90). 장착한 번호면 Ok · IsActive = true, 아니면 ArtifactNotEquipped.</summary>
    [MemoryPackable, Packet(PacketId.S_ArtifactActiveResponse)]
    public partial class S_ArtifactActiveResponse : IPacket
    {
        public EResultCode Result      { get; set; }
        public int         ArtifactTid { get; set; }
        public bool        IsActive    { get; set; }
    }

    /// <summary>
    /// 인벤토리의 아티팩트 즉시 판매(T-181). 판매가는 <c>ArtifactTable.BasePrice</c> × <c>SellRatePermille</c>.
    /// <b>전부 성공하거나 전부 실패한다.</b> 도감에 장착한 것은 인벤토리에 없으니 팔리지 않는다.
    /// </summary>
    [MemoryPackable, Packet(PacketId.C_ArtifactSellRequest)]
    public partial class C_ArtifactSellRequest : IPacket
    {
        public List<ArtifactInfo>? Artifacts { get; set; }  // 팔 번호와 수량(델타)
    }

    /// <summary>판매 결과. Ok면 <see cref="S_ArtifactInventoryResponse"/>가, 골드 잔액은 <see cref="S_CurrencyResponse"/>가 따로 온다.</summary>
    [MemoryPackable, Packet(PacketId.S_ArtifactSellResponse)]
    public partial class S_ArtifactSellResponse : IPacket
    {
        public EResultCode Result     { get; set; }
        public long        GainedGold { get; set; }  // 이번 판매로 번 금액(델타)
    }

    /// <summary>계정 레벨 스냅샷·푸시. 로그인 직후, 경험치가 오를 때, 특성 포인트를 쓸 때 온다(재화와 같은 관례).</summary>
    [MemoryPackable, Packet(PacketId.S_AccountLevelResponse)]
    public partial class S_AccountLevelResponse : IPacket
    {
        public int  Level      { get; set; }
        public long Exp        { get; set; }   // 현재 레벨에서 쌓은 양. 곡선이 캐릭터의 8배라 int를 넘는다
        public int  TraitPoint { get; set; }   // 폐기 예정 — 항상 0. 포인트는 건물마다 따로다(T-183). 클라 전환 뒤 지운다
    }

    [MemoryPackable, Packet(PacketId.S_UnlockResponse)]
    public partial class S_UnlockResponse : IPacket
    {
        public EResultCode Result    { get; set; }
        public int         UnlockTID { get; set; }
    }

    /// <summary>
    /// 열린 해금 전체 목록(로그인 시). <b>작업슬롯 스냅샷보다 먼저 온다</b> —
    /// 클라가 잠긴 칸을 그릴 때 이미 알고 있어야 한다. 조건 문구는 클라가 <c>UnlockTable</c>로 만든다.
    /// </summary>
    [MemoryPackable, Packet(PacketId.S_UnlockListResponse)]
    public partial class S_UnlockListResponse : IPacket
    {
        public List<int> UnlockTIDs { get; set; } = new();
    }

    // ───────────────────────── 장비 (Equip) ─────────────────────────

    /// <summary>보유 장비 전체(로그인 시). 캐릭터 목록 뒤 · 작업슬롯 스냅샷 앞에 온다.</summary>
    [MemoryPackable, Packet(PacketId.S_EquipListResponse)]
    public partial class S_EquipListResponse : IPacket
    {
        public List<EquipInfo> Equips { get; set; } = new();
    }

    /// <summary>바뀐 장비 개체들. 지급·장착·해제·자동 이동(최대 3개)에서 온다. EquipId로 찾아 덮어쓴다 — 확정값이다.</summary>
    [MemoryPackable, Packet(PacketId.S_EquipSyncResponse)]
    public partial class S_EquipSyncResponse : IPacket
    {
        public List<EquipInfo> Equips { get; set; } = new();
    }

    /// <summary>장착 요청. 칸은 클라가 고른다(장신구는 Accessory1·2 어디든). 이미 다른 캐릭터가 착용 중이면 서버가 옮긴다.</summary>
    [MemoryPackable, Packet(PacketId.C_EquipRequest)]
    public partial class C_EquipRequest : IPacket
    {
        public long       CharacterId { get; set; }  // 개체 PK
        public long       EquipId     { get; set; }  // 개체 PK
        public EEquipSlot Slot        { get; set; }
    }

    /// <summary>해제 요청. 그 칸이 비어 있으면 EquipSlotEmpty.</summary>
    [MemoryPackable, Packet(PacketId.C_UnequipRequest)]
    public partial class C_UnequipRequest : IPacket
    {
        public long       CharacterId { get; set; }
        public EEquipSlot Slot        { get; set; }
    }

    /// <summary>장착·해제 결과. 바뀐 개체는 S_EquipSyncResponse가, 속도 변화는 S_WorkStationSlotSyncResponse가 따로 온다.</summary>
    [MemoryPackable, Packet(PacketId.S_EquipResponse)]
    public partial class S_EquipResponse : IPacket
    {
        public EResultCode Result      { get; set; }
        public long        CharacterId { get; set; }
        public EEquipSlot  Slot        { get; set; }
    }

    /// <summary>
    /// 큐브 사용. 인챈트가 없으면 일반으로 시작하고, 있으면 큐브 확률로 한 단계 오른다. 어느 쪽이든 칸 전부를 다시 뽑는다.
    /// 착용 중인 장비는 거절된다(EnchantEquipped): 벗기는 것이 선행 조건이다.
    /// </summary>
    [MemoryPackable, Packet(PacketId.C_EquipEnchantRequest)]
    public partial class C_EquipEnchantRequest : IPacket
    {
        public long EquipId { get; set; }  // 개체 PK
        public int  ItemTid { get; set; }  // 큐브 (EnchantItemTable.ItemTID)
    }

    /// <summary>
    /// 인챈트 결과. <b>Result != Ok(거절)이면 Result·EquipId만 유효하다</b> — 나머지는 기본값이니 그리지 않는다.
    /// Result == Ok면 Success는 등급이 한 단계 올랐는가(첫 부여는 false), Options는 다시 뽑힌 칸 전부다.
    /// AwaitingChoice면(상급 큐브) 장비는 아직 그대로다 — 새 값은 보류됐고 C_EquipEnchantChooseRequest로 고른다.
    /// 바뀐 개체는 S_EquipSyncResponse가 따로 온다.
    /// </summary>
    [MemoryPackable, Packet(PacketId.S_EquipEnchantResponse)]
    public partial class S_EquipEnchantResponse : IPacket
    {
        public EResultCode Result         { get; set; }
        public long        EquipId        { get; set; }
        public bool        Success        { get; set; }
        public int         BeforeGrade    { get; set; }
        public int         AfterGrade     { get; set; }
        public List<int>   Options        { get; set; } = new();
        public bool        AwaitingChoice { get; set; }
    }

    /// <summary>
    /// 상급 큐브의 보류 결과 고르기. KeepNew면 새 값(등급 + 칸 전부)을, 아니면 이전 값을 남긴다.
    /// 보류가 없으면 EnchantNoPending. 보류는 재접속해도 남는다 — EquipInfo.PendingEnchantGrade.
    /// </summary>
    [MemoryPackable, Packet(PacketId.C_EquipEnchantChooseRequest)]
    public partial class C_EquipEnchantChooseRequest : IPacket
    {
        public long EquipId { get; set; }
        public bool KeepNew { get; set; }
    }

    /// <summary>고르기 결과. 바뀐 개체는 S_EquipSyncResponse가 따로 온다.</summary>
    [MemoryPackable, Packet(PacketId.S_EquipEnchantChooseResponse)]
    public partial class S_EquipEnchantChooseResponse : IPacket
    {
        public EResultCode Result  { get; set; }
        public long        EquipId { get; set; }
        public bool        KeepNew { get; set; }
    }

    // ───────────────────────── 상점 (Shop) ─────────────────────────

    /// <summary>
    /// 아이템 즉시 판매 요청. <b>종류 하나가 아니라 목록으로 받는다</b> —
    /// 방치형이라 인벤토리가 저절로 차므로, 일괄 판매가 기본 동선이고 낱개 판매가 그 특수한 경우다.
    /// </summary>
    /// <summary>아이템 사용 — 지금은 상자 개봉뿐이다. 상자의 <c>OpenGachaId</c> 풀을 개수만큼 비용 없이 돈다.</summary>
    [MemoryPackable, Packet(PacketId.C_ItemUseRequest)]
    public partial class C_ItemUseRequest : IPacket
    {
        public int ItemTID { get; set; }
        public int Count   { get; set; }  // 1~99 — 상자 최대 수량과 같다
    }

    /// <summary>
    /// 아이템 사용 결과. <c>Rewards</c>는 가챠와 같은 모양(연출용 · 뽑힌 순서) — 클라 연출을 한 벌로 쓴다.
    /// <c>ItemChangeInfos</c>는 상자 차감과 보상 아이템의 누적 총량. 골드는 <see cref="S_CurrencyResponse"/>, 장비는 <see cref="S_EquipSyncResponse"/>로 따로 온다.
    /// </summary>
    [MemoryPackable, Packet(PacketId.S_ItemUseResponse)]
    public partial class S_ItemUseResponse : IPacket
    {
        public EResultCode            Result          { get; set; }
        public int                    ItemTID         { get; set; }
        public List<GachaRewardInfo>? Rewards         { get; set; }
        public List<ItemChangeInfo>?  ItemChangeInfos { get; set; }
        public bool                   StoredInMail    { get; set; }  // 창고에 안 들어가 보상 전체를 우편으로 보관했다 — ItemChangeInfos엔 상자 차감만 있다
    }

    /// <summary>골드 상점 구매(#78). <b>가격은 서버가 정한다</b> — 클라는 어느 품목(<c>ShopTable.ShopTID</c>)을 몇 개 살지만 보낸다.</summary>
    [MemoryPackable, Packet(PacketId.C_ShopBuyRequest)]
    public partial class C_ShopBuyRequest : IPacket
    {
        public int ShopTID { get; set; }
        public int Count   { get; set; }  // 1~50 — 한 번에 여는 상자 수와 같다(Constants.BoxOpenMax)
    }

    /// <summary>
    /// 구매 결과. <c>ItemChangeInfos</c>는 산 아이템의 누적 총량(칸마다 한 줄).
    /// 골드 잔액은 <see cref="S_CurrencyResponse"/>가 따로 내려간다. 실패하면 아무것도 바뀌지 않는다.
    /// </summary>
    [MemoryPackable, Packet(PacketId.S_ShopBuyResponse)]
    public partial class S_ShopBuyResponse : IPacket
    {
        public EResultCode           Result          { get; set; }
        public int                   ShopTID         { get; set; }
        public List<ItemChangeInfo>? ItemChangeInfos { get; set; }
    }

    [MemoryPackable, Packet(PacketId.C_ItemSellRequest)]
    public partial class C_ItemSellRequest : IPacket
    {
        public List<ItemInfo>? Items { get; set; }  // 팔 종류와 수량. Count는 델타(파는 개수)다
    }

    /// <summary>
    /// 판매 결과. <b>전부 성공하거나 전부 실패한다</b> — 한 종류라도 보유량이 모자라면
    /// 아무것도 팔리지 않는다(<see cref="EResultCode.NotEnoughItem"/>).
    /// 갱신된 골드 잔액은 <see cref="S_CurrencyResponse"/>가 따로 내려간다.
    /// </summary>
    [MemoryPackable, Packet(PacketId.S_ItemSellResponse)]
    public partial class S_ItemSellResponse : IPacket
    {
        public EResultCode Result { get; set; }
        public long GainedGold { get; set; }  // 이번 판매로 번 금액(델타). 잔액이 아니다
        public List<ItemChangeInfo>? ItemChangeInfos { get; set; }  // 갱신 후 누적 총량. 0개는 Kind=Remove
    }

    /// <summary>
    /// 캐릭터·장비 개체 즉시 판매. 아이템(TID·수량 축)과 달리 개체 PK 목록 축이다.
    /// 판매가는 <c>CharacterTable</c>·<c>EquipTable</c>의 <c>BasePrice</c> × <c>SellRatePermille</c>.
    /// </summary>
    [MemoryPackable, Packet(PacketId.C_EntitySellRequest)]
    public partial class C_EntitySellRequest : IPacket
    {
        public List<long>? CharacterIds { get; set; }
        public List<long>? EquipIds     { get; set; }
    }

    /// <summary>
    /// 개체 판매 결과. <b>전부 성공하거나 전부 실패한다.</b> Ok면 요청한 개체가 모두 사라진 것이니 클라가 목록에서 지운다.
    /// 갱신된 골드 잔액은 <see cref="S_CurrencyResponse"/>가 따로 내려간다.
    /// </summary>
    [MemoryPackable, Packet(PacketId.S_EntitySellResponse)]
    public partial class S_EntitySellResponse : IPacket
    {
        public EResultCode Result     { get; set; }
        public long        GainedGold { get; set; }  // 이번 판매로 번 금액(델타). 잔액이 아니다
    }

    /// <summary>
    /// 응축 — 같은 캐릭터 재료를 녹여 대상의 ★을 올린다(캐릭터 기획 5.5). 재료 수에 단계 제한이 없다 —
    /// 누적이 기준을 넘는 만큼 ★이 오르고, 최고 ★을 넘는 몫도 소모된다.
    /// </summary>
    [MemoryPackable, Packet(PacketId.C_CharacterCondenseRequest)]
    public partial class C_CharacterCondenseRequest : IPacket
    {
        public long        CharacterId { get; set; }  // ★을 올릴 개체
        public List<long>? MaterialIds { get; set; }  // 녹일 개체 — 전부 같은 CharacterTid
    }

    /// <summary>응축 결과. Ok면 재료 개체는 모두 사라졌고, <see cref="Character"/>가 올린 개체의 새 값(★ · 누적 재료 수)이다.</summary>
    [MemoryPackable, Packet(PacketId.S_CharacterCondenseResponse)]
    public partial class S_CharacterCondenseResponse : IPacket
    {
        public EResultCode    Result    { get; set; }
        public CharacterInfo? Character { get; set; }
    }

    /// <summary>캐릭터·장비·자원 잠금 켜기/끄기(T-137 · T-159). 잠긴 것은 판매·경매 등록·응축 재료에서 빠진다 — 장착·배치·소비는 그대로 된다.</summary>
    [MemoryPackable, Packet(PacketId.C_EntityLockRequest)]
    public partial class C_EntityLockRequest : IPacket
    {
        public ELockTarget Target { get; set; }
        public long        Id     { get; set; }  // CharacterId · EquipId (개체 PK) · 자원은 아이템 TID
        public bool        Locked { get; set; }  // true = 잠금 · false = 해제
    }

    /// <summary>잠금 결과. Ok면 그 개체의 잠금이 <see cref="Locked"/>로 확정됐다 — 이미 같은 상태여도 Ok다.</summary>
    [MemoryPackable, Packet(PacketId.S_EntityLockResponse)]
    public partial class S_EntityLockResponse : IPacket
    {
        public EResultCode Result { get; set; }
        public ELockTarget Target { get; set; }
        public long        Id     { get; set; }
        public bool        Locked { get; set; }
    }

    /// <summary>우편함 전체 스냅샷(로그인 직후). 받은 우편은 7일 동안 함께 실린다.</summary>
    [MemoryPackable, Packet(PacketId.S_MailListResponse)]
    public partial class S_MailListResponse : IPacket
    {
        public List<MailInfo>? Mails { get; set; }
    }

    /// <summary>접속 중에 새 우편이 들어왔다(운영 발송 · 전체 우편 복사). 목록에 더하기만 하면 된다.</summary>
    [MemoryPackable, Packet(PacketId.S_MailArrivedResponse)]
    public partial class S_MailArrivedResponse : IPacket
    {
        public List<MailInfo>? Mails { get; set; }
    }

    /// <summary>우편 수령. <c>MailId = 0</c>이면 모두 받기 — 오래된 순으로 받다가 창고에 안 들어가는 우편에서 멈춘다.</summary>
    [MemoryPackable, Packet(PacketId.C_MailClaimRequest)]
    public partial class C_MailClaimRequest : IPacket
    {
        public long MailId { get; set; }
    }

    /// <summary>
    /// 수령 결과. 한 통은 <b>전부 받거나 아무것도 안 받는다</b> — 창고가 모자라면 <see cref="EResultCode.StorageFull"/>.
    /// 모두 받기가 중간에 멈춰도 그 전까지 받은 것은 <c>ClaimedMailIds</c>에 실린다.
    /// 재화는 <see cref="S_CurrencyResponse"/>, 캐릭터·장비는 각자의 목록 패킷으로 따로 온다.
    /// </summary>
    [MemoryPackable, Packet(PacketId.S_MailClaimResponse)]
    public partial class S_MailClaimResponse : IPacket
    {
        public EResultCode           Result          { get; set; }
        public List<long>?           ClaimedMailIds  { get; set; }
        public int                   RemainingCount  { get; set; }  // 아직 안 받은 우편 수
        public List<ItemChangeInfo>? ItemChangeInfos { get; set; }  // 받은 아이템의 누적 총량
    }

    /// <summary>받은 우편 삭제. 안 받은 우편은 지울 수 없다(<see cref="EResultCode.MailNotClaimed"/>).</summary>
    [MemoryPackable, Packet(PacketId.C_MailDeleteRequest)]
    public partial class C_MailDeleteRequest : IPacket
    {
        public long MailId { get; set; }
    }

    [MemoryPackable, Packet(PacketId.S_MailDeleteResponse)]
    public partial class S_MailDeleteResponse : IPacket
    {
        public EResultCode Result { get; set; }
        public long        MailId { get; set; }
    }

    /// <summary>
    /// 경매장 검색. 0·빈 목록은 조건 없음. 이름 검색은 클라가 이름을 TID 목록으로 바꿔 <c>Tids</c>에 싣는다.
    /// 정렬은 단가 오름차순 하나 — 다음 페이지는 마지막으로 본 (단가, 매물 ID)를 커서로 보낸다(첫 페이지는 둘 다 0).
    /// </summary>
    [MemoryPackable, Packet(PacketId.C_AuctionSearchRequest)]
    public partial class C_AuctionSearchRequest : IPacket
    {
        public EAuctionKind Kind            { get; set; }
        public int          Category        { get; set; }  // 자원은 ItemType, 장비는 EquipKind. Kind가 None이면 무시
        public List<int>?   Tids            { get; set; }
        public int          MinRarity       { get; set; }
        public int          MaxRarity       { get; set; }
        public int          MinEnchantGrade { get; set; }
        public List<int>?   OptionTids      { get; set; }  // 전부 가진 매물만
        public long         MaxUnitPrice    { get; set; }
        public long         CursorUnitPrice { get; set; }  // (옛) 단가 낮은순의 커서. 새 클라는 CursorSortValue를 쓴다
        public long         CursorListingId { get; set; }
        public int          PageSize        { get; set; }  // 0이면 20, 최대 50

        // ── T-155 ──
        public long                         MinUnitPrice    { get; set; }
        public EAuctionSort                 Sort            { get; set; }
        public EAuctionAttr                 SortAttr        { get; set; }  // Sort = AttrDesc일 때
        public List<AuctionAttrRangeInfo>?  Attrs           { get; set; }  // 전부 만족해야 한다 (최대 16개)
        public long                         CursorSortValue { get; set; }  // 응답의 NextCursorSortValue를 그대로 — 첫 쪽은 0
    }

    [MemoryPackable, Packet(PacketId.S_AuctionSearchResponse)]
    public partial class S_AuctionSearchResponse : IPacket
    {
        public EResultCode               Result   { get; set; }
        public List<AuctionListingInfo>? Listings { get; set; }
        public bool                      HasMore  { get; set; }

        // ── T-155 ──
        public int  TotalCount          { get; set; }  // 조건에 맞는 매물 수 — 쪽과 상관없다. 1000에서 세기를 멈춘다(1000이면 "1,000건 이상")
        public long NextCursorSortValue { get; set; }  // 다음 쪽을 물을 때 CursorSortValue · CursorListingId로 그대로
        public long NextCursorListingId { get; set; }
    }

    /// <summary>
    /// 경매 등록. 자원은 <c>ItemTid</c>·<c>Count</c>, 장비는 <c>EquipId</c>를 채운다(장비는 수량 1).
    /// 등록비는 등록 순간 빠지고, 만료로 돌아오면 환급된다(취소는 환급 없음).
    /// </summary>
    [MemoryPackable, Packet(PacketId.C_AuctionRegisterRequest)]
    public partial class C_AuctionRegisterRequest : IPacket
    {
        public EAuctionKind Kind      { get; set; }
        public int          ItemTid   { get; set; }
        public int          Count     { get; set; }
        public long         EquipId   { get; set; }
        public long         UnitPrice { get; set; }
        public long         CharacterId { get; set; }  // Kind = Character일 때. 수량은 1
        // Kind = Artifact면 ItemTid에 아티팩트 번호(ArtifactTID)를 싣는다 — 인벤토리 아티팩트 탭에서 Count만큼 빠진다 (T-152 · T-181)
    }

    /// <summary>
    /// 등록 결과. 아이템은 이미 창고에서 빠졌다 — 자원은 <c>ItemChangeInfos</c>, 장비는 <c>EquipId</c>, 캐릭터는 <c>CharacterId</c>를 창고에서 지운다.
    /// 골드 잔액은 <see cref="S_CurrencyResponse"/>가 따로 온다.
    /// </summary>
    [MemoryPackable, Packet(PacketId.S_AuctionRegisterResponse)]
    public partial class S_AuctionRegisterResponse : IPacket
    {
        public EResultCode           Result          { get; set; }
        public long                  ListingId       { get; set; }
        public long                  ListingFee      { get; set; }
        public List<ItemChangeInfo>? ItemChangeInfos { get; set; }
        public long                  EquipId         { get; set; }
        public long                  CharacterId     { get; set; }
    }

    /// <summary>즉시구매. 매물은 통째로만 산다. <c>ExpectedTotalPrice</c>가 지금 가격과 다르면 거절된다(AuctionPriceChanged).</summary>
    [MemoryPackable, Packet(PacketId.C_AuctionBuyRequest)]
    public partial class C_AuctionBuyRequest : IPacket
    {
        public long ListingId          { get; set; }
        public long ExpectedTotalPrice { get; set; }
    }

    /// <summary>구매 결과. 산 아이템은 우편으로 온다(<see cref="S_MailArrivedResponse"/>), 골드는 <see cref="S_CurrencyResponse"/>.</summary>
    [MemoryPackable, Packet(PacketId.S_AuctionBuyResponse)]
    public partial class S_AuctionBuyResponse : IPacket
    {
        public EResultCode Result    { get; set; }
        public long        ListingId { get; set; }
    }

    /// <summary>판매 취소. 구매 진행 중이면 거절된다. 아이템은 우편으로 돌아온다(등록비 환급 없음).</summary>
    [MemoryPackable, Packet(PacketId.C_AuctionCancelRequest)]
    public partial class C_AuctionCancelRequest : IPacket
    {
        public long ListingId { get; set; }
    }

    [MemoryPackable, Packet(PacketId.S_AuctionCancelResponse)]
    public partial class S_AuctionCancelResponse : IPacket
    {
        public EResultCode Result    { get; set; }
        public long        ListingId { get; set; }
    }

    /// <summary>내가 올린 진행 중 매물(판매 중 · 구매 진행 중).</summary>
    [MemoryPackable, Packet(PacketId.C_AuctionMyListingsRequest)]
    public partial class C_AuctionMyListingsRequest : IPacket
    {
    }

    [MemoryPackable, Packet(PacketId.S_AuctionMyListingsResponse)]
    public partial class S_AuctionMyListingsResponse : IPacket
    {
        public EResultCode               Result   { get; set; }
        public List<AuctionListingInfo>? Listings { get; set; }
    }

    /// <summary>
    /// 거래소 목록 — 자원을 종류별 한 줄로(최저가 · 판매 중 수량 · 최근가 · 전일 평균가).
    /// <c>Category</c>는 ItemType(0이면 전체). 이름 검색은 클라가 TID 목록으로 바꿔 <c>Tids</c>에 싣는다 — 요청한 TID는 매물이 없어도 시세와 함께 온다.
    /// </summary>
    [MemoryPackable, Packet(PacketId.C_MarketItemsRequest)]
    public partial class C_MarketItemsRequest : IPacket
    {
        public int        Category { get; set; }
        public List<int>? Tids     { get; set; }
        public EAuctionKind Kind   { get; set; }   // 자원 · 아티팩트 · 장비 · 캐릭터(TID별 시세 요약). None이면 자원 (T-152 · T-118)
    }

    [MemoryPackable, Packet(PacketId.S_MarketItemsResponse)]
    public partial class S_MarketItemsResponse : IPacket
    {
        public EResultCode           Result { get; set; }
        public List<MarketItemInfo>? Items  { get; set; }
    }

    /// <summary>거래소 가격대 — 한 종류의 단가별 판매 중 수량(싼 순, 최대 10칸).</summary>
    [MemoryPackable, Packet(PacketId.C_MarketPriceRequest)]
    public partial class C_MarketPriceRequest : IPacket
    {
        public int Tid { get; set; }
        public EAuctionKind Kind { get; set; }   // 자원 · 아티팩트 · 장비 · 캐릭터. None이면 자원 (T-152 · T-118)
    }

    [MemoryPackable, Packet(PacketId.S_MarketPriceResponse)]
    public partial class S_MarketPriceResponse : IPacket
    {
        public EResultCode                 Result { get; set; }
        public int                         Tid    { get; set; }
        public List<MarketPriceLevelInfo>? Levels { get; set; }
    }

    /// <summary>
    /// 거래 기록 — 경매장 완료 탭 (T-156). 최신순 한 쪽. 기록은 30일 보관한다.
    /// <c>PageSize</c> 0이면 줄 없이 새 기록 수만 센다(탭 배지 — 로그인 · 경매 우편 도착 때). <c>MarkSeen</c>이면 지금까지를 "봤다"로 남긴다(탭을 열 때).
    /// </summary>
    [MemoryPackable, Packet(PacketId.C_AuctionHistoryRequest)]
    public partial class C_AuctionHistoryRequest : IPacket
    {
        public EAuctionHistoryType Type            { get; set; }  // None이면 전부
        public int                 Days            { get; set; }  // 최근 며칠 — 0이면 보관 기간 전부
        public long                CursorHistoryId { get; set; }  // 이 쪽 마지막 줄의 HistoryId — 첫 쪽은 0
        public int                 PageSize        { get; set; }  // 0~50
        public bool                MarkSeen        { get; set; }
    }

    [MemoryPackable, Packet(PacketId.S_AuctionHistoryResponse)]
    public partial class S_AuctionHistoryResponse : IPacket
    {
        public EResultCode               Result      { get; set; }
        public List<AuctionHistoryInfo>? Entries     { get; set; }
        public bool                      HasMore     { get; set; }
        public int                       UnseenCount { get; set; }  // 이번 요청이 "봤다"로 남기기 전의 새 기록 수
    }

    /// <summary>일별 시세 — 한 종류의 최근 14일(오늘이 마지막 칸) 체결 수량·평균·최저·최고 단가. 시세 탭 그래프용 (T-118).</summary>
    [MemoryPackable, Packet(PacketId.C_MarketPriceHistoryRequest)]
    public partial class C_MarketPriceHistoryRequest : IPacket
    {
        public EAuctionKind Kind { get; set; }   // 자원 · 아티팩트 · 장비 · 캐릭터. None이면 자원
        public int          Tid  { get; set; }
    }

    [MemoryPackable, Packet(PacketId.S_MarketPriceHistoryResponse)]
    public partial class S_MarketPriceHistoryResponse : IPacket
    {
        public EResultCode                  Result { get; set; }
        public EAuctionKind                 Kind   { get; set; }
        public int                          Tid    { get; set; }
        public List<MarketDailyPriceInfo>?  Days   { get; set; }  // 오래된 날 → 오늘. 체결이 없는 날도 0으로 온다
    }

    /// <summary>
    /// 거래소 구매 — 한 종류를 <c>Count</c>개, 개당 <c>MaxUnitPrice</c> 이하로 최저가부터 산다(여러 판매자에 걸친다).
    /// 다 못 채우면 아무것도 사지 않는다(MarketNotEnough). 실제 낸 금액은 <c>Count × MaxUnitPrice</c>보다 적을 수 있다.
    /// </summary>
    [MemoryPackable, Packet(PacketId.C_MarketBuyRequest)]
    public partial class C_MarketBuyRequest : IPacket
    {
        public int  Tid          { get; set; }
        public int  Count        { get; set; }
        public long MaxUnitPrice { get; set; }
        public EAuctionKind Kind { get; set; }   // 자원(Item) · 아티팩트(Artifact). None이면 자원 — 아티팩트는 우편으로 와서 받을 때 인벤토리 아티팩트 탭에 들어간다 (T-152 · T-181)
    }

    /// <summary>거래소 구매 결과. 산 자원은 우편으로 온다(<see cref="S_MailArrivedResponse"/>), 골드는 <see cref="S_CurrencyResponse"/>.</summary>
    [MemoryPackable, Packet(PacketId.S_MarketBuyResponse)]
    public partial class S_MarketBuyResponse : IPacket
    {
        public EResultCode Result     { get; set; }
        public int         Tid        { get; set; }
        public int         Count      { get; set; }
        public long        TotalPrice { get; set; }  // 실제로 낸 금액
    }

    // 서버가 격자 전체를 0부터 다시 매기고 S_StorageSlotsResponse로 전부 돌려준다.
    [MemoryPackable, Packet(PacketId.C_StorageSortRequest)]
    public partial class C_StorageSortRequest : IPacket
    {
        public EContainer        Container { get; set; }
        public EStorageTab       Tab       { get; set; }
        public EStorageSortKey   SortKey   { get; set; }
        public EStorageSortOrder Order     { get; set; }
    }

    // 목적지가 비었으면 옮기고 차 있으면 교환한다. 칸은 응답이 온 뒤에 바꾼다(낙관적 갱신 없음).
    // 자원은 같은 아이템 칸에 놓으면 MaxStack까지 합치고, 남는 것은 원래 칸에 둔다(T-160).
    [MemoryPackable, Packet(PacketId.C_StorageMoveSlotRequest)]
    public partial class C_StorageMoveSlotRequest : IPacket
    {
        public EContainer  Container { get; set; }
        public EStorageTab Tab       { get; set; }
        public int         FromSlot  { get; set; }
        public int         ToSlot    { get; set; }
    }

    // 정렬이면 격자 전체, 자리 이동이면 바뀐 칸만 싣는다. 거절이면 Slots·Items는 비어 있다.
    // 자원 탭은 Items가 정본이다(T-160) — 바뀐 칸마다 한 줄, Count 0이면 그 칸이 비었다. 클라는 (Container, Slot)으로 덮어쓴다.
    // 자원의 Slots(Key = ItemId)는 같은 아이템이 한 칸뿐일 때만 맞는 옛 형식이라 다중 칸 표시(T-161) 뒤에 지운다.
    [MemoryPackable, Packet(PacketId.S_StorageSlotsResponse)]
    public partial class S_StorageSlotsResponse : IPacket
    {
        public EResultCode           Result    { get; set; }
        public EContainer            Container { get; set; }
        public EStorageTab           Tab       { get; set; }
        public List<StorageSlotInfo> Slots     { get; set; } = new();
        public List<ItemChangeInfo>  Items     { get; set; } = new();
    }

    // 인벤토리 ↔ 창고 (T-107). From 보관함의 FromSlot을 반대 보관함의 ToSlot에 놓는다 — 비었으면 이동, 차 있으면 스왑.
    // 자원은 Count만큼 옮긴다: 빈 칸이면 놓고, 같은 자원 칸이면 MaxStack까지 합치고(남는 것은 원래 칸에 · T-160),
    // 다른 자원 칸이면 전량일 때만 스왑한다(일부면 거절).
    // 캐릭터·장비는 Count를 읽지 않는다. 칸은 응답이 온 뒤에 바꾼다.
    [MemoryPackable, Packet(PacketId.C_WarehouseTransferRequest)]
    public partial class C_WarehouseTransferRequest : IPacket
    {
        public EContainer  From     { get; set; }
        public EStorageTab Tab      { get; set; }
        public int         FromSlot { get; set; }
        public int         ToSlot   { get; set; }
        public int         Count    { get; set; }
    }

    // 양쪽 보관함의 바뀐 칸을 한 번에 싣는다. 거절이면 Changes는 비어 있다.
    [MemoryPackable, Packet(PacketId.S_WarehouseTransferResponse)]
    public partial class S_WarehouseTransferResponse : IPacket
    {
        public EResultCode               Result  { get; set; }
        public EStorageTab               Tab     { get; set; }
        public List<WarehouseChangeInfo> Changes { get; set; } = new();
    }

    // 한 보관함 안의 같은 자원 칸을 앞 칸부터 MaxStack까지 합친다(T-175). 칸 순서는 그대로이고 비게 된 뒤 칸은 빈 칸이 된다.
    // 응답은 자리 이동과 같은 S_StorageSlotsResponse(Tab = 요청한 탭) — 바뀐 칸만, Count 0 = 칸 비움.
    [MemoryPackable, Packet(PacketId.C_StorageMergeRequest)]
    public partial class C_StorageMergeRequest : IPacket
    {
        public EContainer  Container { get; set; }
        public EStorageTab Tab       { get; set; }   // Resource(기본) · Artifact(인벤토리만 · T-181)
    }

    // 인벤토리 → 창고. 창고에 이미 있는 자원만 창고의 같은 자원 칸으로 MaxStack까지 보낸다(T-175).
    // 창고 빈 칸에 새 묶음은 만들지 않고, 남는 몫은 인벤토리에 둔다. 응답은 S_WarehouseTransferResponse(Tab = Resource).
    [MemoryPackable, Packet(PacketId.C_WarehouseStackRequest)]
    public partial class C_WarehouseStackRequest : IPacket
    {
    }

    // 분해 (T-131 · T-185). 장비와 아티팩트를 지우고 등급별 재료(DisassembleTable)를 인벤토리에 넣는다 — 둘을 한 요청에 섞어 보낼 수 있다.
    // 아티팩트는 장비의 2배(ArtifactItemCount). 착용 중 · 잠김 · 큐브 결과 고르기 전 장비, 모자란 아티팩트,
    // 재료가 인벤토리에 다 안 들어가면 전부 거절한다. 아티팩트가 빠지면 S_ArtifactInventoryResponse가 따라온다.
    [MemoryPackable, Packet(PacketId.C_EquipDisassembleRequest)]
    public partial class C_EquipDisassembleRequest : IPacket
    {
        public List<long>?         EquipIds  { get; set; }
        public List<ArtifactInfo>? Artifacts { get; set; }  // 분해할 번호와 수량 — 같은 번호가 여러 줄이면 합친다
    }

    // 분해 결과. 전부 성공하거나 전부 실패한다 — Ok면 요청한 장비가 모두 사라졌으니 클라가 목록에서 지운다.
    [MemoryPackable, Packet(PacketId.S_EquipDisassembleResponse)]
    public partial class S_EquipDisassembleResponse : IPacket
    {
        public EResultCode          Result          { get; set; }
        public List<ItemChangeInfo> ItemChangeInfos { get; set; } = new();  // 받은 재료 — 칸마다 갱신 후 수량
    }

    /// <summary>건물 레벨 스냅샷(로그인 직후). 지은(Lv1 이상) 건물만 싣는다 — 없는 건물은 Lv0이다.</summary>
    [MemoryPackable, Packet(PacketId.S_BuildingListResponse)]
    public partial class S_BuildingListResponse : IPacket
    {
        public List<BuildingInfo> Buildings { get; set; } = new();
    }

    /// <summary>건물을 1레벨 올린다. 비용은 <c>BuildingLevelTable</c>의 다음 레벨 행(골드 + 자원)이다.</summary>
    [MemoryPackable, Packet(PacketId.C_BuildingUpgradeRequest)]
    public partial class C_BuildingUpgradeRequest : IPacket
    {
        public int BuildingTID { get; set; }
    }

    /// <summary>
    /// 건물 업그레이드 결과. 성공하면 이 앞에 골드(<see cref="S_CurrencyResponse"/>)와
    /// 빠진 자원(<see cref="S_UpdateItemResponse"/> — 인벤토리 먼저, 모자란 만큼 창고)이 온다.
    /// </summary>
    [MemoryPackable, Packet(PacketId.S_BuildingUpgradeResponse)]
    public partial class S_BuildingUpgradeResponse : IPacket
    {
        public EResultCode Result      { get; set; }
        public int         BuildingTID { get; set; }
        public int         Level       { get; set; }   // 성공이면 올린 뒤 레벨, 실패면 지금 레벨
    }
}
