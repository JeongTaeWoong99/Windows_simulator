using System.Collections.Generic;
using GameData;
using MikaProtocol;

// 장비 탭 — 보유 장비를 칸으로 내놓는다.
//
// ※ 목록은 로그인 스냅샷('S_EquipListResponse')이 원본이고, 지급·장착·해제는 개체 단위 푸시
//   ('S_EquipSyncResponse')로 온다 — 둘 다 'EquipsChanged' 하나로 온다.
//
// ■ 개체가 키다 — 캐릭터 탭과 같은 모양이다
//   같은 장비를 여러 개 가질 수 있어 종류(TID)로는 하나를 특정하지 못한다.
//   칸의 'Key'는 개체 번호('EquipId')이고, 이름·등급·효과는 TID로 테이블에서 읽는다.
//
// ■ 'SlotPosition'은 **칸 번호가 아니라 도착 순서**로 쓴다 (2026-09-25 · T-044)
//   'EquipInfo.SlotPosition'은 서버가 빈 자리를 찾아 넣는 창고 칸 번호다
//   (서버 'User.NextFreeEquipPosition'). 자원·캐릭터에는 그런 필드가 아예 없어
//   **세 탭의 자리 규칙이 갈라진다** — 장비만 서버 번호를 쓰면 "빈 칸이 왜 여기 있나"의 답이
//   탭마다 달라진다. 그래서 여기서는 **줄 세우는 데만 쓰고**, 실제 칸 번호는 기반 클래스가 준다
//   (처음 보는 것은 앞에서부터 첫 빈 칸 — 'StorageSlotSource.Arrange').
//   ⚠️ 그래서 지금은 **자리가 세션 한정**이다. 재접속하면 위 순서로 처음부터 다시 앉는다 —
//   칸 번호를 서버가 갖는 것은 T-058 → T-044의 몫이다.
//
// ■ 장착 중인 장비도 **제자리에 남는다** (2026-09-25 결정)
//   캐릭터가 끼고 있어도 창고에서 빠지지 않는다 — 딤 처리와 '배' 마크로 구분하고,
//   [정렬]에서만 맨 뒤로 민다('IsAway').
//   서버도 장착 시 'SlotPosition'을 그대로 두므로(`Equip.Wear`) **클라·서버의 견해가 일치한다** —
//   한때 목록에서 빼는 안을 넣었을 때 생겼던 "빈 칸이 보이는데 뽑기가 거절되는" 어긋남이 사라졌다.
//
// ※ 칸의 보조 문구('낚시 +30%')는 'UI/Shared/EquipLabel'이 만든다 —
//   작업슬롯 세팅의 장비 칸이 같은 문구를 쓰기 때문이다(T-074).
public class EquipSlotSource : StorageSlotSource
{
    private readonly PlayerDataModel _data;

    // 'SlotPosition'으로 줄 세울 때 쓰는 버퍼. 매번 새로 만들지 않는다(상주 앱이라 GC가 쌓인다).
    private readonly List<EquipInfo> _ordered = new List<EquipInfo>();

    public EquipSlotSource(PlayerDataModel data)
    {
        _data = data;
    }

    // 보유 장비를 서버 칸 번호 순서로 옮긴다 (Rebuild에서 호출).
    protected override void Fill(List<SlotData> into)
    {
        _ordered.Clear();
        _ordered.AddRange(_data.Equips);
        _ordered.Sort(CompareBySlotPosition);

        foreach (EquipInfo equip in _ordered)
        {
            into.Add(new SlotData(
                equip.EquipId,
                GameDataLoader.GetEquipName(equip.EquipTid),
                EquipLabel.GetEffectText(equip.EquipTid),
                GameDataLoader.GetEquipRarity(equip.EquipTid)));
        }
    }

