using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 즉시 판매를 DB에 반영한다. <b>인벤토리 차감과 재화 잔액을 한 트랜잭션으로 쓴다</b> —
/// 둘로 나누면 차감만 남고 지급이 실패했을 때 아이템이 증발한다.
/// </summary>
public sealed class SellItemsRepository : IRepository
{
    private readonly List<ItemChangeInfo> _itemChanges;
    private readonly CurrencyType _currencyType;
    private readonly long _remain;

    public SellItemsRepository(User user, List<ItemChangeInfo> itemChanges, CurrencyType currencyType, long remain)
    {
        User          = user;
        _itemChanges  = itemChanges;
        _currencyType = currencyType;
        _remain       = remain;
    }

    // DBExecutor 파티션 키 — 같은 유저의 DB 작업(로그인 로드 등)과 직렬로 처리돼야 한다.
    public long Key => User.SessionId;

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
                @"INSERT INTO t_user_currency (user_id, currency_type, amount)
                  VALUES (@userId, @currencyType, @amount)
                  ON CONFLICT (user_id, currency_type) DO UPDATE SET amount = excluded.amount;",
                new { userId = User.Uid, currencyType = (int)_currencyType, amount = _remain });
        });
    }

    public void Apply()
    {
    }
}
