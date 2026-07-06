using System.Data;
using Dapper;
using MikaProtocol;

namespace WSGameServer.Repository;

/// <summary>
/// 이름(user_name) 기반 로그인. t_user에 없으면 자동 가입 후 로그인한다.
/// ExecuteAsync(DB 스레드)에서 조회/가입하고, Apply(로직 스레드)에서 User 등록·응답 전송.
/// </summary>
public sealed class LoginRepository : IRepository
{

    // ExecuteAsync에서 채우고 Apply에서 사용하는 조회 결과
    private long _userId;
    private List<ItemInfo> _items = new();

    // DBExecutor 파티션 키 — 같은 세션 작업은 직렬 처리
    public long Key => User.SessionId;

    public User.User User { get; init; }

    public LoginRepository(User.User user)
    {
        User = user;
    }

    // === DB 스레드에서 실행 ===
    public async Task ExecuteAsync(IDbConnection connection)
    {
        // 1) 이름으로 조회
        var row = await connection.QueryFirstOrDefaultAsync<UserRow>(
            "SELECT user_id FROM t_user WHERE nickname = @name",
            new { name = User.NickName});

        // 2) 없으면 자동 가입 (RETURNING으로 새 PK 확보)
        if (row is null)
        {
            _userId = await connection.ExecuteScalarAsync<long>(
                "INSERT INTO t_user (provider_id, nickname) VALUES (@pid, @name) RETURNING user_id",
                new { pid = User.Pid, name = User.NickName });
        }
        else
        {
            _userId = row.UserId;
        }

        // 3. Data Fetch — 인벤토리 로드(AddItemRepository와 동일하게 account user_id = User.Uid 기준)
        // DB는 InventoryRow로 받고, User에 넘길 때 네트워크/전달용 ItemInfo로 변환한다.
        var inventoryRows = await connection.QueryAsync<InventoryRow>(
            "SELECT item_id, count FROM t_inventory WHERE user_id = @userId",
            new { userId = User.Uid });

        _items = inventoryRows
            .Select(r => new ItemInfo { ItemId = r.ItemId, Count = r.Count })
            .ToList();
    }

    // === 로직 스레드에서 실행 ===
    public void Apply()
    {
        User.LoadDB(_items);   // DB에서 읽어온 데이터 일괄 적재
        User.Login();          // S_LoginResponse
    }

    // Dapper 매핑용 DTO (컬럼 snake_case → MatchNamesWithUnderscores로 매핑)
    private sealed record UserRow
    {
        public long UserId { get; set; }
        public long IsBanned { get; set; }
    }

    // t_inventory 조회 전용 Row (Protocol의 ItemInfo와 분리)
    private sealed record InventoryRow
    {
        public int ItemId { get; set; }
        public int Count { get; set; }
    }
}
