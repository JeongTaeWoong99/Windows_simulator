using System.Data;
using Dapper;
using MikaNetwork;
using MikaProtocol;

namespace WSGameServer.User.Repository;

/// <summary>
/// 이름(user_name) 기반 로그인. t_user에 없으면 자동 가입 후 로그인한다.
/// ExecuteAsync(DB 스레드)에서 조회/가입하고, Apply(로직 스레드)에서 User 등록·응답 전송.
/// </summary>
public sealed class LoginRepository : IRepository
{

    // ExecuteAsync에서 채우고 Apply에서 사용하는 조회 결과
    private long _userId;

    // DBExecutor 파티션 키 — 같은 세션 작업은 직렬 처리
    public long Key => User.SessionId;

    public User User { get; init; }

    public LoginRepository(User user)
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
                "INSERT INTO t_user (user_name) VALUES (@name) RETURNING user_id",
                new { name = User.NickName });
        }
        
        // 3. Data Fetch 
    }

    // === 로직 스레드에서 실행 ===
    public void Apply()
    {
        User.Login();
    }

    // Dapper 매핑용 DTO (컬럼 snake_case → MatchNamesWithUnderscores로 매핑)
    private sealed record UserRow
    {
        public long UserId { get; set; }
        public long IsBanned { get; set; }
    }
}
