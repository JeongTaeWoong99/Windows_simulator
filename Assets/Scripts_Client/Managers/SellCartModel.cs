using System;
using System.Collections.Generic;
using GameData;
using MikaProtocol;
using UnityEngine;

// 판매하려고 담아 둔 것들의 목록 — "장바구니". 자원(수량) · 캐릭터 · 장비(1개씩) 중 **한 종류만** 담는다.
// 아티팩트(번호 · 수량)는 장비 탭에 함께 보여서 **장비와 같은 종류**로 센다(T-151 · 2026-10-25 사용자 결정).
//
// ■ 왜 매니저인가
// 인벤토리 격자('InventoryGridPresenter')는 어느 칸에 담김 표시를 켤지 알아야 하고,
// 정보 칸('SellCartPresenter')은 목록·합계·판매 버튼을 그려야 한다.
// 두 화면이 같은 것을 봐야 하는데 서로를 직접 참조하면 패널 사이 참조가 그물이 된다
// ('Inventory 규칙.md'). 그래서 상태를 여기 한 곳에 두고 양쪽이 각자 구독한다.
//
// ■ 송신하지 않는다
// 판매 요청은 버튼을 누른 Presenter가 직접 보낸다 — 'PlayerDataModel'과 같은 경계다.
// 여기는 "무엇을 얼마나 담았나"와 그 합계까지만 안다.
//
// ⚠️ 인벤토리가 줄면 담긴 수량도 함께 줄여야 한다
//   서버 판매는 **전부 되거나 전혀 안 된다** — 목록 중 한 종류라도 보유량이 모자라면
//   'NotEnoughItem'으로 거절되고 아무것도 팔리지 않는다. 방치형이라 담아 둔 사이에도
//   채취·판매로 인벤토리가 계속 바뀌므로, 여기서 따라가지 않으면 판매가 통째로 막힌다.
//
// ■ 한 종류만 담는다 (#83 · 2026-10-25 사용자 결정)
//   다른 종류를 담으면 담겨 있던 종류를 먼저 비운다 — 목록 머리에 "무엇을 파는지" 한 가지만 적고,
//   장비일 때만 [마석으로 분해]를 함께 내보이려면 목록이 한 종류여야 한다. 탭을 바꿀 때 비우는 일은 'SellCartPresenter'가 한다.
//
// ■ 개체(캐릭터·장비)는 패킷이 다르다 (T-075 · 2026-10-03)
//   서버 패킷이 둘이다 — 자원은 'C_ItemSellRequest', 개체는 'C_EntitySellRequest'.
//   개체도 같은 이유로 따라간다 — 사라졌거나(경매 등록·판매) 일하게 된(배치·착용) 개체는 목록에서 뺀다.
//   서버는 일하는 캐릭터·끼고 있는 장비·마지막 캐릭터를 거절하고, 하나라도 걸리면 개체 판매 전체가 막힌다.
//   ※ 담기 전에 막는 일(사유 알림)은 격자가 한다('EntityBlockText'). 여기는 담긴 뒤의 변화만 따라간다.
//
// ■ 아티팩트는 패킷이 또 따로다 — 'C_ArtifactSellRequest'(번호 · 수량). 서버 저장도 장비와 다른 아티팩트 칸이다.
//   장비와 함께 담겼으면 [판매] 한 번에 두 패킷이 나간다(자원 + 개체와 같은 방식).
//   분해는 장비와 한 요청이다 — 'C_EquipDisassembleRequest.Artifacts'(#89 · 마석 수는 'DisassembleTable.ArtifactItemCount').
public class SellCartModel : MonoService<SellCartModel>
{
    // 담긴 항목들. 'ItemInfo.Count'는 "팔 개수"(델타)다 — 보유량이 아니다.
    private readonly List<ItemInfo> _entries = new List<ItemInfo>();

