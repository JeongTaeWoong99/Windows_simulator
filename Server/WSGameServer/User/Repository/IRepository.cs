namespace WSGameServer.User.Repository;

public interface IRepository
{
    public long GetKey();
    public Task Execute();
    public void Apply();
}