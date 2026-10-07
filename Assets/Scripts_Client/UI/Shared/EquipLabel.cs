using System.Collections.Generic;
using GameData;
using MikaProtocol;
using UnityEngine;

// 장비 → 사용자에게 보일 문구.
//
// ■ 두 화면이 같은 장비를 읽는다
// 인벤토리 장비 탭('EquipSlotSource')과 작업슬롯 세팅의 장비 칸('WorkStationSelectPresenter')이
// 같은 효과 문구를 쓴다. 한쪽에만 두면 같은 장비가 두 화면에서 다르게 읽힌다.
//
// ■ 기본 능력치와 추가 능력치 (T-095)
// 기본 능력치는 장비 종류(TID)가 정한다('GetEffectText'). 추가 능력치는 **개체마다** 붙는 옵션이고,
// 장비 등급이 칸 수를 정한다('GetStatSlotCount'). 칸 하나에 옵션 하나('GetOptionText')다.
// 인챈트 등급은 **장비 하나에 하나**라 모든 칸이 같은 등급의 옵션이다(이슈 #46 — 칸 색이 모두 같다).
public static class EquipLabel
{
    // 이 장비의 효과 한 줄 — '낚시 +30%' · 전 산업이면 '전산업 +10%'. 모르는 TID면 빈 문자열.
    //
    // ⚠️ 산업 'None'에 'IndustryLabel'을 쓰지 않는다 — 거기서는 '미지정'이 나오는데,
    //   장비의 None은 지정을 안 한 것이 아니라 **어느 산업에나 붙는다**는 뜻이다.
    // ※ 'SpeedAddPermille'은 천분율이다(100 = 10%). 감산 장비가 생길 수 있어 부호를 함께 만든다.
    public static string GetEffectText(int equipTid)
    {
        if (!GameDataLoader.TryGetEquip(equipTid, out EquipTableRow row))
        {
            return "";
        }

        string industry = row.Industry == IndustryType.None
            ? "전산업"
            : IndustryLabel.Get((EIndustryType)(byte)row.Industry);

        string sign = row.SpeedAddPermille >= 0 ? "+" : "";

        return $"{industry} {sign}{row.SpeedAddPermille / 10f:0.#}%";
    }

    // 능력치 칸 수의 상한 — 전설·신화 장비의 칸 수다('EnchantGradeTable.SlotCount'의 최댓값).
    // ※ 장비 칸 프리팹의 네모는 이보다 많아도 된다 — 남는 네모는 꺼진다('SlotView' · 'ItemIconView').
    public const int MaxStatSlotCount = 3;

    // 이 등급의 장비가 갖는 능력치 칸 수 — 일반·고급 1 · 희귀·영웅 2 · 전설·신화 3. 모르는 등급이면 0.
    //
    // 칸 수는 서버와 같은 표('EnchantGradeTable.SlotCount')에서 읽는다 — 이슈 #46에서 기획이 확정했다.
    public static int GetStatSlotCount(GlobalRarity rarity)
        => System.Math.Min(GameDataLoader.GetEnchantSlotCount(rarity), MaxStatSlotCount);

    // 능력치 옵션 한 줄의 문구 — '낚시 +4%' · '전산업 +2%' · '경험치 +3%'. 모르는 종류면 빈 문자열.
    //
    // ※ 'Value'는 천분율이다(40 = 4%). 산업 'None'을 '전산업'으로 읽는 이유는 'GetEffectText'와 같다.
    public static string GetOptionText(EnchantOptionTableRow option)
    {
        string percent = $"+{option.Value / 10f:0.#}%";

        return option.OptionType switch
        {
            EnchantOptionType.Speed => option.Industry == IndustryType.None
                ? $"전산업 {percent}"
                : $"{IndustryLabel.Get((EIndustryType)(byte)option.Industry)} {percent}",
            EnchantOptionType.CharacterExp => $"경험치 {percent}",
            _                              => "",
        };
    }

