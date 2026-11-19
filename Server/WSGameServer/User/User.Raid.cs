using System.Text.Json;
using GameData;
using MikaProtocol;

namespace WSGameServer;

public partial class User
{
    private readonly RaidCatalog _raids;
    private RaidProfile? _raid;
    private bool _raidBusy;

    private void InitializeRaid(DateTime now)
    {
        if (_raids.StartingBoss is not { } boss)
        {
            return;
        }

        if (!IsUnlocked(boss.UnlockTID) && !GrantUnlock(boss.UnlockTID, now, notify: false))
        {
            return;
        }

        var tickets = _raid?.Tickets.Recharge(new DateTimeOffset(now, TimeSpan.Zero))
                      ?? RaidTicketState.FirstUnlock(new DateTimeOffset(now, TimeSpan.Zero));
        if (_raid?.Tickets != tickets)
        {
            var profile = new RaidProfile(tickets, _raid?.PartyCharacterIds ?? []);
            _raid = profile;
            SaveRaidProfile(profile);
        }
    }

    public void SendRaidStatus(DateTime now)
    {
        var raid = _raid;
        var tickets = raid?.Tickets.Recharge(new DateTimeOffset(now, TimeSpan.Zero));
        Send(new S_RaidStatusResponse
        {
            Result = EResultCode.Ok,
            Unlocked = raid is not null,
            TicketCount = tickets?.Count ?? 0,
            NextRechargeUnixMs = tickets?.NextRechargeAt.ToUnixTimeMilliseconds() ?? 0,
            PartyCharacterIds = raid?.PartyCharacterIds.ToList() ?? [],
            Bosses = _raids.Bosses.Where(boss => IsUnlocked(boss.UnlockTID)).OrderBy(boss => boss.Level)
                .ThenBy(boss => boss.RaidBossTID).Select(boss => new RaidBossInfo
                {
                    BossTID = boss.RaidBossTID, Name = boss.Name, Level = boss.Level,
                    Attack = boss.Attack, Health = boss.Health, AttackIntervalMs = boss.AttackIntervalMs,
                }).ToList(),
        });
    }

    public void SaveRaidParty(IReadOnlyList<long>? characterIds, DateTime now)
    {
        if (_raid is not { } raid)
        {
            Reply(EResultCode.UnlockLocked);
            return;
        }

        if (_raidBusy)
        {
            Reply(EResultCode.RaidInProgress);
            return;
        }

        var error = ValidateRaidParty(characterIds);
        if (error != EResultCode.Ok)
        {
            Reply(error);
            return;
        }

        var updated = new RaidProfile(raid.Tickets.Recharge(new DateTimeOffset(now, TimeSpan.Zero)), characterIds!.ToArray());
        _raidBusy = true;
        SaveRaidProfile(updated, () =>
        {
            _raid = updated;
            _raidBusy = false;
            Reply(EResultCode.Ok);
        });

        void Reply(EResultCode result)
        {
            Send(new S_RaidSavePartyResponse { Result = result, CharacterIds = _raid?.PartyCharacterIds.ToList() ?? [] });
        }
    }

    private EResultCode ValidateRaidParty(IReadOnlyList<long>? characterIds)
    {
        if (characterIds is null || characterIds.Count is < 1 or > 3 ||
            characterIds.Distinct().Count() != characterIds.Count)
        {
            return EResultCode.InvalidRaidParty;
        }

        foreach (long id in characterIds)
        {
            if (!TryGetCharacter(id, out _))
            {
                return EResultCode.CharacterNotOwned;
            }

            if (WorkStation.Slots.Any(slot => slot.CharacterId == id))
            {
                return EResultCode.RaidCharacterBusy;
            }
        }

        return EResultCode.Ok;
    }

    private void SaveRaidProfile(RaidProfile profile, Action? afterSave = null)
    {
        long userId = Uid;
        PostDBTask(new RaidRequestRepository<bool>(this, async connection =>
        {
            await RaidDb.SaveAsync(connection, userId, profile);
            return true;
        }, _ => afterSave?.Invoke()));
    }

    public void ChallengeRaid(int bossTid, Guid requestId, DateTime now)
    {
        if (requestId == Guid.Empty)
        {
            SendRaidFailure(EResultCode.InvalidRaidRequestId, bossTid, requestId);
            return;
        }

        if (_raidBusy)
        {
            SendRaidFailure(EResultCode.RaidInProgress, bossTid, requestId);
            return;
        }

        _raidBusy = true;
        long userId = Uid;
        string requestKey = requestId.ToString("N");
        PostDBTask(new RaidRequestRepository<string?>(this,
            connection => RaidDb.FindAttemptAsync(connection, userId, requestKey), json =>
            {
                if (json is null)
                {
                    ResolveRaidChallenge(bossTid, requestId, requestKey, now);
                    return;
                }

                _raidBusy = false;
                var prior = JsonSerializer.Deserialize<S_RaidChallengeResponse>(json)
                    ?? throw new InvalidOperationException("저장된 레이드 결과를 읽지 못했습니다.");
                if (prior.BossTID != bossTid)
                {
                    SendRaidFailure(EResultCode.InvalidRaidRequestId, bossTid, requestId);
                    return;
                }

                Send(prior);
            }));
    }

