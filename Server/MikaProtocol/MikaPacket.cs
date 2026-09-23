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
        C_AptitudeUpRequest = 32,   // 27·28은 장비 패킷과 겹쳐 있었다(2026-09-17). PacketEnumTest가 중복을 막는다
        S_AptitudeUpResponse = 33,
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
        public int                   SlotIndex   { get; set; }  // 어느 슬롯에서 나왔는지
        public int                   JudgeCount  { get; set; }  // 이번에 정산된 판정 횟수(연출용)
        public List<ItemChangeInfo>? ItemChanges { get; set; }  // 인벤토리 갱신분
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
    /// 처리 경로가 하나로 끝난다. <b>한쪽만 바뀌어도 둘 다 실어 보낸다</b> — 덮어쓰기라 안전하고,
    /// 재화마다 패킷을 나누면 "무엇을 보내야 하는가"를 호출부가 매번 판단해야 한다.
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
        public long Gold { get; set; }  // 무료 재화
        public long Dia  { get; set; }  // 유료 재화
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
    /// <summary>특성 노드 하나를 찍는다. 조건은 그 노드의 <c>UnlockTable</c> 행 + 특성 포인트다.</summary>
    [MemoryPackable, Packet(PacketId.C_UserTraitLearnRequest)]
    public partial class C_UserTraitLearnRequest : IPacket
    {
        public int UserTraitTID { get; set; }
    }

    /// <summary>
    /// 특성 찍기 결과. 성공하면 이 앞에 <see cref="S_UnlockResponse"/>가, 뒤에 <see cref="S_AccountLevelResponse"/>(남은 포인트)가 온다.
    /// 속도 특성이면 바뀐 슬롯이 <see cref="S_WorkStationSlotSyncResponse"/>로 따로 온다.
    /// </summary>
    [MemoryPackable, Packet(PacketId.S_UserTraitLearnResponse)]
    public partial class S_UserTraitLearnResponse : IPacket
    {
        public EResultCode Result       { get; set; }
        public int         UserTraitTID { get; set; }
    }

    /// <summary>계정 레벨 스냅샷·푸시. 로그인 직후, 경험치가 오를 때, 특성 포인트를 쓸 때 온다(재화와 같은 관례).</summary>
    [MemoryPackable, Packet(PacketId.S_AccountLevelResponse)]
    public partial class S_AccountLevelResponse : IPacket
    {
        public int  Level      { get; set; }
        public long Exp        { get; set; }   // 현재 레벨에서 쌓은 양. 곡선이 캐릭터의 8배라 int를 넘는다
        public int  TraitPoint { get; set; }   // 남은(안 쓴) 특성 포인트
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

    // ───────────────────────── 캐릭터 성장 (Aptitude) ─────────────────────────

    /// <summary>적성 포인트 1개를 그 캐릭터의 산업 하나에 찍는다. 포인트·상한 검증은 서버가 한다.</summary>
    [MemoryPackable, Packet(PacketId.C_AptitudeUpRequest)]
    public partial class C_AptitudeUpRequest : IPacket
    {
        public long          CharacterId { get; set; }   // 개체 PK. TID가 아니다
        public EIndustryType Industry    { get; set; }
    }

    /// <summary>
    /// 찍기 결과. Ok면 갱신된 개체 1건(<c>CharacterInfo</c>)이 실린다 — 목록·동기화와 같은 형태라 CharacterId로 덮어쓴다.
    /// 배치 중인 슬롯의 속도 변화는 <see cref="S_WorkStationSlotSyncResponse"/>가 따로 온다.
    /// </summary>
    [MemoryPackable, Packet(PacketId.S_AptitudeUpResponse)]
    public partial class S_AptitudeUpResponse : IPacket
    {
        public EResultCode    Result    { get; set; }
        public CharacterInfo? Character { get; set; }
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
    /// 인챈트 요청. <b>무엇을 하는지는 아이템이 정한다</b>(EnchantItemTable의 Action) — 클라가 동작을 고르지 않는다.
    /// 착용 중인 장비는 거절된다(EnchantEquipped): 벗기는 것이 선행 조건이다.
    /// </summary>
    [MemoryPackable, Packet(PacketId.C_EquipEnchantRequest)]
    public partial class C_EquipEnchantRequest : IPacket
    {
        public long EquipId { get; set; }  // 개체 PK
        public int  ItemTid { get; set; }  // 인챈트 아이템 (EnchantItemTable.ItemTID)
    }

    /// <summary>
    /// 인챈트 결과. Success는 아이템의 성공 판정이며, <b>실패해도 GradeUp은 줄을 재롤</b>하므로
    /// Options는 항상 갱신된 값이다. 바뀐 개체는 S_EquipSyncResponse가 따로 온다.
    /// </summary>
    [MemoryPackable, Packet(PacketId.S_EquipEnchantResponse)]
    public partial class S_EquipEnchantResponse : IPacket
    {
        public EResultCode Result      { get; set; }
        public long        EquipId     { get; set; }
        public bool        Success     { get; set; }
        public int         BeforeGrade { get; set; }
        public int         AfterGrade  { get; set; }
        public List<int>   Options     { get; set; } = new();
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
}
