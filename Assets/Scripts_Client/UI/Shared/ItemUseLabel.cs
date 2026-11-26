using GameData;

// 소모품('ItemUseTable' — T-195) → 사용자에게 보일 효과 한 줄 · 대상 종류.
//
// ■ 왜 따로 두는가
// 인벤토리 사용 창('ItemUsePresenter') · 작업슬롯 칸의 버프 칩 툴팁 · 슬롯 설정의 자동 사용 칸이
// 같은 효과 문구를 쓴다. 한쪽에만 적어 두면 같은 영약이 화면마다 다르게 읽힌다.
//
// ■ 판정은 서버가 한다
// 대상 종류('TargetsSlots' · 'TargetsCharacter')는 **화면을 고르는 데만** 쓴다.
// 잘못 보내도 서버가 'InvalidUseTarget'으로 거절한다 — 여기서 규칙을 하나 더 만들지 않는다.
public static class ItemUseLabel
{
    // 이 아이템이 소모품인가 — 'ItemUseTable'에 행이 있으면 소모품이다. 상자('IsBox')와 겹치지 않는다
    public static bool IsUsable(int itemTid) => GameTable.ItemUseTable.TryGet(itemTid, out _);

    public static bool TryGet(int itemTid, out ItemUseTableRow row) => GameTable.ItemUseTable.TryGet(itemTid, out row);

    // 슬롯을 대상으로 고르는 소모품인가 — 영약 · 지도 · 나침반
    public static bool TargetsSlots(ItemUseEffect effect) => effect is ItemUseEffect.SlotWorkSpeed
                                                                    or ItemUseEffect.SlotYield
                                                                    or ItemUseEffect.InstantJudge
                                                                    or ItemUseEffect.SlotBoxChance
                                                                    or ItemUseEffect.SlotBoxRelicChance;

    // 캐릭터를 대상으로 고르는 소모품인가 — 성장의 비약
    public static bool TargetsCharacter(ItemUseEffect effect) => effect == ItemUseEffect.CharacterExp;

    // 자동 사용 칸에 넣을 수 있는가 — 슬롯에 남는 버프(영약 · 나침반 — #113 · #153). 서버 'ItemUseCatalog.IsAutoUsable'과 같은 기준.
    // 보물 지도(즉시 판정)는 빠진다 — 인벤토리에서 슬롯을 골라 직접 쓴다.
    public static bool IsAutoUsable(ItemUseEffect effect) => effect is ItemUseEffect.SlotWorkSpeed
                                                                    or ItemUseEffect.SlotYield
                                                                    or ItemUseEffect.SlotBoxChance
                                                                    or ItemUseEffect.SlotBoxRelicChance;

    // 효과 한 줄 — '작업속도 +30% · 판정 20회'. 모르는 효과면 빈 문자열(부르는 쪽이 줄을 뺀다)
    public static string Effect(ItemUseTableRow row) => row.UseEffect switch
    {
        ItemUseEffect.CharacterExp       => Lang.Format("UI.ItemUse.EffectExp", $"{row.EffectValue:N0}"),
        ItemUseEffect.SlotWorkSpeed      => Lang.Format("UI.ItemUse.EffectSpeed", Percent(row.EffectValue), row.EffectCount),
        ItemUseEffect.SlotYield          => Lang.Format("UI.ItemUse.EffectYield", Percent(row.EffectValue), row.EffectCount),
        ItemUseEffect.TraitReset         => Lang.Get("UI.ItemUse.EffectReset"),
        ItemUseEffect.InstantJudge       => Lang.Format("UI.ItemUse.EffectJudge", row.EffectValue),
        ItemUseEffect.SlotBoxChance      => Lang.Format("UI.ItemUse.EffectBox", Percent(row.EffectValue), row.EffectCount),
        ItemUseEffect.SlotBoxRelicChance => Lang.Format("UI.ItemUse.EffectBoxRelic", Percent(row.EffectValue), row.EffectCount),
        _                                => "",
    };

    // 이 아이템의 효과 한 줄. 소모품이 아니면 빈 문자열
    public static string Effect(int itemTid) => TryGet(itemTid, out var row) ? Effect(row) : "";

    // 천분율 → 백분율 글자 — 300 → '30', 125 → '12.5'
    private static string Percent(int permille) => (permille / 10f).ToString("0.#");

    // 비약을 먹인 뒤의 레벨 · 경험치 — 사용 창 미리보기와 사용 뒤 알림이 같은 계산을 쓴다.
    //   level · exp : 지금 값('CharacterInfo.Level' · 'Exp' — 'Exp'는 지금 레벨 안에서 쌓인 몫)
    //   addExp      : 더할 경험치
    // 만렙에 닿으면 남는 경험치는 버린다(서버 'GrantCharacterExp'와 같다 — 만렙 'Exp'는 0).
    // ※ 판정은 서버가 한다 — 여기는 보여 주기만 한다.
    public static (int Level, int Exp) ProjectExp(int level, int exp, long addExp)
    {
        long total = exp + addExp;

        while (GameDataLoader.TryGetRequiredExp(level + 1, out int required) && total >= required)
        {
            total -= required;
            level++;
        }

        return GameDataLoader.TryGetRequiredExp(level + 1, out _) ? (level, (int)total) : (level, 0);
    }

    // 지금 레벨 안의 진행 % (0~99) — 만렙이면 -1
    public static int ExpPercent(int level, int exp)
        => GameDataLoader.TryGetRequiredExp(level + 1, out int required) && required > 0
               ? (int)System.Math.Clamp(exp * 100L / required, 0L, 99L)
               : -1;

    // 'Lv36 (45%)' · 만렙이면 'Lv50 (최대)'
    public static string LevelWithPercent(int level, int exp)
    {
        int percent = ExpPercent(level, exp);

        return percent < 0 ? Lang.Format("UI.ItemUse.LevelMax", level) : Lang.Format("UI.ItemUse.LevelPercent", level, percent);
    }
}