    private void ResolveRaidChallenge(int bossTid, Guid requestId, string requestKey, DateTime now)
    {
        if (!_raids.TryGetBoss(bossTid, out var boss))
        {
            Reject(EResultCode.InvalidRaidBossTID);
            return;
        }

        if (_raid is not { } raid || !IsUnlocked(boss.UnlockTID))
        {
            Reject(EResultCode.UnlockLocked);
            return;
        }

        var tickets = raid.Tickets.Recharge(new DateTimeOffset(now, TimeSpan.Zero));
        if (tickets.Count == 0)
        {
            Reject(EResultCode.NotEnoughRaidTicket);
            return;
        }

        var error = ValidateRaidParty(raid.PartyCharacterIds);
        if (error != EResultCode.Ok)
        {
            Reject(error);
            return;
        }

        var fighterStats = raid.PartyCharacterIds.Select(id =>
        {
            TryGetCharacter(id, out var character);
            return BuildRaidFighterStats(character);
        }).ToArray();
        var battle = RaidBattleSimulator.Simulate(fighterStats, new RaidFighterStats(boss.Attack, boss.Health, boss.AttackIntervalMs));
        bool won = battle.Outcome == RaidBattleOutcome.Victory;
        MailAttachment? reward = null;
        if (won)
        {
            if (!HasMailboxRoom)
            {
                Reject(EResultCode.StorageFull);
                return;
            }

            tickets = tickets.ConsumeAfterVictory();
            reward = new MailAttachment(0, [(boss.RewardBoxItemTID, 1)], [], []);
            // 우편·티켓·결과를 함께 저장하는 동안 다른 넘침 우편이 이 자리를 쓰지 않게 한다.
            _pendingOverflowMails++;
        }

        var updated = raid with { Tickets = tickets };
        var response = new S_RaidChallengeResponse
        {
            Result = EResultCode.Ok, RequestId = requestId, BossTID = bossTid,
            Outcome = battle.Outcome switch
            {
                RaidBattleOutcome.Victory => ERaidBattleOutcome.Victory,
                RaidBattleOutcome.PartyDefeated => ERaidBattleOutcome.PartyDefeated,
                _ => ERaidBattleOutcome.TimeLimitReached,
            },
            DurationMs = battle.DurationMs, BossInitialHealth = boss.Health,
            BossRemainingHealth = battle.RemainingBossHealth, TicketCount = tickets.Count,
            RewardBoxItemTID = won ? boss.RewardBoxItemTID : 0, StoredInMail = won,
            Party = raid.PartyCharacterIds.Select((id, index) => new RaidParticipantInfo
            {
                CharacterId = id, InitialHealth = fighterStats[index].MaxHealth,
                RemainingHealth = battle.RemainingPartyHealth[index],
            }).ToList(),
            Hits = battle.Hits.Select(hit => new RaidHitInfo
            {
                ElapsedMs = hit.ElapsedMs, AttackerIndex = hit.AttackerIndex, TargetIndex = hit.TargetIndex,
                Damage = hit.Damage, TargetHealth = hit.TargetHealth,
            }).ToList(),
        };

        string json = JsonSerializer.Serialize(response);
        long userId = Uid;
        PostDBTask(new RaidRequestRepository<(UserMailRow? Mail, bool Full)>(this,
            async connection =>
            {
                try
                {
                    var mail = await RaidDb.CommitAttemptAsync(connection, userId, updated, requestKey, json, reward, now);
                    return (mail, false);
                }
                catch (RaidMailboxFullException)
                {
                    return (null, true);
                }
            }, stored =>
            {
                _raidBusy = false;
                if (stored.Full)
                {
                    _pendingOverflowMails--;
                    SendRaidFailure(EResultCode.StorageFull, bossTid, requestId);
                    return;
                }

                _raid = updated;
                if (stored.Mail is not null)
                {
                    OnOverflowMailStored(stored.Mail);
                }
                Send(response);
            }));

        void Reject(EResultCode result)
        {
            _raidBusy = false;
            SendRaidFailure(result, bossTid, requestId);
        }
    }

    private void SendRaidFailure(EResultCode result, int bossTid, Guid requestId)
    {
        Send(new S_RaidChallengeResponse { Result = result, BossTID = bossTid, RequestId = requestId });
    }

    private RaidFighterStats BuildRaidFighterStats(Character character)
    {
        var attackFlat = 0;
        var healthFlat = 0;
        var attackPercent = 0;
        var healthPercent = 0;
        foreach (var ((characterId, _), equip) in _worn)
        {
            if (characterId != character.Id)
            {
                continue;
            }

            attackFlat += _raids.StatOf(RaidStatSource.Equip, equip.Tid, RaidStatType.Attack);
            healthFlat += _raids.StatOf(RaidStatSource.Equip, equip.Tid, RaidStatType.Health);
            foreach (var option in equip.EnchantOptions)
            {
                switch (option.OptionType)
                {
                    case EnchantOptionType.AttackFlat: attackFlat += option.Value; break;
                    case EnchantOptionType.HealthFlat: healthFlat += option.Value; break;
                    case EnchantOptionType.AttackPercent: attackPercent += option.Value; break;
                    case EnchantOptionType.HealthPercent: healthPercent += option.Value; break;
                }
            }
        }

        int stars = StarOf(character);
        return new RaidFighterStats(CalculateStat(RaidStatType.Attack, attackPercent, attackFlat),
                                    CalculateStat(RaidStatType.Health, healthPercent, healthFlat),
                                    character.Row.AttackIntervalMs);

        int CalculateStat(RaidStatType statType, int percent, int flat)
        {
            return RaidStatCalculator.Calculate(_raids.StatOf(RaidStatSource.Character, character.Tid, statType),
                                                _raids.StatOf(RaidStatSource.Level, character.Level, statType),
                                                _raids.StatOf(RaidStatSource.Star, stars, statType), percent, flat);
        }
    }
}
