using System.Collections.Generic;
using GameData;
using MikaProtocol;

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
