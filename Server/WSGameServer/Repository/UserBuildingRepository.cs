namespace WSGameServer;

/// <summary>
/// 건물 하나의 레벨을 <c>t_user_building</c>에 기록한다. 마지막 값으로 덮어쓴다 — 치트로는 내려가기도 한다.
/// </summary>
public sealed class SaveUserBuildingRepository : IRepository
{
    public int BuildingTid { get; }
    public int Level       { get; }

    public SaveUserBuildingRepository(User user, int buildingTid, int level)
    {
        User        = user;
        BuildingTid = buildingTid;
        Level       = level;
    }

    public long Key => User.DbKey;

    public User User { get; }

    public async Task ExecuteAsync(DbConnection connection)
    {
        await connection.ExecuteAsync(
            @"INSERT INTO t_user_building (user_id, building_tid, level)
              VALUES (@userId, @buildingTid, @level)
              ON CONFLICT (user_id, building_tid) DO UPDATE SET level = excluded.level;",
            new { userId = User.Uid, buildingTid = BuildingTid, level = Level });
    }

    public void Apply()
    {
    }
}
