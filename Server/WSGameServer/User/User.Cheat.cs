using GameData;
using MikaProtocol;

namespace WSGameServer;

public partial class User
{
    /// <summary>치트를 쓸 수 있는 최소 admin_level. 0은 일반 유저다.</summary>
    /// 개발을 위해서 CheatAdminLevel은 없도록.
    public const int CheatAdminLevel = 0;

    /// <summary>한 번에 지급할 수 있는 캐릭터 장수 상한. 10연차와 같다.</summary>
    public const int CheatMaxCharacterCount = 10;

    /// <summary>정산 치트가 한 번에 앞당길 수 있는 판정 횟수 상한.</summary>
    public const int CheatMaxSettleJudges = 100;

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
            ECheatCommand.Settle           => CheatSettle(req.Arg1, now),
            ECheatCommand.Unlock           => CheatUnlock(req.Arg1, now),
            ECheatCommand.GiveEquip        => CheatGiveEquip(req.Arg1),
            ECheatCommand.GiveAccountExp   => CheatGiveAccountExp(req.Arg1),
            ECheatCommand.SendMail         => CheatSendMail(req.Arg1, req.Arg2, now),
            _                              => (EResultCode.InvalidCheatCommand, "정의되지 않은 명령"),
        };

        Reply(result, message);
        return;

        void Reply(EResultCode code, string text)
        {
            // 성공·실패 전부 남긴다 — 운영에서 누가 무엇을 했는지가 이 한 줄뿐이다.
            ServerLog.Warn("치트", $"Uid={Uid} {req.Command}(Arg1:{req.Arg1}, Arg2:{req.Arg2}) → {code} {text}");
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

        AddItem((int)itemTid, (int)count);
        return (EResultCode.Ok, $"아이템 {itemTid} +{count}");
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

    // 쌓인 진행도만 정산하면 스케줄러(0.1초)가 먼저 가져가 늘 0개다. 판정을 N회분 얹고 정산한다.
    private (EResultCode, string) CheatSettle(long judges, DateTime now)
    {
        if (judges <= 0)
        {
            judges = 1;
        }

        if (judges > CheatMaxSettleJudges)
        {
            return (EResultCode.InvalidCheatArgs, $"판정 횟수는 1~{CheatMaxSettleJudges}");
        }

        var advanced = WorkStation.Slots.Count(slot => slot.AdvanceJudges((int)judges));
        if (advanced == 0)
        {
            return (EResultCode.Ok, "가동 중인 슬롯 없음");
        }

        var settled = SettleWorkStation(now);
        return (EResultCode.Ok, $"슬롯 {advanced}개 × 판정 {judges}회 앞당김 · 정산 슬롯 {settled}개");
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

    private (EResultCode, string) CheatGiveAccountExp(long amount)
    {
        if (amount <= 0)
        {
            return (EResultCode.InvalidCheatArgs, "경험치 범위 밖");
        }

        // 캐릭터 경험치가 계정으로 흘러드는 것과 같은 함수 — 레벨업·포인트·저장·푸시가 같다.
        GainAccountExp(amount, notify: true);
        return (EResultCode.Ok, $"계정 Lv{AccountLevel} Exp{AccountExp} 특성포인트{TraitPoint}");
    }

    private (EResultCode, string) CheatSendMail(long templateTid, long recipientUid, DateTime now)
    {
        if (templateTid <= 0 || templateTid > int.MaxValue)
        {
            return (EResultCode.InvalidCheatArgs, $"MailTemplateTable에 없는 TID {templateTid}");
        }

        // 운영툴과 같은 발송 경로 — 치트 전용 지급 코드를 만들지 않는다(치트 원칙 4).
        return SendOperationMail((int)templateTid, recipientUid, now);
    }

    private (EResultCode, string) CheatGiveEquip(long equipTid)
    {
        if (equipTid <= 0 || equipTid > int.MaxValue || !_equipCatalog.TryGet((int)equipTid, out _))
        {
            return (EResultCode.InvalidCheatArgs, $"EquipTable에 없는 TID {equipTid}");
        }

        // 앞으로 생길 획득 경로와 같은 지급 함수 — PK 발급 후 S_EquipSyncResponse까지 동일하다.
        GrantEquip((int)equipTid);
        return (EResultCode.Ok, $"장비 {equipTid} 지급 요청");
    }
}
