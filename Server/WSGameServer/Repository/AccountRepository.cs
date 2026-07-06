using System.Data;
using Dapper;

namespace WSGameServer.Repository;

public class AccountRepository : IRepository
{
    private long _userId;
    private bool _isNewbie;
    
    private AccountResultRow? _resultRow;
    
    public AccountRepository(User.User user)
    {
        User = user;
    }

    public long Key { get => User.SessionId; }

    public User.User User { get; init; }
    
    public async Task ExecuteAsync(IDbConnection connection)
    {
        var row = await connection.QueryFirstOrDefaultAsync<AccountQueryRow>(
            "SELECT user_id FROM t_account WHERE provider_id = @pid",
        new {pid = User.Pid});

        // 2) 없으면 자동 가입 
        if (row is null)
        {
            _userId = await connection.ExecuteScalarAsync<long>(
                "INSERT INTO t_account (provider_id, nickname) VALUES (@pid, @nickname) RETURNING user_id",
                new { pid = User.Pid, nickname = User.NickName });

            _isNewbie = true;
        }
        else
        {
            _userId = row.UserId; // 있으면 _userId에 넣어주기
        }
        
        
        // 3) AccountResultRow 채워주기
        _resultRow = await connection.QueryFirstOrDefaultAsync<AccountResultRow>(
            "SELECT user_id, nickname, admin_level, is_deleted, is_banned FROM t_account WHERE user_id = @userId",
            new {userId = _userId});
    }
    
    public void Apply()
    {
        if (_resultRow == null)
        {
            return;
        }
        
        // 밴
        if (_resultRow.IsBanned == 1)
        {
            // Log
            return;
        }
        
        // 삭제
        if (_resultRow.IsDeleted == 1)
        {
            //Log
            return;
        }

        User.Initialize(_userId, _resultRow.NickName, _resultRow.AdminLevel, _isNewbie);

    }

    private sealed record AccountQueryRow
    {
        public long UserId { get; init; }
    }

    private sealed record AccountResultRow
    {
        public long UserId { get; init; }
        public required string NickName { get; init; }
        public int AdminLevel { get; init; }
        public int IsDeleted { get; init; }
        public int IsBanned { get; init; }
        //public int IsNewBie { get; init; }
    }
}