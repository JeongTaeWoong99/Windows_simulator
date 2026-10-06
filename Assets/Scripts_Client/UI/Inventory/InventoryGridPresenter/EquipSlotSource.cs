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
// ■ 'SlotPosition'이 곧 칸 번호다 (2026-10-06 · T-044)
//   자원·캐릭터의 'Slot'과 같은 뜻이다 — 장비만 이름이 다른 것은 칸 번호가 먼저 생겼기 때문이다(T-002).
//   한때(2026-09-25) 자원·캐릭터에 같은 필드가 없어 도착 순서로만 썼다. 서버 칸(T-058)이 오면서 세 탭이 같아졌다.
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

    // 능력치 칸 버퍼 — 칸 200개를 그릴 때마다 새로 만들지 않는다.
    // ※ 격자가 칸에 넘기는 즉시 쓰임이 끝난다('InventoryGridPresenter.ReadAptitudes'와 같은 규약).
    private readonly List<GlobalRarity>           _sockets = new List<GlobalRarity>();
    private readonly List<EnchantOptionTableRow?> _options = new List<EnchantOptionTableRow?>();

    public EquipSlotSource(PlayerDataModel data)
    {
        _data = data;
    }

    // 보유 장비를 서버 칸 번호와 함께 칸으로 옮긴다 (Rebuild에서 호출).
    // ※ 창고(T-107)에 맡긴 장비는 인벤토리 격자에 그리지 않는다.
    protected override void Fill(List<PlacedSlot> into)
    {
        foreach (EquipInfo equip in _data.Equips)
        {
            if (equip.Container != EContainer.Inventory)
            {
                continue;
            }

            // 보조 문구는 비운다 — 그 밴드를 능력치 칸이 쓰고, 기본 능력치는 툴팁으로 갔다(T-095).
            var slot = new SlotData(
                equip.EquipId,
                GameDataLoader.GetEquipName(equip.EquipTid),
                "",
                GameDataLoader.GetEquipRarity(equip.EquipTid),
                VisualCatalog.EquipIconOf(equip.EquipTid));

            into.Add(new PlacedSlot(slot, equip.SlotPosition));
        }
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
            .Row("좌클릭 큐브 · 우클릭 판매 · Shift+우클릭 경매", "");
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
}
