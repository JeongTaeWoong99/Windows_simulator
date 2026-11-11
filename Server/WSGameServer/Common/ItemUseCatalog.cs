using GameData;
using MikaUtils;

namespace WSGameServer;

/// <summary>
/// 소모품 효과 보관소(T-195). 아이템 하나 = 효과 하나(<c>ItemUseTable</c>). 표에 없는 아이템은 소모품이 아니다(상자 경로로 간다).
/// 서버 시작 시 <c>GameTable.LoadAll</c> 다음에 <see cref="LoadAll"/>을 한 번 부르고, 이후에는 읽기만 한다.
/// </summary>
public sealed class ItemUseCatalog : Singleton<ItemUseCatalog>
{
    private readonly Dictionary<int, ItemUseTableRow> _byTid = new();

    public int Count => _byTid.Count;

    public void LoadAll()
    {
        Load(GameTable.ItemUseTable.All);
    }

    /// <summary>인덱스를 만들고 검증한다. 데이터 오류면 예외 — 조용히 돌면 써도 아무 일 없는 아이템이 생긴다.</summary>
    public void Load(IEnumerable<ItemUseTableRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        _byTid.Clear();
        foreach (var row in rows)
        {
            if (row.UseEffect == ItemUseEffect.None)
            {
                throw new InvalidDataException($"[ItemUseTable] 효과가 없는 행: ItemTID={row.ItemTID}");
            }

            if (NeedsSlot(row.UseEffect) && row.SlotCount < 1)
            {
                throw new InvalidDataException($"[ItemUseTable] 슬롯 효과인데 SlotCount가 0: ItemTID={row.ItemTID}");
            }

            if (IsSlotBuff(row.UseEffect) && row.EffectCount < 1)
            {
                throw new InvalidDataException($"[ItemUseTable] 버프인데 EffectCount가 0: ItemTID={row.ItemTID}");
            }

            _byTid.Add(row.ItemTID, row);
        }
    }

    public bool TryGet(int itemTid, out ItemUseTableRow row) => _byTid.TryGetValue(itemTid, out row!);

    /// <summary>슬롯에 남는 버프인가 — 판정마다 1씩 주는 효과.</summary>
    public static bool IsSlotBuff(ItemUseEffect effect)
    {
        return effect is ItemUseEffect.SlotWorkSpeed or ItemUseEffect.SlotYield
            or ItemUseEffect.SlotBoxChance or ItemUseEffect.SlotBoxRelicChance;
    }

    /// <summary>자동 사용 칸에 넣을 수 있는가 — 신속·풍요의 영약(이슈 #113). 나침반은 직접 쓴다.</summary>
    public static bool IsAutoUsable(ItemUseEffect effect)
    {
        return effect is ItemUseEffect.SlotWorkSpeed or ItemUseEffect.SlotYield;
    }

    /// <summary>슬롯을 대상으로 쓰는가 — 버프와 보물 지도.</summary>
    public static bool NeedsSlot(ItemUseEffect effect)
    {
        return IsSlotBuff(effect) || effect == ItemUseEffect.InstantJudge;
    }
}
