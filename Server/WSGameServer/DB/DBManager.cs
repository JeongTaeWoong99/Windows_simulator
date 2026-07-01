using MikaNetwork.Server;
using MikaUtils;
using WSGameServer.User.Repository;

namespace WSGameServer.DB;

public class DBManager : Singleton<DBManager>
{
    public void Post(IRepository repository)
    {
        DBExecutor.Instance.Post(repository.GetKey(), async () =>
        {
            await repository.Execute(); // SP 실행 -- 다른 스레드가 작업 이어서 할 수 있음 (순서는 보장)

            LogicExecutor.Instance.Post(repository.Apply);
        });
    }
}