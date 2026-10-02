using System;
using System.Collections.Generic;
using GameData;
using MikaProtocol;
using UnityEngine;

// 판매하려고 담아 둔 것들의 목록 — "장바구니". 자원(수량)과 개체(캐릭터·장비, 1개씩)를 함께 담는다.
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
// ■ 개체(캐릭터·장비)는 따로 담는다 (T-075 · 2026-10-03)
//   서버 패킷이 둘이다 — 자원은 'C_ItemSellRequest', 개체는 'C_EntitySellRequest'. 섞어 담으면 판매 버튼이 둘 다 보낸다.
//   개체도 같은 이유로 따라간다 — 사라졌거나(경매 등록·판매) 일하게 된(배치·착용) 개체는 목록에서 뺀다.
//   서버는 일하는 캐릭터·끼고 있는 장비·마지막 캐릭터를 거절하고, 하나라도 걸리면 개체 판매 전체가 막힌다.
//   ※ 담기 전에 막는 일(사유 알림)은 격자가 한다('EntityBlockText'). 여기는 담긴 뒤의 변화만 따라간다.
public class SellCartModel : MonoService<SellCartModel>
{
    // 담긴 항목들. 'ItemInfo.Count'는 "팔 개수"(델타)다 — 보유량이 아니다.
    private readonly List<ItemInfo> _entries = new List<ItemInfo>();

    // 담긴 개체 번호들 — 캐릭터·장비. 개체는 하나씩이라 수량이 없다.
    private readonly List<long> _characterIds = new List<long>();
    private readonly List<long> _equipIds     = new List<long>();

    public IReadOnlyList<ItemInfo> Entries      => _entries;
    public IReadOnlyList<long>     CharacterIds => _characterIds;
    public IReadOnlyList<long>     EquipIds     => _equipIds;

    // 담긴 줄 수 — 자원 종류 + 개체 수. 비어 있으면 판매 버튼을 잠근다.
    public int Count => _entries.Count + _characterIds.Count + _equipIds.Count;

    public bool HasItems    => _entries.Count > 0;
    public bool HasEntities => _characterIds.Count > 0 || _equipIds.Count > 0;

    // 담긴 것을 다 팔면 받을 골드. 기준가(BasePrice) x 수량에 즉시 판매율을 곱한 값이다('AuctionModel.InstantSellTotal').
    // ※ 미리보기일 뿐이다 — 실제 지급액은 서버가 정한다('S_ItemSellResponse' · 'S_EntitySellResponse'의 'GainedGold').
    public long TotalPrice { get; private set; }

    // Rare 이상이 담겨 있나. 오판매를 막으려고 화면이 경고를 띄우는 근거다.
    public bool HasHighRarity { get; private set; }

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

        _equipIds.Add(equipId);

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

        Recalculate();
        Changed?.Invoke();
    }

    // 자원만 비운다 (자원 판매 성공 후 · 일괄 담기가 범위를 다시 잡을 때). 담아 둔 개체는 남긴다.
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

    // 개체만 비운다 (개체 판매 성공 후). 담아 둔 자원은 남긴다.
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

    // 인벤토리가 바뀌었다 — 보유량을 넘는 항목을 깎거나 뺀다 (PlayerDataModel.InventoryChanged 구독)
    //
    // 바뀐 게 없으면 이벤트를 쏘지 않는다. 채취가 도는 동안 이 경로가 계속 불리므로,
    // 매번 발행하면 인벤토리 격자 200칸이 이유 없이 다시 그려진다.
    private void OnInventoryChanged()
    {
        bool isDirty = false;

        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            int owned = _data.GetItemCount(_entries[i].ItemId);

            if (owned <= 0)
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

    // 캐릭터·장비·작업슬롯이 바뀌었다 — 사라졌거나 일하게 된 개체를 뺀다
    // (PlayerDataModel.CharactersChanged · EquipsChanged · WorkStationSlotsChanged 구독)
    //
    // ※ 마지막 캐릭터 규칙도 여기서 지킨다 — 다른 경로로 캐릭터가 줄어 담긴 수가 보유 수에 닿으면
    //   뒤에 담은 것부터 뺀다. 그대로 두면 서버가 'SellLastCharacter'로 개체 판매 전체를 거절한다.
    private void OnEntitiesChanged()
    {
        int removed = _characterIds.RemoveAll(id => _data.GetCharacterTid(id) == 0 || _data.IsCharacterBusy(id))
                    + _equipIds.RemoveAll(id => _data.GetEquipTid(id) == 0 || _data.IsEquipped(id));

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

    // 합계 골드와 상위 등급 포함 여부를 다시 센다 (담기·빼기·비우기·인벤 변경에서 호출).
    private void Recalculate()
    {
        long total          = 0L;
        bool hasHighRarity  = false;

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
        }

        foreach (long equipId in _equipIds)
        {
            if (!GameDataLoader.TryGetEquip(_data.GetEquipTid(equipId), out EquipTableRow row))
            {
                continue;
            }

            total += AuctionModel.InstantSellTotal(row.BasePrice, 1);

            if (row.GlobalRarity >= GlobalRarity.Rare)
            {
                hasHighRarity = true;
            }
        }

        TotalPrice    = total;
        HasHighRarity = hasHighRarity;
    }

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
