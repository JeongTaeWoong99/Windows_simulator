using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>더미 매물 한 줄 — 원장에 바로 쓴다.</summary>
public sealed record DummyListing(AuctionItemSnapshot Item, long UnitPrice, DateTime ExpiresAt);

/// <summary>더미 매물을 뽑을 재료. 빈 종류는 건너뛴다 — 종류 하나만 올리려면 그 목록만 채운다.</summary>
public sealed record DummyPool
{
    public IReadOnlyList<ItemTableRow>      Items      { get; init; } = Array.Empty<ItemTableRow>();
    public IReadOnlyList<EquipTableRow>     Equips     { get; init; } = Array.Empty<EquipTableRow>();
    public IReadOnlyList<CharacterTableRow> Characters { get; init; } = Array.Empty<CharacterTableRow>();
    public IReadOnlyList<ArtifactTableRow>  Artifacts  { get; init; } = Array.Empty<ArtifactTableRow>();

    /// <summary>장비 인챈트 — 칸 수·옵션 풀. 비어 있으면 인챈트 없이 올린다.</summary>
    public EnchantCatalog Enchants { get; init; } = new();

    /// <summary>캐릭터 응축 누적의 상한과 ★.</summary>
    public CharacterStarCatalog Stars { get; init; } = new();

    public int MaxLevel { get; init; } = 1;
}

/// <summary>
/// 더미 판매자 매물(치트 <c>SeedAuctionListings</c>, T-168 · T-204). 검색·부하 테스트에 쓸 매물을 한 번에 올린다.
/// 판매자가 실제 유저가 아니라 팔려도 대금이 아무에게도 가지 않는다 → Server/docs/치트.md
/// </summary>
public static class AuctionDummy
{
    // 실제 계정 번호(1부터 늘어난다)와 겹치지 않는 값. 경매장은 판매자 0 이하를 받지 않는다.
    public const long SellerId = 9_000_000_000_000_000_000;

    public const string SellerName = "더미상점";

    /// <summary>한 번에 올리는 상한 — 0 하나 더 친 오타가 원장을 수십만 줄로 불리지 않게.</summary>
    public const int MaxCount = 5_000;

    // 종류는 자원 → 장비 → 캐릭터 → 아티팩트 순으로 돌아가고, 종류 안에서는 행을 돌아가며 쓴다.
    // 단가는 즉시 판매가의 1~4.99배, 만료는 등록 기간의 1/24 ~ 전부로 흩어 정렬이 보이게 한다.
    // 장비 인챈트 등급·옵션, 캐릭터 레벨·응축 누적은 random으로 뽑는다. 개체 PK는 원장에 쓸 때 정해진다.
    public static List<DummyListing> Build(DummyPool pool, int count, DateTime now, Random? random = null)
    {
        var rng   = random ?? Random.Shared;
        var kinds = KindsOf(pool);
        if (kinds.Count == 0)
        {
            throw new ArgumentException("더미 매물을 만들 행이 없다", nameof(pool));
        }

        var listings = new List<DummyListing>(count);
        for (var i = 0; i < count; i++)
        {
            var nth = i / kinds.Count;
            var (item, basePrice) = kinds[i % kinds.Count] switch
            {
                EAuctionKind.Equip     => EquipOf(pool, pool.Equips[nth % pool.Equips.Count], rng),
                EAuctionKind.Character => CharacterOf(pool, pool.Characters[nth % pool.Characters.Count], rng),
                EAuctionKind.Artifact  => ArtifactOf(pool.Artifacts[nth % pool.Artifacts.Count], i),
                _                      => ItemOf(pool.Items[nth % pool.Items.Count], i),
            };

            var unitPrice = Math.Min(AuctionRules.MinUnitPrice(basePrice) * (100 + i * 37 % 400) / 100, AuctionRules.MaxUnitPrice);
            var expiresAt = now + AuctionRules.ListingDuration * (1 + i % 24) / 24;
            listings.Add(new DummyListing(item with { SellerName = SellerName }, unitPrice, expiresAt));
        }

        return listings;
    }

    private static List<EAuctionKind> KindsOf(DummyPool pool)
    {
        var kinds = new List<EAuctionKind>();
        if (pool.Items.Count > 0)
        {
            kinds.Add(EAuctionKind.Item);
        }
        if (pool.Equips.Count > 0)
        {
            kinds.Add(EAuctionKind.Equip);
        }
        if (pool.Characters.Count > 0)
        {
            kinds.Add(EAuctionKind.Character);
        }
        if (pool.Artifacts.Count > 0)
        {
            kinds.Add(EAuctionKind.Artifact);
        }

        return kinds;
    }

    private static int StackCount(int i) => Math.Min(1 + i * 7 % 20, AuctionRules.MaxListingCount);

    private static (AuctionItemSnapshot, int) ItemOf(ItemTableRow row, int i)
    {
        var item = new AuctionItemSnapshot
        {
            Kind = EAuctionKind.Item, Tid = row.ItemTID, Category = (int)row.ItemType, Rarity = (int)row.GlobalRarity, Count = StackCount(i),
        };
        return (item, row.BasePrice);
    }

    // 분류는 두지 않는다(0) — 실제 등록(DescribeArtifact)과 같다.
    private static (AuctionItemSnapshot, int) ArtifactOf(ArtifactTableRow row, int i)
    {
        var item = new AuctionItemSnapshot
        {
            Kind = EAuctionKind.Artifact, Tid = row.ArtifactTID, Rarity = (int)row.GlobalRarity, Count = StackCount(i),
        };
        return (item, row.BasePrice);
    }

    private static (AuctionItemSnapshot, int) EquipOf(DummyPool pool, EquipTableRow row, Random rng)
    {
        var equip     = new Equip(0, row, 0);
        var slotCount = pool.Enchants.SlotCountOf(row.GlobalRarity);
        var grade     = (GlobalRarity)rng.Next((int)GlobalRarity.Common, (int)GlobalRarity.Mythic + 1);
        if (slotCount > 0 && pool.Enchants.HasPool(grade))
        {
            equip.SetEnchant(grade, pool.Enchants.RollOptions(grade, slotCount, rng));
        }

        var item = new AuctionItemSnapshot
        {
            Kind         = EAuctionKind.Equip,
            Tid          = equip.Tid,
            Category     = (int)equip.Kind,
            Rarity       = (int)row.GlobalRarity,
            Count        = 1,
            EnchantGrade = (int)equip.EnchantGrade,
            Options      = equip.EnchantOptionTids.ToList(),
            Attrs        = AuctionAttrs.Of(equip),
        };
        return (item, row.BasePrice);
    }

    // 경험치는 0 — 레벨이 오른 직후 상태다. 적성은 TID별 고정이라 테이블 값이 그대로 실린다.
    private static (AuctionItemSnapshot, int) CharacterOf(DummyPool pool, CharacterTableRow row, Random rng)
    {
        var character = new Character(0, row, rng.Next(1, pool.MaxLevel + 1), 0, rng.Next(0, pool.Stars.MaxCount + 1));

        var item = new AuctionItemSnapshot
        {
            Kind      = EAuctionKind.Character,
            Tid       = character.Tid,
            Rarity    = (int)row.GlobalRarity,
            Count     = 1,
            Character = new MailCharacter(0, character.Tid, character.Level, character.Exp, character.CondenseCount),
            Attrs     = AuctionAttrs.Of(character, pool.Stars.StarAt(character.CondenseCount)),
        };
        return (item, row.BasePrice);
    }
}
