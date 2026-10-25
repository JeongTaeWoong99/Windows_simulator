using System;
using System.Collections.Generic;
using GameData;
using MikaNetwork;
using MikaProtocol;
using UnityEngine;

// UnityEngine에도 CharacterInfo(폰트 글리프 정보)가 있어 이름이 겹친다. 우리가 쓰는 건 패킷 쪽이다.
using CharacterInfo = MikaProtocol.CharacterInfo;

// 서버가 밀어준 내 계정 상태를 들고 있는 수신 전담 매니저 (서비스 로케이터 등록).
// 수신 진입점('ServerPacketHandler')을 구독해 캐시를 채우고 가공된 변경 이벤트를 발행한다 —
// UI는 서버 폴더가 아니라 이 매니저만 구독하면 된다.
//
// ⚠️ 송신은 하지 않는다 — 요청은 그 요청을 일으킨 Presenter가 직접 보낸다.
// 'GameDataLoader'와의 역할 구분(고정 테이블 ↔ 내 계정 상태)은 'Managers 규칙.md' 2장,
// 패킷별 수량 규약은 '서버 동작 이해.md', 로그인 1회 수신 세트는 '패킷 레퍼런스.md' 참조.
public class PlayerDataModel : MonoService<PlayerDataModel>
{
    // ─── 상태 캐시 ───
    private readonly List<ItemInfo>            _inventory        = new List<ItemInfo>();
    private readonly List<WorkStationSlotInfo> _workStationSlots = new List<WorkStationSlotInfo>();
    private readonly List<CharacterInfo>       _characters       = new List<CharacterInfo>();
    private readonly List<EquipInfo>           _equips           = new List<EquipInfo>();
    private readonly HashSet<int>              _unlockedTids     = new HashSet<int>(); // 열린 해금 — 영구라 줄지 않는다
    private readonly HashSet<int>              _lockedItemTids   = new HashSet<int>(); // 잠긴 자원 종류(TID) — 보유 0개여도 남는다(T-159)
    private readonly Dictionary<int, int>      _traitLevels      = new Dictionary<int, int>(); // 기본 레벨보다 올린 특성만 (특성 TID → 레벨)
    private readonly List<MailInfo>            _mails            = new List<MailInfo>();
    private readonly Dictionary<int, bool>     _artifactCodex    = new Dictionary<int, bool>(); // 도감에 장착한 번호 → 켜짐 (T-181)
    private readonly List<ItemInfo>            _artifactStacks   = new List<ItemInfo>(); // 아티팩트 칸 — 'ItemId' = 아티팩트 번호 (T-181)

    // 감정 할인 상한(천분율) — 서버 'User.MaxAppraiseDiscountPermille'의 사본이다(Constants에 없다). 서버가 바꾸면 같이 바꾼다
    private const int MaxAppraiseDiscountPermille = 500;

    // ─── 내부 상태 ───
    // 응답을 기다리는 개체 판매 — 'S_EntitySellResponse'에는 개체 ID가 없어 보낸 목록을 여기 둔다('BeginEntitySell').
    private readonly List<long> _sellingCharacterIds = new List<long>();
    private readonly List<long> _sellingEquipIds     = new List<long>();

    // 응답을 기다리는 장비 분해 — 'S_EquipDisassembleResponse'에도 장비 ID가 없다('BeginDisassemble').
    private readonly List<long> _disassemblingEquipIds = new List<long>();

    // 응답을 기다리는 응축의 재료 — 'S_CharacterCondenseResponse'에는 재료 ID가 없어 보낸 목록을 여기 둔다('BeginCondense').
    private readonly List<long> _condensingMaterialIds = new List<long>();

    // 칸 위치 응답을 Key → 칸으로 옮겨 두는 버퍼 — 정렬 응답은 탭 전체(최대 200칸)라 목록을 칸마다 훑지 않는다.
    private readonly Dictionary<long, int> _slotByKey = new Dictionary<long, int>();

    private bool _isSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    public long SessionId  { get; private set; }
    public bool IsLoggedIn { get; private set; }

    // 로그인에 쓴 Id. 서버가 닉네임을 돌려주지 않아 표시에 대신 쓰는 임시값이다
    // — 닉네임 패킷이 생기면 이 값과 'SetLoginId'는 함께 사라진다.
    public string LoginId { get; private set; } = "";

    public IReadOnlyList<ItemInfo>            Inventory        => _inventory;
    public IReadOnlyList<WorkStationSlotInfo> WorkStationSlots => _workStationSlots;

    // 아이템 하나의 보유 수량. 없으면 0이다.
    //
    // 자원은 칸별 묶음이라(T-160) 같은 아이템이 여러 칸에 있다 — 칸들을 모두 더한다. 0개가 된 칸은 캐시에서 지운다('SetItemSlot').
    // 판매 카트와 인벤토리 격자가 **같은 판정을 봐야 한다** — 각자 'Inventory'를 훑으면
    // 한쪽만 고쳐졌을 때 화면과 요청이 다른 수량을 말한다.
    // ★ 인벤토리와 창고(T-107)를 합쳐 센다 — 판매·개봉·큐브·경매 등록은 창고 것도 쓴다(T-164 · 이슈 #74).
    //   서버도 인벤토리 것부터 빼고 모자란 만큼 창고에서 뺀다('Inventory.TryRemoveAcross').
    public int GetItemCount(int itemId)
        => GetItemCount(itemId, EContainer.Inventory) + GetItemCount(itemId, EContainer.Warehouse);

    // 그 보관함에 둔 아이템 하나의 수량 — 칸들의 합이다. 없으면 0이다 (자원 칸 툴팁).
    // ★ 자원은 칸별 묶음이다(T-160 · T-161) — 'MaxStack'은 칸 하나의 상한이라 같은 아이템이 여러 칸에 나뉜다.
    public int GetItemCount(int itemId, EContainer container)
    {
        int sum = 0;

        foreach (var item in _inventory)
        {
            if (item.ItemId == itemId && item.Container == container)
            {
                sum += item.Count;
            }
        }

        return sum;
    }

    // 그 자원 종류가 잠겼나 (T-159). 잠긴 종류는 수량 전부가 즉시 판매·경매 등록에서 빠진다 — 사용(개봉·큐브)은 된다.
    // ★ 칸이 아니라 종류(TID) 단위다 — 같은 TID가 인벤토리·창고 여러 칸에 있어도 함께 잠긴다.
    public bool IsItemLocked(int itemId) => _lockedItemTids.Contains(itemId);

    // 그 보관함 그 칸의 묶음. 비었으면 null이다 (창고 ↔ 인벤토리 이동의 수량 팝업 · 자원 칸 툴팁).
    public ItemInfo? FindItemAt(EContainer container, int slot)
    {
        foreach (var item in _inventory)
        {
            if (item.Container == container && item.Slot == slot && item.Count > 0)
            {
                return item;
            }
        }

        return null;
    }

    // 지금 하나라도 가진 자원 TID를 캐시 순서대로 한 번씩 담는다 (판매 일괄 담기 · 경매 등록 목록).
    // ※ 'Inventory'를 그대로 훑지 않는다 — 같은 TID가 여러 칸(인벤토리·창고 · 칸별 묶음)에 있으면 목록에 여러 번 선다.
    //   수량은 'GetItemCount(itemId)'(두 보관함 합)로 읽는다.
    public void CollectOwnedItemIds(List<int> into)
    {
        into.Clear();

        foreach (var item in _inventory)
        {
            if (!into.Contains(item.ItemId) && GetItemCount(item.ItemId) > 0)
            {
                into.Add(item.ItemId);
            }
        }
    }

    // 그 보관함에 [합치기]로 바뀔 칸이 있나 (T-175) — 같은 자원의 덜 찬 칸 **뒤에** 같은 자원 칸이 하나 더 있으면 된다.
    // 서버 규칙('앞 칸부터 MaxStack까지 채운다')과 같은 판정이다 — 빈 요청을 보내지 않으려고 미리 본다.
    public bool CanMergeItemSlots(EContainer container)
    {
        foreach (var item in _inventory)
        {
            if (item.Container != container || item.Count <= 0 || item.Count >= GameDataLoader.GetItemMaxStack(item.ItemId))
            {
                continue;
            }

            foreach (var other in _inventory)
            {
                if (other.Container == container && other.ItemId == item.ItemId && other.Count > 0 && other.Slot > item.Slot)
                {
                    return true;
                }
            }
        }

        return false;
    }

    // [창고에 쌓기]로 옮겨질 것이 있나 (T-175) — 인벤토리에 있는 자원이 창고에 덜 찬 같은 자원 칸을 가지면 된다.
    // 창고 빈 칸에는 새 묶음을 만들지 않는다(서버 규칙) — 창고에 없는 자원은 세지 않는다.
    public bool CanStackIntoWarehouse()
    {
        foreach (var stored in _inventory)
        {
            if (stored.Container != EContainer.Warehouse || stored.Count <= 0 || stored.Count >= GameDataLoader.GetItemMaxStack(stored.ItemId))
            {
                continue;
            }

            if (GetItemCount(stored.ItemId, EContainer.Inventory) > 0)
            {
                return true;
            }
        }

        return false;
    }

    // 보관함 탭 하나의 칸 수 — 지금은 모든 유저·모든 탭이 인벤토리 'Constants.StorageCapacity' · 창고 'Constants.WarehouseCapacity'다.
    //
    // ★ 칸 수를 묻는 곳은 여기 하나다 — 칸 프레임('InventoryGridPresenter')은 이 값만큼 열고 나머지는 끈다.
    //   유저마다 칸이 늘어나는 확장(특성 등)이 생기면 서버가 유저별 칸 수를 보내고, 이 메서드가 그 값을 돌려주면 된다(이슈 #73).
    public int GetStorageCapacity(EContainer container, EStorageTab tab)
        => (int)(container == EContainer.Warehouse ? Constants.WarehouseCapacity : Constants.StorageCapacity);

