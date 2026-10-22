using DG.Tweening;
using TMPro;
using UnityEngine;

// 토스트 — 확인 없이 화면 아래에 잠깐 떴다 사라지는 안내 한 줄 (T-177).
// 'ServerWaitManager.ToastRaised'를 구독한다. 확인 버튼이 있는 알림('NoticePresenter')과 갈래가 다르다.
//
// ■ 무엇이 여기로 오나
//   완료 안내(경매 등록·구매 완료)와 조작 거절(골드 부족·찾기 중 이동) — 놓쳐도 되고, 매번 클릭을 막으면 흐름이 끊기는 것.
//   놓치면 안 되는 안내·서버가 거절한 결과는 그대로 알림이다('ServerWaitManager.RaiseNotice' 주석).
//
// ■ 거절이면 누른 것을 흔든다
//   흔들 대상은 넘겨받은 것(칸 끌기·우클릭), 없으면 방금 누른 버튼('ButtonPressFx.RecentlyPressed') — 'RefusalShake'.
//
// ■ 막지 않는다
//   'blocksRaycasts'를 늘 끈다 — 떠 있는 동안에도 아래 버튼을 그대로 누를 수 있다.
//   오브젝트는 끄지 않고 'CanvasGroup'으로 보이고 숨긴다(꺼진 오브젝트는 이벤트를 못 받는다 — 알림과 같다).
//
// ■ 한 번에 하나다
//   떠 있는 중에 새 안내가 오면 문구만 바꾸고 시간을 다시 잰다. 같은 문구가 또 오면(연타) 판이 한 번 튄다.
public class ToastPresenter : MonoBehaviour
{
    // 나타나는 시간 · 사라지는 시간 · 떠오르는 거리(px)
    private const float FadeIn   = 0.15f;
    private const float FadeOut  = 0.25f;
    private const float RiseFrom = 14f;

    // 머무는 시간 — 기본 + 글자당, 상한 (초)
    private const float HoldBase    = 1.2f;
    private const float HoldPerChar = 0.04f;
    private const float HoldMax     = 3.5f;

    // 판 크기 — 최대 폭 · 화면 가장자리 여백 · 글자 둘레 여백 (px)
    private const float MaxWidth     = 560f;
    private const float ScreenMargin = 24f;
    private static readonly Vector2 Padding = new Vector2(20f, 12f);

    [CenterHeader("참조")]
    [SerializeField, Tooltip("토스트 몸통의 CanvasGroup. alpha로만 보이고 숨긴다 — 클릭은 늘 통과시킨다")]
    private CanvasGroup group = null!;

    [SerializeField, Tooltip("문구를 담는 판 — 문구 길이에 맞춰 크기가 바뀌고, 떠오르며 나타난다")]
    private RectTransform panel = null!;

    [SerializeField, Tooltip("안내 문구 — 판에 여백을 두고 꽉 차게 둔다(가운데 정렬 · 줄바꿈 켬)")]
    private TMP_Text messageText = null!;

    private ServerWaitManager _wait = null!;

    private Sequence? _seq;
    private Tween?    _bump;
    private float     _restY;   // 판이 머무는 높이 — 씬에 둔 자리
    private string?   _current; // 지금 떠 있는 문구. 숨었으면 null

    private bool _isSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 참조 확보 → 구독 → 초기화 순서로 진행한다 (클라 공통 규약)
    private void Start()
    {
        this.RequireRef(group,       nameof(group));
        this.RequireRef(panel,       nameof(panel));
        this.RequireRef(messageText, nameof(messageText));

        _wait  = Services.Get<ServerWaitManager>();
        _restY = panel.anchoredPosition.y;

        Subscribe();

        group.alpha          = 0f; // 시작은 숨김
        group.blocksRaycasts = false;
        group.interactable   = false;
        _isReady             = true;
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

        _isSubscribed      = true;
        _wait.ToastRaised += OnToastRaised;
    }

    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _isSubscribed      = false;
        _wait.ToastRaised -= OnToastRaised;
    }

    // 안내가 왔다 — 거절이면 누른 것을 흔들고 문구를 띄운다 (ServerWaitManager.ToastRaised 구독)
    private void OnToastRaised(string message, bool refusal, Transform? shake)
    {
        if (refusal)
        {
            Transform? target = shake != null ? shake : ButtonPressFx.RecentlyPressed;

            if (target != null)
            {
                RefusalShake.Play(target);
            }
        }

        Show(message);
    }

    private void Show(string message)
    {
        bool isShown = _current != null;
        bool isSame  = isShown && _current == message;

        _current         = message;
        messageText.text = message;
        FitPanel(message);

        StopSequence();

        _seq = DOTween.Sequence()
                      .SetLink(gameObject, LinkBehaviour.KillOnDisable)
                      .SetUpdate(true);

        if (isShown)
        {
            // 떠 있는 중 — 자리·투명도는 그대로 두고 시간만 다시 잰다
            group.alpha            = 1f;
            panel.anchoredPosition = new Vector2(panel.anchoredPosition.x, _restY);

            if (isSame)
            {
                Bump();
            }
        }
        else
        {
            group.alpha            = 0f;
            panel.anchoredPosition = new Vector2(panel.anchoredPosition.x, _restY - RiseFrom);

            _seq.Append(group.DOFade(1f, FadeIn).SetEase(Ease.OutQuad));
            _seq.Join(panel.DOAnchorPosY(_restY, FadeIn * 1.6f).SetEase(Ease.OutCubic));
        }

        _seq.AppendInterval(Mathf.Min(HoldBase + HoldPerChar * message.Length, HoldMax));
        _seq.Append(group.DOFade(0f, FadeOut).SetEase(Ease.InQuad));
        _seq.OnKill(Settle);
    }

    // 문구 길이에 맞춰 판 크기를 정한다 — 짧으면 좁게, 길면 최대 폭에서 줄을 바꾼다
    private void FitPanel(string message)
    {
        float screenWidth = ((RectTransform)transform).rect.width - ScreenMargin * 2f;
        float textMax     = Mathf.Min(MaxWidth, screenWidth) - Padding.x * 2f;

        float width  = Mathf.Min(messageText.GetPreferredValues(message, textMax, 0f).x, textMax);
        float height = messageText.GetPreferredValues(message, width, 0f).y;

        panel.sizeDelta = new Vector2(width + Padding.x * 2f, height + Padding.y * 2f);
    }

    // 같은 안내가 또 왔다 — 판이 한 번 튀어 "또 막혔다"를 알린다
    private void Bump()
    {
        if (_bump != null && _bump.IsActive())
        {
            _bump.Kill();
        }

        panel.localScale = Vector3.one;

        _bump = panel.DOPunchScale(Vector3.one * 0.08f, 0.25f, 6, 0.6f)
                     .SetUpdate(true)
                     .SetLink(gameObject, LinkBehaviour.KillOnDisable)
                     .OnKill(() => panel.localScale = Vector3.one);
    }

    // 도는 연출을 끊는다 — 끝 상태로 맞추지 않는다(다음 연출이 지금 값에서 이어 간다)
    private void StopSequence()
    {
        Sequence? seq = _seq;

        _seq = null;

        if (seq != null && seq.IsActive())
        {
            seq.OnKill(null);
            seq.Kill();
        }
    }

    // 다 사라졌거나 꺼져 끊겼다 — 숨김 상태로 (OnKill)
    private void Settle()
    {
        _seq                   = null;
        _current               = null;
        group.alpha            = 0f;
        panel.anchoredPosition = new Vector2(panel.anchoredPosition.x, _restY);
    }
}