    // 장비 개체의 능력치 칸을 'into'에 채운다 — 길이 = 칸 수, 빈 칸은 null.
    //   optionTids : 칸마다 박힌 옵션 TID ('EnchantOptions' — 칸 수만큼, 인챈트 전이면 비어 있다)
    //
    // 인벤토리 장비 칸('EquipSlotSource')과 경매장 매물 줄('AuctionText')이 같은 장비를 같은 칸으로 읽게 한 곳에 둔다.
    // ※ 옵션이 칸 수보다 많이 오면(옛 데이터) **숨기지 않고 칸을 늘려** 보인다 —
    //   잘라 버리면 실제로 붙어 있는 옵션이 화면에서 사라진다. 상한만 지킨다.
    public static void ReadStatOptions(int equipTid, IReadOnlyList<int>? optionTids, List<EnchantOptionTableRow?> into)
    {
        into.Clear();

        int optionCount = optionTids?.Count ?? 0;
        int slotCount   = GetStatSlotCount(GameDataLoader.GetEquipRarity(equipTid));
        int count       = System.Math.Min(System.Math.Max(slotCount, optionCount), MaxStatSlotCount);

        for (int i = 0; i < count; i++)
        {
            EnchantOptionTableRow? option = null;

            if (i < optionCount && GameDataLoader.TryGetEnchantOption(optionTids![i], out EnchantOptionTableRow row))
            {
                option = row;
            }

            into.Add(option);
        }
    }

    // 칸마다의 등급 — 칸의 네모 색으로 쓴다. 빈 칸은 'None' ('SlotView' · 'ItemIconView'가 그린다).
    public static void ToSocketGrades(IReadOnlyList<EnchantOptionTableRow?> options, List<GlobalRarity> into)
    {
        into.Clear();

        foreach (EnchantOptionTableRow? option in options)
        {
            into.Add(option?.Grade ?? GlobalRarity.None);
        }
    }

    // 박힌 칸의 수.
    public static int CountFilled(IReadOnlyList<EnchantOptionTableRow?> options)
    {
        int filled = 0;

        foreach (EnchantOptionTableRow? option in options)
        {
            if (option != null)
            {
                filled++;
            }
        }

        return filled;
    }

    // 능력치 칸을 툴팁 줄로 붙인다 — 묶음 제목 '능력치 칸 n/N' + 칸마다 한 줄(바탕 = 그 옵션의 등급색). 칸이 없으면 붙이지 않는다.
    // 칸의 네모는 색만 말하므로 무엇이 박혔는지는 여기서 적는다 — 인벤토리와 경매장이 같은 모양으로 읽힌다.
    public static void AddStatRows(TooltipContent content, IReadOnlyList<EnchantOptionTableRow?> options)
    {
        if (options.Count == 0)
        {
            return;
        }

        content.Header($"능력치 칸 {CountFilled(options)}/{options.Count}");

        // 인챈트 등급은 장비 하나에 하나다 — 칸마다 같은 등급이라 첫 칸의 것을 읽는다(이슈 #46).
        GlobalRarity grade = FirstGrade(options);

        content.Row("인챈트 등급",
                    grade == GlobalRarity.None ? "없음" : RarityLabel.Get(grade),
                    "",
                    grade == GlobalRarity.None ? null : RarityPalette.Get(grade));

        for (int i = 0; i < options.Count; i++)
        {
            EnchantOptionTableRow? option = options[i];

            if (option == null)
            {
                content.Row($"{i + 1}", "비어 있음");

                continue;
            }

            content.Row($"{i + 1}", GetOptionText(option), RarityLabel.Get(option.Grade), RarityPalette.Get(option.Grade));
        }
    }

    // 박힌 칸의 인챈트 등급 — 칸이 모두 같은 등급이라 첫 칸의 것이다. 비어 있으면 'None'.
    public static GlobalRarity FirstGrade(IReadOnlyList<EnchantOptionTableRow?> options)
    {
        foreach (EnchantOptionTableRow? option in options)
        {
            if (option != null)
            {
                return option.Grade;
            }
        }

        return GlobalRarity.None;
    }

    // 만분율 확률 — '16%' · '0.4%' · '0.05%'. 큐브 상승 확률을 적는다.
    public static string FormatPermyriad(int permyriad) => $"{permyriad / 100f:0.##}%";

