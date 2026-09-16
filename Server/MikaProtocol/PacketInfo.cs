using System.Collections.Generic;
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
    public partial class ItemChangeInfo   // Count는 델타가 아니라 갱신 후 누적 총량 — 클라는 덮어쓴다
    {
        public int ItemId { get; set; }
        public int Count  { get; set; }
        public EItemChangeKind Kind { get; set; }
    }

    // 가챠로 뽑힌 결과 1건(이번에 획득한 것). RewardType이 ItemId·CharacterTid 중 어느 것을 읽을지 정한다 — 겹쳐 담으면
    // 종류를 잘못 읽어도 조용히 지나간다. 개체 PK는 뒤따르는 S_CharacterListResponse에서 → Server/docs/데이터-카탈로그.md 3장
    [MemoryPackable]
    public partial class GachaRewardInfo
    {
        public EGachaRewardType RewardType { get; set; }
        public int ItemId       { get; set; }     // RewardType=Item일 때만 유효
        public int CharacterTid { get; set; }     // RewardType=Character일 때만 유효
        public int Count  { get; set; }           // 이번에 획득한 수량
        public EGlobalRarity Rarity { get; set; } // 연출용 등급
    }

    // 산업 하나에 대한 캐릭터 적성. 값의 주인은 서버다 — 클라는 CharacterTable을 직접 읽지 않는다.
    // Value는 기본값에 찍은 적성 포인트를 더한 실효값이고, 장비·특성 보정이 생기면 그것도 여기 실린다.
    [MemoryPackable]
    public partial struct AptitudeInfo
    {
        public EIndustryType Industry { get; set; }
        public byte          Value    { get; set; }   // 0~10. 0이면 그 산업을 다루지 못한다(배치 거절)
        public byte          Cap      { get; set; }   // 적성 포인트로 오를 수 있는 최댓값. Value < Cap이면 더 찍을 수 있다
    }

    /// <summary>
    /// 보유 캐릭터 개체 하나.
    /// <c>CharacterId</c>는 DB 발급 개체 PK, <c>CharacterTid</c>는 테이블 정의 —
    /// 같은 캐릭터(TID)를 여러 장 가질 수 있으므로 배치·식별은 반드시 <c>CharacterId</c>로 한다.
    /// 이름처럼 TID로 고정된 값은 내려보내지 않는다 — 클라이언트가 <c>CharacterTable</c>에서 읽는다.
    /// </summary>
    [MemoryPackable]
    public partial class CharacterInfo
    {
        public long CharacterId  { get; set; }
        public int  CharacterTid { get; set; }
        public int  Level        { get; set; }
        public int  Exp          { get; set; }
        public int  AptitudePoints { get; set; }   // 남은 적성 포인트. 서버가 레벨·찍은 양에서 계산한다

        // 1차 산업 5종이 값 0까지 포함해 전부 들어온다 — 배치 UI가 '적성 0 = 잠금'을 그려야 하기 때문이다.
        public List<AptitudeInfo> Aptitudes { get; set; } = new();
    }

    // 작업슬롯 한 칸의 상태. 주기 대신 진행도·속도·비용을 주어 클라가 카운트다운을 직접 구한다(연출일 뿐, 개수는 서버가 정한다).
    // LastTickAtUnixMs와 ProgressUnits는 같은 순간을 가리켜야 해 밀리초로 보낸다(이슈 #11) → Server/docs/채취-정산.md 7장
    [MemoryPackable]
    public partial class WorkStationSlotInfo
    {
        public int  SlotIndex      { get; set; }
        public EIndustryType Industry { get; set; }  // None=비어 있음
        public byte IndustryLevel  { get; set; }  // 이 슬롯이 돌고 있는 산업 레벨 (1~)
        public long CharacterId    { get; set; }  // 0=비어 있음 (채취하지 않는다)
        public long LastTickAtUnixMs { get; set; }  // 마지막 정산 시각 (Unix epoch 밀리초, UTC)
        public long ProgressUnits  { get; set; }  // 마지막 정산 시점의 누적 작업량 (판정에 못 미친 자투리)
        public int  CurrentWorkSpeed { get; set; }  // 현재 작업속도 — 보정 전부 적용된 확정값 (1000 = 기준 1.0배)
        public long JudgeCostUnits { get; set; }  // 판정 1회에 필요한 작업량
    }
}