    // 담긴 개체 번호들 — 캐릭터·장비. 개체는 하나씩이라 수량이 없다.
    private readonly List<long> _characterIds = new List<long>();
    private readonly List<long> _equipIds     = new List<long>();

    // 담긴 아티팩트 — 'ArtifactInfo.Count'는 팔 개수다(보유량이 아니다).
    private readonly List<ArtifactInfo> _artifacts = new List<ArtifactInfo>();

    public IReadOnlyList<ItemInfo>     Entries      => _entries;
    public IReadOnlyList<ArtifactInfo> Artifacts    => _artifacts;
    public IReadOnlyList<long>     CharacterIds => _characterIds;
    public IReadOnlyList<long>     EquipIds     => _equipIds;

    // 담긴 줄 수 — 자원 종류 + 개체 수. 비어 있으면 판매 버튼을 잠근다.
    public int Count => _entries.Count + _characterIds.Count + _equipIds.Count + _artifacts.Count;

    public bool HasItems     => _entries.Count > 0;
    public bool HasEntities  => _characterIds.Count > 0 || _equipIds.Count > 0;
    public bool HasArtifacts => _artifacts.Count > 0;

    // 지금 담긴 종류 — 비어 있으면 null. 한 종류만 담기므로 하나로 정해진다.
    public SellCartKind? Kind => _entries.Count > 0      ? SellCartKind.Item
                               : _characterIds.Count > 0 ? SellCartKind.Character
                               : _equipIds.Count > 0 || _artifacts.Count > 0 ? SellCartKind.Equip
                               : null;

    // 담긴 장비를 모두 분해하면 받을 재료 수량 · 그 재료 TID('DisassembleTable'). 장비가 없으면 0.
    // ※ 미리보기다 — 실제 지급은 서버가 정한다('S_EquipDisassembleResponse').
    public int TotalShards  { get; private set; }
    public int ShardItemId  { get; private set; }

    // 담긴 것을 다 팔면 받을 골드. 기준가(BasePrice) x 수량에 즉시 판매율을 곱한 값이다('AuctionModel.InstantSellTotal').
    // ※ 미리보기일 뿐이다 — 실제 지급액은 서버가 정한다('S_ItemSellResponse' · 'S_EntitySellResponse'의 'GainedGold').
    public long TotalPrice { get; private set; }

    // Rare 이상이 담겨 있나. 오판매를 막으려고 화면이 경고를 띄우는 근거다.
    public bool HasHighRarity { get; private set; }

    // 도감에 없는 아티팩트(NEW)가 담겨 있나 — 팔면 도감에 남지 않아 화면이 경고한다.
    public bool HasUncollectedArtifact { get; private set; }

    // 담긴 성장 개체 수 — 키운 캐릭터 · 인챈트 장비(T-170). 다시 키우기 어려워 화면이 몇 개인지 경고한다.
    public int GrownCount { get; private set; }

    // 캐릭터를 키웠다고 보는 레벨 — 응축 재료 확인('CharacterCondensePresenter.ConfirmMaterialLevel')과 같은 기준이다.
    public const int GrownCharacterLevel = 2;

    // 성장한 개체인가 — 캐릭터는 레벨을 올렸거나 응축을 쌓았다(★ 전 누적 포함), 장비는 인챈트가 붙었다.
    // 판매 목록 줄 표시와 경고 개수가 같은 기준을 쓴다.
    // ※ ★만 보면 일하며 레벨이 오른 캐릭터와 ★ 직전까지 응축을 쌓은 캐릭터가 경고 없이 팔린다(2026-10-27 사용자 지적).
    public static bool IsGrown(MikaProtocol.CharacterInfo? character)
        => character != null && (character.Level >= GrownCharacterLevel || character.CondenseCount > 0);
    public static bool IsGrown(EquipInfo? equip)         => equip != null && equip.EnchantGrade > 0;

    // 담긴 내용이 바뀌었다 — 격자(담김 표시)와 정보 칸(목록·합계)이 다시 그린다.
    public event Action? Changed;

