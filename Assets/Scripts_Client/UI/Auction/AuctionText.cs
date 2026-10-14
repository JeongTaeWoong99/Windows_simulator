using System;
using System.Collections.Generic;
using GameData;
using MikaProtocol;

// UnityEngine에도 CharacterInfo(폰트 글리프 정보)가 있어 이름이 겹친다. 우리가 쓰는 건 패킷 쪽이다.
using CharacterInfo = MikaProtocol.CharacterInfo;

/// 경매장 Presenter들이 함께 쓰는 문구·변환 — 종류별 이름·등급·아이콘, 표 한 줄, 툴팁, 남은 시간, 이름 → TID 검색 · 검색 후보.
//
// 매물 줄은 검색 결과와 판매 중 두 표가 같은 아이템 칸으로 그린다. 각자 만들면 한쪽만 고쳐져 두 화면이 다르게 말한다.
//
// ■ 줄은 요약, 툴팁은 전부
// 줄에는 "무엇을 · 얼마에 · 언제까지"만 적는다(이름 · 한 줄 설명 · 열 칸). 등급 이름 · 기본 능력치 · 능력치 칸 ·
// 가격 기준(즉시 판매가 · 경매 등록가)은 줄에 마우스를 올리면 뜨는 툴팁에 둔다 — 인벤토리 칸 툴팁과 같은 줄 모양이다.
// ■ 글자 색으로 가른다('UIRichText') — 라벨은 흐리게, 가격은 강조색으로, 6시간 안에 끝나는 매물은 경고색으로.
// ■ 캐릭터 매물: 한 줄 설명에 레벨 · ★ · 가장 높은 적성 하나, 적성 5종은 툴팁에 둔다(2026-10-03 결정).
// ■ 종류는 넷이다 — 자원(Item) · 장비 · 캐릭터 · 아티팩트(T-152). TID 범위가 겹치는 종류가 있어(장비·캐릭터 1001~)
//   TID만으로 무엇인지 알 수 없다 — 언제나 (종류, TID)로 찾는다.
public static class AuctionText
{
    // 판매자 이름이 비어 온 매물의 판매자 칸(이슈 #47 — 서버가 이름을 싣는다. 탈퇴 등으로 비면 이것).
    private const string UnknownSeller = "-";

    // 남은 시간을 경고색으로 칠하는 선 — 목업의 "6시간 안".
    private const double SoonHours = 6d;

    #region 종류별 조회

    // 매물 종류에 맞춰 이름을 찾는다 — 자원은 ItemTable, 장비는 EquipTable, 캐릭터는 CharacterTable, 아티팩트는 ArtifactTable.
    public static string GetName(EAuctionKind kind, int tid) => kind switch
    {
        EAuctionKind.Equip     => GameDataLoader.GetEquipName(tid),
        EAuctionKind.Character => GameDataLoader.GetCharacterName(tid),
        EAuctionKind.Artifact  => GameDataLoader.TryGetArtifact(tid, out ArtifactTableRow row) ? row.Name : $"?#{tid}",
        _                      => GameDataLoader.GetItemName(tid),
    };

    // 종류별 등급.
    public static GlobalRarity GetRarity(EAuctionKind kind, int tid) => kind switch
    {
        EAuctionKind.Equip     => GameDataLoader.GetEquipRarity(tid),
        EAuctionKind.Character => GameDataLoader.GetCharacterRarity(tid),
        EAuctionKind.Artifact  => GameDataLoader.TryGetArtifact(tid, out ArtifactTableRow row) ? row.GlobalRarity : GlobalRarity.None,
        _                      => GameDataLoader.GetItemRarity(tid),
    };

    // 종류별 기준가 — 각 테이블의 BasePrice. 모르면 0.
    public static int GetBasePrice(EAuctionKind kind, int tid) => kind switch
    {
        EAuctionKind.Equip     => GameDataLoader.TryGetEquip(tid, out EquipTableRow row) ? row.BasePrice : 0,
        EAuctionKind.Character => GameDataLoader.GetCharacterPrice(tid),
        EAuctionKind.Artifact  => GameDataLoader.TryGetArtifact(tid, out ArtifactTableRow artifact) ? artifact.BasePrice : 0,
        _                      => GameDataLoader.GetItemPrice(tid),
    };

