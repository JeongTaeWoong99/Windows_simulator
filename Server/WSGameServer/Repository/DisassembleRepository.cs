using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 장비 분해를 DB에 반영한다(T-131). 장비 삭제와 재료 지급을 한 트랜잭션으로 쓴다 — 나누면 장비만 사라지고 재료가 빠질 수 있다.
/// 인챈트는 장비 행에 들어 있어 함께 지워진다. 경매에 잠긴 장비(auction_trade_id ≠ 0)는 조건으로 한 번 더 막는다.
/// </summary>
public sealed class DisassembleEquipsRepository(User user, List<long> equipIds, List<ItemChangeInfo> itemChanges) : IRepository
{
    public long Key => User.DbKey;

    public User User { get; } = user;

    public IReadOnlyList<long>           EquipIds    => equipIds;
    public IReadOnlyList<ItemChangeInfo> ItemChanges => itemChanges;

    public Task ExecuteAsync(DbConnection connection)
    {
        return connection.InTransactionAsync(async tx =>
        {
            await tx.ExecuteAsync(
                "DELETE FROM t_user_equip WHERE user_id = @userId AND auction_trade_id = 0 AND equip_id IN @equipIds",
                new { userId = User.Uid, equipIds });

            foreach (var change in itemChanges)
            {
                await SaveItemChangesRepository.WriteAsync(tx, User.Uid, change);
            }
        });
    }

    public void Apply()
    {
    }
}
