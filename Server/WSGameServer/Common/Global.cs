namespace WSGameServer.Common;

public static class Global
{
    private static ulong _seq = 1; 
    
    public static ulong AllocKey64()
    {
        return Interlocked.Increment(ref _seq);
    }
}

