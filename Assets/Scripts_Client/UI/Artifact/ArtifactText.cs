using GameData;
using MikaProtocol;

// 아티팩트 글자 — 효과 한 줄 · 상태 · 칸 툴팁. 도감 · 인벤토리 장비 탭 칸 · 아티팩트 창 · 판매 목록이 같은 문구를 쓴다.
//
// ■ 효과 문구는 테이블에서 만든다
//   'ArtifactTable.Description'은 기획 메모라 로직에서 읽지 않는다 — 효과 종류 · 산업 · 값으로 조립한다.
//   값은 천분율이다('UserTraitEffect' 주석) — 특성 화면과 같은 "+0.2%" 표기.
public static class ArtifactText
{
    // 효과 한 줄 — "농사 작업속도 +0.2%" · "감정 비용 −0.5%". 모르는 종류면 빈 문자열.
    public static string Effect(ArtifactTableRow row)
    {
        string value = $"{row.EffectValue / 10f:0.#}%";

        return row.EffectType switch
        {
            UserTraitEffect.SpeedAdd             => Lang.Format("UI.Artifact.EffectSpeed", IndustryName(row.Industry), value),
            UserTraitEffect.YieldAdd             => Lang.Format("UI.Artifact.EffectYield", IndustryName(row.Industry), value),
            UserTraitEffect.AppraiseCostDiscount => Lang.Format("UI.Artifact.EffectAppraise", value),
            UserTraitEffect.RelicChanceAdd       => Lang.Format("UI.Artifact.EffectRelic", value),
            _                                    => "",
        };
    }

    // 도감 상태 한 마디 — 장착 / 도감에 없음. 장착하면 항상 켜져 있다(#90).
    public static string State(PlayerDataModel data, int artifactTid)
        => Lang.Get(data.IsArtifactEquipped(artifactTid) ? "UI.Artifact.FilterEquipped" : "UI.Artifact.StateNone");

    // 칸 툴팁 — 등급 · 효과 · 상태 · 보유 · 값. 조작 줄은 인벤토리 장비 탭 칸만 붙인다('EquipSlotSource.BuildTooltip').
    public static TooltipContent? BuildTooltip(PlayerDataModel data, int artifactTid)
    {
        if (!GameDataLoader.TryGetArtifact(artifactTid, out ArtifactTableRow row))
        {
            return null;
        }

        int owned = data.GetArtifactCount(artifactTid);

        return new TooltipContent(Loc.Get(row.NameLocKey))
            .Row(Lang.Get("UI.Equip.Grade"), RarityLabel.Get(row.GlobalRarity), "", RarityPalette.Get(row.GlobalRarity))
            .Row(Lang.Get("UI.Artifact.Effect"), Effect(row))
            .Row(Lang.Get("UI.Artifact.Codex"), State(data, artifactTid))
            .Row(Lang.Get("UI.Inventory.Owned"), Lang.Format("UI.Inventory.CountUnit", $"{owned:N0}"))
            .Row(Lang.Get("UI.Equip.InstantSellPrice"), Lang.Format("UI.Equip.Gold", $"{SellCartModel.ArtifactInstantPrice(artifactTid):N0}"), Lang.Get("UI.Sell.Each"), null)
            .Row(Lang.Get("UI.Equip.AuctionPrice"), AuctionModel.FormatBand(row.BasePrice), Lang.Get("UI.Sell.UnitPrice"), null);
    }

    // 효과가 붙는 산업 — None은 '전 산업'.
    private static string IndustryName(IndustryType industry)
        => industry == IndustryType.None ? Lang.Get("UI.Artifact.AllIndustry") : IndustryLabel.Get((EIndustryType)(byte)industry);
}
