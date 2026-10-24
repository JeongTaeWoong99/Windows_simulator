using MikaProtocol;

namespace WSGameServer;

/// <summary>인벤토리 아티팩트 탭(<c>t_user_artifact_stack</c>)의 칸 쓰기 — 아티팩트 칸을 바꾸는 저장은 전부 이 문장을 지난다 (T-181).</summary>
public static class ArtifactStackDb
{
    /// <summary>칸 하나의 확정 상태를 쓴다. 수량 0이면 행을 지운다. <c>ItemId</c>는 아티팩트 번호다.</summary>
    public static Task WriteAsync(DbConnection tx, long userId, ItemChangeInfo change)
    {
        if (change.Count == 0)
        {
            return tx.ExecuteAsync(
                "DELETE FROM t_user_artifact_stack WHERE user_id = @userId AND slot = @slot",
                new { userId, slot = change.Slot });
        }

        return tx.ExecuteAsync(
            @"INSERT INTO t_user_artifact_stack (user_id, slot, artifact_tid, count)
              VALUES (@userId, @slot, @artifactTid, @count)
              ON CONFLICT (user_id, slot) DO UPDATE SET artifact_tid = excluded.artifact_tid, count = excluded.count;",
            new { userId, slot = change.Slot, artifactTid = change.ItemId, count = change.Count });
    }

    public static async Task WriteAllAsync(DbConnection tx, long userId, IReadOnlyList<ItemChangeInfo> changes)
    {
        foreach (var change in changes)
        {
            await WriteAsync(tx, userId, change);
        }
    }
}

/// <summary>아티팩트 칸 변경분을 한 트랜잭션으로 쓴다(감정 · 우편 수령 · 이동·정렬).</summary>
public sealed class SaveArtifactStacksRepository : IRepository
{
    private readonly List<ItemChangeInfo> _changes;

    // 목록을 복사해 둔다 — 넘긴 쪽이 로직 스레드에서 목록을 고쳐도 DB 스레드의 순회가 깨지지 않게.
    public SaveArtifactStacksRepository(User user, List<ItemChangeInfo> changes)
    {
        User     = user;
        _changes = new List<ItemChangeInfo>(changes);
    }

    public IReadOnlyList<ItemChangeInfo> Changes => _changes;

    public long Key => User.DbKey;

    public User User { get; }

    public Task ExecuteAsync(DbConnection connection)
        => connection.InTransactionAsync(tx => ArtifactStackDb.WriteAllAsync(tx, User.Uid, _changes));

    public void Apply()
    {
    }
}

/// <summary>도감 장착 — 칸 1개 차감과 도감 행을 같은 트랜잭션에 쓴다. 둘이 갈라지면 아티팩트가 복제·증발한다.</summary>
public sealed class EquipArtifactRepository : IRepository
{
    private readonly List<ItemChangeInfo> _changes;

    public EquipArtifactRepository(User user, int artifactTid, List<ItemChangeInfo> changes)
    {
        User        = user;
        ArtifactTid = artifactTid;
        _changes    = new List<ItemChangeInfo>(changes);
    }

    public int ArtifactTid { get; }

    public IReadOnlyList<ItemChangeInfo> Changes => _changes;

    public long Key => User.DbKey;

    public User User { get; }

    public Task ExecuteAsync(DbConnection connection)
    {
        return connection.InTransactionAsync(async tx =>
        {
            await ArtifactStackDb.WriteAllAsync(tx, User.Uid, _changes);
            await tx.ExecuteAsync(
                "INSERT INTO t_user_artifact (user_id, artifact_tid, is_active) VALUES (@userId, @artifactTid, 1);",
                new { userId = User.Uid, artifactTid = ArtifactTid });
        });
    }

    public void Apply()
    {
    }
}

/// <summary>도감에 장착한 번호의 켜짐 여부를 쓴다.</summary>
public sealed class SaveArtifactActiveRepository : IRepository
{
    public SaveArtifactActiveRepository(User user, int artifactTid, bool isActive)
    {
        User        = user;
        ArtifactTid = artifactTid;
        IsActive    = isActive;
    }

    public int ArtifactTid { get; }

    public bool IsActive { get; }

    public long Key => User.DbKey;

    public User User { get; }

    public async Task ExecuteAsync(DbConnection connection)
    {
        await connection.ExecuteAsync(
            "UPDATE t_user_artifact SET is_active = @isActive WHERE user_id = @userId AND artifact_tid = @artifactTid;",
            new { userId = User.Uid, artifactTid = ArtifactTid, isActive = IsActive ? 1 : 0 });
    }

    public void Apply()
    {
    }
}

/// <summary>아티팩트 즉시 판매 — 칸 차감과 골드 확정 잔액을 같은 트랜잭션에 쓴다.</summary>
public sealed class SellArtifactsRepository : IRepository
{
    private readonly List<ItemChangeInfo> _changes;

    public SellArtifactsRepository(User user, List<ItemChangeInfo> changes, long gold)
    {
        User     = user;
        _changes = new List<ItemChangeInfo>(changes);
        Gold     = gold;
    }

    public IReadOnlyList<ItemChangeInfo> Changes => _changes;

    public long Gold { get; }

    public long Key => User.DbKey;

    public User User { get; }

    public Task ExecuteAsync(DbConnection connection)
    {
        return connection.InTransactionAsync(async tx =>
        {
            await ArtifactStackDb.WriteAllAsync(tx, User.Uid, _changes);

            // 델타가 아니라 확정 잔액을 쓴다 — 재시도·중복 전송이 곧 재화 복제가 된다.
            await tx.ExecuteAsync(
                @"INSERT INTO t_user_currency (user_id, gold)
                  VALUES (@userId, @gold)
                  ON CONFLICT (user_id) DO UPDATE SET gold = excluded.gold;",
                new { userId = User.Uid, gold = Gold });
        });
    }

    public void Apply()
    {
    }
}
