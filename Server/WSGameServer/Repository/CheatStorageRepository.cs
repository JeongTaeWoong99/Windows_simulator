using MikaProtocol;

namespace WSGameServer;

/// <summary>레벨과 장착 해제를 함께 저장해 재접속 후에도 장착 조건이 맞도록 한다.</summary>
public sealed class SetCheatCharacterLevelsRepository(
    User user, IReadOnlyList<long> characterIds, int level, IReadOnlyList<EquipMappingChange> removedMappings) : IRepository
{
    public long Key => User.DbKey;
    public User User { get; } = user;

    public Task ExecuteAsync(DbConnection connection)
    {
        return connection.InTransactionAsync(async tx =>
        {
            foreach (var characterId in characterIds)
            {
                await SaveCharacterGrowthRepository.WriteAsync(tx, User.Uid, characterId, level, exp: 0);
            }

            await SaveCharacterEquipRepository.WriteAsync(tx, removedMappings);
        });
    }

    public void Apply()
    {
    }
}

/// <summary>선택한 보관함의 자원 칸과 소유 개체 삭제를 한 트랜잭션에 적용한다.</summary>
public sealed class ClearCheatItemsRepository(
    User user, IReadOnlyList<ItemChangeInfo> itemChanges, IReadOnlyList<long> equipIds, IReadOnlyList<long> characterIds) : IRepository
{
    public long Key => User.DbKey;
    public User User { get; } = user;

    public Task ExecuteAsync(DbConnection connection)
    {
        return connection.InTransactionAsync(async tx =>
        {
            foreach (var change in itemChanges)
            {
                await SaveItemChangesRepository.WriteAsync(tx, User.Uid, change);
            }

            await SellEntitiesRepository.DeleteOwnedAsync(tx, User.Uid, characterIds, equipIds);
        });
    }

    public void Apply()
    {
    }
}