    // 내가 가진 캐릭터들.
    //
    // ⚠️ 'CharacterInfo.CharacterId'는 개체 번호이고, 'CharacterInfo.CharacterTid'가
    // 캐릭터 종류다. 같은 캐릭터를 여러 마리 가질 수 있어서 종류로는 하나를 특정하지 못한다.
    // 슬롯 배치에 넣을 값은 개체 번호(CharacterId)다 — 종류(1001 같은 TID)를 보내면
    // 서버가 'CharacterNotOwned'로 거절한다. 이름·적성은 TID로 테이블에서 읽는다.
    public IReadOnlyList<CharacterInfo> Characters => _characters;

    // 첫 번째 보유 캐릭터의 개체 번호. 없으면 0(= 서버에선 배치 해제로 읽힌다).
    // 캐릭터 선택 UI가 생기기 전까지 테스트 버튼들이 쓰는 임시 통로다.
    public long FirstCharacterId => _characters.Count > 0 ? _characters[0].CharacterId : 0L;

    // 캐릭터 개체 번호로 표시 이름을 얻는다.
    //
    // 이름은 종류(TID)에 달린 값이라 개체 번호만으로는 못 찾는다 — 보유 목록에서 TID를 거쳐 간다.
    // 'GameDataLoader.GetCharacterName'에 개체 번호를 그대로 넣으면 '?#2'가 나온다.
    public string GetCharacterName(long characterId)
    {
        foreach (var character in _characters)
        {
            if (character.CharacterId == characterId)
            {
                return GameDataLoader.GetCharacterName(character.CharacterTid);
            }
        }

        return $"?#{characterId}"; // 아직 목록을 못 받았거나 서버가 모르는 개체
    }

    // 캐릭터 개체 번호로 레벨을 얻는다. 모르는 개체면 0.
    public int GetCharacterLevel(long characterId)
    {
        foreach (var character in _characters)
        {
            if (character.CharacterId == characterId)
            {
                return character.Level;
            }
        }

        return 0;
    }

    // 캐릭터 개체 번호로 경험치 진행률(0~1)을 얻는다. 만렙이면 1, 모르는 개체면 0.
    //
    // 분모는 **다음 레벨의 필요치**('RequiredExp(Level + 1)')다 — 'Exp'가 누적이 아니라
    // 현재 레벨에서 쌓은 양이라서다(레벨업하면 서버가 필요치를 뺀 나머지로 줄여 보낸다).
    public float GetExpProgress(long characterId)
    {
        foreach (var character in _characters)
        {
            if (character.CharacterId != characterId)
            {
                continue;
            }

            bool hasNext = GameDataLoader.TryGetRequiredExp(character.Level + 1, out int required);

            if (!hasNext || required <= 0)
            {
                return 1f; // 만렙 — 다음 행이 없다
            }

            return Mathf.Clamp01((float)character.Exp / required);
        }

        return 0f;
    }

    // 캐릭터 개체를 찾는다. 모르는 개체면 null.
    // ※ ★·누적처럼 한 화면이 여러 값을 함께 읽을 때 쓴다 — 값마다 목록을 다시 훑지 않는다.
    public CharacterInfo? FindCharacter(long characterId)
    {
        foreach (var character in _characters)
        {
            if (character.CharacterId == characterId)
            {
                return character;
            }
        }

        return null;
    }

    // 캐릭터 개체의 응축 ★(0~최고). 모르는 개체면 0. 값의 주인은 서버다('CharacterInfo.Star').
    public int GetCharacterStar(long characterId) => FindCharacter(characterId)?.Star ?? 0;

    // 캐릭터 개체 번호로 종류(TID)를 얻는다. 모르는 개체면 0.
    //
    // 등급처럼 종류에 달린 값을 개체 번호만 들고 있는 화면이 찾을 때 거쳐 간다('GetCharacterName'과 같은 이유).
    public int GetCharacterTid(long characterId)
    {
        foreach (var character in _characters)
        {
            if (character.CharacterId == characterId)
            {
                return character.CharacterTid;
            }
        }

        return 0;
    }

    // 캐릭터 개체 번호로 그 산업의 적성(0~10)을 얻는다. 모르는 개체·산업이면 0
    // (= 그 산업을 다루지 못한다. 서버가 배치를 'NoAptitude'로 거절한다).
    // ⚠️ 'CharacterTable'을 직접 읽지 않는다 — 값의 주인은 서버다
    // (근거는 '패킷 레퍼런스.md' 적성 절).
    public byte GetAptitude(long characterId, EIndustryType industry)
    {
        foreach (var character in _characters)
        {
            if (character.CharacterId != characterId)
            {
                continue;
            }

            foreach (var aptitude in character.Aptitudes)
            {
                if (aptitude.Industry == industry)
                {
                    return aptitude.Value;
                }
            }

            return 0; // 1차 산업 5종이 전부 실려 오므로 여기 오면 산업 쪽이 이상한 것이다
        }

        return 0;
    }

    // 이 캐릭터가 배치된 작업슬롯 번호. 배치돼 있지 않으면 -1.
    //
    // 인벤토리 캐릭터 탭과 작업슬롯 선택 화면이 **같은 판정을 봐야 한다** — 각자 'WorkStationSlots'를
    // 훑으면 한쪽만 고쳐졌을 때 두 화면이 다른 말을 한다. 그래서 여기 한 번 두고 양쪽이 부른다.
    // ⚠️ 리스트 순번이 아니라 'SlotIndex'를 돌려준다 — 열린 슬롯만 실려 오므로 둘이 어긋난다.
    public int FindSlotIndexOf(long characterId)
    {
        if (characterId == 0L)
        {
            return -1; // 0은 '비어 있음'이라 빈 슬롯 전부와 맞아 버린다
        }

        foreach (var slot in _workStationSlots)
        {
            if (slot.CharacterId == characterId)
            {
                return slot.SlotIndex;
            }
        }

        return -1;
    }

    // 내가 가진 장비들. 인벤토리에 있든 캐릭터가 끼고 있든 전부 여기 있다.
    //
    // ⚠️ 캐릭터와 같은 모양이다 — 'EquipId'가 개체 번호이고 'EquipTid'가 종류다.
    //   이름·등급·효과는 TID로 'EquipTable'에서 읽는다.
    // ※ 장착해도 목록에서 빠지지 않는다 — 서버가 장착 시 'SlotPosition'(인벤토리 칸)을 그대로 두므로
    //   인벤토리에 남아 있고, 'IsEquipped'로 구분한다.
    public IReadOnlyList<EquipInfo> Equips => _equips;

    // 창고에 맡긴 개체인가 — 섞여 보이는 목록이 칸 왼쪽 아래 상자 아이콘을 그린다(T-163). 모르는 개체면 false.
    public bool IsCharacterInWarehouse(long characterId) => FindCharacter(characterId)?.Container == EContainer.Warehouse;
    public bool IsEquipInWarehouse(long equipId)         => FindEquip(equipId)?.Container == EContainer.Warehouse;

    // 장비 개체 하나를 찾는다. 모르는 개체면 null.
    public EquipInfo? FindEquip(long equipId)
    {
        foreach (var equip in _equips)
        {
            if (equip.EquipId == equipId)
            {
                return equip;
            }
        }

        return null;
    }

    // 장비 개체 번호로 종류(TID)를 얻는다. 모르는 개체면 0.
    public int GetEquipTid(long equipId)
    {
        foreach (var equip in _equips)
        {
            if (equip.EquipId == equipId)
            {
                return equip.EquipTid;
            }
        }

        return 0;
    }

    // 이 캐릭터가 일하는 중인가 — 작업슬롯에 배치됐거나 장비를 끼고 있다.
    // ※ 서버 'User.IsCharacterBusy'와 같은 판정이다. 바쁜 캐릭터는 팔 수도(SellCharacterBusy)
    //   경매에 올릴 수도(AuctionCharacterBusy) 없다 — 화면이 미리 막고 이유를 적는 데 쓴다.
    public bool IsCharacterBusy(long characterId)
    {
        return FindSlotIndexOf(characterId) >= 0 || IsWearingEquip(characterId);
    }

    // 이 캐릭터가 장비를 하나라도 끼고 있는가.
    public bool IsWearingEquip(long characterId)
    {
        if (characterId == 0L)
        {
            return false; // 0은 '인벤토리'라 끼지 않은 장비 전부와 맞아 버린다
        }

        return _equips.Exists(equip => equip.EquippedCharacterId == characterId);
    }

    // 이 캐릭터가 이 칸에 낀 장비. 비었으면(또는 모르는 캐릭터면) null.
    // ※ 인벤토리 캐릭터 칸 · 작업슬롯 칸의 장착 네모(T-104)와 슬롯 설정의 장비 칸이 같은 판정을 쓴다.
    public EquipInfo? FindWornEquip(long characterId, EEquipSlot slot)
    {
        if (characterId == 0L || slot == EEquipSlot.None)
        {
            return null; // 0은 '인벤토리'라 끼지 않은 장비 전부와 맞아 버린다
        }

        foreach (var equip in _equips)
        {
            if (equip.EquippedCharacterId == characterId && equip.EquippedSlot == slot)
            {
                return equip;
            }
        }

        return null;
    }

    // 이 장비를 캐릭터가 끼고 있는가. 인벤토리에 있으면(또는 모르는 개체면) false.
    // ※ 서버의 'Equip.IsEquipped'와 같은 판정이다 — 'EquippedCharacterId = 0'이 인벤토리다.
    public bool IsEquipped(long equipId)
    {
        foreach (var equip in _equips)
        {
            if (equip.EquipId == equipId)
            {
                return equip.EquippedCharacterId != 0L;
            }
        }

        return false;
    }

