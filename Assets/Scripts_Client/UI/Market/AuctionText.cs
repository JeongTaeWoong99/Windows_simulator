using System;
using System.Collections.Generic;
using GameData;
using MikaProtocol;

/// 경매장 탭의 Presenter들이 함께 쓰는 문구·변환 — 매물 줄 문구·아이콘·툴팁, 남은 시간, 이름 → TID 검색.
//
// 매물 줄은 장비 검색과 내 매물 두 곳이 같은 모양으로 그린다. 각자 만들면 한쪽만 고쳐져 두 화면이 다르게 말한다.
//
// ■ 줄은 요약, 툴팁은 전부
// 줄에는 "무엇을 · 얼마에 · 언제까지"만 적는다(이름 · 한 줄 정보 · 가격). 등급 이름 · 기본 능력치 · 능력치 칸 ·
// 가격 기준(즉시 판매가 · 경매 등록가)은 줄에 마우스를 올리면 뜨는 툴팁에 둔다 — 인벤토리 칸 툴팁과 같은 줄 모양이다.
// ■ 글자 색으로 가른다('UIRichText') — 라벨은 흐리게, 가격은 강조색으로. 같은 한 줄이 표처럼 읽힌다.
public static class AuctionText
{
    // 판매자 줄 — 서버가 판매자 이름을 싣기 전까지 내 것만 알아본다.
    //
    // ⏸ 'AuctionListingInfo'에 판매자가 없다(서버가 일부러 싣지 않았다) — 이름 필드를 요청했다(이슈 #47).
    //   필드가 오면 '-' 자리에 이름을 넣는다. 내 매물 판별은 그 전에도 'AuctionModel.IsMine'으로 된다.
    private const string UnknownSeller = "-";

    // 매물 줄 하나의 완성값을 만든다. 버튼 문구와 누를 수 있는지는 부르는 쪽이 정한다.
    //   isMine   : 내가 올린 매물 — 판매자를 '나'로 적는다(툴팁 포함)
    //   markMine : 이름 앞에 '[내 매물]'을 붙이고 판매자 줄을 보인다. 전부 내 것인 [내 매물] 탭은 끈다 — 거기선 소음이다
    public static AuctionRowContent ToRow(AuctionListingInfo listing, string actionLabel, bool canAct, bool isMine, bool markMine = true)
    {
        var    rarity = (GlobalRarity)listing.Rarity;
        string dot    = UIRichText.Dot;

        string remain = listing.State == EAuctionListingState.Reserved
            ? UIRichText.Paint("구매 진행 중", UIThemeRole.Negative)
            : $"{UIRichText.Label("남은")} {FormatRemaining(listing.ExpiresAtUnixMs)}";

        string info;
        string detail = UIRichText.Gold(listing.TotalPrice);

        if (listing.Kind == EAuctionKind.Item)
        {
            info   = $"{UIRichText.Label("수량")} {listing.Count:N0}개{dot}{remain}";
            detail = $"{detail}  {UIRichText.Label($"개당 {listing.UnitPrice:N0} G")}";
        }
        else
        {
            info = $"{EquipLabel.GetKindName((EquipKind)listing.Category)}{dot}{FormatStatSummary(listing.Tid, listing.EnchantOptions)}{dot}{remain}";
        }

        string title  = GetName(listing.Kind, listing.Tid);
        string seller = isMine ? UIRichText.Paint("나", UIThemeRole.Highlight) : UnknownSeller;

        return new AuctionRowContent
        {
            Key         = listing.ListingId,
            Icon        = ToIcon(listing),
            Title       = isMine && markMine ? $"{UIRichText.Paint("[내 매물]", UIThemeRole.Highlight)} {title}" : title,
            TitleColor  = RarityPalette.Get(rarity),
            Info        = info,
            Detail      = detail,
            Seller      = markMine ? $"{UIRichText.Label("판매자")} {seller}" : "",
            ActionLabel = actionLabel,
            CanAct      = canAct,
            Tooltip     = () => BuildListingTooltip(listing, isMine),
        };
    }

    // 매물의 아이콘 칸 — 자원은 수량, 장비는 능력치 칸까지.
    public static ItemIconContent ToIcon(AuctionListingInfo listing)
        => listing.Kind == EAuctionKind.Equip
            ? ItemIconContent.ForEquip(listing.Tid, listing.EnchantOptions)
            : ItemIconContent.ForItem(listing.Tid, listing.Count);

