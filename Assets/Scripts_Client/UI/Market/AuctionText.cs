using System;
using System.Collections.Generic;
using GameData;
using MikaProtocol;

// UnityEngine에도 CharacterInfo(폰트 글리프 정보)가 있어 이름이 겹친다. 우리가 쓰는 건 패킷 쪽이다.
using CharacterInfo = MikaProtocol.CharacterInfo;

/// 경매장 탭의 Presenter들이 함께 쓰는 문구·변환 — 매물 줄 문구·아이콘·툴팁, 남은 시간, 이름 → TID 검색.
//
// 매물 줄은 장비 검색과 내 매물 두 곳이 같은 모양으로 그린다. 각자 만들면 한쪽만 고쳐져 두 화면이 다르게 말한다.
//
// ■ 줄은 요약, 툴팁은 전부
// 줄에는 "무엇을 · 얼마에 · 언제까지"만 적는다(이름 · 한 줄 정보 · 가격). 등급 이름 · 기본 능력치 · 능력치 칸 ·
// 가격 기준(즉시 판매가 · 경매 등록가)은 줄에 마우스를 올리면 뜨는 툴팁에 둔다 — 인벤토리 칸 툴팁과 같은 줄 모양이다.
// ■ 글자 색으로 가른다('UIRichText') — 라벨은 흐리게, 가격은 강조색으로. 같은 한 줄이 표처럼 읽힌다.
// ■ 캐릭터 매물: 줄에는 레벨 + 가장 높은 적성 하나, 적성 5종은 툴팁에 둔다(2026-10-03 결정).
public static class AuctionText
{
    // 판매자 이름이 비어 온 매물의 판매자 칸(이슈 #47 — 서버가 이름을 싣는다. 탈퇴 등으로 비면 이것).
    private const string UnknownSeller = "-";

    // 매물 줄 하나의 완성값을 만든다. 버튼 문구와 누를 수 있는지는 부르는 쪽이 정한다.
    //   isMine   : 내가 올린 매물 — 판매자 이름 뒤에 '(나)'를 붙인다(툴팁 포함)
    //   markMine : 이름 앞에 '[내 매물]'을 붙이고 판매자 줄을 보인다. 전부 내 것인 [내 매물] 탭은 끈다 — 거기선 소음이다
    // ※ 내 매물 판별은 이름이 아니라 'AuctionModel.IsMine'(내 매물 ID)으로 한다 — 이름은 겹칠 수 있다.
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
        else if (listing.Kind == EAuctionKind.Character)
        {
            info = $"{FormatCharacterSummary(listing.Character)}{dot}{remain}";
        }
        else
        {
            info = $"{EquipLabel.GetKindName((EquipKind)listing.Category)}{dot}{FormatStatSummary(listing.Tid, listing.EnchantOptions)}{dot}{remain}";
        }

        string title  = GetName(listing.Kind, listing.Tid);
        string seller = isMine
            ? UIRichText.Paint($"{GetSellerName(listing)} (나)", UIThemeRole.Highlight)
            : GetSellerName(listing);

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

    // 매물의 아이콘 칸 — 자원은 수량, 장비는 능력치 칸까지, 캐릭터는 등급 바탕만.
    public static ItemIconContent ToIcon(AuctionListingInfo listing) => listing.Kind switch
    {
        EAuctionKind.Equip     => ItemIconContent.ForEquip(listing.Tid, listing.EnchantOptions),
        EAuctionKind.Character => ItemIconContent.ForCharacter(listing.Tid),
        _                      => ItemIconContent.ForItem(listing.Tid, listing.Count),
    };

    // 판매자 이름. 비어 오면 '-'.
    public static string GetSellerName(AuctionListingInfo listing)
        => string.IsNullOrEmpty(listing.SellerName) ? UnknownSeller : listing.SellerName;

    // "LV.12 · 낚시 7" — 레벨 + 가장 높은 적성 하나. 캐릭터 정보가 없으면 빈 문자열.
    public static string FormatCharacterSummary(CharacterInfo? character)
    {
        if (character == null)
        {
            return "";
        }

        return $"{CharacterSlotSource.GetLevelLabel(character.Level)}{UIRichText.Dot}{FormatTopAptitude(character)}";
    }

    // 가장 높은 적성 — "낚시 7". 같은 값이면 산업 순서(농사·낚시·…)가 앞인 것. 적성이 하나도 없으면 "적성 없음".
    public static string FormatTopAptitude(CharacterInfo character)
    {
        EIndustryType best      = EIndustryType.None;
        byte          bestValue = 0;

        foreach (EIndustryType industry in InventoryGridPresenter.StripIndustries)
        {
            byte value = GetAptitude(character, industry);

            if (value > bestValue)
            {
                best      = industry;
                bestValue = value;
            }
        }

        return bestValue == 0
            ? UIRichText.Label("적성 없음")
            : $"{UIRichText.Label(IndustryLabel.Get(best))} {AptitudeLabel.GetText(bestValue)}";
    }

    // 캐릭터 정보의 한 산업 적성. 없으면 0.
    private static byte GetAptitude(CharacterInfo character, EIndustryType industry)
    {
        foreach (AptitudeInfo aptitude in character.Aptitudes)
        {
            if (aptitude.Industry == industry)
            {
                return aptitude.Value;
            }
        }

        return 0;
    }

