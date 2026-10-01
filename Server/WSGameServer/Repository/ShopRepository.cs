using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 즉시 판매를 DB에 반영한다. <b>인벤토리 차감과 재화 잔액을 한 트랜잭션으로 쓴다</b> —
/// 둘로 나누면 차감만 남고 지급이 실패했을 때 아이템이 증발한다.
/// </summary>
public sealed class SellItemsRepository : IRepository
{
    private readonly List<ItemChangeInfo> _itemChanges;
    private readonly long _gold;

    public SellItemsRepository(User user, List<ItemChangeInfo> itemChanges, long gold)
    {
        User         = user;
        _itemChanges = itemChanges;
        _gold        = gold;
    }

    public long Key => User.DbKey;

    public User User { get; }

    public Task ExecuteAsync(DbConnection connection)
    {
        return connection.InTransactionAsync(async tx =>
        {
            foreach (var change in _itemChanges)
            {
                // 0개가 된 아이템은 행을 지운다 — 남겨 두면 다음 로그인에 0개짜리로 적재된다.
                if (change.Count == 0)
                {
                    await tx.ExecuteAsync(
                        "DELETE FROM t_user_inventory WHERE user_id = @userId AND item_id = @itemId",
                        new { userId = User.Uid, itemId = change.ItemId });
                    continue;
                }

                await tx.ExecuteAsync(
                    @"INSERT INTO t_user_inventory (user_id, item_id, count)
                      VALUES (@userId, @itemId, @count)
                      ON CONFLICT (user_id, item_id) DO UPDATE SET count = excluded.count;",
                    new { userId = User.Uid, itemId = change.ItemId, count = change.Count });
            }

            // 델타가 아니라 확정 잔액을 쓴다 — 재시도·중복 전송이 곧 재화 복제가 된다.
            await tx.ExecuteAsync(
                @"INSERT INTO t_user_currency (user_id, gold)
                  VALUES (@userId, @gold)
                  ON CONFLICT (user_id) DO UPDATE SET gold = excluded.gold;",
                new { userId = User.Uid, gold = _gold });
        });
    }

    public void Apply()
    {
    }
}

/// <summary>
/// 개체 판매를 DB에 반영한다. 개체 삭제와 재화 잔액을 한 트랜잭션으로 쓴다 — 나누면 개체만 사라지고 대금이 빠질 수 있다.
/// 경매에 잠긴 개체(auction_trade_id ≠ 0)는 메모리에 없으므로 여기 올 수 없지만, 조건으로 한 번 더 막는다.
/// </summary>
public sealed class SellEntitiesRepository(User user, List<long> characterIds, List<long> equipIds, long gold) : IRepository
{
    public long Key => User.DbKey;

    public User User { get; } = user;

    public IReadOnlyList<long> CharacterIds => characterIds;
    public IReadOnlyList<long> EquipIds     => equipIds;

    public Task ExecuteAsync(DbConnection connection)
    {
        return connection.InTransactionAsync(async tx =>
        {
            await tx.ExecuteAsync(
                "DELETE FROM t_character WHERE user_id = @userId AND auction_trade_id = 0 AND character_id IN @characterIds",
                new { userId = User.Uid, characterIds });

            await tx.ExecuteAsync(
                "DELETE FROM t_user_equip WHERE user_id = @userId AND auction_trade_id = 0 AND equip_id IN @equipIds",
                new { userId = User.Uid, equipIds });

            // 델타가 아니라 확정 잔액을 쓴다 — 재시도·중복 전송이 곧 재화 복제가 된다.
            await tx.ExecuteAsync(
                @"INSERT INTO t_user_currency (user_id, gold)
                  VALUES (@userId, @gold)
                  ON CONFLICT (user_id) DO UPDATE SET gold = excluded.gold;",
                new { userId = User.Uid, gold });
        });
    }

    public void Apply()
    {
    }
}
