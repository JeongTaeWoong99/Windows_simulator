using System.Data;
using Dapper;

namespace WSGameServer;

/// <summary>
/// DB 스레드에서 리포지토리에게 건네주는 쿼리 실행기. Dapper 호출 관례(AsList 등)를 한 곳에 모은다 —
/// 리포지토리는 커넥션이 아니라 이 타입의 메서드만 안다.
/// </summary>
public sealed class DbConnection(IDbConnection conn, IDbTransaction? tx = null)
{
    /// <summary>여러 행 조회. List로 바로 받는다.</summary>
    public async Task<List<T>> QueryAsync<T>(string sql, object? param = null)
        => (await conn.QueryAsync<T>(sql, param, tx)).AsList();

    /// <summary>한 행 조회. 없으면 null.</summary>
    public Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null)
        => conn.QueryFirstOrDefaultAsync<T>(sql, param, tx);

    /// <summary>INSERT/UPDATE/DELETE. 영향 행 수를 돌려준다.</summary>
    public Task<int> ExecuteAsync(string sql, object? param = null)
        => conn.ExecuteAsync(sql, param, tx);

    /// <summary>단일 값 조회 (RETURNING 등).</summary>
    public Task<T> ExecuteScalarAsync<T>(string sql, object? param = null)
        => conn.ExecuteScalarAsync<T>(sql, param, tx)!;

    /// <summary>
    /// 콜백 안의 쓰기를 한 트랜잭션으로 묶는다. 예외가 나면 롤백하고 그대로 던진다 —
    /// 인벤토리 차감과 재화 지급처럼 <b>함께 성공하거나 함께 실패해야</b> 하는 쌍에 쓴다.
    /// </summary>
    public async Task InTransactionAsync(Func<DbConnection, Task> body)
    {
        using var transaction = conn.BeginTransaction();

        try
        {
            await body(new DbConnection(conn, transaction));
        }
        catch
        {
            transaction.Rollback();
            throw;
        }

        transaction.Commit();
    }
}