    // "능력치 2/3" — 박힌 칸 / 칸 수. 칸이 없는 장비면 "능력치 없음".
    public static string FormatStatSummary(int equipTid, IReadOnlyList<int>? optionTids)
    {
        var options = new List<EnchantOptionTableRow?>();

        EquipLabel.ReadStatOptions(equipTid, optionTids, options);

        return options.Count == 0
            ? UIRichText.Label("능력치 없음")
            : $"{UIRichText.Label("능력치")} {EquipLabel.CountFilled(options)}/{options.Count}";
    }

    #region 툴팁

    // 매물 한 건의 툴팁 — 등급 · 종류 · 수량/기본 능력치/레벨 · 가격 · 남은 시간 · 판매자 · 가격 기준 · 능력치 칸/적성.
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
        else if (listing.Kind == EAuctionKind.Character)
        {
            if (listing.Character != null)
            {
                content.Row("레벨", CharacterSlotSource.GetLevelLabel(listing.Character.Level));
            }

            content.Row("가격", $"{listing.TotalPrice:N0} 골드");
        }
        else
        {
            content.Row("수량", $"{listing.Count:N0} 개")
                   .Row("가격", $"{listing.TotalPrice:N0} 골드", $"개당 {listing.UnitPrice:N0}", null);
        }

        content.Row("남은 시간", listing.State == EAuctionListingState.Reserved ? "구매 진행 중" : FormatRemaining(listing.ExpiresAtUnixMs))
               .Row("판매자", GetSellerName(listing), isMine ? "나 — 내 매물은 살 수 없습니다" : "", null);

        AddPriceRules(content, GetBasePrice(listing.Kind, listing.Tid));

        if (listing.Kind == EAuctionKind.Equip)
        {
            var options = new List<EnchantOptionTableRow?>();

            EquipLabel.ReadStatOptions(listing.Tid, listing.EnchantOptions, options);
            EquipLabel.AddStatRows(content, options);
        }
        else if (listing.Kind == EAuctionKind.Character && listing.Character != null)
        {
            AddAptitudeRows(content, listing.Character);
        }

        return content;
    }

    // 적성 5종 묶음 — 산업 이름 · 적성 · 기본 속도('CharacterSlotSource.BuildTooltip'과 같은 줄 모양).
    // ※ 속도는 적성만의 값이다 — 장비·특성 가산은 빠진다.
    public static void AddAptitudeRows(TooltipContent content, CharacterInfo character)
    {
        content.Header("적성");

        foreach (EIndustryType industry in InventoryGridPresenter.StripIndustries)
        {
            byte   aptitude = GetAptitude(character, industry);
            string speed    = aptitude == 0 ? "" : $"{GameDataLoader.GetBaseWorkSpeed(aptitude) / (float)Constants.WorkSpeedScale:0.00}배";

            content.Row(IndustryLabel.Get(industry), AptitudeLabel.GetText(aptitude), speed, null);
        }
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
    //   blocked : 올릴 수 없는 사유. null이면 사유 줄을 넣지 않는다
    public static TooltipContent? BuildRegisterEquipTooltip(EquipInfo equip, string? blocked)
    {
        TooltipContent? content = EquipLabel.BuildTooltip(equip.EquipTid, equip.EnchantOptions);

        if (content != null && blocked != null)
        {
            content.Row("등록 불가", blocked);
        }

        return content;
    }

    // 등록 후보 캐릭터 줄의 툴팁 — 등급 · 레벨 · (등록 불가 사유) · 가격 기준 · 적성 5종.
    public static TooltipContent BuildRegisterCharacterTooltip(CharacterInfo character, string? blocked)
    {
        var content = new TooltipContent(GameDataLoader.GetCharacterName(character.CharacterTid));

        AddRarityRow(content, GameDataLoader.GetCharacterRarity(character.CharacterTid))
            .Row("레벨", CharacterSlotSource.GetLevelLabel(character.Level));

        if (blocked != null)
        {
            content.Row("등록 불가", blocked);
        }

        AddPriceRules(content, GameDataLoader.GetCharacterPrice(character.CharacterTid));
        AddAptitudeRows(content, character);

        return content;
    }

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

    // 종류별 기준가 — 자원은 ItemTable, 장비는 EquipTable, 캐릭터는 CharacterTable의 BasePrice. 모르면 0.
    public static int GetBasePrice(EAuctionKind kind, int tid) => kind switch
    {
        EAuctionKind.Equip     => GameDataLoader.TryGetEquip(tid, out EquipTableRow row) ? row.BasePrice : 0,
        EAuctionKind.Character => GameDataLoader.GetCharacterPrice(tid),
        _                      => GameDataLoader.GetItemPrice(tid),
    };

    #endregion

    // 매물 종류에 맞춰 이름을 찾는다 — 자원은 ItemTable, 장비는 EquipTable, 캐릭터는 CharacterTable.
    public static string GetName(EAuctionKind kind, int tid) => kind switch
    {
        EAuctionKind.Equip     => GameDataLoader.GetEquipName(tid),
        EAuctionKind.Character => GameDataLoader.GetCharacterName(tid),
        _                      => GameDataLoader.GetItemName(tid),
    };

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

    // 이름에 'query'가 든 캐릭터 TID를 모은다. 규칙은 'FindItemTids'와 같다.
    public static List<int>? FindCharacterTids(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var tids = new List<int>();

        foreach (CharacterTableRow row in GameTable.CharacterTable.All)
        {
            if (row.Name.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                tids.Add(row.CharacterTID);
            }
        }

        return Truncate(tids, query);
    }

    // TID가 상한을 넘으면 앞에서 자르고 알린다 (FindItemTids · FindEquipTids · FindCharacterTids에서 호출).
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
