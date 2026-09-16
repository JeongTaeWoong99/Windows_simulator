using WSGameServer;

namespace WSGameServer;

public interface IRepository
{
    public long Key { get; }

    public User User { get; }

    // === DB 스레드에서 실행 ===
    public Task ExecuteAsync(DbConnection connection);

    // === 로직 스레드에서 실행 === ExecuteAsync가 성공했을 때만 불린다.
    public void Apply();

    // === 로직 스레드에서 실행 === ExecuteAsync가 예외로 끝났을 때 Apply 대신 불린다.
    // 기본 정책은 세션 종료다 — 메모리와 DB가 갈라진 채 두지 않고 재접속 때 DB를 다시 읽게 한다.
    public void OnFailed(Exception e) => User.OnDbFailed(GetType().Name, e);
}