    private PlayerDataModel _data = null!;

    private bool _isSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 참조 확보 → 구독 순서로 진행한다 (클라 공통 규약)
    // ※ 서비스 조회는 반드시 Start — Awake·OnEnable은 등록 순서가 보장되지 않는다.
    private void Start()
    {
        _data = Services.Get<PlayerDataModel>();

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

    // 인벤토리·캐릭터·장비·작업슬롯 변경 구독 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _isSubscribed                  = true;
        _data.InventoryChanged        += OnInventoryChanged;
        _data.CharactersChanged       += OnEntitiesChanged;
        _data.EquipsChanged           += OnEntitiesChanged;
        _data.WorkStationSlotsChanged += OnEntitiesChanged;
        _data.ArtifactsChanged        += OnArtifactsChanged;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _isSubscribed                  = false;
        _data.InventoryChanged        -= OnInventoryChanged;
        _data.CharactersChanged       -= OnEntitiesChanged;
        _data.EquipsChanged           -= OnEntitiesChanged;
        _data.WorkStationSlotsChanged -= OnEntitiesChanged;
        _data.ArtifactsChanged        -= OnArtifactsChanged;
    }

    #endregion

    #region 담기 · 빼기

    // 이 아이템을 'count'개 담는다. 이미 담겨 있으면 그 수량을 덮어쓴다.
    //
    // 누적하지 않는 이유 — 담김 표시가 켜진 칸을 다시 우클릭하면 '빼기'로 동작하므로,
    // 같은 칸으로 수량을 늘리는 경로가 애초에 없다. 누적으로 두면 보유량을 넘는 값이
    // 조용히 만들어진다.
    public void Add(int itemId, int count)
    {
        int owned = _data.GetItemCount(itemId);

        if (owned <= 0)
        {
            ClientLogger.Warn(ClientLogger.UI, $"보유하지 않은 아이템 {itemId}를 판매 목록에 담으려 했다.", this);

            return;
        }

        int amount = Mathf.Clamp(count, 1, owned);

        ClearOtherKinds(SellCartKind.Item);

        int index  = FindIndex(itemId);

        if (index >= 0)
        {
            _entries[index].Count = amount;
        }
        else
        {
            _entries.Add(new ItemInfo { ItemId = itemId, Count = amount });
        }

        Recalculate();
        Changed?.Invoke();
    }

    // 이 아이템을 판매 목록에서 뺀다. 담겨 있지 않으면 아무 일도 하지 않는다.
    public void Remove(int itemId)
    {
        int index = FindIndex(itemId);

        if (index < 0)
        {
            return;
        }

        _entries.RemoveAt(index);

        Recalculate();
        Changed?.Invoke();
    }

    // 이 아이템이 담겨 있나 (격자가 담김 표시를 켤지 정할 때 호출).
    public bool Contains(int itemId) => FindIndex(itemId) >= 0;

    // 담긴 수량. 담겨 있지 않으면 0이다.
    public int GetCount(int itemId)
    {
        int index = FindIndex(itemId);

        return index >= 0 ? _entries[index].Count : 0;
    }

    // 이 캐릭터를 담는다. 이미 담겨 있으면 아무 일도 하지 않는다.
    // ⚠️ 팔 수 있는지(바쁨·마지막)는 부르는 쪽이 먼저 본다('EntityBlockText.ForCharacter').
    public void AddCharacter(long characterId)
    {
        if (_characterIds.Contains(characterId))
        {
            return;
        }

        ClearOtherKinds(SellCartKind.Character);
        _characterIds.Add(characterId);

        Recalculate();
        Changed?.Invoke();
    }