    // "능력치 2/4" — 박힌 칸 / 칸 수. 칸이 없는 장비면 "능력치 없음".
    // ⏸ 지금은 옛 인챈트 필드로 센다 — 새 능력치 패킷(이슈 #46)이 매물에도 실리면 'EquipLabel.ReadStatOptions'만 바뀐다.
    public static string FormatStatSummary(int equipTid, IReadOnlyList<int>? optionTids)
    {
        var options = new List<EnchantOptionTableRow?>();

        EquipLabel.ReadStatOptions(equipTid, optionTids, options);

        return options.Count == 0
            ? UIRichText.Label("능력치 없음")
            : $"{UIRichText.Label("능력치")} {EquipLabel.CountFilled(options)}/{options.Count}";
    }

    #region 툴팁

    // 매물 한 건의 툴팁 — 등급 · 종류 · 수량/기본 능력치 · 가격 · 남은 시간 · 판매자 · 가격 기준 · 능력치 칸.
    public static TooltipContent BuildListingTooltip(AuctionListingInfo listing, bool isMine)
    {
        var content = new TooltipContent(GetName(listing.Kind, listing.Tid));

        AddRarityRow(content, (GlobalRarity)listing.Rarity);

        if (listing.Kind == EAuctionKind.Equip)
        {
            content.Row("종류", EquipLabel.GetKindName((EquipKind)listing.Category))
                   .Row("기본 능력치", EquipLabel.GetEffectText(listing.Tid))
                   .Row("가격", $"{listing.TotalPrice:N0} 골드");
        }
        else
        {
            content.Row("수량", $"{listing.Count:N0} 개")
                   .Row("가격", $"{listing.TotalPrice:N0} 골드", $"개당 {listing.UnitPrice:N0}", null);
        }

        content.Row("남은 시간", listing.State == EAuctionListingState.Reserved ? "구매 진행 중" : FormatRemaining(listing.ExpiresAtUnixMs))
               .Row("판매자", isMine ? "나 — 내 매물은 살 수 없습니다" : "비공개");

        AddPriceRules(content, GetBasePrice(listing.Kind, listing.Tid));

        if (listing.Kind == EAuctionKind.Equip)
        {
            var options = new List<EnchantOptionTableRow?>();

            EquipLabel.ReadStatOptions(listing.Tid, listing.EnchantOptions, options);
            EquipLabel.AddStatRows(content, options);
        }

        return content;
    }

    // 거래소 자원 줄의 툴팁 — 판매 현황 · 시세 · 보유 · 가격 기준.
    //   mine  : 내가 올린 수량
    //   owned : 인벤토리 보유 수량
    public static TooltipContent BuildMarketItemTooltip(MarketItemInfo item, long mine, long owned)
    {
        var content = new TooltipContent(GameDataLoader.GetItemName(item.Tid));

        AddRarityRow(content, GameDataLoader.GetItemRarity(item.Tid))
            .Row("판매 중", $"{item.AvailableCount:N0} 개", mine > 0L ? $"내 매물 {mine:N0}" : "", null)
            .Row("최저가", item.AvailableCount > 0 ? $"{item.LowestUnitPrice:N0} 골드" : "매물 없음")
            .Row("최근 거래가", item.RecentUnitPrice > 0 ? $"{item.RecentUnitPrice:N0} 골드" : "-")
            .Row("전일 평균", item.YesterdayAvgPrice > 0 ? $"{item.YesterdayAvgPrice:N0} 골드" : "-")
            .Row("보유", $"{owned:N0} 개");

        AddPriceRules(content, GameDataLoader.GetItemPrice(item.Tid));

        return content.Row("[선택] 후 아래에서 수량·최대 단가를 정해 삽니다", "");
    }

    // 등록 후보 자원 줄의 툴팁 — 보유 · 가격 기준.
    public static TooltipContent BuildRegisterItemTooltip(int itemTid, long owned)
    {
        var content = new TooltipContent(GameDataLoader.GetItemName(itemTid));

        AddRarityRow(content, GameDataLoader.GetItemRarity(itemTid))
            .Row("보유", $"{owned:N0} 개");

        AddPriceRules(content, GameDataLoader.GetItemPrice(itemTid));

        return content;
    }

