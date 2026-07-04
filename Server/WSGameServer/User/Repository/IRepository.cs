using System.Data;

namespace WSGameServer.User.Repository;

public interface IRepository
{
    public long Key { get;  }
    
    public Task ExecuteAsync(IDbConnection connection);
    public void Apply();
}