    // 이 장비를 담는다. 이미 담겨 있으면 아무 일도 하지 않는다.
    // ⚠️ 팔 수 있는지(착용)는 부르는 쪽이 먼저 본다('EntityBlockText.ForEquip').
    public void AddEquip(long equipId)
    {
        if (_equipIds.Contains(equipId))
        {
            return;
        }

        ClearOtherKinds(SellCartKind.Equip);
        _equipIds.Add(equipId);

        Recalculate();
        Changed?.Invoke();
    }

    // 여러 캐릭터·장비를 한 번에 담는다 (일괄 담기). 이미 담긴 것은 건너뛴다.
    // ※ 한 번에 한 종류만 넘긴다(탭별 일괄 담기) — 둘 다 넘기면 뒤의 장비만 남는다('ClearOtherKinds').
    // ※ 하나씩 'AddCharacter'를 부르면 담을 때마다 'Changed'가 나가 격자·목록이 개체 수만큼 다시 그린다.
    // ⚠️ 팔 수 있는지는 부르는 쪽이 먼저 본다('EntityBlockText').
    public void AddEntities(IReadOnlyList<long> characterIds, IReadOnlyList<long> equipIds)
    {
        if (characterIds.Count > 0)
        {
            ClearOtherKinds(SellCartKind.Character);
        }

        if (equipIds.Count > 0)
        {
            ClearOtherKinds(SellCartKind.Equip);
        }

        int before = _characterIds.Count + _equipIds.Count;

        foreach (long id in characterIds)
        {
            if (!_characterIds.Contains(id))
            {
                _characterIds.Add(id);
            }
        }

        foreach (long id in equipIds)
        {
            if (!_equipIds.Contains(id))
            {
                _equipIds.Add(id);
            }
        }

        if (_characterIds.Count + _equipIds.Count == before)
        {
            return;
        }

        Recalculate();
        Changed?.Invoke();
    }

    // 이 캐릭터를 뺀다. 담겨 있지 않으면 아무 일도 하지 않는다.
    public void RemoveCharacter(long characterId)
    {
        if (_characterIds.Remove(characterId))
        {
            Recalculate();
            Changed?.Invoke();
        }
    }

    // 이 장비를 뺀다. 담겨 있지 않으면 아무 일도 하지 않는다.
    public void RemoveEquip(long equipId)
    {
        if (_equipIds.Remove(equipId))
        {
            Recalculate();
            Changed?.Invoke();
        }
    }

    public bool ContainsCharacter(long characterId) => _characterIds.Contains(characterId);
    public bool ContainsEquip(long equipId)         => _equipIds.Contains(equipId);

    // 이 아티팩트를 'count'개 담는다 — 이미 담겨 있으면 수량을 덮어쓴다('Add'와 같은 이유).
    // ※ 장비와 같은 종류라 담긴 장비는 그대로 둔다(클래스 머리 주석).
    public void AddArtifact(int artifactTid, int count)
    {
        int owned = _data.GetArtifactCount(artifactTid);

        if (owned <= 0)
        {
            ClientLogger.Warn(ClientLogger.UI, $"보유하지 않은 아티팩트 {artifactTid}를 판매 목록에 담으려 했다.", this);

            return;
        }

        ClearOtherKinds(SellCartKind.Equip);

        int amount = Mathf.Clamp(count, 1, owned);
        int index  = FindArtifactIndex(artifactTid);

        if (index >= 0)
        {
            _artifacts[index].Count = amount;
        }
        else
        {
            _artifacts.Add(new ArtifactInfo { ArtifactTid = artifactTid, Count = amount });
        }

        Recalculate();
        Changed?.Invoke();
    }

    // 이 아티팩트를 뺀다. 담겨 있지 않으면 아무 일도 하지 않는다.
    public void RemoveArtifact(int artifactTid)
    {
        int index = FindArtifactIndex(artifactTid);

        if (index < 0)
        {
            return;
        }

        _artifacts.RemoveAt(index);

        Recalculate();
        Changed?.Invoke();
    }

    public bool ContainsArtifact(int artifactTid) => FindArtifactIndex(artifactTid) >= 0;