    // 종류별 아이콘 — 자원·아티팩트는 수량, 장비는 능력치 칸까지, 캐릭터는 상반신.
    //   count          : 모서리 수량 (1이면 적지 않는다)
    //   enchantOptions : 장비 능력치 칸 — 모르면 null(칸 수만큼 빈 칸)
    public static ItemIconContent ToIcon(EAuctionKind kind, int tid, long count = 1L, IReadOnlyList<int>? enchantOptions = null) => kind switch
    {
        EAuctionKind.Equip     => ItemIconContent.ForEquip(tid, enchantOptions),
        EAuctionKind.Character => ItemIconContent.ForCharacter(tid),
        EAuctionKind.Artifact  => new ItemIconContent(GetRarity(kind, tid), GlyphOf(GetName(kind, tid)), count > 1L ? $"x{count:N0}" : "", null),
        _                      => ItemIconContent.ForItem(tid, count),
    };

    // 매물의 아이콘 칸.
    public static ItemIconContent ToIcon(AuctionListingInfo listing)
        => ToIcon(listing.Kind, listing.Tid, listing.Count, listing.EnchantOptions);

    // 종류 이름 — "자원" · "장비" · "캐릭터" · "아티팩트".
    public static string GetKindName(EAuctionKind kind) => kind switch
    {
        EAuctionKind.Equip     => "장비",
        EAuctionKind.Character => "캐릭터",
        EAuctionKind.Artifact  => "아티팩트",
        _                      => "자원",
    };

    // 아이템 분류 이름 — 산업 5종은 산업 이름, 나머지는 상자 · 소모품 · 기타 · 유물 (T-124 · T-150).
    public static string GetItemCategoryName(ItemType type) => type switch
    {
        ItemType.Farming    => IndustryLabel.Get(EIndustryType.Farming),
        ItemType.Excavation => IndustryLabel.Get(EIndustryType.Excavation),
        ItemType.Mining     => IndustryLabel.Get(EIndustryType.Mining),
        ItemType.Logging    => IndustryLabel.Get(EIndustryType.Logging),
        ItemType.Hunting    => IndustryLabel.Get(EIndustryType.Hunting),
        ItemType.Box        => "상자",
        ItemType.Consumable => "소모품",
        ItemType.Relic      => "유물",
        _                   => "기타",
    };

    // 이름의 첫 글자. 모르는 이름('?#12')이나 빈 이름은 '?'.
    private static string GlyphOf(string name)
        => string.IsNullOrEmpty(name) || name[0] == '?' ? "?" : name.Substring(0, 1);

    #endregion

    #region 한 줄 설명 · 시간

    // 이름 아래 한 줄 — 자원: 분류 / 장비: 부위 · 기본 능력치 · 능력치 칸 / 캐릭터: 레벨 · ★ · 적성 / 아티팩트: 효과.
    public static string FormatSub(AuctionListingInfo listing) => listing.Kind switch
    {
        EAuctionKind.Equip     => $"{EquipLabel.GetKindName((EquipKind)listing.Category)}{UIRichText.Dot}{EquipLabel.GetEffectText(listing.Tid)}{UIRichText.Dot}{FormatStatSummary(listing.Tid, listing.EnchantOptions)}",
        EAuctionKind.Character => FormatCharacterSummary(listing.Character),
        _                      => FormatSub(listing.Kind, listing.Tid),
    };

    // 개체 정보 없이 (종류, TID)만으로 쓰는 한 줄 — 시세 탭 머리 · 거래 기록.
    public static string FormatSub(EAuctionKind kind, int tid) => kind switch
    {
        EAuctionKind.Equip     => GameDataLoader.TryGetEquip(tid, out EquipTableRow row) ? $"{EquipLabel.GetKindName(row.EquipKind)}{UIRichText.Dot}{EquipLabel.GetEffectText(tid)}" : "장비",
        EAuctionKind.Character => "캐릭터",
        EAuctionKind.Artifact  => "아티팩트",
        _                      => $"{UIRichText.Label("자원")} {GetItemCategoryName(GameDataLoader.GetItemType(tid))}",
    };

