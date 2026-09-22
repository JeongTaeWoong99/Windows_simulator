using GameData;
using MikaProtocol;

namespace WSGameServer;

public partial class User
{
    /// <summary>열린 해금(<c>UnlockTID</c>). 영구라 지워지는 일이 없다. 0은 넣지 않는다 — 항상 열려 있다.</summary>
    private readonly HashSet<int> _unlocks = new();

    /// <summary>DB에서 읽은 열린 해금을 적재한다(로그인 시 1회). 작업슬롯 적재보다 먼저다.</summary>
    public void LoadUnlocks(IReadOnlyList<UserUnlockRow> rows)
    {
        _unlocks.Clear();

        foreach (var r in rows)
        {
            _unlocks.Add(r.unlock_tid);
        }
    }

    /// <summary>콘텐츠가 묻는 유일한 질문. <c>0</c>은 "항상 열림"이라 기록 없이 true다.</summary>
    public bool IsUnlocked(int unlockTid) => unlockTid == 0 || _unlocks.Contains(unlockTid);

    /// <summary>열린 해금 전체를 보낸다(로그인 직후). <b>슬롯 스냅샷보다 먼저</b> 나가야 클라가 잠긴 칸을 그릴 수 있다.</summary>
    public void SendUnlockList()
    {
        Send(new S_UnlockListResponse { UnlockTIDs = _unlocks.OrderBy(t => t).ToList() });
    }

    /// <summary>
    /// 유저의 해금 요청. 판정 순서는 해금 기획 2.1 — <b>차감이 맨 뒤다.</b>
    /// 무료 조건(선행·레벨·재화 선택)을 먼저 거르고 전부 통과했을 때만 골드를 뺀다. 되돌리는 코드가 없어야 한다.
    /// </summary>
    public void TryUnlock(int unlockTid, CurrencyType currency, DateTime now)
    {
        if (!_unlockCatalog.TryGetUnlock(unlockTid, out var row))
        {
            Reject(EResultCode.InvalidUnlockTID, "없는 TID");
            return;
        }

        // 특성 노드는 포인트를 내야 열린다. 여기로 오면 골드 컬럼만 보고 공짜로 열리므로 막는다.
        if (_traitCatalog.IsTraitUnlock(unlockTid))
        {
            Reject(EResultCode.TraitOnlyUnlock, "특성 노드 — C_UserTraitLearnRequest로 연다");
            return;
        }

        var (code, reason) = CheckUnlockConditions(unlockTid, row);
        if (code != EResultCode.Ok)
        {
            Reject(code, reason);
            return;
        }

        // 지불 컬럼은 아직 Gold 하나다. Dia 컬럼이 생기면 여기에 분기를 더한다 — 지불 컬럼끼리는 OR(해금 #16).
        if (row.Gold > 0)
        {
            if (currency != CurrencyType.Gold)
            {
                Reject(EResultCode.UnlockLocked, $"이 해금에 없는 재화 {currency}");
                return;
            }

            if (!TrySpendGold(row.Gold))
            {
                Reject(EResultCode.NotEnoughCurrency, $"골드 {Gold} < {row.Gold}");
                return;
            }
        }

        ApplyUnlock(unlockTid, now);
        return;

        void Reject(EResultCode code, string reason)
        {
            ServerLog.Warn("해금", $"거절 — {reason}. Uid={Uid} UnlockTID={unlockTid} Currency={currency}");
            Send(new S_UnlockResponse { Result = code, UnlockTID = unlockTid });
        }
    }

    /// <summary>
    /// 차감 없는 조건(이미 열림 · 선행 · 계정 레벨)을 본다. 해금 요청과 특성 찍기가 같은 판정을 쓴다 —
    /// 조건은 <c>UnlockTable</c> 한 곳에 있고 컬럼끼리는 AND다(해금 2.1).
    /// </summary>
    private (EResultCode Code, string Reason) CheckUnlockConditions(int unlockTid, UnlockTableRow row)
    {
        if (IsUnlocked(unlockTid))
        {
            return (EResultCode.AlreadyUnlocked, "이미 열림");
        }

        foreach (var required in row.RequiredUnlockTIDs)
        {
            if (!IsUnlocked(required))
            {
                return (EResultCode.UnlockLocked, $"선행 {required} 미충족");
            }
        }

        if (AccountLevel < row.AccountLevel)
        {
            return (EResultCode.UnlockLocked, $"계정 레벨 {AccountLevel} < {row.AccountLevel}");
        }

        return (EResultCode.Ok, "");
    }

    /// <summary>
    /// 서버가 직접 연다 — 퀘스트 보상·튜토리얼·치트용(해금 2.4). <b>조건 검사·차감이 없고</b> 기록·통지·콘텐츠 후속은 유저 행동과 같다.
    /// </summary>
    /// <returns>새로 열었으면 true. 없는 TID거나 이미 열려 있으면 아무것도 하지 않고 false.</returns>
    public bool GrantUnlock(int unlockTid, DateTime now)
    {
        if (!_unlockCatalog.TryGetUnlock(unlockTid, out _))
        {
            ServerLog.Warn("해금", $"지급 거절 — 없는 TID. Uid={Uid} UnlockTID={unlockTid}");
            return false;
        }

        if (IsUnlocked(unlockTid))
        {
            return false;
        }

        ApplyUnlock(unlockTid, now);
        return true;
    }

    // 기록 → 저장 → 응답 → 콘텐츠 후속. 해금 시스템은 "열렸다"까지만 알고, 열린 뒤의 변화는 콘텐츠가 자기 패킷으로 알린다.
    private void ApplyUnlock(int unlockTid, DateTime now)
    {
        _unlocks.Add(unlockTid);
        PostDBTask(new SaveUnlockRepository(this, unlockTid));

        ServerLog.Info("해금", $"열림 Uid={Uid} UnlockTID={unlockTid}");
        Send(new S_UnlockResponse { Result = EResultCode.Ok, UnlockTID = unlockTid });

        OnWorkSlotUnlocked(unlockTid, now);
        OnTraitUnlocked(unlockTid, now);
    }
}
