using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 알림 표시 — 요청 실패·타임아웃, 또는 연결 계층 치명 오류의 사유를 화면에 띄운다.
// 사용자가 닫기(확인)를 눌러야 사라진다(조용히 자동으로 닫지 않는다).
// 짧은 안내·조작 거절은 여기가 아니라 'ToastPresenter'다 — 확인 없이 사라진다(T-177).
//
// ■ 두 갈래
// - 일반 알림('NoticeRaised') — 닫기로 확인하고 앱은 계속된다.
// - 치명 알림('FatalRaised', 최초 접속 실패·연결 끊김) — 닫기가 곧 앱 종료다
//   (에디터에서는 'WindowManager.QuitApplication'이 플레이 모드를 멈춘다).
//
// ⚠️ 오브젝트를 끄지 않고 'CanvasGroup'으로 표시/숨김한다 — 자기 자신을 끄면 다시 켤 이벤트를
// 받지 못한다(꺼진 오브젝트는 콜백이 오지 않는다). alpha로 보이고, blocksRaycasts로 뒤 UI를 막는다.
// 로딩과 달리 지연 표시는 없다 — 알림은 사용자 확인형이라 깜빡임 대상이 아니다.
public class NoticePresenter : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("알림 몸통의 CanvasGroup. alpha·blocksRaycasts로 표시/숨김한다(오브젝트는 끄지 않는다)")]
    private CanvasGroup group = null!;

    [SerializeField, Tooltip("가운데 판 — 열 때 살짝 커지며 나타난다. 비우면 알림 전체가 커진다")]
    private RectTransform? panel;

    [SerializeField, Tooltip("알림 사유 문구")]
    private TMP_Text messageText = null!;

    [SerializeField, Tooltip("닫기(확인) 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button closeButton = null!;

    private ServerWaitManager _wait   = null!;
    private WindowManager     _window = null!;

    // 지금 떠 있는 알림이 치명(연결 계층)인가 — 닫기가 앱 종료로 이어진다.
    private bool _isFatal;

    // 지금 알림이 떠 있는가 — 치명 알림을 일반 알림이 덮지 않게 판단하는 데 쓴다.
    private bool _isShown;

    private bool _isSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 참조 확보 → 구독 → 초기화 순서로 진행한다 (클라 공통 규약)
    private void Start()
    {
        this.RequireRef(group,       nameof(group));
        this.RequireRef(messageText, nameof(messageText));
        this.RequireRef(closeButton, nameof(closeButton));

        _wait   = Services.Get<ServerWaitManager>();
        _window = Services.Get<WindowManager>();

        Subscribe();
        closeButton.onClick.AddListener(OnCloseClicked);

        SetVisible(false, instant: true); // 시작은 숨김 — 알림이 생기면 켜진다
        _isReady = true;
    }

    // 껐다 켠 경우의 재구독 (Unity 메시지)
    private void OnEnable()
    {
        if (_isReady)
        {
            Subscribe();
        }
    }

    // 구독 해제 (Unity 메시지)
    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _isSubscribed       = true;
        _wait.NoticeRaised += OnNoticeRaised;
        _wait.FatalRaised  += OnFatalRaised;
    }

    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _isSubscribed       = false;
        _wait.NoticeRaised -= OnNoticeRaised;
        _wait.FatalRaised  -= OnFatalRaised;
    }

    // 요청 실패·타임아웃 — 사용자가 닫기로 확인하면 사라진다 (ServerWaitManager.NoticeRaised 구독)
    // ※ 치명 알림이 이미 떠 있으면 덮지 않는다 — 종료 출구(치명 닫기)가 일반 알림에 가려지지 않게 한다.
    private void OnNoticeRaised(string message)
    {
        if (_isFatal && _isShown)
        {
            return;
        }

        _isFatal = false;
        Show(message);
    }

    // 연결 계층 치명 — 닫기가 앱 종료로 이어진다 (ServerWaitManager.FatalRaised 구독)
    private void OnFatalRaised(string message)
    {
        _isFatal = true;
        Show(message);
    }

    private void Show(string message)
    {
        messageText.text = message;
        SetVisible(true);
    }

    // 닫기(확인) — 치명이면 앱을 종료하고, 아니면 알림만 내린다 (closeButton OnClick에 코드로 연결)
    private void OnCloseClicked()
    {
        if (_isFatal)
        {
            ClientLogger.Warn(ClientLogger.Network, "치명 오류를 사용자가 확인 — 앱을 종료한다.", this);
            _window.QuitApplication();

            return;
        }

        SetVisible(false);
    }

    // 몸통을 켜고 끈다 — 오브젝트는 항상 활성이라 이벤트를 계속 받는다(자기를 끄지 않는다).
    // 연출은 'PanelTransition'(확인 팝업과 같다 · T-177) — 차단(모달)·입력은 여는 즉시 켜고 닫는 즉시 끈다.
    private void SetVisible(bool on, bool instant = false)
    {
        _isShown = on;

        PanelTransition.SetVisible(group, on, panel, instant);
    }
}