    // "LV.12 · ★2 · 발굴 7" — 레벨 · ★(있을 때) · 가장 높은 적성 하나. 캐릭터 정보가 없으면 빈 문자열.
    public static string FormatCharacterSummary(CharacterInfo? character)
    {
        if (character == null)
        {
            return "";
        }

        string star = character.Star > 0 ? $"{UIRichText.Dot}{UIRichText.Paint(new string('★', character.Star), UIThemeRole.Highlight)}" : "";

        return $"{CharacterSlotSource.GetLevelLabel(character.Level)}{star}{UIRichText.Dot}{FormatTopAptitude(character)}";
    }

    // 가장 높은 적성 — "발굴 7". 같은 값이면 산업 순서(농사·발굴·…)가 앞인 것. 적성이 하나도 없으면 "적성 없음".
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

    // 만료까지 남은 시간 — "23시간 10분" · "15분" · "곧 만료".
    public static string FormatRemaining(long expiresAtUnixMs)
    {
        TimeSpan left = DateTimeOffset.FromUnixTimeMilliseconds(expiresAtUnixMs) - ServerClock.UtcNow;

        if (left.TotalHours >= 1d)
        {
            return $"{(int)left.TotalHours}시간 {left.Minutes}분";
        }

        if (left.TotalMinutes >= 1d)
        {
            return $"{(int)left.TotalMinutes}분";
        }

        return "곧 만료";
    }

    // 남은 시간 칸 — 6시간 안이면 경고색, 누가 사는 중이면 "구매 진행 중".
    public static string PaintRemaining(AuctionListingInfo listing)
    {
        if (listing.State == EAuctionListingState.Reserved)
        {
            return UIRichText.Paint("구매 진행 중", UIThemeRole.Negative);
        }

        TimeSpan left = DateTimeOffset.FromUnixTimeMilliseconds(listing.ExpiresAtUnixMs) - ServerClock.UtcNow;
        string   text = FormatRemaining(listing.ExpiresAtUnixMs);

        return left.TotalHours < SoonHours ? UIRichText.Paint(text, UIThemeRole.Negative) : text;
    }

    // 기록 시각 — "10/14 18:40" (내 PC 시간대).
    public static string FormatDateTime(long unixMs)
    {
        DateTime local = DateTimeOffset.FromUnixTimeMilliseconds(unixMs).LocalDateTime;

        return $"{local.Month}/{local.Day} {local:HH:mm}";
    }

    // 판매자 이름. 비어 오면 '-'.
    public static string GetSellerName(AuctionListingInfo listing)
        => string.IsNullOrEmpty(listing.SellerName) ? UnknownSeller : listing.SellerName;

    // 총액 칸 — 수량이 1이면 단가와 같아 '—'로 둔다(목업).
    private static string FormatTotal(AuctionListingInfo listing)
        => listing.Count > 1 ? UIRichText.Gold(listing.TotalPrice) : UIRichText.Paint("—", UIThemeRole.TextDisabled);

    #endregion

    #region 표 한 줄

    // 검색 결과 한 줄 — 열: 수량 · 단가 · 총액 · 남은 시간 · 판매자 / 버튼: [시세] [구매].
    //   isMine : 내가 올린 매물 — 이름 뒤 꼬리표 · 판매자 '나' · [구매] 대신 잠긴 [내 매물]
    //   canBuy : [구매]를 누를 수 있나 (부르는 쪽이 골드·대기·상태를 본다)
    public static AuctionTableRowContent ToSearchRow(AuctionListingInfo listing, bool isMine, bool canBuy)
    {
        string title = GetName(listing.Kind, listing.Tid);

        return new AuctionTableRowContent
        {
            Key        = listing.ListingId,
            Icon       = ToIcon(listing),
            Title      = isMine ? $"{title} {UIRichText.Small(UIRichText.Paint("내 매물", UIThemeRole.Highlight))}" : title,
            TitleColor = RarityPalette.Get((GlobalRarity)listing.Rarity),
            Sub        = FormatSub(listing),
            Cells      = new[]
            {
                $"{listing.Count:N0}",
                UIRichText.Gold(listing.UnitPrice),
                FormatTotal(listing),
                PaintRemaining(listing),
                isMine ? UIRichText.Paint("나", UIThemeRole.Highlight) : UIRichText.Label(GetSellerName(listing)),
            },
            SecondaryLabel = "시세",
            CanSecondary   = true,
            PrimaryLabel   = isMine ? "내 매물" : "구매",
            CanPrimary     = canBuy && !isMine,
            Highlight      = isMine,
            Tooltip        = () => BuildListingTooltip(listing, isMine),
        };
    }