    // 장비 [정렬] 규칙 — 등급 높은 순 → 부위 → 산업 → 종류(TID) → 개체 번호 (Sort에서 호출).
    //
    // 같은 장비를 여러 개 가질 수 있어 개체 번호까지 가야 동점이 없다.
    // ※ 칸의 'Key'는 개체 번호라 TID는 보유 목록에서 찾아온다.
    protected override int CompareForSort(SlotData a, SlotData b)
    {
        int byRarity = ((byte)b.Rarity).CompareTo((byte)a.Rarity);

        if (byRarity != 0)
        {
            return byRarity;
        }

        int tidA = _data.GetEquipTid(a.Key);
        int tidB = _data.GetEquipTid(b.Key);

        bool hasA = GameDataLoader.TryGetEquip(tidA, out EquipTableRow rowA);
        bool hasB = GameDataLoader.TryGetEquip(tidB, out EquipTableRow rowB);

        // 테이블에 없는 TID는 이름이 '?#'으로 보이는 칸이라 뒤로 민다.
        if (!hasA || !hasB)
        {
            int byKnown = (hasA ? 0 : 1).CompareTo(hasB ? 0 : 1);

            return byKnown != 0 ? byKnown : a.Key.CompareTo(b.Key);
        }

        int byKind = ((byte)rowA.EquipKind).CompareTo((byte)rowB.EquipKind);

        if (byKind != 0)
        {
            return byKind;
        }

        int byIndustry = ((byte)rowA.Industry).CompareTo((byte)rowB.Industry);

        if (byIndustry != 0)
        {
            return byIndustry;
        }

        int byTid = tidA.CompareTo(tidB);

        return byTid != 0 ? byTid : a.Key.CompareTo(b.Key);
    }

    // 이 장비를 지금 캐릭터가 끼고 있나 (딤·'배' 마크·[정렬] 맨 뒤 — 기반 클래스가 호출).
    public override bool IsAway(long key) => _data.IsEquipped(key);

    // 장비 칸 툴팁 — 등급 · 종류 · 효과 · 누가 끼고 있나 (격자가 칸에 올린 순간 호출).
    //
    // 칸의 '배' 마크는 장착 여부만 말한다 — 누구의 어느 부위인지는 여기서만 알 수 있다.
    // ⏸ 인챈트 등급·옵션은 인챈트 UI(T-095)가 표기 규칙과 함께 넣는다.
    public override TooltipContent? BuildTooltip(long key)
    {
        EquipInfo? equip = FindEquip(key);

        if (equip == null || !GameDataLoader.TryGetEquip(equip.EquipTid, out EquipTableRow row))
        {
            return null; // 목록이 아직 낡았거나 모르는 TID — 칸도 이름을 그리지 못한다
        }

        string state = equip.EquippedCharacterId == 0L
            ? "창고"
            : $"{_data.GetCharacterName(equip.EquippedCharacterId)} · {EquipLabel.GetSlotName(equip.EquippedSlot)}";

        var content = new TooltipContent(GameDataLoader.GetEquipName(equip.EquipTid));

        AddRarityRow(content, GameDataLoader.GetEquipRarity(equip.EquipTid))
            .Row("종류", EquipLabel.GetKindName(row.EquipKind))
            .Row("효과", EquipLabel.GetEffectText(equip.EquipTid))
            .Row("장착", state);

        return content;
    }

    // 개체 번호로 장비를 찾는다. 모르는 개체면 null (BuildTooltip에서 호출).
    private EquipInfo? FindEquip(long equipId)
    {
        foreach (EquipInfo equip in _data.Equips)
        {
            if (equip.EquipId == equipId)
            {
                return equip;
            }
        }

        return null;
    }

    // 장비 변경 구독 (Subscribe에서 호출)
    //
    // ※ 캐릭터 탭과 달리 이벤트 하나만 듣는다 — 지급도 장착·해제도 전부
    //   'S_EquipSyncResponse'로 와서 'EquipsChanged' 하나로 합쳐지기 때문이다.
    protected override void OnSubscribe()
    {
        _data.EquipsChanged += Rebuild;
    }

    // 구독 해제 (Unsubscribe에서 호출)
    protected override void OnUnsubscribe()
    {
        _data.EquipsChanged -= Rebuild;
    }

    // 서버가 정한 창고 칸 번호 순서 (Fill의 정렬 비교자).
    // ⚠️ 끝까지 가서 0이 나오지 않게 개체 번호까지 본다 — 'List.Sort'는 안정 정렬이 아니다.
    private static int CompareBySlotPosition(EquipInfo a, EquipInfo b)
    {
        int byPosition = a.SlotPosition.CompareTo(b.SlotPosition);

        return byPosition != 0 ? byPosition : a.EquipId.CompareTo(b.EquipId);
    }
}
