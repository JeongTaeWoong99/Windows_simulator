namespace MikaProtocol
{
    /// <summary>
    /// 요청 1:1 응답 패킷의 처리 결과. 응답 패킷의 <b>첫 프로퍼티</b>로 들어간다.
    /// <b>Ok가 아니면 그 응답의 payload는 전부 null</b>이다 — 클라이언트는 읽지 않는다.
    /// 서버 푸시 패킷(스냅샷·채취 결과 등)에는 넣지 않는다(실패 개념이 없다).
    /// 값 대역: 1~99 공통 / 100~ 가챠 / 200~ 작업슬롯 / 300~ 상점. 도메인이 늘면 대역을 이어서 판다.
    /// </summary>
    public enum EResultCode : ushort
    {
        Ok = 0,

        // ── 1~99: 공통 ──
        NotLoggedIn     = 1,    // 로그인(User 생성) 전에 보낸 요청
        AlreadyLoggedIn = 2,    // 이미 로그인된 세션이 로그인을 다시 요청
        Banned          = 3,    // 밴된 계정 — 서버가 세션을 끊는다
        Deleted         = 4,    // 삭제된 계정 — 서버가 세션을 끊는다
        DbError         = 5,    // 로그인 중 DB 작업 실패 — 서버가 세션을 끊는다. 재접속하면 된다

        // ── 100~: 가챠 ──
        InvalidDrawCount   = 100, // 허용되지 않는 뽑기 횟수 (1·10만 허용)
        InvalidGachaId     = 101, // 존재하지 않는 가챠 풀
        NotEnoughCurrency  = 102, // 비용을 낼 재화가 모자람 — 아무것도 지급하지 않는다
        StorageFull        = 103, // 창고 칸(자원 종류·캐릭터·장비 각 200)이 모자람 — 뽑기·상자 개봉 모두. 아무것도 바꾸지 않는다

        // ── 200~: 작업슬롯 ──
        InvalidSlotIndex    = 200, // 보유하지 않은 슬롯 번호
        CharacterNotOwned   = 201, // 미보유 캐릭터 배치 시도
        NoAptitude          = 202, // 해당 산업 적성이 0인 캐릭터 배치 시도
        IndustryLevelLocked = 203, // 해금하지 않은(또는 범위 밖) 산업 레벨 배치 시도

        // ── 300~: 상점 ──
        InvalidSellRequest = 300, // 빈 목록·수량 0 이하·존재하지 않는 아이템
        NotEnoughItem      = 301, // 보유량보다 많이 팔려는(열려는) 시도
        ItemNotUsable      = 302, // 쓸 수 없는 아이템 (상자가 아님 · 테이블에 없음)
        InvalidUseCount    = 303, // 한 번에 쓰는 개수가 1~99 밖

        // ── 400~: 치트 ──
        NoPermission        = 400, // admin_level이 0인 유저의 치트 요청 — 아무것도 바꾸지 않는다
        InvalidCheatCommand = 401, // 정의되지 않은 명령
        InvalidCheatArgs    = 402, // 인자 범위·존재 검사 실패 (없는 TID, 0 이하 수량 등)

        // ── 500~: 해금 ──
        InvalidUnlockTID = 500, // UnlockTable에 없는 TID
        AlreadyUnlocked  = 501, // 이미 열린 해금을 다시 요청
        UnlockLocked     = 502, // 선행 미충족 · 계정 레벨 미달 · 이 해금에 없는 재화 선택. 골드 부족은 NotEnoughCurrency

        // ── 600~: 장비 ──
        EquipNotOwned      = 600, // 미보유 장비 개체
        InvalidEquipSlot   = 601, // None·범위 밖 칸
        EquipKindMismatch  = 602, // 종류가 칸에 맞지 않음 (무기를 보석 칸에 등)
        EquipSlotEmpty     = 603, // 해제할 장비가 없는 칸

        // ── 610~: 인챈트 ──
        EnchantItemNotOwned  = 610, // 인챈트 아이템 미보유
        EnchantAlreadyRolled = 611, // 이미 인챈트가 있는 장비에 부여
        EnchantNotRolled     = 612, // 인챈트가 없는 장비에 재롤·확장
        EnchantLineMax       = 613, // 이미 상한(3줄)
        EnchantEquipped      = 614, // 착용 중 — 벗겨야 인챈트할 수 있다

        // ── 700~: 캐릭터 (600은 장비와 겹쳐 있었다 — 2026-09-17) ──
        NoAptitudePoint = 700, // 남은 적성 포인트가 0 — 아무것도 바꾸지 않는다
        AptitudeAtCap   = 701, // 그 산업이 이미 상한 — 아무것도 바꾸지 않는다. 미보유는 CharacterNotOwned

        // ── 800~: 특성 ──
        InvalidUserTraitTID = 800, // UserTraitTable에 없는 TID. 이미 찍음 → AlreadyUnlocked · 조건 미달 → UnlockLocked
        NotEnoughTraitPoint = 801, // 남은 특성 포인트가 비용보다 적다 — 아무것도 바꾸지 않는다
        TraitOnlyUnlock     = 802, // 특성 노드의 해금을 C_UnlockRequest로 열려 했다 — 특성으로만 연다

        // ── 900~: 우편 ── 창고가 모자라 못 받으면 StorageFull(103)
        MailNotFound       = 900, // 없는(남의) 우편
        MailAlreadyClaimed = 901, // 이미 받은 우편을 다시 받으려 했다
        MailNotClaimed     = 902, // 안 받은 우편을 지우려 했다 — 보상을 실수로 버리지 않게 막는다
    }

    // 지불 재화 선택. GameData.CurrencyType(Enum.xlsx)과 이름·값이 1:1이어야 한다 —
    // 서버가 byte 캐스팅으로 그대로 옮긴다(PacketEnumTest가 어긋남을 잡는다).
    public enum ECurrencyType : byte
    {
        None = 0,

        Gold = 1,
        Dia  = 2,
    }

    // 장비 칸. GameData.EquipSlot(Enum.xlsx)과 이름·값이 1:1이어야 한다 — 서버가 byte 캐스팅으로 옮기고 DB에 저장한다.
    public enum EEquipSlot : byte
    {
        None       = 0,

        Weapon     = 1,
        Accessory1 = 2,
        Accessory2 = 3,
        Gem        = 4,
    }

    /// <summary>
    /// 치트 명령. 문자열 파싱 대신 enum이다 — 오타가 컴파일에서 잡힌다. <b>뒤에만 추가한다.</b>
    /// 인자 의미와 거절 조건은 <c>Server/docs/치트.md</c> 2장.
    /// </summary>
    public enum ECheatCommand : byte
    {
        None             = 0,
        GiveGold         = 1,  // Arg1 = 금액 (음수면 차감)
        GiveDia          = 2,  // Arg1 = 금액 (음수면 차감)
        GiveItem         = 3,  // Arg1 = ItemTID · Arg2 = 개수
        GiveCharacter    = 4,  // Arg1 = CharacterTID · Arg2 = 장수 (1~10)
        GiveCharacterExp = 5,  // Arg1 = CharacterId(개체) · Arg2 = 경험치
        Settle           = 6,  // 작업슬롯 판정을 Arg1회 앞당겨 정산 (0이면 1회)
        Unlock           = 7,  // Arg1 = UnlockTID — 조건·차감 없이 연다(GrantUnlock)
        GiveEquip        = 8,  // Arg1 = EquipTID — 개체 1개 지급, 창고 첫 빈 칸
        GiveAccountExp   = 9,  // Arg1 = 계정 경험치 — 레벨업·특성 포인트까지 실제 경로와 같다
        SendMail         = 10, // Arg1 = MailTemplateTID · Arg2 = 받는 UID(0이면 전체 우편 — 템플릿의 PeriodDays 동안)
    }

    public enum EItemChangeKind : byte
    {
        None = 0,
        Add = 1,
        Update = 2,
        Remove = 3,
    }

    // 배치·적성의 대상이 되는 1차 산업. GameData.IndustryType(Enum.xlsx)과 이름·값이 1:1이어야 한다 —
    // 서버가 byte 캐스팅으로 그대로 실어 보낸다(PacketEnumTest가 어긋남을 잡는다).
    public enum EIndustryType : byte
    {
        None = 0,   // 미지정 — 빈 슬롯·배치 해제

        Farming = 1,
        Fishing = 2,
        Mining  = 3,
        Logging = 4,
        Hunting = 5,
    }

    // 가챠 보상이 무엇인지. GameData.GachaRewardType(Enum.xlsx)과 이름·값이 1:1이어야 한다 —
    // 서버가 byte 캐스팅으로 그대로 실어 보낸다(PacketEnumTest가 어긋남을 잡는다).
    public enum EGachaRewardType : byte
    {
        None = 0,

        Item      = 1,   // GachaRewardInfo.ItemId를 읽는다
        Character = 2,   // GachaRewardInfo.CharacterTid를 읽는다
        Equip     = 3,   // GachaRewardInfo.EquipTid를 읽는다
        Gold      = 4,   // GachaRewardInfo.Count가 골드 양이다 (상자 전용)
    }

    // 아이템 등급(전역 공통). GameData.GlobalRarity(Enum.xlsx)와 값이 1:1이어야 한다 —
    // 서버가 테이블 값을 byte 캐스팅으로 그대로 실어 보내기 때문이다.
    public enum EGlobalRarity : byte
    {
        None = 0,
        Common = 1,
        Uncommon = 2,
        Rare = 3,
        Epic = 4,
        Legendary = 5,
        Mythic = 6,
    }
}