    // 해금('UnlockTable')이 열렸는가. 'UnlockTID = 0'은 조건이 없는 것이라 항상 열려 있다.
    // 서버 'User.IsUnlocked'와 같은 모양이다 — 콘텐츠는 "내 UnlockTID가 열렸나"만 묻는다.
    // 조건(골드·선행·레벨)을 여기서 다시 보지 않는다 — 목록에 있다는 것이 서버가 판정해 열어 줬다는 뜻이다.
    public bool IsUnlocked(int unlockTid)
    {
        return unlockTid == 0 || _unlockedTids.Contains(unlockTid);
    }

    // 아티팩트 도감 — 장착한 번호와 켜짐. 켜 둔 번호만 효과가 있다 (T-181)
    public IReadOnlyDictionary<int, bool> ArtifactCodex => _artifactCodex;

    // 아티팩트 칸 — 서버 저장('EStorageTab.Artifact')이다. 인벤토리 화면에는 탭이 없고 보여 줄 화면은 T-182에서 정한다
    public IReadOnlyList<ItemInfo> ArtifactStacks => _artifactStacks;

    // 이 유물 1개의 감정 비용 — 기본 비용에 켜 둔 아티팩트의 감정 할인을 붙인다(서버 'User.GetAppraiseCost'와 같은 식).
    // 0이면 골드 없이 여는 상자다. 실제 차감은 서버가 정한다 — 이 값은 보여 주고 미리 막는 데만 쓴다.
    public long GetAppraiseCost(int itemId)
    {
        long baseCost = GameDataLoader.GetOpenCostGold(itemId);

        if (baseCost <= 0L)
        {
            return 0L;
        }

        int discount = 0;

        foreach (KeyValuePair<int, bool> entry in _artifactCodex)
        {
            if (entry.Value
                && GameDataLoader.TryGetArtifact(entry.Key, out ArtifactTableRow artifact)
                && artifact.EffectType == UserTraitEffect.AppraiseCostDiscount
                && artifact.Industry == IndustryType.None)
            {
                discount += artifact.EffectValue;
            }
        }

        long scale = Constants.WorkSpeedScale;

        return baseCost * (scale - Mathf.Clamp(discount, 0, MaxAppraiseDiscountPermille)) / scale;
    }

    // 특성의 지금 레벨. 올린 적 없으면 'UserTraitTable.BaseLevel', 없는 특성이면 0 (서버 'User.GetTraitLevel'과 같다).
    public int GetTraitLevel(int userTraitTid)
    {
        if (_traitLevels.TryGetValue(userTraitTid, out int level))
        {
            return level;
        }

        return GameDataLoader.TryGetUserTrait(userTraitTid, out var trait) ? trait.BaseLevel : 0;
    }

    // 이 산업 레벨이 열렸는가 — **그 산업 개척 특성의 레벨 ≥ 산업 레벨**이면 열린다 (2026-10-02 · T-108).
    //
    // ※ 개척 특성이 없는 산업이면 Lv1만 연다 — 표가 비었을 때 상위 레벨을 열어 두면 서버 거절로만 드러난다.
    public bool IsIndustryLevelOpen(EIndustryType industry, int level)
    {
        if (level <= 1)
        {
            return true;
        }

        return GameDataLoader.TryGetIndustryUnlockTrait(industry, out var trait)
            && GetTraitLevel(trait.UserTraitTID) >= level;
    }

    // 이 효과를 가진 특성 중 **이 산업에 붙는** 것의 가산 합(천분율).
    //
    // ⚠️ 서버 'User.SumTraitEffect'의 사본이다 — 레벨당 효과 × (지금 레벨 − 기본 레벨).
    //   대상 산업이 'None'인 특성(공통)은 모든 산업에 붙는다. 표를 고치면 화면만 틀리고 조용하다.
    public int GetTraitEffectSum(UserTraitEffect effect, EIndustryType industry)
    {
        int sum = 0;

        foreach (var trait in GameDataLoader.UserTraits)
        {
            if (trait.EffectType != effect)
            {
                continue;
            }

            if (trait.Industry != IndustryType.None && (byte)trait.Industry != (byte)industry)
            {
                continue;
            }

            sum += trait.EffectValue * (GetTraitLevel(trait.UserTraitTID) - trait.BaseLevel);
        }

        return sum;
    }

    // 우편함 — 안 받은 우편과 받은 지 7일이 안 된 우편. 서버가 준 순서 그대로다(정렬은 화면이 한다).
    // ※ 'ClaimedAtUnixMs = 0'이 안 받은 우편이다. 받은 우편은 7일 뒤 로그인 때 서버가 지운다.
    public IReadOnlyList<MailInfo> Mails => _mails;

    // 안 받은 우편이 하나라도 있는가 — 우편함 버튼의 점 하나가 이것만 본다(숫자는 적지 않는다).
    public bool HasUnclaimedMail
    {
        get
        {
            foreach (var mail in _mails)
            {
                if (mail.ClaimedAtUnixMs == 0L)
                {
                    return true;
                }
            }

            return false;
        }
    }

    // 재화 보유량. 종류마다 필드다 — 서버가 행이 아니라 컬럼으로 싣기 때문이다
    // (DB 't_user_currency'도 같은 축이다). 재화가 늘면 패킷에 필드가 하나 늘고 여기도 하나 는다.
    //
    // ※ 예전에는 'Dictionary<byte, long>'에 'CurrencyType'을 키로 담았다. 그 축이 없어졌는데
    //   사전만 채우는 식으로 두면 패킷과 캐시가 다시 어긋난다 — 사전째 걷어냈다.
    public long Gold { get; private set; }  // 유일한 재화 — 유료 재화(다이아)는 폐지했다(#41)

    // 계정 레벨 — 캐릭터가 얻은 경험치가 그대로 계정 경험치가 된다(캐릭터가 만렙이어도 계정은 자란다).
    // 재화와 같은 관례로 스냅샷이 통째로 온다 — 로그인 직후 · 경험치가 오를 때 · 특성 포인트를 쓸 때.
    //
    // ※ 특성 레벨은 여기 없다 — 'GetTraitLevel'이 따로 든다(S_UserTraitListResponse · 2026-10-02).
    public int  AccountLevel { get; private set; } = 1;
    public long AccountExp   { get; private set; }  // 현재 레벨에서 쌓은 양. 곡선이 캐릭터의 8배라 long이다
    public int  TraitPoint   { get; private set; }  // 남은(안 쓴) 특성 포인트

    // 로그인 요청에 쓴 Id를 표시용으로 기억한다 (로그인을 보낸 UI가 호출).
    //
    // 이 매니저는 송신을 모르므로 "무엇으로 로그인했는가"를 스스로 알 수 없다.
    // 서버가 닉네임을 돌려주기 시작하면 'LoginId'와 함께 지운다.
    public void SetLoginId(string id) => LoginId = id;

    // ─── 가공 이벤트 (UI가 구독) ───
    // ※ 완료 이벤트에는 성공 여부와 함께 결과 코드를 싣는다 — 실패 사유를 화면에 보여 주려면
    //   "실패했다"만으로는 부족하고 왜인지(EResultCode)가 필요하다.
    //   받은 Presenter가 'ResultMessages'로 문구를 만들어 'ServerWaitManager'에 넘긴다.
    public event Action<bool, EResultCode>?     LoginCompleted;   // 로그인 완료 (성공 여부·결과 코드)
    public event Action?                        InventoryChanged; // 인벤토리 갱신됨 (스냅샷 반영 후)
    public event Action<List<GachaRewardInfo>>? GachaCompleted;   // 가챠 성공 (뽑힌 보상 목록)
    public event Action<EResultCode>?           GachaFailed;      // 가챠 실패 (거절 사유)

    public event Action?                         CharactersChanged;          // 보유 캐릭터 캐시 갱신됨
    public event Action<long, int, int>?         CharacterLeveledUp;         // 캐릭터 레벨업 (개체·이전 레벨·새 레벨 — 한 정산에 여러 레벨이 오를 수 있다 · 'CharactersChanged'보다 먼저)
    public event Action?                         EquipsChanged;              // 보유 장비 캐시 갱신됨 (지급·장착·해제 전부)
    public event Action<bool, EResultCode>?      EquipCompleted;             // 장착·해제 완료 (성공 여부·결과 코드)
    public event Action<S_EquipEnchantResponse>? EquipEnchantCompleted;      // 큐브 사용 결과 (거절 포함 — 'Result'를 먼저 본다)
    public event Action<S_EquipEnchantChooseResponse>? EquipEnchantChooseCompleted; // 상급 큐브 결과 고르기 (거절 포함 — 'Result'를 먼저 본다)
    public event Action<S_CharacterCondenseResponse>? CharacterCondenseCompleted; // 응축 결과 (거절 포함 — 'Result'를 먼저 본다 · 캐시 반영 뒤에 온다)
    public event Action<S_EntityLockResponse>?   EntityLockCompleted;        // 잠금 결과 (거절 포함 — 'Result'를 먼저 본다 · 캐시 반영 뒤에 온다)
    public event Action<bool, EResultCode>?      WorkStationAssignCompleted; // 슬롯 변경 완료 (성공 여부·결과 코드)
    public event Action?                         WorkStationSlotsChanged;    // 슬롯 캐시 갱신됨
    public event Action<S_GatherResultResponse>? GatherResultReceived;       // 채취 결과 푸시 도착
    public event Action<long>?                   GatherValueEarned;          // 채취로 늘어난 아이템의 즉시 판매가 합 (0이면 오지 않는다 · 위젯 누적 집계)
    public event Action?                         CurrencyChanged;            // 재화 캐시 갱신됨

    public event Action<long>?        ItemSellCompleted;   // 판매 성공 (이번에 번 골드 — 잔액은 'CurrencyChanged'로 따로 온다)
    public event Action<EResultCode>? ItemSellFailed;      // 판매 실패 (거절 사유)
    public event Action<long>?        EntitySellCompleted; // 개체(캐릭터·장비) 판매 성공 (이번에 번 골드)
    public event Action<EResultCode>? EntitySellFailed;    // 개체 판매 실패 (거절 사유)
    public event Action?              EquipDisassembleCompleted; // 장비 분해 성공 (재료는 인벤토리에 이미 반영됐다)
    public event Action<EResultCode>? EquipDisassembleFailed;    // 장비 분해 실패 (거절 사유)

