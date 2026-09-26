using GameData;
using MikaProtocol;

// 장비 → 사용자에게 보일 문구.
//
// ■ 두 화면이 같은 장비를 읽는다
// 창고 장비 탭('EquipSlotSource')과 작업슬롯 세팅의 장비 칸('WorkStationSelectPresenter')이
// 같은 효과 문구를 쓴다. 한쪽에만 두면 같은 장비가 두 화면에서 다르게 읽힌다.
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
