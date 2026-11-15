using System.Collections.Generic;
using MikaProtocol;
using UnityEngine;

// 작업슬롯 칸 무대 왼쪽 위의 버프 칩 묶음 — 이 슬롯에 걸린 소모품 버프마다 칩 하나 (T-194).
// ※ 처음엔 머리 줄에 두기로 했지만(2026-11-14) 칸 폭(약 150px)이 번호·산업·속도로 이미 차서 무대 모서리로 옮겼다 — 대상·캐릭터를 덜 가리게 작게.
//
// ■ 무엇을 띄우나
// 'WorkStationSlotInfo.Buffs' 한 줄 = 칩 하나 — 아이콘 + 남은 판정 횟수. 버프가 없으면 칩이 없다(줄이 비어 칸이 덜 붐빈다).
// 자동 사용이 켜진 영약(이슈 #123)은 남은 횟수 뒤에 괄호로 보유 개수를 붙인다 '20(5)' — 더하기로 읽히지 않게.
// 켜졌는데 버프가 없으면(보유분이 떨어졌다) 흐린 칩 '0(0)'으로 남겨 켜져 있다는 것을 보인다.
// 효과·남은 횟수·보유 개수는 툴팁으로도 읽는다.
//
// ■ 칩은 지우지 않고 끈다
// 버프는 판정마다 숫자만 바뀌고 종류는 거의 그대로다. 매번 만들고 지우면 칸 8개 × 판정마다 GC가 난다 —
// 모자랄 때만 견본을 복제하고, 남는 칩은 끈다('object-pool'을 쓸 만큼 많지 않다 — 칩은 칸당 많아야 4~5개).
public class SlotBuffChipsView : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("칩 견본 — 이 오브젝트의 자식으로 꺼 둔다. 복제해서 쓴다")]
    private SlotBuffChipView chipTemplate = null!;

    private readonly List<SlotBuffChipView> _chips = new List<SlotBuffChipView>();

    // 툴팁이 띄우는 순간에 읽을 값 — 칩마다 넘긴 함수가 이 표를 본다(다시 그리면 함수를 새로 만들지 않아도 된다)
    private readonly Dictionary<int, int> _remaining = new Dictionary<int, int>();
    private readonly HashSet<int>         _autoOn    = new HashSet<int>();

    private PlayerDataModel? _data; // 보유 개수 — 처음 그릴 때 찾는다(견본 복제 순서와 상관없게)

    // 필수 참조 검증 · 견본은 꺼 둔다 (Unity 메시지)
    private void Awake()
    {
        this.RequireRef(chipTemplate, nameof(chipTemplate));

        chipTemplate.gameObject.SetActive(false);
    }

    // 슬롯의 버프를 칩으로 그린다 ('WorkStationSlotView.Bind'가 호출).
    //   buffs   : 걸린 버프 — 아이템마다 한 줄. null·빈 목록이면 칩을 모두 끈다
    //   autoUse : 자동 사용이 켜진 영약 — 칩에 '(보유 개수)'를 붙인다
    public void Bind(IReadOnlyList<SlotBuffInfo>? buffs, IReadOnlyList<SlotAutoUseInfo>? autoUse)
    {
        _data ??= Services.Get<PlayerDataModel>();

        _remaining.Clear();
        _autoOn.Clear();

        if (autoUse != null)
        {
            foreach (SlotAutoUseInfo auto in autoUse)
            {
                if (auto.On)
                {
                    _autoOn.Add(auto.ItemTID);
                }
            }
        }

        int shown = 0;

        if (buffs != null)
        {
            foreach (SlotBuffInfo buff in buffs)
            {
                if (buff.Remaining <= 0)
                {
                    continue; // 서버는 0을 보내지 않는다 — 와도 그릴 것이 없다
                }

                int itemTid = buff.ItemTID;

                _remaining[itemTid] = buff.Remaining;
                ChipAt(shown).Bind(itemTid, buff.Remaining, OwnedIfOn(itemTid), () => BuildTooltip(itemTid));
                shown++;
            }
        }

        // 켜졌는데 버프가 없는 영약 — 보유분이 떨어졌다. 흐린 칩으로 남긴다
        foreach (int itemTid in _autoOn)
        {
            if (_remaining.ContainsKey(itemTid))
            {
                continue;
            }

            ChipAt(shown).Bind(itemTid, 0, OwnedIfOn(itemTid), () => BuildTooltip(itemTid));
            shown++;
        }

        for (int i = 0; i < _chips.Count; i++)
        {
            _chips[i].gameObject.SetActive(i < shown);
        }
    }

    // 자동 사용이 켜졌으면 보유 개수, 아니면 -1 (칩이 '(n)'을 붙일지 정한다)
    private int OwnedIfOn(int itemTid) => _autoOn.Contains(itemTid) ? _data!.GetItemCount(itemTid) : -1;

    // 'index'번째 칩 — 모자라면 견본을 복제한다 (Bind에서 호출)
    private SlotBuffChipView ChipAt(int index)
    {
        while (_chips.Count <= index)
        {
            SlotBuffChipView chip = Instantiate(chipTemplate, transform);

            chip.name = $"Buff Chip {_chips.Count}";
            _chips.Add(chip);
        }

        SlotBuffChipView found = _chips[index];

        found.gameObject.SetActive(true);

        return found;
    }

    // 칩 툴팁 — 이름 / 효과 / 판정 n회 남음 / 자동 사용 켜짐 · 보유 n개 (칩의 TooltipTrigger가 띄우는 순간에 호출)
    private TooltipContent? BuildTooltip(int itemTid)
    {
        var content = new TooltipContent(GameDataLoader.GetItemName(itemTid));

        string effect = ItemUseLabel.Effect(itemTid);

        if (effect.Length > 0)
        {
            content.Row(effect, "");
        }

        if (_remaining.TryGetValue(itemTid, out int remaining))
        {
            content.Row(Lang.Format("UI.WorkStation.BuffTooltip", remaining), "");
        }

        if (_autoOn.Contains(itemTid))
        {
            content.Row(Lang.Format("UI.WorkStation.AutoUseInSlot", $"{_data!.GetItemCount(itemTid):N0}"), "");
        }

        return content;
    }
}