    // 아이템 사용 성공 (쓴 아이템 TID · 얻은 보상 목록 — 상자 개봉·유물 감정 · 인벤토리가 차서 보상 전체가 우편으로 갔는가)
    public event Action<int, List<GachaRewardInfo>, bool>? ItemUseCompleted;
    public event Action<EResultCode>?                 ItemUseFailed; // 아이템 사용 실패 (거절 사유)

    public event Action?                        MailsChanged;        // 우편함 캐시 갱신됨 (목록·도착·수령·삭제)
    public event Action<List<MailInfo>>?        MailRewardsClaimed;  // 이번에 받은 우편들 (첨부 연출용 — 받은 게 있을 때만)
    public event Action<EResultCode, int, int>? MailClaimCompleted;  // 수령 결과 (결과 코드·이번에 받은 수·남은 수)
    public event Action<bool, EResultCode>?     MailDeleteCompleted; // 삭제 결과 (성공 여부·결과 코드)

    public event Action?                   UnlocksChanged;  // 열린 해금 목록 갱신됨 (로그인 목록 · 해금 성공 후)
    public event Action<bool, EResultCode>? UnlockCompleted; // 해금 결과 (성공 여부·결과 코드)

    public event Action?                    AccountLevelChanged; // 계정 레벨·경험치·특성 포인트 갱신됨
    public event Action?                    TraitsChanged;       // 특성 레벨 갱신됨 (로그인 목록 · 레벨 올리기 응답)
    public event Action<bool, EResultCode>? TraitLearnCompleted; // 특성 레벨 올리기 결과 (성공 여부·결과 코드)

    public event Action? ArtifactsChanged; // 아티팩트 도감·칸 갱신됨 (두 스냅샷 어느 쪽이든)

    // 인벤토리 [정렬]·칸 이동 결과 (거절 포함). 성공이면 칸 번호를 캐시에 반영하고 탭의 변경 이벤트를 먼저 발행한 뒤에 온다.
    public event Action<EResultCode>? StorageSlotsResponded;

    // 인벤토리 ↔ 창고 이동 결과 (거절 포함). 성공이면 양쪽 보관함의 바뀐 칸을 캐시에 반영하고 변경 이벤트를 먼저 발행한 뒤에 온다.
    public event Action<EResultCode>? WarehouseTransferResponded;

