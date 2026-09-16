using GameData;
using MikaProtocol;

namespace WSGameServer;

public partial class User
{
    /// <summary>치트를 쓸 수 있는 최소 admin_level. 0은 일반 유저다.</summary>
    public const int CheatAdminLevel = 1;

    /// <summary>한 번에 지급할 수 있는 캐릭터 장수 상한. 10연차와 같다.</summary>
    public const int CheatMaxCharacterCount = 10;

    /// <summary>
    /// 치트 명령을 실행하고 <c>S_CheatResponse</c>로 결과를 돌려준다.
    /// 게임과 같은 지급 함수를 부르므로 결과는 기존 동기화 패킷으로도 나간다 (<c>Server/docs/치트.md</c>).
    /// </summary>
    public void ExecuteCheat(C_CheatRequest req, DateTime now)
    {
        if (AdminLevel < CheatAdminLevel)
        {
            Reply(EResultCode.NoPermission, "권한 없음");
            return;
        }

        var (result, message) = req.Command switch
        {
            ECheatCommand.GiveGold         => CheatGiveCurrency(req.Arg1, CurrencyType.Gold),
            ECheatCommand.GiveDia          => CheatGiveCurrency(req.Arg1, CurrencyType.Dia),
            ECheatCommand.GiveItem         => CheatGiveItem(req.Arg1, req.Arg2),
            ECheatCommand.GiveCharacter    => CheatGiveCharacter(req.Arg1, req.Arg2),
            ECheatCommand.GiveCharacterExp => CheatGiveCharacterExp(req.Arg1, req.Arg2),
            ECheatCommand.Settle           => CheatSettle(now),
            ECheatCommand.Unlock           => CheatUnlock(req.Arg1, now),
            _                              => (EResultCode.InvalidCheatCommand, "정의되지 않은 명령"),
        };

        Reply(result, message);
        return;

        void Reply(EResultCode code, string text)
        {
            // 성공·실패 전부 남긴다 — 운영에서 누가 무엇을 했는지가 이 한 줄뿐이다.
            ServerLog.Warn("치트", $"Uid={Uid} {req.Command}({req.Arg1}, {req.Arg2}) → {code} {text}");
            Send(new S_CheatResponse { Result = code, Command = req.Command, Message = text });
        }
    }

    private (EResultCode, string) CheatGiveCurrency(long amount, CurrencyType currency)
    {
        if (amount == 0)
        {
            return (EResultCode.InvalidCheatArgs, "금액이 0");
        }

        if (amount > 0)
        {
            var balance = currency == CurrencyType.Gold ? GainGold(amount) : GainDia(amount);
            return (EResultCode.Ok, $"{currency} +{amount} → {balance}");
        }

        var spent = currency == CurrencyType.Gold ? TrySpendGold(-amount) : TrySpendDia(-amount);
        if (!spent)
        {
            return (EResultCode.NotEnoughCurrency, $"{currency} 잔액 부족");
        }

        return (EResultCode.Ok, $"{currency} {amount}");
    }

    private (EResultCode, string) CheatGiveItem(long itemTid, long count)
    {
        if (count <= 0 || count > int.MaxValue || itemTid <= 0 || itemTid > int.MaxValue)
        {
            return (EResultCode.InvalidCheatArgs, "아이템 TID·개수 범위 밖");
        }

        if (!GameTable.ItemTable.TryGet((int)itemTid, out _))
        {
            return (EResultCode.InvalidCheatArgs, $"ItemTable에 없는 TID {itemTid}");
        }

        var change = GainItem((int)itemTid, (int)count);
        return (EResultCode.Ok, $"아이템 {itemTid} +{count} → {change.Count}");
    }

    private (EResultCode, string) CheatGiveCharacter(long characterTid, long count)
    {
        if (count <= 0 || count > CheatMaxCharacterCount || characterTid <= 0 || characterTid > int.MaxValue)
        {
            return (EResultCode.InvalidCheatArgs, $"캐릭터 TID·장수 범위 밖 (1~{CheatMaxCharacterCount})");
        }

        if (!GameTable.CharacterTable.TryGet((int)characterTid, out _))
        {
            return (EResultCode.InvalidCheatArgs, $"CharacterTable에 없는 TID {characterTid}");
        }

        // 가챠와 같은 지급 경로 — 개체 PK 발급 후 목록 재전송까지 동일하다.
        GrantGachaCharacters(Enumerable.Repeat((int)characterTid, (int)count).ToList());
        return (EResultCode.Ok, $"캐릭터 {characterTid} × {count} 지급 요청");
    }

    private (EResultCode, string) CheatGiveCharacterExp(long characterId, long amount)
    {
        if (amount <= 0 || amount > int.MaxValue)
        {
            return (EResultCode.InvalidCheatArgs, "경험치 범위 밖");
        }

        if (!TryGetCharacter(characterId, out var character))
        {
            return (EResultCode.CharacterNotOwned, $"미보유 캐릭터 {characterId}");
        }

        GrantCharacterExp(character, (int)amount, notify: true);
        return (EResultCode.Ok, $"캐릭터 {characterId} Lv{character.Level} Exp{character.Exp}");
    }

    private (EResultCode, string) CheatSettle(DateTime now)
    {
        var settled = SettleWorkStation(now);
        return (EResultCode.Ok, $"정산 슬롯 {settled}개");
    }

    private (EResultCode, string) CheatUnlock(long unlockTid, DateTime now)
    {
        if (unlockTid <= 0 || unlockTid > int.MaxValue || !_unlockCatalog.TryGetUnlock((int)unlockTid, out _))
        {
            return (EResultCode.InvalidCheatArgs, $"UnlockTable에 없는 TID {unlockTid}");
        }

        if (IsUnlocked((int)unlockTid))
        {
            return (EResultCode.AlreadyUnlocked, $"이미 열린 해금 {unlockTid}");
        }

        // 퀘스트·튜토리얼과 같은 지급 경로 — 조건·차감 없이 기록·통지·콘텐츠 후속까지 동일하다.
        GrantUnlock((int)unlockTid, now);
        return (EResultCode.Ok, $"해금 {unlockTid} 지급");
    }
}