    // 판매 중 한 줄 — 열: 수량 · 단가 · 총액 · 팔리면 받는 금액 · 남은 시간 · 상태 / 버튼: [취소].
    public static AuctionTableRowContent ToSellRow(AuctionListingInfo listing, bool canCancel)
    {
        bool isReserved = listing.State == EAuctionListingState.Reserved;

        return new AuctionTableRowContent
        {
            Key        = listing.ListingId,
            Icon       = ToIcon(listing),
            Title      = GetName(listing.Kind, listing.Tid),
            TitleColor = RarityPalette.Get((GlobalRarity)listing.Rarity),
            Sub        = FormatSub(listing),
            Cells      = new[]
            {
                $"{listing.Count:N0}",
                UIRichText.Gold(listing.UnitPrice),
                FormatTotal(listing),
                UIRichText.Paint($"{AuctionModel.SaleNet(listing.TotalPrice):N0} G", UIThemeRole.Positive),
                isReserved ? UIRichText.Label("—") : PaintRemaining(listing),
                isReserved ? UIRichText.Paint("구매 진행 중", UIThemeRole.Negative) : UIRichText.Label("판매 중"),
            },
            PrimaryLabel = "취소",
            CanPrimary   = canCancel && !isReserved,
            Tooltip      = () => BuildListingTooltip(listing, isMine: true),
        };
    }

    #endregion

    #region 툴팁

