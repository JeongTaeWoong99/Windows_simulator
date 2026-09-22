using GameData;
using MikaProtocol;
using MikaUtils;

namespace WSGameServer;

/// <summary>
/// 가챠 뽑기 흐름을 조립하는 서비스 계층(Singleton).
/// 추첨은 <see cref="GachaPoolCatalog"/>(엑셀 가챠 시트 기반)에 위임하고,
/// 인벤토리/재화/DB 부수효과는 <see cref="User"/>에 위임한다. 여기서는 검증·조립·응답만 담당한다.
/// 골드로 뽑는 <see cref="Draw"/>와 상자를 여는 <see cref="OpenBox"/>는 <b>지급 경로가 같다</b>(<see cref="Grant"/>).
/// </summary>
public sealed class GachaService : Singleton<GachaService>
{
    // 허용하는 뽑기 횟수(단차 / 10연차)
    private const int SingleDraw = 1;
    private const int MultiDraw = 10;

    /// <summary>한 번에 여는 상자 수 상한. 상자 최대 수량(MaxStack 99)과 같다.</summary>
    public const int MaxOpenCount = 99;

    private readonly GachaPoolCatalog _pools;

    public GachaService() : this(null)
    {
    }

    /// <summary>풀 보관소를 생략하면 전역 인스턴스를 쓴다 — <see cref="User"/>의 카탈로그 규약과 같다.</summary>
    public GachaService(GachaPoolCatalog? pools)
    {
        _pools = pools ?? GachaPoolCatalog.Instance;
    }

    public void Draw(User user, int gachaId, int drawCount)
    {
        // 1) 검증: 뽑기 횟수와 풀 존재 여부 — 실패해도 반드시 응답한다(코드로 이유를 구분)
        if (drawCount != SingleDraw && drawCount != MultiDraw)
        {
            user.Send(new S_GachaDrawResponse { Result = EResultCode.InvalidDrawCount });
            return;
        }

        // 비용을 적어 둔 메타가 없으면 거절한다 — 없는 채로 통과시키면 그 풀만 공짜가 된다.
        // 비용 재화가 None인 풀은 상자 전용이다 — 골드로 열면 상자 없이 내용물을 산다.
        if (!GameTable.GachaInfoTable.TryGet(gachaId, out var info) || info.CostCurrency == CurrencyType.None)
        {
            user.Send(new S_GachaDrawResponse { Result = EResultCode.InvalidGachaId });
            return;
        }

        if (!_pools.TryGet(gachaId, out var pool))
        {
            user.Send(new S_GachaDrawResponse { Result = EResultCode.InvalidGachaId });
            return;
        }

        // 2) 추첨(순수) → 칸 검사 → 비용 차감 → 지급. 칸 검사에 결과가 필요해 추첨이 차감보다 앞선다.
        var picked = pool.PickMany(drawCount);
        if (!HasStorageFor(user, picked))
        {
            user.Send(new S_GachaDrawResponse { Result = EResultCode.StorageFull });
            return;
        }

        // 차감은 반드시 지급 전에 한다. 모자라면 아무것도 바뀌지 않는다.
        var cost = drawCount == MultiDraw ? info.CostMulti : info.CostSingle;
        if (!TrySpend(user, info.CostCurrency, cost))
        {
            user.Send(new S_GachaDrawResponse { Result = EResultCode.NotEnoughCurrency });
            return;
        }

        // 3) 지급. Rewards는 연출용(델타), ItemChangeInfos는 인벤토리 반영용(누적 총량)
        var (rewards, changes) = Grant(user, picked);

        user.Send(new S_GachaDrawResponse
        {
            Result = EResultCode.Ok,
            Rewards = rewards,
            ItemChangeInfos = changes,
        });
    }

    /// <summary>
    /// 상자를 연다(T-029). 상자 = <c>OpenGachaId</c>가 있는 아이템. 그 풀을 개수만큼 <b>비용 없이</b> 돈다.
    /// 차감을 지급보다 먼저 한다 — 모자라면 아무것도 바뀌지 않는다.
    /// </summary>
    public void OpenBox(User user, int itemTid, int count)
    {
        if (count < 1 || count > MaxOpenCount)
        {
            Reply(EResultCode.InvalidUseCount);
            return;
        }

        if (!GameTable.ItemTable.TryGet(itemTid, out var item) || item.OpenGachaId == 0 ||
            !_pools.TryGet(item.OpenGachaId, out var pool))
        {
            Reply(EResultCode.ItemNotUsable);
            return;
        }

        if (user.GetItemCount(itemTid) < count)
        {
            Reply(EResultCode.NotEnoughItem);
            return;
        }

        // 상자를 전부 열면 그 상자 칸이 빈다 — 빈 칸까지 보고 판정한다.
        var picked = pool.PickMany(count);
        var freed  = user.GetItemCount(itemTid) == count ? itemTid : 0;
        if (!HasStorageFor(user, picked, freed))
        {
            Reply(EResultCode.StorageFull);
            return;
        }

        if (!user.TryConsumeItems(new Dictionary<int, int> { [itemTid] = count }, out var consumed))
        {
            Reply(EResultCode.NotEnoughItem);
            return;
        }

        var (rewards, changes) = Grant(user, picked);

        // 상자 차감을 앞에 싣는다 — 클라는 누적 총량으로 덮어쓰기만 하면 된다.
        consumed.AddRange(changes);
        user.Send(new S_ItemUseResponse
        {
            Result          = EResultCode.Ok,
            ItemTID         = itemTid,
            Rewards         = rewards,
            ItemChangeInfos = consumed,
        });
        return;

        void Reply(EResultCode code)
        {
            ServerLog.Warn("상자", $"거절 — {code}. Uid={user.Uid} ItemTID={itemTid} Count={count}");
            user.Send(new S_ItemUseResponse { Result = code, ItemTID = itemTid });
        }
    }

