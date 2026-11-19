namespace WSGameServer;

internal sealed class RaidRequestRepository<T>(User user, Func<DbConnection, Task<T>> execute, Action<T> apply) : IRepository
{
    private T _result = default!;

    public long Key => User.DbKey;
    public User User { get; } = user;

    public async Task ExecuteAsync(DbConnection connection)
    {
        _result = await execute(connection);
    }

    public void Apply()
    {
        if (!User.IsDestroyed)
        {
            apply(_result);
        }
    }
}