    // 아티팩트만 비운다 (아티팩트 판매 성공 후).
    public void ClearArtifacts()
    {
        if (_artifacts.Count == 0)
        {
            return;
        }

        _artifacts.Clear();

        Recalculate();
        Changed?.Invoke();
    }

    // 아티팩트 판매 요청에 실을 목록 — 패킷이 들고 나가므로 사본이다('ToRequestItems'와 같은 이유).
    public List<ArtifactInfo> ToRequestArtifacts()
    {
        var list = new List<ArtifactInfo>(_artifacts.Count);

        foreach (ArtifactInfo entry in _artifacts)
        {
            list.Add(new ArtifactInfo { ArtifactTid = entry.ArtifactTid, Count = entry.Count });
        }

        return list;
    }

    // 아티팩트 1개의 즉시 판매가 — 기준가 × 즉시 판매율. 테이블에 없으면 0.
    public static long ArtifactInstantPrice(int artifactTid)
        => GameDataLoader.TryGetArtifact(artifactTid, out ArtifactTableRow row) ? AuctionModel.InstantSellPrice(row.BasePrice) : 0L;

    // 아티팩트 1개를 분해하면 받을 마석 수 — 등급의 'DisassembleTable.ArtifactItemCount'. 테이블에 없으면 0.
    public static int ArtifactShards(int artifactTid)
        => TryGetArtifactDisassemble(artifactTid, out DisassembleTableRow row) ? row.ArtifactItemCount : 0;

    // 아티팩트 등급의 분해 행 — 마석 수 · 재료 TID를 함께 읽는다.
    private static bool TryGetArtifactDisassemble(int artifactTid, out DisassembleTableRow row)
    {
        row = null!;

        return GameDataLoader.TryGetArtifact(artifactTid, out ArtifactTableRow artifact)
            && GameDataLoader.TryGetDisassemble(artifact.GlobalRarity, out row);
    }

    // 목록을 통째로 비운다.
    public void Clear()
    {
        if (Count == 0)
        {
            return;
        }

        _entries.Clear();
        _characterIds.Clear();
        _equipIds.Clear();
        _artifacts.Clear();

        Recalculate();
        Changed?.Invoke();
    }

    // 자원만 비운다 (자원 판매 성공 후 · 일괄 담기가 범위를 다시 잡을 때).
    public void ClearItems()
    {
        if (_entries.Count == 0)
        {
            return;
        }

        _entries.Clear();

        Recalculate();
        Changed?.Invoke();
    }

    // 개체만 비운다 (개체 판매 · 분해 성공 후).
    public void ClearEntities()
    {
        if (!HasEntities)
        {
            return;
        }

        _characterIds.Clear();
        _equipIds.Clear();

        Recalculate();
        Changed?.Invoke();
    }

    // 캐릭터만 비운다 (캐릭터 탭 일괄 담기가 범위를 다시 잡을 때).
    public void ClearCharacters()
    {
        if (_characterIds.Count == 0)
        {
            return;
        }

        _characterIds.Clear();

        Recalculate();
        Changed?.Invoke();
    }

    // 장비 탭 것만 비운다 — 장비 · 아티팩트 (장비 탭 일괄 담기가 범위를 다시 잡을 때).
    public void ClearEquips()
    {
        if (_equipIds.Count == 0 && _artifacts.Count == 0)
        {
            return;
        }

        _equipIds.Clear();
        _artifacts.Clear();

        Recalculate();
        Changed?.Invoke();
    }

    // 판매 요청에 실을 목록을 만든다 (판매 버튼을 누른 Presenter가 호출).
    //
    // ※ 복사본을 준다 — 내부 리스트를 그대로 넘기면 판매 성공 후 'Clear'가 패킷이 들고 있는
    //   목록까지 비운다. 요청이 이미 나간 뒤라도 로그·재사용 경로에서 빈 목록으로 보인다.
    public List<ItemInfo> ToRequestItems()
    {
        var items = new List<ItemInfo>(_entries.Count);

        foreach (var entry in _entries)
        {
            items.Add(new ItemInfo { ItemId = entry.ItemId, Count = entry.Count });
        }

        return items;
    }