    // 뽑힌 항목을 종류별로 지급하고 연출용 목록과 인벤토리 변경분을 돌려준다.
    // 같은 종류는 한 번에 모은다 — 아이템은 종류당 UPSERT 1번, 골드는 저장·통지 1번, 캐릭터는 DB 왕복 1번.
    private static (List<GachaRewardInfo> Rewards, List<ItemChangeInfo> Changes) Grant(User user, IReadOnlyList<GachaEntry> entries)
    {
        var rewards = new List<GachaRewardInfo>(entries.Count);
        var gained = new Dictionary<int, int>();
        var characterTids = new List<int>();
        long gold = 0;

        foreach (var entry in entries)
        {
            var count = RollCount(entry);
            rewards.Add(ToRewardInfo(entry, count));

            switch (entry.RewardType)
            {
                case GachaRewardType.Item:
                    gained[entry.RewardTID] = gained.GetValueOrDefault(entry.RewardTID) + count;
                    break;

                case GachaRewardType.Character:
                    // 중복은 그대로 쌓인다 — 캐릭터 기획 1.1. 개체 PK는 DB가 발급해 목록이 늦게 내려간다.
                    characterTids.AddRange(Enumerable.Repeat(entry.RewardTID, count));
                    break;

                case GachaRewardType.Equip:
                    // 개체마다 창고 칸이 달라 1개씩 요청한다. 개체는 S_EquipSyncResponse로 늦게 내려간다.
                    for (var i = 0; i < count; i++)
                    {
                        user.GrantEquip(entry.RewardTID);
                    }

                    break;

                case GachaRewardType.Gold:
                    gold += count;
                    break;
            }
        }

        var changes = gained.Select(kv => user.GainItem(kv.Key, kv.Value)).ToList();

        if (characterTids.Count > 0)
        {
            user.GrantGachaCharacters(characterTids);
        }

        if (gold > 0)
        {
            user.GainGold(gold);
        }

        return (rewards, changes);
    }

    // 캐릭터·장비는 개체 수, 아이템은 종류 수가 칸이다. 개체 수는 구간이 있으면 최대치로 본다 — 지급 때 굴린 값이 검사보다 크면 안 된다.
    private static bool HasStorageFor(User user, IReadOnlyList<GachaEntry> picked, int freedItemTid = 0)
    {
        var itemTids   = picked.Where(e => e.RewardType == GachaRewardType.Item).Select(e => e.RewardTID);
        var characters = picked.Where(e => e.RewardType == GachaRewardType.Character).Sum(MaxCountOf);
        var equips     = picked.Where(e => e.RewardType == GachaRewardType.Equip).Sum(MaxCountOf);
        return user.HasStorageFor(itemTids, characters, equips, freedItemTid);
    }

    private static int MaxCountOf(GachaEntry entry) => Math.Max(entry.Count, entry.MaxCount);

    // 구간 보상(골드)은 Count~MaxCount에서 고른다. MaxCount가 없거나 작으면 Count 고정이다.
    private static int RollCount(GachaEntry entry)
        => entry.MaxCount > entry.Count ? Random.Shared.Next(entry.Count, entry.MaxCount + 1) : entry.Count;

    // 재화가 늘면 여기에 분기를 추가한다. 엑셀 CurrencyType에 없는 값은 데이터 오류이므로 지급하지 않는다.
    private static bool TrySpend(User user, CurrencyType currency, long cost)
    {
        return currency switch
        {
            CurrencyType.Gold => user.TrySpendGold(cost),
            _                 => false,
        };
    }

    private static GachaRewardInfo ToRewardInfo(GachaEntry entry, int count)
    {
        return entry.RewardType switch
        {
            GachaRewardType.Equip => new GachaRewardInfo
            {
                RewardType = EGachaRewardType.Equip, EquipTid = entry.RewardTID, Count = count, Rarity = EquipRarityOf(entry.RewardTID),
            },
            GachaRewardType.Character => new GachaRewardInfo
            {
                RewardType = EGachaRewardType.Character, CharacterTid = entry.RewardTID, Count = count, Rarity = CharacterRarityOf(entry.RewardTID),
            },
            // 골드는 등급이 없다 — 연출은 None을 기본 칸으로 그린다.
            GachaRewardType.Gold => new GachaRewardInfo { RewardType = EGachaRewardType.Gold, Count = count },
            _ => new GachaRewardInfo
            {
                RewardType = EGachaRewardType.Item, ItemId = entry.RewardTID, Count = count, Rarity = ItemRarityOf(entry.RewardTID),
            },
        };
    }

    // GameData.GlobalRarity와 프로토콜 EGlobalRarity는 값이 1:1이라 byte 캐스팅으로 옮긴다.
    // 풀의 TID는 시트 Ref 검사로 실재가 보장되므로 None은 실제로는 나오지 않는다.
    private static EGlobalRarity ItemRarityOf(int itemTid)
    {
        if (!GameTable.ItemTable.TryGet(itemTid, out var item))
        {
            return EGlobalRarity.None;
        }

        return (EGlobalRarity)(byte)item.GlobalRarity;
    }

    private static EGlobalRarity EquipRarityOf(int equipTid)
    {
        if (!GameTable.EquipTable.TryGet(equipTid, out var equip))
        {
            return EGlobalRarity.None;
        }

        return (EGlobalRarity)(byte)equip.GlobalRarity;
    }

    private static EGlobalRarity CharacterRarityOf(int characterTid)
    {
        if (!GameTable.CharacterTable.TryGet(characterTid, out var character))
        {
            return EGlobalRarity.None;
        }

        return (EGlobalRarity)(byte)character.GlobalRarity;
    }
}
