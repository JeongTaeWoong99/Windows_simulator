using MikaNetwork.Server;

namespace WSGameServer;

public abstract class Entity
{
    private bool _created;

    // Destroy는 여러 곳에서 들어온다(소켓 해제·유휴 스윕·중복 로그인 정리).
    // 가드가 없으면 OnDestroy가 여러 번 큐에 실려 종료 정산이 중복된다.
    private int _destroyed;

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

    /// <summary>정리를 예약한다. <b>여러 번 불러도 한 번만 실행된다.</b></summary>
    /// <returns>이 호출이 실제로 정리를 예약했으면 true.</returns>
    public bool Destroy()
    {
        if (Interlocked.Exchange(ref _destroyed, 1) == 1)
            return false;

        Post(OnDestroy);
        return true;
    }

    /// <summary>정리가 이미 예약됐는지. 좀비 판정처럼 "이 객체를 아직 살아 있다고 볼지"에 쓴다.</summary>
    public bool IsDestroyed => Volatile.Read(ref _destroyed) == 1;
    
    public void Post(ulong id, Action job)
    {
        LogicExecutor.Instance.Post(job);
    }

    public void Post(Action job)
    {
        Post(Key, job);
    }
    
}