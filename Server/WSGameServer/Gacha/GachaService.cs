using GameData;
using MikaProtocol;
using MikaUtils;

namespace WSGameServer;

/// <summary>
/// 가챠 뽑기 흐름을 조립하는 서비스 계층(Singleton).
/// 추첨은 <see cref="GachaPoolCatalog"/>(엑셀 가챠 시트 기반)에 위임하고,
/// 인벤토리/재화/DB 부수효과는 <see cref="User"/>에 위임한다. 여기서는 검증·조립·응답만 담당한다.
/// </summary>
public sealed class GachaService : Singleton<GachaService>
{
    // 허용하는 뽑기 횟수(단차 / 10연차)
    private const int SingleDraw = 1;
    private const int MultiDraw = 10;

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
        if (!GameTable.GachaInfoTable.TryGet(gachaId, out var info))
        {
            user.Send(new S_GachaDrawResponse { Result = EResultCode.InvalidGachaId });
            return;
        }

        if (!_pools.TryGet(gachaId, out var pool))
        {
            user.Send(new S_GachaDrawResponse { Result = EResultCode.InvalidGachaId });
            return;
        }

        // 2) 비용 차감 — 반드시 지급 전에 한다. 모자라면 아무것도 바뀌지 않는다.
        var cost = drawCount == MultiDraw ? info.CostMulti : info.CostSingle;
        if (!TrySpend(user, info.CostCurrency, cost))
        {
            user.Send(new S_GachaDrawResponse { Result = EResultCode.NotEnoughCurrency });
            return;
        }

        // 3) 순수 추첨(뽑힌 순서대로)
        var entries = pool.PickMany(drawCount);

        // 4) 연출용 결과: 뽑힌 순서/개별 항목 그대로. 등급은 정의 테이블에서 읽는다(단일 원본)
        var rewards = new List<GachaRewardInfo>(entries.Count);
        foreach (var entry in entries)
        {
            rewards.Add(ToRewardInfo(entry));
        }

        // 5) 인벤토리 반영: itemId별 수량을 합산해 아이템당 한 번만 갱신(UPSERT 최소화)
        var gained = new Dictionary<int, int>();
        foreach (var entry in entries.Where(e => e.RewardType == GachaRewardType.Item))
        {
            gained[entry.RewardTID] = gained.GetValueOrDefault(entry.RewardTID) + entry.Count;
        }

        var changes = new List<ItemChangeInfo>(gained.Count);
        foreach (var (itemId, count) in gained)
        {
            changes.Add(user.GainItem(itemId, count));
        }

        // 6) 캐릭터 지급: Count만큼 개체를 늘린다(중복은 그대로 쌓인다 — 캐릭터 기획 1.1).
        //    개체 PK는 DB가 발급하므로 갱신된 목록은 이 응답보다 늦게 따로 내려간다.
        var characterTids = new List<int>();
        foreach (var entry in entries.Where(e => e.RewardType == GachaRewardType.Character))
        {
            for (var i = 0; i < entry.Count; i++)
            {
                characterTids.Add(entry.RewardTID);
            }
        }

        if (characterTids.Count > 0)
        {
            user.GrantGachaCharacters(characterTids);
        }

        // 7) 장비 지급: 개체마다 창고 칸이 달라 1개씩 요청한다. 캐릭터처럼 개체는 S_EquipSyncResponse로 늦게 내려간다.
        foreach (var entry in entries.Where(e => e.RewardType == GachaRewardType.Equip))
        {
            for (var i = 0; i < entry.Count; i++)
            {
                user.GrantEquip(entry.RewardTID);
            }
        }

        // 8) 뽑기 결과 응답 — Rewards는 연출용(델타), ItemChangeInfos는 인벤토리 반영용(누적 총량)
        user.Send(new S_GachaDrawResponse
        {
            Result = EResultCode.Ok,
            Rewards = rewards,
            ItemChangeInfos = changes,
        });
    }

    // 재화가 늘면 여기에 분기를 추가한다. 엑셀 CurrencyType에 없는 값은 데이터 오류이므로 지급하지 않는다.
    private static bool TrySpend(User user, CurrencyType currency, long cost)
    {
        return currency switch
        {
            CurrencyType.Gold => user.TrySpendGold(cost),
            _                 => false,
        };
    }

    private static GachaRewardInfo ToRewardInfo(GachaEntry entry)
    {
        if (entry.RewardType == GachaRewardType.Equip)
        {
            return new GachaRewardInfo
            {
                RewardType = EGachaRewardType.Equip,
                EquipTid   = entry.RewardTID,
                Count      = entry.Count,
                Rarity     = EquipRarityOf(entry.RewardTID),
            };
        }

        if (entry.RewardType == GachaRewardType.Character)
        {
            return new GachaRewardInfo
            {
                RewardType   = EGachaRewardType.Character,
                CharacterTid = entry.RewardTID,
                Count        = entry.Count,
                Rarity       = CharacterRarityOf(entry.RewardTID),
            };
        }

        return new GachaRewardInfo
        {
            RewardType = EGachaRewardType.Item,
            ItemId     = entry.RewardTID,
            Count      = entry.Count,
            Rarity     = ItemRarityOf(entry.RewardTID),
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