    #endregion

    #region 보조

    // 인벤토리가 바뀌었다 — 보유량을 넘는 항목을 깎거나, 없어졌거나 잠긴 항목을 뺀다 (PlayerDataModel.InventoryChanged 구독)
    //
    // ※ 담은 뒤에 잠그면 빠진다 — 자원 잠금 응답이 'InventoryChanged'를 낸다(T-159). 남겨 두면 서버가 'EntityLocked'로 판매 전체를 거절한다.
    //
    // 바뀐 게 없으면 이벤트를 쏘지 않는다. 채취가 도는 동안 이 경로가 계속 불리므로,
    // 매번 발행하면 인벤토리 격자 200칸이 이유 없이 다시 그려진다.
    private void OnInventoryChanged()
    {
        bool isDirty = false;

        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            int owned = _data.GetItemCount(_entries[i].ItemId);

            if (owned <= 0 || _data.IsItemLocked(_entries[i].ItemId))
            {
                _entries.RemoveAt(i);
                isDirty = true;

                continue;
            }

            if (_entries[i].Count > owned)
            {
                _entries[i].Count = owned;
                isDirty = true;
            }
        }

        if (!isDirty)
        {
            return;
        }

        Recalculate();
        Changed?.Invoke();
    }

    // 캐릭터·장비·작업슬롯이 바뀌었다 — 사라졌거나 일하게 됐거나 잠긴 개체를 뺀다
    // (PlayerDataModel.CharactersChanged · EquipsChanged · WorkStationSlotsChanged 구독)
    //
    // ※ 담은 뒤에 잠그면 빠진다 — 잠금 응답이 'CharactersChanged'·'EquipsChanged'를 낸다. 남겨 두면 서버가 'EntityLocked'로 판매 전체를 거절한다.
    //
    // ※ 마지막 캐릭터 규칙도 여기서 지킨다 — 다른 경로로 캐릭터가 줄어 담긴 수가 보유 수에 닿으면
    //   뒤에 담은 것부터 뺀다. 그대로 두면 서버가 'SellLastCharacter'로 개체 판매 전체를 거절한다.
    private void OnEntitiesChanged()
    {
        int removed = _characterIds.RemoveAll(id => _data.GetCharacterTid(id) == 0 || _data.IsCharacterBusy(id)
                                                    || _data.FindCharacter(id)?.IsLocked == true)
                    + _equipIds.RemoveAll(id => _data.GetEquipTid(id) == 0 || _data.IsEquipped(id)
                                                || _data.FindEquip(id)?.IsLocked == true);

        while (_characterIds.Count > 0 && _characterIds.Count >= _data.Characters.Count)
        {
            _characterIds.RemoveAt(_characterIds.Count - 1);
            removed++;
        }

        if (removed == 0)
        {
            return;
        }

        Recalculate();
        Changed?.Invoke();
    }

    // 아티팩트 칸·도감이 바뀌었다 — 담긴 수량을 보유량 안으로 줄이고, 다 빠진 번호는 뺀다 (PlayerDataModel.ArtifactsChanged 구독)
    // ※ 장착도 여기로 온다 — 칸에서 1개가 빠지고 NEW가 풀린다. 그래서 수량이 그대로여도 다시 센다.
    private void OnArtifactsChanged()
    {
        if (_artifacts.Count == 0)
        {
            return;
        }

        for (int i = _artifacts.Count - 1; i >= 0; i--)
        {
            int owned = _data.GetArtifactCount(_artifacts[i].ArtifactTid);

            if (owned <= 0)
            {
                _artifacts.RemoveAt(i);
            }
            else if (_artifacts[i].Count > owned)
            {
                _artifacts[i].Count = owned;
            }
        }

        Recalculate();
        Changed?.Invoke();
    }

    // 합계 골드 · 분해 재료 · 상위 등급 포함 여부 · 성장 개체 수를 다시 센다 (담기·빼기·비우기·인벤 변경에서 호출).
    private void Recalculate()
    {
        long total          = 0L;
        bool hasHighRarity  = false;
        int  grownCount     = 0;
        int  shards         = 0;
        int  shardItemId    = 0;

        foreach (var entry in _entries)
        {
            total += AuctionModel.InstantSellTotal(GameDataLoader.GetItemPrice(entry.ItemId), entry.Count);

            if (GameDataLoader.GetItemRarity(entry.ItemId) >= GlobalRarity.Rare)
            {
                hasHighRarity = true;
            }
        }

        foreach (long characterId in _characterIds)
        {
            int characterTid = _data.GetCharacterTid(characterId);

            total += AuctionModel.InstantSellTotal(GameDataLoader.GetCharacterPrice(characterTid), 1);

            if (GameDataLoader.GetCharacterRarity(characterTid) >= GlobalRarity.Rare)
            {
                hasHighRarity = true;
            }

            if (IsGrown(_data.FindCharacter(characterId)))
            {
                grownCount++;
            }
        }

        foreach (long equipId in _equipIds)
        {
            if (!GameDataLoader.TryGetEquip(_data.GetEquipTid(equipId), out EquipTableRow row))
            {
                continue;
            }

            total += AuctionModel.InstantSellTotal(row.BasePrice, 1);

            if (GameDataLoader.TryGetDisassemble(row.GlobalRarity, out DisassembleTableRow disassemble))
            {
                shards      += disassemble.ItemCount;
                shardItemId  = disassemble.ItemTID;
            }

            if (row.GlobalRarity >= GlobalRarity.Rare)
            {
                hasHighRarity = true;
            }

            if (IsGrown(_data.FindEquip(equipId)))
            {
                grownCount++;
            }
        }

        bool uncollected = false;

        foreach (ArtifactInfo entry in _artifacts)
        {
            total += ArtifactInstantPrice(entry.ArtifactTid) * entry.Count;

            if (TryGetArtifactDisassemble(entry.ArtifactTid, out DisassembleTableRow disassemble))
            {
                shards      += disassemble.ArtifactItemCount * entry.Count;
                shardItemId  = disassemble.ItemTID;
            }

            if (!_data.IsArtifactEquipped(entry.ArtifactTid))
            {
                uncollected = true;
            }
        }

        TotalPrice             = total;
        HasHighRarity          = hasHighRarity;
        HasUncollectedArtifact = uncollected;
        GrownCount    = grownCount;
        TotalShards   = shards;
        ShardItemId   = shardItemId;
    }

    // 담으려는 종류가 아닌 것을 비운다 — 한 종류만 담는다(클래스 머리 주석). 이벤트는 부르는 쪽의 담기가 한 번에 낸다.
    private void ClearOtherKinds(SellCartKind keep)
    {
        if (keep != SellCartKind.Item)
        {
            _entries.Clear();
        }

        if (keep != SellCartKind.Character)
        {
            _characterIds.Clear();
        }

        if (keep != SellCartKind.Equip)
        {
            _equipIds.Clear();
            _artifacts.Clear();
        }
    }

    // 담긴 아티팩트에서 이 번호의 자리를 찾는다. 없으면 -1.
    private int FindArtifactIndex(int artifactTid) => _artifacts.FindIndex(entry => entry.ArtifactTid == artifactTid);

    // 담긴 목록에서 이 아이템의 자리를 찾는다. 없으면 -1.
    private int FindIndex(int itemId)
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].ItemId == itemId)
            {
                return i;
            }
        }

        return -1;
    }

    #endregion
}
