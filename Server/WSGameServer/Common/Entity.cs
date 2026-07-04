using MikaNetwork.Server;

namespace WSGameServer.Common;

public abstract class Entity
{
    private bool _created;
    
    protected virtual void OnCreate()   {}
    protected virtual void OnDestroy()  {}
    protected virtual void OnUpdate()   {}

    public virtual ulong Key { get; } = AllocKey64();
    public virtual ulong GetJobId() { return Key; }

    public bool Create()
    {
        if (_created) return false;
        
        _created = true;
        
        Post(OnCreate);

        return true;
    }

    public void Destroy()
    {
        Post(OnDestroy);
    }
    
    public void Post(ulong id, Action job)
    {
        LogicExecutor.Instance.Post(job);
    }

    public void Post(Action job)
    {
        Post(Key, job);
    }
    
}