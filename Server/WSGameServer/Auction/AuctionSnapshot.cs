using System.Text.Json;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 등록 순간의 아이템 정보. 거래 원장(<c>t_auction_trade.snapshot</c>)에 남아 우편 첨부·경매장 등록에 쓰인다.
/// 장비는 등록 순간 메모리에서 빠지므로 인챈트를 여기서만 알 수 있다.
/// </summary>
public sealed record AuctionItemSnapshot
{
    public EAuctionKind Kind         { get; init; }
    public int          Tid          { get; init; }
    public int          Category     { get; init; }
    public int          Rarity       { get; init; }
    public int          Count        { get; init; }
    public long         EquipId      { get; init; }
    public int          EnchantGrade { get; init; }
    public List<int>    Options      { get; init; } = new();

    public string ToJson() => JsonSerializer.Serialize(this);

    public static AuctionItemSnapshot FromJson(string json) => JsonSerializer.Deserialize<AuctionItemSnapshot>(json)!;

    /// <summary>이 물건을 우편으로 보낼 때의 첨부. 자원은 수량 그대로, 장비는 잠긴 개체 그대로다.</summary>
    public MailAttachment ToAttachment(long gold)
    {
        if (Kind == EAuctionKind.Equip)
        {
            return new MailAttachment(gold, 0, new(), new(), new(), new List<MailEquip> { new(EquipId, Tid, EnchantGrade, Options.ToList()) });
        }

        return new MailAttachment(gold, 0, new List<(int, int)> { (Tid, Count) }, new(), new());
    }
}

/// <summary>반환 사유. 등록비 환급 여부와 우편 템플릿이 갈린다.</summary>
public enum AuctionReturnReason
{
    Cancelled = 1,  // 판매자 취소 — 등록비 환급 없음
    Expired   = 2,  // 기간 만료 — 환급
    Rejected  = 3,  // 경매장이 등록을 거부 — 환급
    Lost      = 4,  // 대사에서 경매장에 없음 — 환급
}

/// <summary>경매 우편 템플릿 TID. <c>MailTemplateTable</c>에 있어야 한다 — 없으면 기동이 멈춘다(MailCatalog).</summary>
public static class AuctionMail
{
    public const int PurchasedTemplateTid = 3;
    public const int SoldTemplateTid      = 4;
    public const int CancelledTemplateTid = 5;
    public const int ExpiredTemplateTid   = 6;
    public const int FailedTemplateTid    = 7;

    public static readonly int[] All = { PurchasedTemplateTid, SoldTemplateTid, CancelledTemplateTid, ExpiredTemplateTid, FailedTemplateTid };

    public static int ReturnTemplate(AuctionReturnReason reason)
    {
        switch (reason)
        {
            case AuctionReturnReason.Cancelled:
                return CancelledTemplateTid;
            case AuctionReturnReason.Expired:
                return ExpiredTemplateTid;
            default:
                return FailedTemplateTid;
        }
    }

    /// <summary>취소만 등록비를 돌려주지 않는다 — 방치하다 만료돼도 손해가 없어야 한다(P3).</summary>
    public static bool RefundsFee(AuctionReturnReason reason) => reason != AuctionReturnReason.Cancelled;
}
