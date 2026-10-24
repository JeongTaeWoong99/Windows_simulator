using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

// 토스트 — 확인 없이 안내가 난 열의 가운데에 잠깐 떴다 사라지는 안내 한 줄 (T-177).
// 'ServerWaitManager.ToastRaised'를 구독한다. 확인 버튼이 있는 알림('NoticePresenter')과 갈래가 다르다.
//
// ■ 무엇이 여기로 오나
//   완료 안내(경매 등록·구매 완료)와 조작 거절(골드 부족·찾기 중 이동) — 놓쳐도 되고, 매번 클릭을 막으면 흐름이 끊기는 것.
//   놓치면 안 되는 안내·서버가 거절한 결과는 그대로 알림이다('ServerWaitManager.RaiseNotice' 주석).
//
// ■ 거절이면 누른 것을 흔든다
//   흔들 대상은 넘겨받은 것(칸 끌기·우클릭), 없으면 방금 누른 버튼('ButtonPressFx.RecentlyPressed') — 'RefusalShake'.
//
// ■ 안내가 난 열의 가운데에 뜬다
//   인벤토리 열은 설정에 따라 왼쪽·오른쪽으로 옮겨 간다 — 화면 가운데에 띄우면 다른 열 위에 떠 어디 이야기인지 끊긴다.
//   출처는 넘겨받은 것(흔들 대상) → 방금 누른 버튼 → 커서 밑 UI 순으로 찾고, 그것을 품은 열('columns')의 가운데에 띄운다.
//   폭도 그 열 안으로 줄인다. 어느 열에도 없으면(경매장처럼 열을 덮는 화면 · 서버 알림) 씬에 둔 자리(화면 가운데)다.
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

    [SerializeField, Tooltip("메인 열 — 안내가 이 안에서 났으면 그 열의 가운데에 뜬다. 어느 열에도 없으면 씬에 둔 자리")]
    private RectTransform[] columns = System.Array.Empty<RectTransform>();

    private ServerWaitManager _wait   = null!;
    private WindowManager     _window = null!;

    private Sequence? _seq;
    private Tween?    _bump;
    private Vector2   _home;    // 열을 모를 때의 자리 — 씬에 둔 자리
    private Vector2   _rest;    // 지금 판이 머무는 자리
    private string?   _current; // 지금 떠 있는 문구. 숨었으면 null

    private bool _isSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 참조 확보 → 구독 → 초기화 순서로 진행한다 (클라 공통 규약)
    private void Start()
    {
        this.RequireRef(group,       nameof(group));
        this.RequireRef(panel,       nameof(panel));
        this.RequireRef(messageText, nameof(messageText));

        _wait   = Services.Get<ServerWaitManager>();
        _window = Services.Get<WindowManager>();
        _home   = panel.anchoredPosition;
        _rest   = _home;

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

        Show(message, FindColumn(shake));
    }

    private void Show(string message, RectTransform? column)
    {
        bool isShown = _current != null;
        bool isSame  = isShown && _current == message;

        _current         = message;
        messageText.text = message;
        _rest            = column != null ? CenterOf(column) : _home;
        FitPanel(message, column);

        StopSequence();

        _seq = DOTween.Sequence()
                      .SetLink(gameObject, LinkBehaviour.KillOnDisable)
                      .SetUpdate(true);

        if (isShown)
        {
            // 떠 있는 중 — 자리·투명도는 그대로 두고 시간만 다시 잰다
            group.alpha            = 1f;
            panel.anchoredPosition = _rest;

            if (isSame)
            {
                Bump();
            }
        }
        else
        {
            group.alpha            = 0f;
            panel.anchoredPosition = new Vector2(_rest.x, _rest.y - RiseFrom);

            _seq.Append(group.DOFade(1f, FadeIn).SetEase(Ease.OutQuad));
            _seq.Join(panel.DOAnchorPosY(_rest.y, FadeIn * 1.6f).SetEase(Ease.OutCubic));
        }

        _seq.AppendInterval(Mathf.Min(HoldBase + HoldPerChar * message.Length, HoldMax));
        _seq.Append(group.DOFade(0f, FadeOut).SetEase(Ease.InQuad));
        _seq.OnKill(Settle);
    }

    // 안내가 난 열 — 넘겨받은 것 → 방금 누른 버튼 → 커서 밑 UI 순으로 출처를 찾는다. 열 밖이면 null
    private RectTransform? FindColumn(Transform? shake)
    {
        Transform? source = shake != null ? shake : ButtonPressFx.RecentlyPressed;

        if (source == null)
        {
            IReadOnlyList<RaycastResult> hits = _window.UIHitsUnderCursor;

            source = hits.Count > 0 ? hits[0].gameObject.transform : null;
        }

        if (source == null)
        {
            return null;
        }

        foreach (RectTransform column in columns)
        {
            if (column != null && source.IsChildOf(column))
            {
                return column;
            }
        }

        return null;
    }

    // 열 가운데를 판의 자리(anchoredPosition)로 바꾼다 — 열과 토스트는 다른 캔버스라 화면 좌표를 거친다
    private Vector2 CenterOf(RectTransform column)
    {
        var     area   = (RectTransform)panel.parent;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(CameraOf(column), column.TransformPoint(column.rect.center));

        RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, CameraOf(area), out Vector2 local);

        // 판의 앵커는 부모 가운데다 — 부모 기준 좌표에서 부모 가운데를 뺀다
        return local - area.rect.center;
    }

    // 화면 좌표 변환에 쓸 카메라 — Overlay 캔버스면 null
    private static Camera? CameraOf(RectTransform target)
    {
        Canvas? canvas = target.GetComponentInParent<Canvas>();

        return canvas == null || canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.rootCanvas.worldCamera;
    }

    // 문구 길이에 맞춰 판 크기를 정한다 — 짧으면 좁게, 길면 최대 폭에서 줄을 바꾼다. 열을 알면 그 열 폭 안으로
    private void FitPanel(string message, RectTransform? column)
    {
        float areaWidth = column != null
            ? column.rect.width * column.lossyScale.x / panel.parent.lossyScale.x
            : ((RectTransform)transform).rect.width;
        float screenWidth = areaWidth - ScreenMargin * 2f;
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
        panel.anchoredPosition = _rest;
    }
}
