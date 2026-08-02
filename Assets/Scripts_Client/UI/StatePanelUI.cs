using TMPro;
using UnityEngine;

/// <summary>
/// 상태 패널 — 계정 이름과 골드를 표시한다.
///
/// 닉네임은 아직 서버가 돌려주지 않는다. 로그인에 쓴 Id(<see cref="SessionManager.LoginId"/>)를
/// 그대로 보여 주고, 닉네임 패킷이 생기면 그때 바꾼다.
/// </summary>
public class StatePanelUI : MonoBehaviour
{
    [CenterHeader("< 참조 >")]
    [SerializeField, Tooltip("계정 이름. 지금은 로그인 Id를 그대로 표시한다")]
    private TMP_Text nickNameText = null!;

    [SerializeField, Tooltip("골드 보유량")]
    private TMP_Text goldText = null!;

    private SessionManager _session = null!;
    private bool           _isSubscribed;

    // 서비스 확보 후 최초 구독 (Unity 메시지)
    // ※ OnEnable에서 Get 하지 않는다 — MonoService 주석의 초기화 순서 규칙 참조.
    private void Start()
    {
        // 필수 참조 검증 — 미연결이면 여기서 멈춘다(WindowPanelUI와 같은 규칙).
        this.RequireRef(nickNameText, nameof(nickNameText));
        this.RequireRef(goldText,     nameof(goldText));

        _session = Services.Get<SessionManager>();
        Subscribe();
        Refresh(); // 이미 통지를 받은 뒤에 켜졌을 수 있다
    }

    // 껐다 켠 경우의 재구독 (Unity 메시지)
    private void OnEnable()
    {
        if (_session != null)
            Subscribe();
    }

    // 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        Unsubscribe();
    }

    // 로그인·재화 변경 구독 (Start · OnEnable에서 호출)
    private void Subscribe()
    {
        if (_isSubscribed)
            return;

        _isSubscribed             = true;
        _session.CurrencyChanged += Refresh;
        _session.LoginCompleted  += OnLoginCompleted;
    }

    // 구독 해제 (OnDisable에서 호출)
    private void Unsubscribe()
    {
        if (!_isSubscribed)
            return;

        _isSubscribed             = false;
        _session.CurrencyChanged -= Refresh;
        _session.LoginCompleted  -= OnLoginCompleted;
    }

    // 로그인 결과 도착 — 성공했을 때만 이름을 갱신한다 (LoginCompleted 구독)
    private void OnLoginCompleted(bool success)
    {
        if (success)
            Refresh();
    }

    // 이름·골드를 현재 세션 값으로 갱신한다 (CurrencyChanged 구독 · 로그인 시)
    private void Refresh()
    {
        nickNameText.text = string.IsNullOrEmpty(_session.LoginId) ? "-" : _session.LoginId;
        goldText.text     = _session.Gold.ToString("N0"); // 천 단위 구분
    }
}
