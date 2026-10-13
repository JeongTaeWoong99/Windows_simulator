using MikaProtocol;

namespace WSGameServer;

/// <summary>거래 기록 — 경매장 완료 탭 (T-156). 기록은 정산·반환 트랜잭션이 남기고, 여기서는 읽기만 한다.</summary>
public partial class User
{
    // 목록·검색과 같은 빈도 제한 — 탭을 바꿔 가며 긁는 매크로를 한 통으로 막는다.
    public void TryGetAuctionHistory(C_AuctionHistoryRequest req, DateTime now)
    {
        if (req.PageSize < 0 || req.PageSize > AuctionHistoryDb.MaxPageSize || req.Days < 0)
        {
            Send(new S_AuctionHistoryResponse { Result = EResultCode.AuctionInvalidRequest });
            return;
        }

        if (!TakeSearchToken(now))
        {
            Send(new S_AuctionHistoryResponse { Result = EResultCode.AuctionTooManyRequests });
            return;
        }

        PostDBTask(new AuctionHistoryRepository(this, req.Type, req.Days, req.CursorHistoryId, req.PageSize, req.MarkSeen, now));
    }

    public void OnAuctionHistoryLoaded(AuctionHistoryPage page)
    {
        Send(new S_AuctionHistoryResponse
        {
            Result      = EResultCode.Ok,
            HasMore     = page.HasMore,
            UnseenCount = page.UnseenCount,
            Entries     = page.Entries.Select(r => new AuctionHistoryInfo
            {
                HistoryId    = r.history_id,
                Type         = (EAuctionHistoryType)r.type,
                Kind         = (EAuctionKind)r.kind,
                Tid          = r.tid,
                Count        = r.count,
                UnitPrice    = r.unit_price,
                Amount       = r.amount,
                Fee          = r.fee,
                EnchantGrade = r.enchant_grade,
                AtUnixMs     = new DateTimeOffset(MailDb.FromDb(r.created_at)).ToUnixTimeMilliseconds(),
            }).ToList(),
        });
    }
}
