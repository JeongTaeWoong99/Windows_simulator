using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 서버 왕복 중의 사용자 피드백(로딩 표시·응답 타임아웃·오류 알림)을 한 곳에 모은 단일 창구
/// (서비스 로케이터 등록).
/// </summary>
/// <remarks>
/// 콜백 수집기가 아니다 — 서버 패킷은 받지 않는다. 요청을 보낸 Presenter가 <c>Begin</c>으로 대기를 열고,
/// 자기 응답 이벤트에서 <c>Succeed</c>/<c>Fail</c>로 결과만 보고한다. 아무 보고도 없는 무응답만 이 창구가
/// 타이머로 잡는다.
///
/// ⚠️ 연결이 살아 있는지(주기적 하트비트)는 'PingManager'가 본다 — 축이 다르다.
/// 여기는 "방금 보낸 요청 한 건이 돌아왔나"만 다룬다. 둘이 만나는 곳은 하나뿐이다 —
/// 'PingManager'가 연결 끊김을 판정하면 그 알림을 띄울 창구로 <c>RaiseFatal</c>을 빌려 쓴다.
/// </remarks>
public class ServerWaitManager : MonoService<ServerWaitManager>
{
    // 요청 응답을 이만큼 기다린다. 넘기면 무응답으로 보고 알림을 띄운다.
    // ※ 모든 요청 공통 단일 기준값 — 지점마다 다르게 두지 않는다(정책상 5초).
    private const float RequestTimeoutSeconds = 5f;

    // 진행 중인 대기 수. 0→1에서 로딩을 켜고, 1→0에서 끈다.
    private int _activeCount;

    /// <summary>대기 중인 요청이 하나라도 있는가 — 로딩 표시의 on/off ('LoadingPresenter'가 구독).</summary>
    public event Action<bool>? BusyChanged;

    /// <summary>요청이 실패했거나 응답이 없었다 — 사용자에게 보일 문구 ('NoticePresenter'가 구독).</summary>
    public event Action<string>? NoticeRaised;

    /// <summary>연결 계층 치명 오류 — 알림 확인 후 종료로 이어진다 ('NoticePresenter'가 구독).</summary>
    public event Action<string>? FatalRaised;

    /// <summary>
    /// 서버 요청 하나의 대기를 시작한다 — 로딩을 띄우고 응답까지 감시한다 (요청을 보낸 직후 호출).
    /// 돌려준 핸들에 응답이 오면 <c>Succeed</c>/<c>Fail</c>을 부른다.
    /// </summary>
    /// <param name="label">타임아웃 문구에 쓸 요청 이름 (예: "로그인").</param>
    /// <param name="onClosed">대기가 끝날 때(성공·실패·타임아웃 공통) 호출 — 호출부의 버튼 잠금 해제에 쓴다.</param>
    public ServerWaitHandle Begin(string label, Action? onClosed = null)
    {
        var handle = new ServerWaitHandle(this, onClosed);

        _activeCount++;
        if (_activeCount == 1)
            BusyChanged?.Invoke(true);

        handle.Timeout = StartCoroutine(WatchTimeout(handle, label));
        return handle;
    }

    // 성공·실패·타임아웃 어느 쪽이든 대기 하나를 끝낸다 (핸들·타임아웃 코루틴이 호출).
    // 이미 닫힌 핸들은 무시하므로, 타임아웃 뒤 늦게 온 응답은 아무 일도 하지 않는다.
    internal void Resolve(ServerWaitHandle handle, string? failMessage)
    {
        if (handle.IsClosed)
            return;

        handle.Close(); // 닫힘 표시 + onClosed 호출

        if (handle.Timeout != null)
        {
            StopCoroutine(handle.Timeout);
            handle.Timeout = null;
        }

        if (failMessage != null)
            NoticeRaised?.Invoke(failMessage);

        _activeCount--;
        if (_activeCount <= 0)
        {
            _activeCount = 0;
            BusyChanged?.Invoke(false);
        }
    }

    // 제한 시간까지 성공·실패 보고가 없으면 스스로 끝내고 알린다 (Begin이 시작).
    // 타임스케일이 0이어도(일시정지 연출 등) 대기는 실제 시간으로 흘러야 한다.
    private IEnumerator WatchTimeout(ServerWaitHandle handle, string label)
    {
        yield return new WaitForSecondsRealtime(RequestTimeoutSeconds);

        handle.Timeout = null; // 이 코루틴은 이미 끝났다 — Resolve가 StopCoroutine하지 않게 비운다
        Resolve(handle, $"'{label}' 응답이 {RequestTimeoutSeconds:F0}초 안에 오지 않았습니다. 잠시 후 다시 시도해 주세요.");
    }

    /// <summary>
    /// 연결 계층 치명 오류를 알린다 (최초 접속 실패·게임 중 연결 끊김 — 'PingManager'가 호출).
    /// 개별 요청 대기와 달리 앱 종료로 이어지므로 별도 이벤트로 낸다.
    /// </summary>
    public void RaiseFatal(string message) => FatalRaised?.Invoke(message);
}

/// <summary>
/// 'ServerWaitManager.Begin'이 돌려주는 대기 한 건의 손잡이. 응답을 받은 Presenter가
/// <c>Succeed</c>/<c>Fail</c>로 결과를 보고한다. 한 번 닫히면 이후 호출은 무시된다
/// (타임아웃이 먼저 닫은 뒤 늦게 온 응답을 안전하게 흘려보낸다).
/// </summary>
public sealed class ServerWaitHandle
{
    private readonly ServerWaitManager _owner;
    private readonly Action?           _onClosed;

    // 이 대기의 타임아웃 코루틴 핸들. 매니저가 시작·정리한다.
    internal Coroutine? Timeout;

    private bool _isClosed;

    /// <summary>이미 끝난 대기인가 (성공·실패·타임아웃 중 하나로 닫혔다).</summary>
    public bool IsClosed => _isClosed;

    internal ServerWaitHandle(ServerWaitManager owner, Action? onClosed)
    {
        _owner    = owner;
        _onClosed = onClosed;
    }

    /// <summary>정상 응답을 받았다 — 로딩을 조용히 내린다.</summary>
    public void Succeed() => _owner.Resolve(this, null);

    /// <summary>실패·오류 응답을 받았다 — 로딩을 내리고 알림 문구를 띄운다.</summary>
    public void Fail(string message) => _owner.Resolve(this, message);

    // 매니저가 부른다 — 한 번만 닫히고 onClosed를 알린다.
    internal void Close()
    {
        _isClosed = true;
        _onClosed?.Invoke();
    }
}
