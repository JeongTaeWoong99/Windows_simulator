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
//   'EquipInfo.SlotPosition'은 서버가 빈 자리를 찾아 넣는 인벤토리 칸 번호다
//   (서버 'User.NextFreeEquipPosition'). 자원·캐릭터에는 그런 필드가 아예 없어
//   **세 탭의 자리 규칙이 갈라진다** — 장비만 서버 번호를 쓰면 "빈 칸이 왜 여기 있나"의 답이
//   탭마다 달라진다. 그래서 여기서는 **줄 세우는 데만 쓰고**, 실제 칸 번호는 기반 클래스가 준다
//   (처음 보는 것은 앞에서부터 첫 빈 칸 — 'InventorySlotSource.Arrange').
//   ⚠️ 그래서 지금은 **자리가 세션 한정**이다. 재접속하면 위 순서로 처음부터 다시 앉는다 —
//   칸 번호를 서버가 갖는 것은 T-058 → T-044의 몫이다.
//
// ■ 장착 중인 장비도 **제자리에 남는다** (2026-09-25 결정)
//   캐릭터가 끼고 있어도 인벤토리에서 빠지지 않는다 — 딤 처리와 '배' 마크로 구분하고,
//   [정렬]에서만 맨 뒤로 민다('IsAway').
//   서버도 장착 시 'SlotPosition'을 그대로 두므로(`Equip.Wear`) **클라·서버의 견해가 일치한다** —
//   한때 목록에서 빼는 안을 넣었을 때 생겼던 "빈 칸이 보이는데 뽑기가 거절되는" 어긋남이 사라졌다.
//
// ※ 기본 능력치 문구('낚시 +30%')는 'UI/Shared/EquipLabel'이 만든다 —
//   작업슬롯 세팅의 장비 칸이 같은 문구를 쓰기 때문이다(T-074).
//   인벤토리 칸에서는 툴팁에만 나온다 — 칸 아래 밴드는 능력치 칸 줄이 쓴다(T-095).
//
// ■ 능력치 칸 (T-095)
//   칸 수는 장비 등급('EquipLabel.GetStatSlotCount' — 1·1·2·2·3·3), 칸 색은 장비의 인챈트 등급 하나다(이슈 #46).
//   옵션은 'EquipInfo.EnchantOptions'에서 'EquipLabel.ReadStatOptions'로 읽는다(경매장 매물 줄도 같은 함수를 쓴다).
public class EquipSlotSource : InventorySlotSource
{
    private readonly PlayerDataModel _data;

    // 'SlotPosition'으로 줄 세울 때 쓰는 버퍼. 매번 새로 만들지 않는다(상주 앱이라 GC가 쌓인다).
    private readonly List<EquipInfo> _ordered = new List<EquipInfo>();

    // 능력치 칸 버퍼 — 칸 200개를 그릴 때마다 새로 만들지 않는다.
    // ※ 격자가 칸에 넘기는 즉시 쓰임이 끝난다('InventoryGridPresenter.ReadAptitudes'와 같은 규약).
    private readonly List<GlobalRarity>           _sockets = new List<GlobalRarity>();
    private readonly List<EnchantOptionTableRow?> _options = new List<EnchantOptionTableRow?>();

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
            // 보조 문구는 비운다 — 그 밴드를 능력치 칸이 쓰고, 기본 능력치는 툴팁으로 갔다(T-095).
            into.Add(new SlotData(
                equip.EquipId,
                GameDataLoader.GetEquipName(equip.EquipTid),
                "",
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

    // 장비의 대상 산업 (Matches에서 호출). 테이블에 없는 TID는 어느 산업에도 속하지 않는다.
    protected override bool MatchesIndustry(SlotData slot, byte industry)
        => GameDataLoader.TryGetEquip(_data.GetEquipTid(slot.Key), out EquipTableRow row) && (byte)row.Industry == industry;

    // 이 장비를 지금 캐릭터가 끼고 있나 (딤·'배' 마크·[정렬] 맨 뒤 — 기반 클래스가 호출).
    public override bool IsAway(long key) => _data.IsEquipped(key);

    // 이 장비의 능력치 칸 — 칸마다 박힌 옵션의 등급, 빈 칸은 'None' (격자가 매번 그릴 때 호출).
    public override IReadOnlyList<GlobalRarity>? GetStatSockets(long key)
    {
        EquipInfo? equip = FindEquip(key);

        if (equip == null)
        {
            return null;
        }

        EquipLabel.ReadStatOptions(equip.EquipTid, equip.EnchantOptions, _options);
        EquipLabel.ToSocketGrades(_options, _sockets);

        return _sockets;
    }

    // 장비 칸 툴팁 — 공용 장비 툴팁('EquipLabel.BuildTooltip')에 "누가 어느 부위에 끼고 있나"를 더한다 (격자가 칸에 올린 순간 호출).
    // 칸의 '배' 마크는 장착 여부만 말한다 — 누구의 어느 부위인지는 여기서만 알 수 있다.
    public override TooltipContent? BuildTooltip(long key)
    {
        EquipInfo? equip = FindEquip(key);

        if (equip == null)
        {
            return null; // 목록이 아직 낡았다 — 칸도 이름을 그리지 못한다
        }

        string state = equip.EquippedCharacterId == 0L
            ? "인벤토리"
            : $"{_data.GetCharacterName(equip.EquippedCharacterId)} · {EquipLabel.GetSlotName(equip.EquippedSlot)}";

        // 조작 안내 — 칸에는 표시가 없어 눌러 봐야 안다('ResourceSlotSource'와 같다).
        return EquipLabel.BuildTooltip(equip.EquipTid, equip.EnchantOptions, state)?
            .Row("우클릭 판매 담기 · Shift+우클릭 경매 등록", "");
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

    // 서버가 정한 인벤토리 칸 번호 순서 (Fill의 정렬 비교자).
    // ⚠️ 끝까지 가서 0이 나오지 않게 개체 번호까지 본다 — 'List.Sort'는 안정 정렬이 아니다.
    private static int CompareBySlotPosition(EquipInfo a, EquipInfo b)
    {
        int byPosition = a.SlotPosition.CompareTo(b.SlotPosition);

        return byPosition != 0 ? byPosition : a.EquipId.CompareTo(b.EquipId);
    }
}