    // 큐브 규칙 툴팁 — 칸 수 · 첫 사용 · 등급 상승 · 다시 뽑기 · 큐브별 상승 확률 표.
    //
    // ■ 왜 툴팁인가 (2026-10-03 사용자 피드백)
    // "처음엔 일반만 나오다가 확률로 한 단계씩 오른다"는 문서를 읽지 않으면 화면만 보고 알 수 없었다.
    // 큐브 창에는 한 줄 요약만 두고, 전체 규칙과 확률 표는 올리면 여기서 펼친다.
    // ※ 값은 테이블에서 읽는다 — 수치를 문구에 적어 두면 엑셀이 바뀔 때 거짓이 된다.
    //
    // ■ 규칙 문장은 라벨 칸에만 쓴다 (2026-10-04 사용자 피드백 — "좌우가 너무 길다")
    // 값·보조 값 열 폭은 툴팁의 모든 줄에서 가장 긴 글자에 맞춰진다('TooltipPresenter.FitColumns').
    // 문장을 값 칸에 두면 확률 표까지 그 폭을 물려받아 패널이 늘어난다 — 값 칸에는 짧은 확률만 둔다.
    public static TooltipContent BuildCubeRuleTooltip()
    {
        var content = new TooltipContent("인챈트 큐브")
            .Row(SlotCountSummary(), "")
            .Row("첫 사용은 일반 · 이후 확률로 한 단계씩 상승", "")
            .Row("매번 칸 전부 다시 뽑기 (옵션 중복 가능)", "")
            .Row("착용 중이면 벗긴 뒤 사용", "");

        IReadOnlyList<EnchantItemTableRow> cubes = GameDataLoader.EnchantCubes;

        if (cubes.Count == 0)
        {
            return content;
        }

        // 확률 표 — 값 칸이 첫 큐브, 보조 칸이 둘째 큐브다. 큐브가 셋 이상이 되면 표 모양을 다시 정한다.
        // 열 머리는 큐브 이름에서 공통 꼬리('인챈트 큐브')를 뗀 짧은 이름이다 — 긴 이름은 열을 넓힌다.
        content.Row("■ 등급 상승 확률",
                    ShortCubeName(cubes[0].ItemTID),
                    cubes.Count > 1 ? ShortCubeName(cubes[1].ItemTID) : "",
                    null);

        for (GlobalRarity grade = GlobalRarity.Common; grade < GlobalRarity.Mythic; grade++)
        {
            GlobalRarity next = grade + 1;

            content.Row($"{RarityLabel.Get(grade)} ▶ {RarityLabel.Get(next)}",
                        FormatPermyriad(GameDataLoader.GetEnchantUpPermyriad(grade, cubes[0].ItemTID)),
                        cubes.Count > 1 ? FormatPermyriad(GameDataLoader.GetEnchantUpPermyriad(grade, cubes[1].ItemTID)) : "",
                        RarityPalette.Get(next));
        }

        return content.Row($"{RarityLabel.Get(GlobalRarity.Mythic)}은 최고 등급 · 옵션만 다시 뽑기", "");
    }

    // 확률 표 열 머리 — '인챈트 큐브' → '기본', '상급 인챈트 큐브' → '상급'. 꼬리가 다르면 이름 그대로.
    private static string ShortCubeName(int cubeTid)
    {
        const string Tail = "인챈트 큐브";

        string name  = GameDataLoader.GetItemName(cubeTid);
        string front = name.EndsWith(Tail) ? name[..^Tail.Length].Trim() : name;

        return front.Length == 0 ? "기본" : front;
    }

    // 등급별 칸 수 요약 — '일반·고급 1칸 · 희귀·영웅 2칸 · 전설·신화 3칸'. 같은 칸 수의 등급을 묶는다.
    private static string SlotCountSummary()
    {
        var parts = new List<string>();
        var names = new List<string>();
        int run   = -1;

        for (GlobalRarity grade = GlobalRarity.Common; grade <= GlobalRarity.Mythic; grade++)
        {
            int count = GetStatSlotCount(grade);

            if (count != run && names.Count > 0)
            {
                parts.Add($"{string.Join("·", names)} {run}칸");
                names.Clear();
            }

            run = count;
            names.Add(RarityLabel.Get(grade));
        }

        if (names.Count > 0)
        {
            parts.Add($"{string.Join("·", names)} {run}칸");
        }

        return string.Join(" · ", parts);
    }

    // 장비 개체 하나의 툴팁 — 등급 · 종류 · 기본 능력치 · (장착) · 즉시 판매가·경매 등록가 · 능력치 칸. 모르는 TID면 null.
    //   equipped : '장착' 줄에 적을 문구. null이면 줄을 뺀다(장비 고르기·경매 등록 후보는 전부 인벤토리에 있다)
    //
    // 인벤토리 장비 칸 · 장비 고르기 줄 · 경매 등록 후보가 같은 장비를 **같은 툴팁**으로 읽게 한 곳에 둔다.
    // ※ 두 가격을 나란히 적는다 — 즉시 판매는 정해진 값, 경매는 그 값 ~ x10 사이에서 직접 정한다('Market 규칙.md').
    public static TooltipContent? BuildTooltip(int equipTid, IReadOnlyList<int>? optionTids, string? equipped = null)
    {
        if (!GameDataLoader.TryGetEquip(equipTid, out EquipTableRow row))
        {
            return null;
        }

        var content = new TooltipContent(row.Name)
            .Row("등급", RarityLabel.Get(row.GlobalRarity), "", RarityPalette.Get(row.GlobalRarity))
            .Row("종류", GetKindName(row.EquipKind))
            .Row("기본 능력치", GetEffectText(equipTid));

        if (equipped != null)
        {
            content.Row("장착", equipped);
        }

        content.Row("즉시 판매가", $"{AuctionModel.InstantSellPrice(row.BasePrice):N0} 골드", "인벤토리 [판매]", null)
               .Row("경매 등록가", AuctionModel.FormatBand(row.BasePrice));

        var options = new List<EnchantOptionTableRow?>();

        ReadStatOptions(equipTid, optionTids, options);
        AddStatRows(content, options);

        return content;
    }