    // 등록 후보 장비 줄의 툴팁 — 인벤토리 장비 칸과 같은 공용 툴팁('EquipLabel.BuildTooltip').
    public static TooltipContent? BuildRegisterEquipTooltip(EquipInfo equip)
        => EquipLabel.BuildTooltip(equip.EquipTid, equip.EnchantOptions);

    // 등급 한 줄 — 줄 바탕이 등급색이다('InventorySlotSource.AddRarityRow'와 같은 모양).
    private static TooltipContent AddRarityRow(TooltipContent content, GlobalRarity rarity)
        => content.Row("등급", RarityLabel.Get(rarity), "", RarityPalette.Get(rarity));

    // 가격 기준 묶음 — 즉시 판매가(정해진 값) · 경매 등록가(그 값 ~ x10). 두 판매 방식을 한눈에 비교하게 둔다.
    private static void AddPriceRules(TooltipContent content, int basePrice)
    {
        content.Header("가격 기준")
               .Row("즉시 판매가", $"{AuctionModel.InstantSellPrice(basePrice):N0} 골드", "인벤토리 [판매]", null)
               .Row("경매 등록가", AuctionModel.FormatBand(basePrice), "개당 단가", null);
    }

    // 종류별 기준가 — 자원은 ItemTable, 장비는 EquipTable의 BasePrice. 모르면 0.
    private static int GetBasePrice(EAuctionKind kind, int tid)
    {
        if (kind != EAuctionKind.Equip)
        {
            return GameDataLoader.GetItemPrice(tid);
        }

        return GameDataLoader.TryGetEquip(tid, out EquipTableRow row) ? row.BasePrice : 0;
    }

    #endregion

    // 매물 종류에 맞춰 이름을 찾는다 — 자원은 ItemTable, 장비는 EquipTable.
    public static string GetName(EAuctionKind kind, int tid)
    {
        return kind == EAuctionKind.Equip ? GameDataLoader.GetEquipName(tid) : GameDataLoader.GetItemName(tid);
    }

    // 만료까지 남은 시간 — "23시간" · "15분" · "곧 만료".
    public static string FormatRemaining(long expiresAtUnixMs)
    {
        TimeSpan left = DateTimeOffset.FromUnixTimeMilliseconds(expiresAtUnixMs) - DateTimeOffset.UtcNow;

        if (left.TotalHours >= 1d)
        {
            return $"{(int)left.TotalHours}시간";
        }

        if (left.TotalMinutes >= 1d)
        {
            return $"{(int)left.TotalMinutes}분";
        }

        return "곧 만료";
    }

    // 이름에 'query'가 든 자원 TID를 모은다. 비었으면 null — 이름 조건 없음이다.
    //
    // 서버는 이름을 모른다 — 클라가 TID 목록으로 바꿔 보낸다. 한 요청의 TID 상한을 넘으면 앞에서 자른다.
    public static List<int>? FindItemTids(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var tids = new List<int>();

        foreach (ItemTableRow row in GameTable.ItemTable.All)
        {
            if (row.Name.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                tids.Add(row.ItemTID);
            }
        }

        return Truncate(tids, query);
    }

    // 이름에 'query'가 든 장비 TID를 모은다. 규칙은 'FindItemTids'와 같다.
    public static List<int>? FindEquipTids(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var tids = new List<int>();

        foreach (EquipTableRow row in GameTable.EquipTable.All)
        {
            if (row.Name.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                tids.Add(row.EquipTID);
            }
        }

        return Truncate(tids, query);
    }

    // TID가 상한을 넘으면 앞에서 자르고 알린다 (FindItemTids · FindEquipTids에서 호출).
    //
    // ⚠️ 맞는 이름이 하나도 없으면 빈 목록을 그대로 돌려준다 — null로 바꾸면 "조건 없음"이 되어 전체가 나온다.
    //   서버도 빈 목록을 "조건 없음"으로 읽으므로, 부르는 쪽은 빈 목록이면 요청을 보내지 않는다.
    private static List<int> Truncate(List<int> tids, string query)
    {
        if (tids.Count <= AuctionModel.MaxQueryTids)
        {
            return tids;
        }

        ClientLogger.Warn(ClientLogger.UI, $"'{query}'에 맞는 이름이 {tids.Count}개라 앞의 {AuctionModel.MaxQueryTids}개만 찾는다.");

        return tids.GetRange(0, AuctionModel.MaxQueryTids);
    }
}
