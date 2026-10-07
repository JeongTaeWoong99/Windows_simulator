using GameData;
using MikaProtocol;

namespace WSGameServer;

public partial class User
{
    /// <summary>치트를 쓸 수 있는 최소 admin_level. 0은 일반 유저다.</summary>
    /// 개발을 위해서 CheatAdminLevel은 없도록.
    public const int CheatAdminLevel = 0;

    /// <summary>한 번에 지급할 수 있는 캐릭터 장수 상한 — Constants.xlsx. 클라 치트창과 같은 값을 읽는다.</summary>
    public static int CheatMaxCharacterCount => (int)Constants.CheatMaxCharacterCount;

    /// <summary>정산 치트가 한 번에 앞당길 수 있는 판정 횟수 상한 — Constants.xlsx.</summary>
    public static int CheatMaxSettleJudges => (int)Constants.CheatMaxSettleJudges;

    /// <summary>시간 치트가 한 번에 넘길 수 있는 상한. 0 하나 더 친 오타와 DateTime 넘침을 막는다.</summary>
    public static readonly TimeSpan CheatMaxAdvanceTime = TimeSpan.FromDays(365);

    /// <summary>전역 배수 치트의 범위(천분율). ×0.1 ~ ×100 — 0 하나 더 친 오타가 판정 폭주로 번지지 않게.</summary>
    public const int CheatMinGatherSpeedPermille = 100;
    public const int CheatMaxGatherSpeedPermille = 100_000;

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
            ECheatCommand.GiveGold         => CheatGiveGold(req.Arg1),
            ECheatCommand.GiveItem         => CheatGiveItem(req.Arg1, req.Arg2),
            ECheatCommand.GiveCharacter    => CheatGiveCharacter(req.Arg1, req.Arg2),
            ECheatCommand.GiveCharacterExp => CheatGiveCharacterExp(req.Arg1, req.Arg2, now),
            ECheatCommand.Settle           => CheatSettle(req.Arg1, now),
            ECheatCommand.Unlock           => CheatUnlock(req.Arg1, now),
            ECheatCommand.GiveEquip        => CheatGiveEquip(req.Arg1),
            ECheatCommand.GiveAccountExp   => CheatGiveAccountExp(req.Arg1),
            ECheatCommand.SendMail         => CheatSendMail(req.Arg1, req.Arg2, now),
            ECheatCommand.SetTraitLevel    => CheatSetTraitLevel(req.Arg1, req.Arg2, now),
            ECheatCommand.AdvanceTime      => CheatAdvanceTime(req.Arg1, now),
            ECheatCommand.ResetTime        => CheatResetTime(now),
            ECheatCommand.SetCondenseCount => CheatSetCondenseCount(req.Arg1, req.Arg2, now),
            ECheatCommand.SetGatherSpeed   => CheatSetGatherSpeed(req.Arg1, now),
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