    // 뒤에 다른 알림이 이어지는 이벤트(캐시 변경 'XxxChanged' 등)를 구독자마다 따로 부른다 —
    // 한 화면이 터져도 다른 화면과 뒤이은 알림('XxxCompleted')은 나간다.
    // ⚠️ '?.Invoke()'로 되돌리지 않는다 — 첫 예외에서 멈춰 결과를 기다리는 화면이 응답을 못 받는다 (T-172)
    private static void Notify(Action? evt)
    {
        if (evt == null)
        {
            return;
        }

        foreach (Delegate handler in evt.GetInvocationList())
        {
            try
            {
                ((Action)handler).Invoke();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }

    private static void Notify<T>(Action<T>? evt, T arg)
    {
        if (evt == null)
        {
            return;
        }

        foreach (Delegate handler in evt.GetInvocationList())
        {
            try
            {
                ((Action<T>)handler).Invoke(arg);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }

    // ─── Unity 메시지 ───

    // 구독 → 초기화 순서로 진행한다 (매니저 공통 규약 — 이 매니저는 확보할 참조가 없다)
    private void Start()
    {
        Subscribe();
        _isReady = true;
    }

    // 껐다 켠 경우의 재구독 (Unity 메시지)
    private void OnEnable()
    {
        if (_isReady)
        {
            Subscribe();
        }
    }

    // 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        Unsubscribe();
    }

    #region 구독

    // 수신 진입점 구독 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _isSubscribed = true;

        ServerPacketHandler.LoginResponded           += OnLoginResponded;
        ServerPacketHandler.InventoryReceived        += OnInventoryReceived;
        ServerPacketHandler.GachaDrawn               += OnGachaDrawn;
        ServerPacketHandler.CharacterListReceived    += OnCharacterListReceived;
        ServerPacketHandler.CharacterSynced          += OnCharacterSynced;
        ServerPacketHandler.EquipListReceived        += OnEquipListReceived;
        ServerPacketHandler.EquipSynced              += OnEquipSynced;
        ServerPacketHandler.EquipResponded           += OnEquipResponded;
        ServerPacketHandler.EquipEnchantResponded    += OnEquipEnchantResponded;
        ServerPacketHandler.EquipEnchantChooseResponded += OnEquipEnchantChooseResponded;
        ServerPacketHandler.CharacterCondenseResponded += OnCharacterCondenseResponded;
        ServerPacketHandler.EntityLockResponded      += OnEntityLockResponded;
        ServerPacketHandler.WorkStationAssigned      += OnWorkStationAssigned;
        ServerPacketHandler.WorkStationSlotsReceived += OnWorkStationSlotsReceived;
        ServerPacketHandler.GatherResultReceived     += OnGatherResultReceived;
        ServerPacketHandler.WorkStationSlotSynced    += OnWorkStationSlotSynced;
        ServerPacketHandler.CurrencyReceived         += OnCurrencyReceived;
        ServerPacketHandler.ItemUpdated              += OnItemUpdated;
        ServerPacketHandler.ItemSold                 += OnItemSold;
        ServerPacketHandler.EntitySold               += OnEntitySold;
        ServerPacketHandler.EquipDisassembled        += OnEquipDisassembled;
        ServerPacketHandler.ItemUsed                 += OnItemUsed;
        ServerPacketHandler.UnlockListReceived       += OnUnlockListReceived;
        ServerPacketHandler.UnlockResponded          += OnUnlockResponded;
        ServerPacketHandler.AccountLevelReceived     += OnAccountLevelReceived;
        ServerPacketHandler.UserTraitLearnResponded  += OnUserTraitLearnResponded;
        ServerPacketHandler.UserTraitListReceived    += OnUserTraitListReceived;
        ServerPacketHandler.MailListReceived         += OnMailListReceived;
        ServerPacketHandler.MailArrived              += OnMailArrived;
        ServerPacketHandler.MailClaimResponded       += OnMailClaimResponded;
        ServerPacketHandler.MailDeleteResponded      += OnMailDeleteResponded;
        ServerPacketHandler.AuctionRegisterResponded += OnAuctionRegisterResponded;
        ServerPacketHandler.StorageSlotsReceived     += OnStorageSlotsReceived;
        ServerPacketHandler.WarehouseTransferred     += OnWarehouseTransferred;
        ServerPacketHandler.ArtifactListReceived     += OnArtifactListReceived;
        ServerPacketHandler.ArtifactInventoryReceived += OnArtifactInventoryReceived;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _isSubscribed = false;

        ServerPacketHandler.LoginResponded           -= OnLoginResponded;
        ServerPacketHandler.InventoryReceived        -= OnInventoryReceived;
        ServerPacketHandler.GachaDrawn               -= OnGachaDrawn;
        ServerPacketHandler.CharacterListReceived    -= OnCharacterListReceived;
        ServerPacketHandler.CharacterSynced          -= OnCharacterSynced;
        ServerPacketHandler.EquipListReceived        -= OnEquipListReceived;
        ServerPacketHandler.EquipSynced              -= OnEquipSynced;
        ServerPacketHandler.EquipResponded           -= OnEquipResponded;
        ServerPacketHandler.EquipEnchantResponded    -= OnEquipEnchantResponded;
        ServerPacketHandler.EquipEnchantChooseResponded -= OnEquipEnchantChooseResponded;
        ServerPacketHandler.CharacterCondenseResponded -= OnCharacterCondenseResponded;
        ServerPacketHandler.EntityLockResponded      -= OnEntityLockResponded;
        ServerPacketHandler.WorkStationAssigned      -= OnWorkStationAssigned;
        ServerPacketHandler.WorkStationSlotsReceived -= OnWorkStationSlotsReceived;
        ServerPacketHandler.GatherResultReceived     -= OnGatherResultReceived;
        ServerPacketHandler.WorkStationSlotSynced    -= OnWorkStationSlotSynced;
        ServerPacketHandler.CurrencyReceived         -= OnCurrencyReceived;
        ServerPacketHandler.ItemUpdated              -= OnItemUpdated;
        ServerPacketHandler.ItemSold                 -= OnItemSold;
        ServerPacketHandler.EntitySold               -= OnEntitySold;
        ServerPacketHandler.EquipDisassembled        -= OnEquipDisassembled;
        ServerPacketHandler.ItemUsed                 -= OnItemUsed;
        ServerPacketHandler.UnlockListReceived       -= OnUnlockListReceived;
        ServerPacketHandler.UnlockResponded          -= OnUnlockResponded;
        ServerPacketHandler.AccountLevelReceived     -= OnAccountLevelReceived;
        ServerPacketHandler.UserTraitLearnResponded  -= OnUserTraitLearnResponded;
        ServerPacketHandler.UserTraitListReceived    -= OnUserTraitListReceived;
        ServerPacketHandler.MailListReceived         -= OnMailListReceived;
        ServerPacketHandler.MailArrived              -= OnMailArrived;
        ServerPacketHandler.MailClaimResponded       -= OnMailClaimResponded;
        ServerPacketHandler.MailDeleteResponded      -= OnMailDeleteResponded;
        ServerPacketHandler.AuctionRegisterResponded -= OnAuctionRegisterResponded;
        ServerPacketHandler.StorageSlotsReceived     -= OnStorageSlotsReceived;
        ServerPacketHandler.WarehouseTransferred     -= OnWarehouseTransferred;
        ServerPacketHandler.ArtifactListReceived     -= OnArtifactListReceived;
        ServerPacketHandler.ArtifactInventoryReceived -= OnArtifactInventoryReceived;
    }

    #endregion

    #region 응답 처리 (ServerPacketHandler 구독)

    // 로그인 응답 — 세션 상태 갱신 후 이벤트 발행
    private void OnLoginResponded(S_LoginResponse res)
    {
        IsLoggedIn = res.Result == EResultCode.Ok;
        SessionId  = res.SessionId;

        if (!IsLoggedIn)
        {
            ClientLogger.Warn(ClientLogger.Recv, $"로그인 실패 — 결과={res.Result}");
        }

        LoginCompleted?.Invoke(IsLoggedIn, res.Result);
    }

    // 인벤토리 스냅샷 — 캐시 교체 후 이벤트 발행
    // ★ 로그인 시 자동으로 1회 수신. 잠긴 자원 종류(T-159)도 같은 패킷에 실려 온다 — 보유 0개인 종류까지 온다.
    private void OnInventoryReceived(S_InventoryResponse res)
    {
        _inventory.Clear();
        if (res.Items != null)
        {
            _inventory.AddRange(res.Items.FindAll(item => item.Count > 0));
        }

        _lockedItemTids.Clear();
        if (res.LockedItemTids != null)
        {
            _lockedItemTids.UnionWith(res.LockedItemTids);
        }

        Notify(InventoryChanged);
    }

    // 가챠 응답 — 인벤토리 반영 후 보상 이벤트 발행
    // ★ 한 패킷에 두 가지가 실려 온다. 용도가 다르니 섞어 쓰지 않는다.
    //   ItemChangeInfos = 갱신 후 누적 총량 → 인벤토리 반영 (다른 경로와 같은 규칙)
    //   Rewards         = 이번에 뽑힌 개별 항목(델타) → 연출 전용
    private void OnGachaDrawn(S_GachaDrawResponse res)
    {
        if (res.Result != EResultCode.Ok)
        {
            ClientLogger.Warn(ClientLogger.Recv, $"가챠 실패 — 결과={res.Result}");
            GachaFailed?.Invoke(res.Result);

            return;
        }

        ApplyItemChanges(res.ItemChangeInfos);
        GachaCompleted?.Invoke(res.Rewards ?? new List<GachaRewardInfo>());
    }

    // 보유 캐릭터 스냅샷 — 캐시 교체 후 이벤트 발행
    // ★ 로그인 시 자동으로 1회 수신. 여기 실린 CharacterId(개체 번호)가 슬롯 배치에 넣을 값이다.
    private void OnCharacterListReceived(S_CharacterListResponse res)
    {
        _characters.Clear();
        if (res.Characters != null)
        {
            _characters.AddRange(res.Characters);
        }

        if (_characters.Count == 0)
        {
            ClientLogger.Warn(ClientLogger.Recv, "보유 캐릭터가 0마리다 — 작업슬롯 배치가 전부 거절된다(서버 지급 로직 확인)");
        }

        Notify(CharactersChanged);
    }

    // 캐릭터 1개체 동기화 — 판정 정산으로 레벨·경험치가 바뀌거나, 우편으로 캐릭터를 받으면 요청 없이 도착한다.
    //
    // ★ 증감이 아니라 확정값이라 통째로 **교체**한다 (레벨업하면 'Exp'가 줄어드는 것도 그대로 받는다).
    // ★ 목록에 없는 개체는 **추가한다** (2026-10-03 · T-096) — 경매로 산 캐릭터·취소로 돌아온 캐릭터는
    //   우편 수령 때 이 패킷으로만 온다('User.Character' 우편 캐릭터 잠금 해제). 장비('OnEquipSynced')와 같아졌다.
    private void OnCharacterSynced(S_CharacterSyncResponse res)
    {
        var synced = res.Character;

        if (synced == null)
        {
            return;
        }

        int index = _characters.FindIndex(character => character.CharacterId == synced.CharacterId);

        if (index < 0)
        {
            _characters.Add(synced);
            ClientLogger.Info(ClientLogger.Recv, $"새 캐릭터 도착 — 개체={synced.CharacterId}, TID={synced.CharacterTid}");
            Notify(CharactersChanged);

            return;
        }

        CharacterInfo previous = _characters[index];

        _characters[index] = synced;

        if (synced.Level > previous.Level)
        {
            ClientLogger.Info(ClientLogger.Recv,
                $"캐릭터 레벨업 — 개체={synced.CharacterId} Lv.{previous.Level} → Lv.{synced.Level}");

            // 캐시를 바꾼 뒤 · 'CharactersChanged'보다 먼저 — 받는 쪽(작업슬롯 칸 연출)이 새 레벨을 읽고,
            // 이어지는 다시 그리기가 연출을 덮지 않는다 (T-052)
            CharacterLeveledUp?.Invoke(synced.CharacterId, previous.Level, synced.Level);
        }

        Notify(CharactersChanged);
    }

    // 보유 장비 스냅샷 — 캐시 교체 후 이벤트 발행
    // ★ 로그인 시 자동으로 1회 수신(캐릭터 목록 뒤·작업슬롯 앞). 인벤토리에 있든 끼고 있든 전부 실려 온다.
    private void OnEquipListReceived(S_EquipListResponse res)
    {
        _equips.Clear();
        _equips.AddRange(res.Equips);

        Notify(EquipsChanged);
        NotifyPendingEnchants();
    }

    // 상급 큐브 결과를 고르지 않은 장비가 있으면 접속하자마자 알린다 (OnEquipListReceived — 접속마다 1회).
    // 보류는 서버에 남아 있어도 인벤토리 장비 탭을 열기 전에는 보이지 않는다 — 고르기 전에는 그 장비에 큐브를 더 쓸 수 없다.
    // 알림 화면('NoticePresenter')이 'ServerWaitManager'만 구독하므로 그 창구를 쓴다('WindowManager'와 같은 이유).
    private void NotifyPendingEnchants()
    {
        int count = 0;

        foreach (EquipInfo equip in _equips)
        {
            if (equip.PendingEnchantGrade > 0)
            {
                count++;
            }
        }

        if (count == 0)
        {
            return;
        }

        Services.Get<ServerWaitManager>().RaiseNotice(
            Lang.Format("UI.Common.PendingCube", count));
    }

    // 바뀐 장비 개체들 — 지급·장착·해제·자동 이동. 'EquipId'로 찾아 통째로 교체한다(확정값이다).
    //
    // ⚠️ 캐릭터('OnCharacterSynced')와 **반대로, 목록에 없는 개체는 추가한다.**
    //   캐릭터는 보유 목록의 원본이 로그인 스냅샷 하나뿐이라 푸시로 늘리지 않지만,
    //   장비는 이 패킷이 **지급 경로 그 자체**다(치트·가챠로 새 개체가 여기로 들어온다).
    private void OnEquipSynced(S_EquipSyncResponse res)
    {
        foreach (var synced in res.Equips)
        {
            int index = _equips.FindIndex(equip => equip.EquipId == synced.EquipId);

            if (index >= 0)
            {
                _equips[index] = synced;
            }
            else
            {
                _equips.Add(synced);
            }
        }

        Notify(EquipsChanged);
    }

    // 장착·해제 응답 — 결과만 알린다.
    //
    // ※ **바뀐 값은 여기로 오지 않는다.** 개체는 'S_EquipSyncResponse'가, 슬롯 속도는
    //   'S_WorkStationSlotSyncResponse'가 따로 싣고 온다 — 이 응답은 "받아들여졌는가"뿐이다.
    //   그래서 화면은 결과 문구에만 이걸 쓰고, 그리는 것은 'EquipsChanged'로 한다.
    private void OnEquipResponded(S_EquipResponse res)
    {
        EquipCompleted?.Invoke(res.Result == EResultCode.Ok, res.Result);
    }

    // 큐브 사용 응답 — 결과를 그대로 넘긴다.
    //
    // ※ 장착 응답과 같은 경계다 — **바뀐 개체는 'S_EquipSyncResponse'가 따로 싣고 온다.**
    //   여기 'Options'·등급은 결과 화면(이전 ▶ 이후)을 그리는 데만 쓰고, 캐시는 건드리지 않는다.
    private void OnEquipEnchantResponded(S_EquipEnchantResponse res)
    {
        EquipEnchantCompleted?.Invoke(res);
    }

    // 상급 큐브 결과 고르기 응답 — 결과를 그대로 넘긴다. 보류가 풀린 장비는 'S_EquipSyncResponse'가 따로 싣고 온다.
    private void OnEquipEnchantChooseResponded(S_EquipEnchantChooseResponse res)
    {
        EquipEnchantChooseCompleted?.Invoke(res);
    }

    // 응축을 보낸다고 적어 둔다 — 응답에 재료 ID가 없어서다 ('CharacterCondensePresenter'가 'C_CharacterCondenseRequest'를 보내며 호출).
    // ※ 송신은 Presenter가 한다(이 모델은 보내지 않는다). 여기는 성공했을 때 무엇을 지울지만 기억한다('BeginEntitySell'과 같다).
    public void BeginCondense(IReadOnlyList<long> materialIds)
    {
        _condensingMaterialIds.Clear();
        _condensingMaterialIds.AddRange(materialIds);
    }

    // 응축 응답 — 성공이면 재료를 지우고 대상을 새 값으로 덮은 뒤 결과를 알린다.
    //
    // ★ 지우는 일은 여기만 한다 — 서버는 재료 제거를 동기화 패킷으로 따로 보내지 않는다('OnEntitySold'와 같다).
    // ★ 캐시를 먼저 고치고 'CharactersChanged'를 낸 뒤 완료를 알린다 — 완료를 받은 화면이 새 ★을 읽을 수 있어야 한다.
    private void OnCharacterCondenseResponded(S_CharacterCondenseResponse res)
    {
        if (res.Result == EResultCode.Ok)
        {
            _characters.RemoveAll(character => _condensingMaterialIds.Contains(character.CharacterId));

            CharacterInfo? target = res.Character;

            if (target != null)
            {
                int index = _characters.FindIndex(character => character.CharacterId == target.CharacterId);

                if (index >= 0)
                {
                    _characters[index] = target;
                }
            }

            Notify(CharactersChanged);
        }
        else
        {
            ClientLogger.Warn(ClientLogger.Recv, $"응축 실패 — 결과={res.Result}");
        }

        _condensingMaterialIds.Clear();
        CharacterCondenseCompleted?.Invoke(res);
    }

    // 잠금 응답 — 성공이면 그 개체의 'IsLocked'(자원은 잠긴 종류 목록)를 바꾸고 알린다(T-137 · T-159).
    //
    // ★ 목록이 다시 오지 않는다 — 캐시를 여기서 직접 고친다(서버 이슈 #65). 같은 상태로 보내도 Ok가 온다.
    // ★ 캐시를 먼저 고치고 'CharactersChanged'·'EquipsChanged'·'InventoryChanged'를 낸 뒤 완료를 알린다 — 자물쇠가 바로 그려진다.
    // ※ 자원은 'Id'가 아이템 TID다. 보유 여부와 상관없이 기록한다 — 0개여도 잠금은 남는다.
    private void OnEntityLockResponded(S_EntityLockResponse res)
    {
        if (res.Result == EResultCode.Ok)
        {
            if (res.Target == ELockTarget.Character && FindCharacter(res.Id) is CharacterInfo character)
            {
                character.IsLocked = res.Locked;
                Notify(CharactersChanged);
            }
            else if (res.Target == ELockTarget.Equip && FindEquip(res.Id) is EquipInfo equip)
            {
                equip.IsLocked = res.Locked;
                Notify(EquipsChanged);
            }
            else if (res.Target == ELockTarget.Resource)
            {
                int itemId = (int)res.Id;

                if (res.Locked ? _lockedItemTids.Add(itemId) : _lockedItemTids.Remove(itemId))
                {
                    Notify(InventoryChanged);
                }
            }
        }
        else
        {
            ClientLogger.Warn(ClientLogger.Recv, $"잠금 실패 — 결과={res.Result} 대상={res.Target} id={res.Id}");
        }

        EntityLockCompleted?.Invoke(res);
    }

    // 작업슬롯 스냅샷 — 캐시 교체 후 이벤트 발행
    // ★ 로그인 시 자동으로 1회 수신. 슬롯 조회 요청 패킷이 없어 전체 스냅샷은 이때뿐이고,
    //   이후에는 배치 응답으로 한 칸씩만 갱신된다.
    private void OnWorkStationSlotsReceived(S_WorkStationSlotsResponse res)
    {
        _workStationSlots.Clear();
        if (res.Slots != null)
        {
            _workStationSlots.AddRange(res.Slots);
        }

        Notify(WorkStationSlotsChanged);
    }

    // 슬롯 배치 응답 — 변경된 슬롯 하나만 오므로 캐시에 병합한다.
    // 스냅샷을 다시 받지 않으므로 여기서 반영하지 않으면 캐시가 서버 상태와 어긋난다.
    //
    // ※ 배치였는지 해제였는지는 알리지 않는다 — 실패 응답에는 슬롯이 실려 오지 않아(Slot=null)
    //   수신만으로는 구분할 수 없다. 그건 요청을 보낸 UI가 안다.
    private void OnWorkStationAssigned(S_WorkStationAssignResponse res)
    {
        var  changed = res.Slot;
        bool success = res.Result == EResultCode.Ok;

        // 실패 사유는 결과 코드에만 들어 있다(미보유 캐릭터·적성 0·없는 슬롯…).
        // 여기서 남기지 않으면 UI는 "실패했다"까지만 알고 왜인지는 서버 콘솔을 봐야 안다.
        if (!success)
        {
            ClientLogger.Warn(ClientLogger.Recv, $"작업슬롯 변경 실패 — 결과={res.Result}");
        }

        if (success && changed != null)
        {
            int index = _workStationSlots.FindIndex(slot => slot.SlotIndex == changed.SlotIndex);

            if (index >= 0)
            {
                _workStationSlots[index] = changed;
            }
            else
            {
                _workStationSlots.Add(changed);
            }

            Notify(WorkStationSlotsChanged);
        }

        WorkStationAssignCompleted?.Invoke(success, res.Result);
    }

    // 채취 결과 푸시 — 판정이 완성될 때마다 요청 없이 도착한다(수확이 없으면 오지 않는다).
    private void OnGatherResultReceived(S_GatherResultResponse res)
    {
        long earned = SumGainedValue(res.ItemChanges); // 덮어쓰기 전에 센다 — 지나면 이전 수량이 사라진다

        ApplyItemChanges(res.ItemChanges);
        Notify(GatherResultReceived, res);

        if (earned > 0)
        {
            GatherValueEarned?.Invoke(earned);
        }
    }

    // 이번 변경으로 늘어난 수량 × 즉시 판매가의 합 (OnGatherResultReceived에서 호출 — 반영 전에 불러야 한다).
    //
    // ⚠️ 'ItemChangeInfo.Count'는 늘어난 양이 아니라 그 칸의 갱신 후 수량이다 — 캐시의 그 칸 수량을 빼야 증가분이 나온다.
    // ※ 기준이 시세가 아니라 'BasePrice'인 이유: 고정값이라 같은 수확이 언제나 같은 숫자로 보인다(2026-10-09 사용자 결정).
    private long SumGainedValue(List<ItemChangeInfo>? changes)
    {
        if (changes == null)
        {
            return 0;
        }

        long sum = 0;

        foreach (var change in changes)
        {
            ItemInfo? held   = FindItemAt(change.Container, change.Slot);
            int       before = held != null && held.ItemId == change.ItemId ? held.Count : 0;
            int gained = change.Count - before;

            if (gained > 0)
            {
                sum += (long)gained * GameDataLoader.GetItemPrice(change.ItemId);
            }
        }

        return sum;
    }

    // 슬롯 1칸 동기화 — 정산·속도 변경 후 도착한다. 카운트다운 기준점이 매번 교정된다.
    private void OnWorkStationSlotSynced(S_WorkStationSlotSyncResponse res)
    {
        var synced = res.Slot;

        if (synced == null)
        {
            return;
        }

        int index = _workStationSlots.FindIndex(slot => slot.SlotIndex == synced.SlotIndex);

        if (index >= 0)
        {
            _workStationSlots[index] = synced;
        }
        else
        {
            _workStationSlots.Add(synced);
        }

        Notify(WorkStationSlotsChanged);
    }

    // 아이템 증감 푸시 (가챠·즉시 지급 등)
    private void OnItemUpdated(S_UpdateItemResponse res)
    {
        ApplyItemChanges(res.ItemChangeInfos);
    }

    // 판매 응답 — 인벤토리 반영 후 결과 이벤트 발행. 'OnGachaDrawn'과 같은 모양이다.
    //
    // ★ 골드를 여기서 건드리지 않는다 — 'GainedGold'는 이번에 번 금액이고,
    //   확정 잔액은 뒤이어 오는 'S_CurrencyResponse'가 내려준다. 여기서 더하면 두 번 오른다.
    // ★ 전부 되거나 전혀 안 된다 — 실패면 인벤토리도 그대로다.
    private void OnItemSold(S_ItemSellResponse res)
    {
        if (res.Result != EResultCode.Ok)
        {
            ClientLogger.Warn(ClientLogger.Recv, $"판매 실패 — 결과={res.Result}");
            ItemSellFailed?.Invoke(res.Result);

            return;
        }

        ApplyItemChanges(res.ItemChangeInfos);
        ItemSellCompleted?.Invoke(res.GainedGold);
    }

    // 개체 판매를 보낸다고 적어 둔다 — 응답에 개체 ID가 없어서다 (SellCartPresenter가 'C_EntitySellRequest'를 보내며 호출).
    // ※ 송신은 Presenter가 한다(이 모델은 보내지 않는다). 여기는 성공했을 때 무엇을 지울지만 기억한다.
    public void BeginEntitySell(IReadOnlyList<long> characterIds, IReadOnlyList<long> equipIds)
    {
        _sellingCharacterIds.Clear();
        _sellingCharacterIds.AddRange(characterIds);
        _sellingEquipIds.Clear();
        _sellingEquipIds.AddRange(equipIds);
    }

    // 개체 판매 응답 — 성공이면 보낸 개체를 전부 지운다(전부 되거나 전혀 안 된다).
    //
    // ★ 골드는 건드리지 않는다 — 'OnItemSold'와 같은 이유('S_CurrencyResponse'가 확정 잔액을 준다).
    // ★ 지우는 일은 여기만 한다 — 서버는 개체 제거를 동기화 패킷으로 따로 보내지 않는다.
    private void OnEntitySold(S_EntitySellResponse res)
    {
        if (res.Result != EResultCode.Ok)
        {
            ClientLogger.Warn(ClientLogger.Recv, $"개체 판매 실패 — 결과={res.Result}");
            _sellingCharacterIds.Clear();
            _sellingEquipIds.Clear();
            EntitySellFailed?.Invoke(res.Result);

            return;
        }

        int removedCharacters = _characters.RemoveAll(character => _sellingCharacterIds.Contains(character.CharacterId));
        int removedEquips     = _equips.RemoveAll(equip => _sellingEquipIds.Contains(equip.EquipId));

        _sellingCharacterIds.Clear();
        _sellingEquipIds.Clear();

        if (removedCharacters > 0)
        {
            Notify(CharactersChanged);
        }

        if (removedEquips > 0)
        {
            Notify(EquipsChanged);
        }

        EntitySellCompleted?.Invoke(res.GainedGold);
    }

    // 장비 분해를 보낸다고 적어 둔다 — 응답에 장비 ID가 없어서다 (SellCartPresenter가 'C_EquipDisassembleRequest'를 보내며 호출).
    public void BeginDisassemble(IReadOnlyList<long> equipIds)
    {
        _disassemblingEquipIds.Clear();
        _disassemblingEquipIds.AddRange(equipIds);
    }

    // 장비 분해 응답 — 성공이면 보낸 장비를 지우고 받은 재료를 인벤토리에 반영한다(전부 되거나 전혀 안 된다).
    // ★ 지우는 일은 여기만 한다 — 서버는 장비 제거를 동기화 패킷으로 따로 보내지 않는다('OnEntitySold'와 같다).
    private void OnEquipDisassembled(S_EquipDisassembleResponse res)
    {
        if (res.Result != EResultCode.Ok)
        {
            ClientLogger.Warn(ClientLogger.Recv, $"장비 분해 실패 — 결과={res.Result}");
            _disassemblingEquipIds.Clear();
            EquipDisassembleFailed?.Invoke(res.Result);

            return;
        }

        int removed = _equips.RemoveAll(equip => _disassemblingEquipIds.Contains(equip.EquipId));

        _disassemblingEquipIds.Clear();

        if (removed > 0)
        {
            Notify(EquipsChanged);
        }

        ApplyItemChanges(res.ItemChangeInfos);
        EquipDisassembleCompleted?.Invoke();
    }

    // 아이템 사용 응답 — 인벤토리 반영 후 보상 이벤트 발행. 'OnGachaDrawn'과 같은 모양이다.
    //
    // ★ 한 패킷에 두 가지가 실려 오는 것도 가챠와 같다.
    //   ItemChangeInfos = 갱신 후 누적 총량(쓴 상자의 차감과 받은 아이템) → 인벤토리 반영
    //   Rewards         = 이번에 얻은 개별 항목(델타) → 연출 전용
    // ★ 골드와 장비는 여기서 건드리지 않는다 — 골드는 'S_CurrencyResponse',
    //   장비 개체는 'S_EquipSyncResponse'로 따로 온다. 보상 목록의 골드는 **보여 주기 위한 값**이다.
    // ★ 'StoredInMail'이면 인벤토리가 모자라 보상 전체가 우편 한 통으로 갔다 — 'Rewards'는 연출용으로 그대로 오고,
    //   'ItemChangeInfos'에는 상자 차감만 있다. 그 우편은 'S_MailArrivedResponse'로 따로 온다.
    private void OnItemUsed(S_ItemUseResponse res)
    {
        if (res.Result != EResultCode.Ok)
        {
            ClientLogger.Warn(ClientLogger.Recv, $"아이템 사용 실패 — TID={res.ItemTID}, 결과={res.Result}");
            ItemUseFailed?.Invoke(res.Result);

            return;
        }

        ApplyItemChanges(res.ItemChangeInfos);
        ItemUseCompleted?.Invoke(res.ItemTID, res.Rewards ?? new List<GachaRewardInfo>(), res.StoredInMail);
    }

    // 재화 통지 — 스냅샷과 변경이 같은 패킷이라 덮어쓰기만 하면 된다.
    //
    // ★ 값이 델타가 아니라 확정 잔액이라 안전하다.
    private void OnCurrencyReceived(S_CurrencyResponse res)
    {
        Gold = res.Gold;

        Notify(CurrencyChanged);
    }

    // 열린 해금 전체 — 로그인 직후 1회. 스냅샷이라 비우고 채운다.
    // 서버가 슬롯 스냅샷보다 **먼저** 보내므로 목록 화면이 칸을 그릴 때는 이미 채워져 있다.
    private void OnUnlockListReceived(S_UnlockListResponse res)
    {
        _unlockedTids.Clear();
        _unlockedTids.UnionWith(res.UnlockTIDs);

        Notify(UnlocksChanged);
    }

    // 해금 결과 — 성공이면 목록에 더하고 알린다.
    //
    // ★ 내가 보낸 요청이 아니어도 온다(치트·퀘스트로 서버가 직접 연 경우). 그래서 목록 갱신은 요청 여부와
    //   상관없이 하고, "내 요청의 결과인가"는 받는 Presenter가 대기 핸들로 가린다.
    // ★ 골드·슬롯은 여기서 건드리지 않는다 — 차감 잔액은 'S_CurrencyResponse',
    //   새 칸은 'S_WorkStationSlotSyncResponse'가 뒤이어 온다('OnItemSold'와 같은 이유).
    private void OnUnlockResponded(S_UnlockResponse res)
    {
        bool success = res.Result == EResultCode.Ok;

        if (success)
        {
            _unlockedTids.Add(res.UnlockTID);
            Notify(UnlocksChanged);
        }
        else
        {
            ClientLogger.Warn(ClientLogger.Recv, $"해금 실패 — {res.UnlockTID}, 결과={res.Result}");
        }

        UnlockCompleted?.Invoke(success, res.Result);
    }

    // 계정 레벨 스냅샷 — 재화와 같은 관례로 통째로 덮어쓴다(델타가 아니다).
    // 로그인 직후 1회 + 경험치가 오를 때 + 특성 포인트를 쓸 때 온다.
    private void OnAccountLevelReceived(S_AccountLevelResponse res)
    {
        AccountLevel = res.Level;
        AccountExp   = res.Exp;
        TraitPoint   = res.TraitPoint;

        Notify(AccountLevelChanged);
    }

    // 특성 레벨 올리기 결과 — 레벨을 반영하고 결과 코드를 나른다.
    //
    // ★ 실패여도 'Level'(지금 레벨)을 반영한다 — 서버가 "지금 이 레벨이다"라고 알려 준 것이라
    //   캐시가 어긋나 있었다면 여기서 맞는다. 남은 포인트는 앞서 온 'S_AccountLevelResponse'가 채운다.
    private void OnUserTraitLearnResponded(S_UserTraitLearnResponse res)
    {
        bool success = res.Result == EResultCode.Ok;

        if (!success)
        {
            ClientLogger.Warn(ClientLogger.Recv, $"특성 레벨 올리기 실패 — {res.UserTraitTID}, 결과={res.Result}");
        }

        if (GameDataLoader.TryGetUserTrait(res.UserTraitTID, out _) && res.Level != GetTraitLevel(res.UserTraitTID))
        {
            _traitLevels[res.UserTraitTID] = res.Level;
            Notify(TraitsChanged);
        }

        TraitLearnCompleted?.Invoke(success, res.Result);
    }

    // 특성 레벨 전체 — 로그인 직후 1회. 스냅샷이라 비우고 채운다.
    // 기본 레벨보다 올린 특성만 실리므로 빠진 특성은 'GetTraitLevel'이 기본 레벨로 읽는다.
    private void OnUserTraitListReceived(S_UserTraitListResponse res)
    {
        _traitLevels.Clear();

        foreach (var info in res.Traits)
        {
            _traitLevels[info.UserTraitTID] = info.Level;
        }

        Notify(TraitsChanged);
    }

    #endregion

    #region 아티팩트 (ServerPacketHandler 구독)

    // 도감 스냅샷 — 비우고 채운다 (로그인 직후 · 장착 · 켜고 끌 때)
    private void OnArtifactListReceived(S_ArtifactListResponse res)
    {
        _artifactCodex.Clear();

        foreach (ArtifactCodexInfo info in res.Artifacts)
        {
            _artifactCodex[info.ArtifactTid] = info.IsActive;
        }

        Notify(ArtifactsChanged);
    }

    // 아티팩트 칸 스냅샷 — 비우고 채운다 (로그인 직후 · 감정 · 우편 수령 · 장착 · 판매 · 경매 등록 · 이동)
    private void OnArtifactInventoryReceived(S_ArtifactInventoryResponse res)
    {
        _artifactStacks.Clear();
        _artifactStacks.AddRange(res.Stacks);

        Notify(ArtifactsChanged);
    }

    #endregion

    #region 우편 (ServerPacketHandler 구독)

    // 우편함 전체 — 로그인 직후 1회. 스냅샷이라 비우고 채운다.
    // ※ 다른 스냅샷보다 늦게 온다 — 그 전에 우편함을 열었으면 빈 목록이다가 여기서 채워진다.
    private void OnMailListReceived(S_MailListResponse res)
    {
        _mails.Clear();
        if (res.Mails != null)
        {
            _mails.AddRange(res.Mails);
        }

        Notify(MailsChanged);
    }

    // 새 우편 — 더하기만 한다. 같은 id가 이미 있으면 교체한다(두 번 쌓이지 않게).
    private void OnMailArrived(S_MailArrivedResponse res)
    {
        if (res.Mails == null || res.Mails.Count == 0)
        {
            return;
        }

        foreach (var arrived in res.Mails)
        {
            int index = _mails.FindIndex(mail => mail.MailId == arrived.MailId);

            if (index >= 0)
            {
                _mails[index] = arrived;
            }
            else
            {
                _mails.Add(arrived);
            }
        }

        Notify(MailsChanged);
    }

    // 수령 결과 — 받은 우편을 "받음"으로 바꾸고 아이템을 반영한 뒤 결과를 알린다.
    //
    // ★ 결과가 실패여도 목록을 반영한다 — 모두 받기가 'StorageFull'로 멈춰도
    //   그 전까지 받은 우편은 'ClaimedMailIds'에 실려 온다(서버는 이미 지급했다).
    // ※ 받은 시각은 서버가 보내지 않는다 — 게임 시계('ServerClock')의 지금으로 채운다. 표시에만 쓰고,
    //   7일 정리는 서버가 자기 시각으로 한다.
    private void OnMailClaimResponded(S_MailClaimResponse res)
    {
        int claimedCount = res.ClaimedMailIds?.Count ?? 0;

        if (res.Result != EResultCode.Ok)
        {
            ClientLogger.Warn(ClientLogger.Recv, $"우편 수령 실패 — 결과={res.Result}, 그 전까지 받음 {claimedCount}통");
        }

        ApplyItemChanges(res.ItemChangeInfos);

        if (claimedCount > 0)
        {
            long now     = ServerClock.UtcNowUnixMs;
            var  claimed = new List<MailInfo>(claimedCount);

            foreach (long mailId in res.ClaimedMailIds!)
            {
                MailInfo? mail = _mails.Find(m => m.MailId == mailId);

                if (mail != null)
                {
                    mail.ClaimedAtUnixMs = now;
                    claimed.Add(mail);
                }
            }

            Notify(MailsChanged);

            // ※ 응답에는 받은 것의 목록이 없다 — 우편 캐시의 첨부(보낸 순간 복사된 값)가 곧 받은 것이다.
            //   모두 받기가 'StorageFull'로 멈춰도 그 전까지 받은 것은 보여 준다.
            if (claimed.Count > 0)
            {
                Notify(MailRewardsClaimed, claimed);
            }
        }

        MailClaimCompleted?.Invoke(res.Result, claimedCount, res.RemainingCount);
    }

    // 삭제 결과 — 성공이면 목록에서 뺀다.
    private void OnMailDeleteResponded(S_MailDeleteResponse res)
    {
        bool success = res.Result == EResultCode.Ok;

        if (success)
        {
            _mails.RemoveAll(mail => mail.MailId == res.MailId);
            Notify(MailsChanged);
        }
        else
        {
            ClientLogger.Warn(ClientLogger.Recv, $"우편 삭제 실패 — 우편={res.MailId}, 결과={res.Result}");
        }

        MailDeleteCompleted?.Invoke(success, res.Result);
    }

    #endregion

    #region 인벤토리 반영

    // 경매 등록 응답 — 올린 물건을 인벤토리에서 뺀다. 결과 알림은 'AuctionModel'이 맡는다.
    //
    // ★ 성공이면 서버가 이미 뺐다 — 자원은 'ItemChangeInfos'(누적 총량), 장비는 'EquipId', 캐릭터는 'CharacterId'로 지운다.
    //   개체 제거는 동기화 패킷으로 오지 않으므로 여기서 지우지 않으면 인벤토리에 유령이 남는다.
    // ⚠️ 거절 응답에도 'EquipId'·'CharacterId'가 실려 온다 — 결과를 먼저 본다.
    private void OnAuctionRegisterResponded(S_AuctionRegisterResponse res)
    {
        if (res.Result != EResultCode.Ok)
        {
            return;
        }

        ApplyItemChanges(res.ItemChangeInfos);

        if (res.EquipId != 0L && _equips.RemoveAll(equip => equip.EquipId == res.EquipId) > 0)
        {
            Notify(EquipsChanged);
        }

        if (res.CharacterId != 0L && _characters.RemoveAll(character => character.CharacterId == res.CharacterId) > 0)
        {
            Notify(CharactersChanged);
        }
    }

    // 아이템 변경분을 인벤토리 캐시에 반영하고 'InventoryChanged'를 발행한다.
    // Count는 델타가 아니라 그 칸의 갱신 후 수량이다 — 더하지 말고 덮어쓴다
    // ('PacketInfo.ItemChangeInfo' 주석).
    //
    // 아이템이 늘어나는 모든 경로가 이 하나를 쓴다(채취·즉시 지급·가챠).
    // 수량은 전부 서버가 정한 값이고 클라는 계산하지 않는다 — 경로가 늘어도 규칙은 그대로다.
    private void ApplyItemChanges(List<ItemChangeInfo>? changes)
    {
        if (changes == null || changes.Count == 0)
        {
            return;
        }

        foreach (var change in changes)
        {
            SetItemSlot(change.Container, change.Slot, change.ItemId, change.Count);
        }

        Notify(InventoryChanged);
    }

    // 칸 하나를 덮어쓴다 — 0개면 그 칸을 비운다 (아이템 변경 · 정렬·칸 이동 · 창고 이동에서 호출).
    //
    // ★ 캐시의 열쇠는 (보관함, 칸)이다 — 아이템 TID가 아니다(T-160 · T-161).
    //   같은 자원이 여러 칸에 나뉘므로 TID로 찾으면 두 칸이 한 줄로 뭉개진다(9,999개를 넘겨 받은 몫이 화면에서 사라졌다).
    // ※ 비운 칸은 지운다 — 남겨 두면 그 칸에 다음에 들어오는 다른 자원과 한 칸에 두 줄이 생긴다.
    private void SetItemSlot(EContainer container, int slot, int itemId, int count)
    {
        int index = _inventory.FindIndex(item => item.Container == container && item.Slot == slot);

        if (count <= 0)
        {
            if (index >= 0)
            {
                _inventory.RemoveAt(index);
            }

            return;
        }

        if (index >= 0)
        {
            _inventory[index].ItemId = itemId;
            _inventory[index].Count  = count;

            return;
        }

        _inventory.Add(new ItemInfo
        {
            ItemId    = itemId,
            Count     = count,
            Container = container,
            Slot      = slot
        });
    }

    // 인벤토리·창고 [정렬]·칸 이동 결과 — 성공이면 칸 번호를 캐시에 반영하고 그 탭의 변경 이벤트를 발행한다 (ServerPacketHandler 구독)
    //
    // ★ 칸은 **이 응답이 온 뒤에만** 바뀐다 — 놓는 순간 미리 옮기지 않는다(낙관적 갱신 없음 · T-044).
    //   자리의 주인은 서버 DB다. 거절이면 아무것도 바뀌지 않고 결과만 알린다.
    // ※ 정렬이면 탭 전체, 이동이면 바뀐 칸만(교환이면 둘) 온다 — 실린 것만 덮어쓰면 둘 다 맞는다.
    // ★ 자원 탭은 'Items'(칸마다 한 줄 · 0개면 빈 칸)가 정본이다(T-160). 'Slots'의 자원 줄은 칸이 하나뿐일 때만 맞는 옛 형식이라 읽지 않는다.
    private void OnStorageSlotsReceived(S_StorageSlotsResponse res)
    {
        if (res.Result == EResultCode.Ok && res.Tab == EStorageTab.Resource)
        {
            foreach (ItemChangeInfo change in res.Items)
            {
                SetItemSlot(change.Container, change.Slot, change.ItemId, change.Count);
            }

            Notify(InventoryChanged);
        }
        else if (res.Result == EResultCode.Ok)
        {
            _slotByKey.Clear();

            foreach (StorageSlotInfo slot in res.Slots)
            {
                _slotByKey[slot.Key] = slot.Slot;
            }

            ApplySlots(res.Container, res.Tab);
        }

        StorageSlotsResponded?.Invoke(res.Result);
    }

    // 받은 칸 번호('_slotByKey')를 그 보관함·탭의 캐시에 덮어쓰고 변경 이벤트를 발행한다 (OnStorageSlotsReceived에서 호출).
    // ※ 캐릭터·장비만 온다 — 자원은 'Items'로 칸을 덮어쓴다('OnStorageSlotsReceived'). 같은 개체 번호가 두 보관함에 있을 수 없지만 보관함을 가린다.
    private void ApplySlots(EContainer container, EStorageTab tab)
    {
        switch (tab)
        {
            case EStorageTab.Character:
                foreach (CharacterInfo character in _characters)
                {
                    if (character.Container == container && _slotByKey.TryGetValue(character.CharacterId, out int slot))
                    {
                        character.Slot = slot;
                    }
                }

                Notify(CharactersChanged);
                break;

            case EStorageTab.Equip:
                foreach (EquipInfo equip in _equips)
                {
                    if (equip.Container == container && _slotByKey.TryGetValue(equip.EquipId, out int slot))
                    {
                        equip.SlotPosition = slot;
                    }
                }

                Notify(EquipsChanged);
                break;
        }
    }

    // 인벤토리 ↔ 창고 이동 결과 — 성공이면 양쪽 보관함의 바뀐 칸을 캐시에 반영한다 (ServerPacketHandler 구독)
    //
    // ★ 칸 이동과 같이 **응답이 온 뒤에만** 바뀐다(낙관적 갱신 없음). 거절이면 'Changes'가 비어 있다.
    // ※ 자원의 'Count'는 그 칸의 수량이다 — 0이면 그 칸이 비었다(T-160 · 'ApplyItemChanges'와 같다).
    //   캐릭터·장비는 그 개체가 'Container'의 'Slot'으로 옮겨졌다는 뜻이다.
    private void OnWarehouseTransferred(S_WarehouseTransferResponse res)
    {
        if (res.Result == EResultCode.Ok && res.Changes.Count > 0)
        {
            switch (res.Tab)
            {
                case EStorageTab.Resource:
                    ApplyItemTransfers(res.Changes);
                    Notify(InventoryChanged);
                    break;

                case EStorageTab.Character:
                    ApplyCharacterTransfers(res.Changes);
                    Notify(CharactersChanged);
                    break;

                case EStorageTab.Equip:
                    ApplyEquipTransfers(res.Changes);
                    Notify(EquipsChanged);
                    break;
            }
        }

        WarehouseTransferResponded?.Invoke(res.Result);
    }

    // 자원 이동분을 칸마다 덮어쓴다 (OnWarehouseTransferred에서 호출).
    private void ApplyItemTransfers(List<WarehouseChangeInfo> changes)
    {
        foreach (WarehouseChangeInfo change in changes)
        {
            SetItemSlot(change.Container, change.Slot, (int)change.Key, change.Count);
        }
    }

    // 캐릭터 이동분 — 개체의 보관함과 칸을 바꾼다 (OnWarehouseTransferred에서 호출).
    private void ApplyCharacterTransfers(List<WarehouseChangeInfo> changes)
    {
        foreach (WarehouseChangeInfo change in changes)
        {
            if (FindCharacter(change.Key) is CharacterInfo character)
            {
                character.Container = change.Container;
                character.Slot      = change.Slot;
            }
        }
    }

    // 장비 이동분 — 개체의 보관함과 칸을 바꾼다 (OnWarehouseTransferred에서 호출).
    private void ApplyEquipTransfers(List<WarehouseChangeInfo> changes)
    {
        foreach (WarehouseChangeInfo change in changes)
        {
            if (FindEquip(change.Key) is EquipInfo equip)
            {
                equip.Container    = change.Container;
                equip.SlotPosition = change.Slot;
            }
        }
    }

    #endregion
}
