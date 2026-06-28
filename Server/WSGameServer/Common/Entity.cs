using MikaNetwork.Server;

namespace WSGameServer.Common;

public abstract class Entity
{
    private readonly ulong _key = AllocKey64();

    protected virtual void OnCreate()   {}
    protected virtual void OnDestroy()  {}
    protected virtual void OnUpdate()   {}

    public virtual ulong GetKey() { return _key; }
    public virtual ulong GetJobId() { return GetKey(); }

    public void Post(ulong id, Action job)
    {
        LogicExecutor.Instance.Post(job);
    }

    public void Post(Action job)
    {
        Post(GetKey(), job);
    }
    
}