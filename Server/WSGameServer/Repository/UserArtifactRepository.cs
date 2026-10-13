namespace WSGameServer;

/// <summary>
/// 아티팩트 번호별 수량을 <c>t_user_artifact</c>에 기록한다. 마지막 값으로 덮어쓰고, 처음 얻은 시각은 처음 행을 만들 때만 적는다.
/// </summary>
public sealed class SaveUserArtifactRepository : IRepository
{
    public IReadOnlyList<(int ArtifactTid, int Count)> Changes { get; }

    public SaveUserArtifactRepository(User user, IReadOnlyList<(int ArtifactTid, int Count)> changes)
    {
        User    = user;
        Changes = changes;
    }

    public long Key => User.DbKey;

    public User User { get; }

    public async Task ExecuteAsync(DbConnection connection)
    {
        foreach (var (artifactTid, count) in Changes)
        {
            await connection.ExecuteAsync(
                @"INSERT INTO t_user_artifact (user_id, artifact_tid, count)
                  VALUES (@userId, @artifactTid, @count)
                  ON CONFLICT (user_id, artifact_tid) DO UPDATE SET count = excluded.count;",
                new { userId = User.Uid, artifactTid, count });
        }
    }

    public void Apply()
    {
    }
}