    // 이 칸에 낄 수 있는 종류인가. 클라가 목록을 거를 때만 쓴다 —
    // **거절은 서버가 한다**('EquipKindMismatch'). 서버 'EquipCatalog.CanEquip'과 같은 표다.
    public static bool CanEquip(EquipKind kind, EEquipSlot slot) => slot switch
    {
        EEquipSlot.Weapon     => kind == EquipKind.Weapon,
        EEquipSlot.Accessory1 => kind == EquipKind.Accessory,
        EEquipSlot.Accessory2 => kind == EquipKind.Accessory,
        EEquipSlot.Gem        => kind == EquipKind.Gem,
        _                     => false,
    };

    // 장비 종류의 이름 — '무기' · '장신구' · '보석'. 모르는 값이면 영문 이름 그대로('IndustryLabel'과 같다).
    public static string GetKindName(EquipKind kind) => kind switch
    {
        EquipKind.Weapon    => "무기",
        EquipKind.Accessory => "장신구",
        EquipKind.Gem       => "보석",
        _                   => kind.ToString(),
    };

    // 캐릭터의 장비 칸 — **배열 순서 = 장착 네모의 왼쪽 → 오른쪽**(T-104). 슬롯 설정의 장비 칸 4개와 같은 순서다.
    public static readonly EEquipSlot[] WornSlots =
    {
        EEquipSlot.Weapon, EEquipSlot.Accessory1, EEquipSlot.Accessory2, EEquipSlot.Gem,
    };

    // 이 캐릭터가 칸마다 낀 장비의 등급(+ 아이콘)을 'WornSlots' 순서로 담는다 — 빈 칸은 'None' · null (장착 네모가 호출, T-104).
    //   icons : 아이콘까지 그리는 줄(작업슬롯 칸)만 넘긴다. null이면 등급만 읽는다(인벤토리 캐릭터 칸)
    //
    // ※ 인벤토리 캐릭터 칸과 작업슬롯 칸이 같은 값을 그린다 — 각자 훑으면 두 화면이 다르게 말한다.
    public static void ReadWornGrades(PlayerDataModel data, long characterId, List<GlobalRarity> into, List<Sprite?>? icons = null)
    {
        into.Clear();
        icons?.Clear();

        foreach (EEquipSlot slot in WornSlots)
        {
            EquipInfo? equip = data.FindWornEquip(characterId, slot);

            into.Add(equip != null ? GameDataLoader.GetEquipRarity(equip.EquipTid) : GlobalRarity.None);
            icons?.Add(equip != null ? VisualCatalog.EquipIconOf(equip.EquipTid) : null);
        }
    }

    // 툴팁에 '장비' 묶음 — 칸 이름 · 장비 이름 · 등급(줄 바탕 등급색) · 빈 칸은 '비어 있음' (캐릭터 칸 툴팁이 호출, T-104).
    //
    // 네모는 등급만 말한다 — 무엇을 꼈는지는 여기서 읽는다.
    public static void AddWornRows(TooltipContent content, PlayerDataModel data, long characterId)
    {
        content.Header("장비");

        foreach (EEquipSlot slot in WornSlots)
        {
            EquipInfo? equip = data.FindWornEquip(characterId, slot);

            if (equip == null)
            {
                content.Row(GetSlotName(slot), "비어 있음");

                continue;
            }

            GlobalRarity rarity = GameDataLoader.GetEquipRarity(equip.EquipTid);

            content.Row(GetSlotName(slot), GameDataLoader.GetEquipName(equip.EquipTid), RarityLabel.Get(rarity), RarityPalette.Get(rarity));
        }
    }

    // 장비 칸의 부위 이름 — '무기' · '장신구1' · '장신구2' · '보석'.
    public static string GetSlotName(EEquipSlot slot) => slot switch
    {
        EEquipSlot.Weapon     => "무기",
        EEquipSlot.Accessory1 => "장신구1",
        EEquipSlot.Accessory2 => "장신구2",
        EEquipSlot.Gem        => "보석",
        _                     => "",
    };
}