    // 매물 한 건의 툴팁 — 등급 · 종류 · 수량/기본 능력치/레벨 · 가격 · 남은 시간 · 판매자 · 가격 기준 · 능력치 칸/적성.
    public static TooltipContent BuildListingTooltip(AuctionListingInfo listing, bool isMine)
    {
        var content = new TooltipContent(GetName(listing.Kind, listing.Tid));

        AddRarityRow(content, (GlobalRarity)listing.Rarity);

        if (listing.Kind == EAuctionKind.Equip)
        {
            content.Row("종류", EquipLabel.GetKindName((EquipKind)listing.Category))
                   .Row("기본 능력치", EquipLabel.GetEffectText(listing.Tid));
        }
        else if (listing.Kind == EAuctionKind.Character && listing.Character != null)
        {
            content.Row("레벨", CharacterSlotSource.GetLevelLabel(listing.Character.Level))
                   .Row("응축", listing.Character.Star > 0 ? new string('★', listing.Character.Star) : "없음");
        }
        else
        {
            content.Row("종류", listing.Kind == EAuctionKind.Item ? $"자원 · {GetItemCategoryName(GameDataLoader.GetItemType(listing.Tid))}" : GetKindName(listing.Kind));
        }

        content.Row("수량", $"{listing.Count:N0} 개")
               .Row("단가", $"{listing.UnitPrice:N0} 골드");

        if (listing.Count > 1)
        {
            content.Row("총액", $"{listing.TotalPrice:N0} 골드");
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

    // 가격 기준 묶음 — 즉시 판매가(정해진 값) · 경매 등록가(그 값 이상). 두 판매 방식을 한눈에 비교하게 둔다.
    private static void AddPriceRules(TooltipContent content, int basePrice)
    {
        content.Header("가격 기준")
               .Row("즉시 판매가", $"{AuctionModel.InstantSellPrice(basePrice):N0} 골드", "인벤토리 [판매]", null)
               .Row("경매 등록가", AuctionModel.FormatBand(basePrice), "개당 단가", null);
    }

    #endregion

    #region 이름 → TID

    // 이름에 'query'가 든 TID를 모은다. 비었으면 null — 이름 조건 없음이다.
    //
    // 서버는 이름을 모른다 — 클라가 TID 목록으로 바꿔 보낸다. 한 요청의 TID 상한을 넘으면 앞에서 자른다.
    //   kind : None이면 네 종류를 모두 본다(검색 탭 '전체') — ⚠️ TID 범위가 겹쳐 다른 종류가 섞여 올 수 있다.
    //          받은 쪽이 'MatchesName'으로 한 번 더 거른다.
    public static List<int>? FindTids(EAuctionKind kind, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        string trimmed = query.Trim();
        var    tids    = new List<int>();

        if (kind == EAuctionKind.None || kind == EAuctionKind.Item)
        {
            foreach (ItemTableRow row in GameTable.ItemTable.All)
            {
                if (Contains(row.Name, trimmed)) tids.Add(row.ItemTID);
            }
        }

        if (kind == EAuctionKind.None || kind == EAuctionKind.Equip)
        {
            foreach (EquipTableRow row in GameTable.EquipTable.All)
            {
                if (Contains(row.Name, trimmed)) tids.Add(row.EquipTID);
            }
        }

        if (kind == EAuctionKind.None || kind == EAuctionKind.Character)
        {
            foreach (CharacterTableRow row in GameTable.CharacterTable.All)
            {
                if (Contains(row.Name, trimmed)) tids.Add(row.CharacterTID);
            }
        }

        if (kind == EAuctionKind.None || kind == EAuctionKind.Artifact)
        {
            foreach (ArtifactTableRow row in GameTable.ArtifactTable.All)
            {
                if (Contains(row.Name, trimmed)) tids.Add(row.ArtifactTID);
            }
        }

        return Truncate(tids, trimmed);
    }

    // 이 매물의 이름에 'query'가 드는가 — '전체' 검색에서 TID가 겹쳐 섞여 온 다른 종류를 거른다. 빈 질의는 참.
    public static bool MatchesName(AuctionListingInfo listing, string query)
        => string.IsNullOrWhiteSpace(query) || Contains(GetName(listing.Kind, listing.Tid), query.Trim());

    // 이 종류·TID의 이름에 'query'가 드는가 — 판매·완료 탭의 이름 거르기. 빈 질의는 참.
    public static bool MatchesName(EAuctionKind kind, int tid, string query)
        => string.IsNullOrWhiteSpace(query) || Contains(GetName(kind, tid), query.Trim());

    // 이름으로 하나를 찾는다 — 시세 탭. 이름이 똑같은 것을 먼저, 없으면 이름에 든 첫 것(자원 → 장비 → 캐릭터 → 아티팩트 순).
    public static bool TryFindByName(string query, out EAuctionKind kind, out int tid)
    {
        kind = EAuctionKind.None;
        tid  = 0;

        if (string.IsNullOrWhiteSpace(query))
        {
            return false;
        }

        string trimmed = query.Trim();

        return TryFind(trimmed, exact: true, out kind, out tid) || TryFind(trimmed, exact: false, out kind, out tid);
    }

    private static bool TryFind(string query, bool exact, out EAuctionKind kind, out int tid)
    {
        bool Match(string name) => exact ? name == query : Contains(name, query);

        foreach (ItemTableRow row in GameTable.ItemTable.All)
        {
            if (Match(row.Name)) { kind = EAuctionKind.Item; tid = row.ItemTID; return true; }
        }

        foreach (EquipTableRow row in GameTable.EquipTable.All)
        {
            if (Match(row.Name)) { kind = EAuctionKind.Equip; tid = row.EquipTID; return true; }
        }

        foreach (CharacterTableRow row in GameTable.CharacterTable.All)
        {
            if (Match(row.Name)) { kind = EAuctionKind.Character; tid = row.CharacterTID; return true; }
        }

        foreach (ArtifactTableRow row in GameTable.ArtifactTable.All)
        {
            if (Match(row.Name)) { kind = EAuctionKind.Artifact; tid = row.ArtifactTID; return true; }
        }

        kind = EAuctionKind.None;
        tid  = 0;

        return false;
    }

    private static bool Contains(string name, string query) => name.Contains(query, StringComparison.OrdinalIgnoreCase);

    // TID가 상한을 넘으면 앞에서 자르고 알린다 (FindTids에서 호출).
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

    #endregion

    #region 검색 후보

    // 게임 데이터 전부에서 이름이 맞는 것을 후보로 모은다 — 이름이 'query'로 시작하는 것을 먼저, 그다음 이름에 든 것 (검색·시세 탭 검색창).
    //   kind : None이면 네 종류 모두(자원 → 장비 → 캐릭터 → 아티팩트 순)
    public static void CollectCatalogSuggestions(EAuctionKind kind, string query, List<AuctionSuggestion> into, int max)
    {
        into.Clear();

        if (string.IsNullOrWhiteSpace(query))
        {
            return;
        }

        string trimmed = query.Trim();

        AddCatalogMatches(kind, trimmed, startsWith: true,  into, max);
        AddCatalogMatches(kind, trimmed, startsWith: false, into, max);
    }

    // 받은 목록(내 매물 · 거래 기록)의 한 줄을 후보로 넣는다 — 이름이 맞고 같은 (종류, TID)가 아직 없을 때만.
    public static void AddListSuggestion(List<AuctionSuggestion> into, EAuctionKind kind, int tid, string query, int max)
    {
        if (into.Count >= max || string.IsNullOrWhiteSpace(query) || !MatchesName(kind, tid, query))
        {
            return;
        }

        foreach (AuctionSuggestion suggestion in into)
        {
            if (suggestion.Kind == kind && suggestion.Tid == tid)
            {
                return;
            }
        }

        into.Add(ToSuggestion(kind, tid));
    }

    // (종류, TID)를 후보 한 줄로 — 이름은 등급 색, 오른쪽은 종류 · 분류. 'sub'를 주면 그것을 쓴다.
    public static AuctionSuggestion ToSuggestion(EAuctionKind kind, int tid, long key = 0L, string? sub = null) => new AuctionSuggestion
    {
        Kind      = kind,
        Tid       = tid,
        Key       = key,
        Name      = GetName(kind, tid),
        NameColor = RarityPalette.Get(GetRarity(kind, tid)),
        Sub       = sub ?? FormatSuggestionSub(kind, tid),
    };

    // 후보 오른쪽 글자 — "자원 · 농사" · "장비 · 무기" · "캐릭터" · "아티팩트".
    private static string FormatSuggestionSub(EAuctionKind kind, int tid) => kind switch
    {
        EAuctionKind.Equip     => GameDataLoader.TryGetEquip(tid, out EquipTableRow row) ? $"장비{UIRichText.Dot}{EquipLabel.GetKindName(row.EquipKind)}" : "장비",
        EAuctionKind.Character => "캐릭터",
        EAuctionKind.Artifact  => "아티팩트",
        _                      => $"자원{UIRichText.Dot}{GetItemCategoryName(GameDataLoader.GetItemType(tid))}",
    };

    // 한 단계의 후보를 모은다 — 'startsWith'면 이름이 그 글자로 시작하는 것, 아니면 이름 가운데에 든 것.
    private static void AddCatalogMatches(EAuctionKind kind, string query, bool startsWith, List<AuctionSuggestion> into, int max)
    {
        foreach ((EAuctionKind rowKind, int tid, string name) in Catalog(kind))
        {
            if (into.Count >= max)
            {
                return;
            }

            bool starts = name.StartsWith(query, StringComparison.OrdinalIgnoreCase);

            if (startsWith ? starts : !starts && Contains(name, query))
            {
                into.Add(ToSuggestion(rowKind, tid));
            }
        }
    }

    // 게임 데이터의 (종류, TID, 이름) 전부 — 자원 → 장비 → 캐릭터 → 아티팩트 순.
    private static IEnumerable<(EAuctionKind kind, int tid, string name)> Catalog(EAuctionKind kind)
    {
        if (kind == EAuctionKind.None || kind == EAuctionKind.Item)
        {
            foreach (ItemTableRow row in GameTable.ItemTable.All) yield return (EAuctionKind.Item, row.ItemTID, row.Name);
        }

        if (kind == EAuctionKind.None || kind == EAuctionKind.Equip)
        {
            foreach (EquipTableRow row in GameTable.EquipTable.All) yield return (EAuctionKind.Equip, row.EquipTID, row.Name);
        }

        if (kind == EAuctionKind.None || kind == EAuctionKind.Character)
        {
            foreach (CharacterTableRow row in GameTable.CharacterTable.All) yield return (EAuctionKind.Character, row.CharacterTID, row.Name);
        }

        if (kind == EAuctionKind.None || kind == EAuctionKind.Artifact)
        {
            foreach (ArtifactTableRow row in GameTable.ArtifactTable.All) yield return (EAuctionKind.Artifact, row.ArtifactTID, row.Name);
        }
    }

    #endregion
}