    private (EResultCode, string) CheatGiveGold(long amount)
    {
        if (amount == 0)
        {
            return (EResultCode.InvalidCheatArgs, "금액이 0");
        }

        if (amount > 0)
        {
            var balance = GainGold(amount);
            return (EResultCode.Ok, $"Gold +{amount} → {balance}");
        }

        if (!TrySpendGold(-amount))
        {
            return (EResultCode.NotEnoughCurrency, "Gold 잔액 부족");
        }

        return (EResultCode.Ok, $"Gold {amount}");
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

        // 뽑기와 같은 칸 검사 — 넘기면 클라 격자에 칸이 없어 보이지 않는다(#40).
        if (!HasStorageFor(new[] { (int)itemTid }, characterCount: 0, equipCount: 0))
        {
            return (EResultCode.StorageFull, $"창고가 가득 참 (자원 {ItemSlotsUsed}/{StorageCapacity})");
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

        if (!HasStorageFor(Array.Empty<int>(), characterCount: (int)count, equipCount: 0))
        {
            return (EResultCode.StorageFull, $"창고가 가득 참 (캐릭터 {CharacterSlotsUsed}/{StorageCapacity})");
        }

        // 가챠와 같은 지급 경로 — 개체 PK 발급 후 목록 재전송까지 동일하다.
        GrantGachaCharacters(Enumerable.Repeat((int)characterTid, (int)count).ToList());
        return (EResultCode.Ok, $"캐릭터 {characterTid} × {count} 지급 요청");
    }

    private (EResultCode, string) CheatGiveCharacterExp(long characterId, long amount, DateTime now)
    {
        if (amount <= 0 || amount > int.MaxValue)
        {
            return (EResultCode.InvalidCheatArgs, "경험치 범위 밖");
        }

        if (!TryGetCharacter(characterId, out var character))
        {
            return (EResultCode.CharacterNotOwned, $"미보유 캐릭터 {characterId}");
        }

        // 레벨이 오르면 배치 슬롯 속도를 다시 매긴다. 정산 시점의 슬롯 속도는 아직 옛 값이라 소급되지 않는다.
        if (GrantCharacterExp(character, (int)amount, notify: true))
        {
            RefreshWorkStationSpeed(now);
        }

        return (EResultCode.Ok, $"캐릭터 {characterId} Lv{character.Level} Exp{character.Exp}");
    }

    // 재료 없이 응축 누적을 정한다. 실제 응축과 같은 저장·속도 갱신을 탄다 — 재료 목록만 비어 있다.
    private (EResultCode, string) CheatSetCondenseCount(long characterId, long count, DateTime now)
    {
        if (!TryGetCharacter(characterId, out var character))
        {
            return (EResultCode.CharacterNotOwned, $"미보유 캐릭터 {characterId}");
        }

        var starBefore = StarOf(character);
        character.SetCondenseCount((int)Math.Clamp(count, 0, _characterStars.MaxCount));

        PostDBTask(new CondenseCharacterRepository(this, character, new List<long>()));
        Send(new S_CharacterSyncResponse { Character = ToCharacterInfo(character) });

        if (StarOf(character) != starBefore)
        {
            RefreshWorkStationSpeed(now);
        }

        return (EResultCode.Ok, $"캐릭터 {characterId} 누적 {character.CondenseCount} ★{StarOf(character)}");
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

    private (EResultCode, string) CheatSetTraitLevel(long userTraitTid, long level, DateTime now)
    {
        if (level < 0)
        {
            return (EResultCode.InvalidCheatArgs, "레벨이 음수");
        }

        List<UserTraitTableRow> targets;
        if (userTraitTid == 0)
        {
            targets = _traitCatalog.All.ToList();
        }
        else if (userTraitTid > 0 && userTraitTid <= int.MaxValue && _traitCatalog.TryGet((int)userTraitTid, out var trait))
        {
            targets = new List<UserTraitTableRow> { trait };
        }
        else
        {
            return (EResultCode.InvalidCheatArgs, $"UserTraitTable에 없는 TID {userTraitTid}");
        }

        // 최대 레벨을 넘는 값은 어차피 최대로 잘린다 — int로 좁힐 때 넘침만 막는다.
        var changed = SetTraitLevels(targets, (int)Math.Min(level, int.MaxValue), now);
        return (EResultCode.Ok, $"특성 {targets.Count}개 중 {changed}개 변경 (요청 Lv{level})");
    }

    private (EResultCode, string) CheatAdvanceTime(long seconds, DateTime now)
    {
        if (seconds <= 0 || seconds > (long)CheatMaxAdvanceTime.TotalSeconds)
        {
            return (EResultCode.InvalidCheatArgs, $"넘길 초는 1~{(long)CheatMaxAdvanceTime.TotalSeconds}");
        }

        var delta = TimeSpan.FromSeconds(seconds);
        _clock.Advance(delta);
        ShiftGameClock(delta, now + delta);
        return (EResultCode.Ok, $"게임 시계 +{delta} → 오프셋 {_clock.Offset}");
    }

    private (EResultCode, string) CheatResetTime(DateTime now)
    {
        var delta = _clock.Reset();
        if (delta == TimeSpan.Zero)
        {
            return (EResultCode.Ok, "오프셋이 이미 0");
        }

        ShiftGameClock(delta, now + delta);
        return (EResultCode.Ok, $"게임 시계 {delta} 되돌림 → 오프셋 0");
    }

    // 오프셋은 서버 전체 하나라 접속 중인 모두에게 알린다. 경매장은 릴레이가 다음 바퀴에 같은 오프셋을 맞춘다.
    private void ShiftGameClock(TimeSpan delta, DateTime now)
    {
        foreach (var user in _onlineUsers.All.Append(this).Distinct())
        {
            user.OnGameClockShifted(delta, now);
        }

        _auction.KickRelay();
    }

    /// <summary>
    /// 게임 시계가 <paramref name="delta"/>만큼 움직였다. 슬롯 기준 시각을 같이 밀어 <b>넘긴 시간은 채취에 쌓이지 않는다</b> —
    /// 보상이 크게 튀지 않게(이슈 #48 결정). 판정을 당기려면 <c>Settle</c> 치트를 쓴다.
    /// </summary>
    public void OnGameClockShifted(TimeSpan delta, DateTime now)
    {
        WorkStation.ShiftClock(delta);
        SendWorkStationSlots();
        SendServerTime(now);
    }

    // 배수는 서버 전체 하나라 접속 중인 모두를 정산한 뒤 새 속도로 갈아태운다 — 바꾸기 전 구간은 예전 배수로 끝난다.
    private (EResultCode, string) CheatSetGatherSpeed(long permille, DateTime now)
    {
        if (permille < CheatMinGatherSpeedPermille || permille > CheatMaxGatherSpeedPermille)
        {
            return (EResultCode.InvalidCheatArgs, $"배수 천분율은 {CheatMinGatherSpeedPermille}~{CheatMaxGatherSpeedPermille} (1000 = ×1.0)");
        }

        _gatherSpeed.Set((int)permille);
        foreach (var user in _onlineUsers.All.Append(this).Distinct())
        {
            user.RefreshWorkStationSpeed(now);
        }

        return (EResultCode.Ok, $"채취 전역 배수 ×{permille / 1000.0:0.###} (저장 안 함 — 재시작하면 ×1)");
    }

    private (EResultCode, string) CheatGiveEquip(long equipTid)
    {
        if (equipTid <= 0 || equipTid > int.MaxValue || !_equipCatalog.TryGet((int)equipTid, out _))
        {
            return (EResultCode.InvalidCheatArgs, $"EquipTable에 없는 TID {equipTid}");
        }

        if (!HasStorageFor(Array.Empty<int>(), characterCount: 0, equipCount: 1))
        {
            return (EResultCode.StorageFull, $"창고가 가득 참 (장비 {EquipSlotsUsed}/{StorageCapacity})");
        }

        // 앞으로 생길 획득 경로와 같은 지급 함수 — PK 발급 후 S_EquipSyncResponse까지 동일하다.
        GrantEquip((int)equipTid);
        return (EResultCode.Ok, $"장비 {equipTid} 지급 요청");
    }
}
