using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 버튼 눌림 반응 — 누르는 동안 살짝 들어가고, 떼면 튕기듯 돌아온다.
// ★ 버튼마다 붙이지 않는다 — 'Event System' 오브젝트에 하나만 둔다. 누른 순간 커서 밑의 버튼을 찾아 그것을 줄인다.
//   버튼이 씬·프리팹에 200개가 넘고 목록 줄은 실행 중에 생긴다 — 하나씩 붙이면 새 버튼마다 빠뜨린다.
//
// ■ 어떤 버튼을 찾나 — EventSystem이 클릭을 줄 대상과 같다
//   커서 밑 맨 위 UI(WindowManager.UIHitsUnderCursor[0])에서 클릭을 받는 부모('IPointerClickHandler')를 찾는다.
//   그게 누를 수 있는 'Button'일 때만 반응한다(Toggle · 드롭다운 · 입력창은 빠진다).
//
// ■ 빠지는 것
//   너무 큰 버튼 — 창 뒤를 덮는 차단막(경매 등록 창의 'Dim Button' 1920×920)이 통째로 줄면 화면이 출렁인다.
//   애니메이션 전환('Transition: Animation')을 쓰는 버튼 — 그쪽 크기 연출과 겹친다.
//
// ■ 수명 — 트윈은 그 버튼에 묶는다(KillOnDisable). 누른 채 버튼이 꺼져도 'OnKill'이 크기를 되돌린다.
// ■ 방금 누른 버튼을 알려 준다('RecentlyPressed') — 거절 흔들기가 대상을 찾을 때 본다('ToastPresenter' · T-177).
public class ButtonPressFx : MonoBehaviour
{
    // 눌린 크기 · 들어가는 시간 · 돌아오는 시간 · 튕김 세기
    private const float PressedScale = 0.94f;
    private const float PressTime    = 0.06f;
    private const float ReleaseTime  = 0.2f;
    private const float Overshoot    = 3f;

    // 이보다 큰 버튼은 줄이지 않는다 (캔버스 좌표 px)
    private const float MaxWidth  = 700f;
    private const float MaxHeight = 300f;

    // 이 시간 안에 누른 버튼만 "방금 누른 것"이다 — 오래전에 누른 버튼이 엉뚱하게 흔들리지 않게 (초)
    private const float RecentWindow = 0.5f;

    private static Transform? s_lastPressed;
    private static float      s_lastPressedTime;

    // 방금(0.5초 안에) 누른 버튼. 없거나 파괴됐으면 null
    public static Transform? RecentlyPressed
        => s_lastPressed != null && Time.unscaledTime - s_lastPressedTime <= RecentWindow ? s_lastPressed : null;

    private WindowManager _window = null!;

    private Transform? _pressed;
    private Tween?     _tween;

    // 플레이마다 지난 플레이의 버튼을 잊는다 (Unity 런타임 초기화 훅 — 도메인 리로드를 끈 플레이 대비)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        s_lastPressed = null;
    }

    // 서비스 확보 (Unity 메시지 — Awake에서 조회하지 않는다)
    private void Start()
    {
        _window = Services.Get<WindowManager>();
    }

    // 누름·뗌을 본다 (Unity 메시지)
    private void Update()
    {
        Mouse? mouse = Mouse.current;

        if (mouse == null)
        {
            return;
        }

        if (mouse.leftButton.wasPressedThisFrame)
        {
            Press(FindButton());
        }
        else if (mouse.leftButton.wasReleasedThisFrame)
        {
            Release();
        }
    }

    // 커서 밑에서 클릭을 받을 버튼 — 없거나 빠지는 버튼이면 null
    private Transform? FindButton()
    {
        var hits = _window.UIHitsUnderCursor;

        if (hits.Count == 0)
        {
            return null;
        }

        GameObject? handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);

        if (handler == null || !handler.TryGetComponent(out Button button))
        {
            return null;
        }

        if (!button.IsInteractable() || button.transition == Selectable.Transition.Animation)
        {
            return null;
        }

        Rect rect = ((RectTransform)button.transform).rect;

        if (rect.width > MaxWidth || rect.height > MaxHeight)
        {
            return null;
        }

        return button.transform;
    }

    private void Press(Transform? target)
    {
        Release(); // 앞서 눌린 채 남은 것 — 창 밖에서 뗀 경우

        if (target == null)
        {
            return;
        }

        _pressed          = target;
        s_lastPressed     = target;
        s_lastPressedTime = Time.unscaledTime;

        _tween = target.DOScale(PressedScale, PressTime)
                         .SetEase(Ease.OutQuad)
                         .SetUpdate(true)
                         .SetLink(target.gameObject, LinkBehaviour.KillOnDisable)
                         .OnKill(() => Restore(target));
    }

    private void Release()
    {
        if (_pressed == null)
        {
            return;
        }

        Transform target = _pressed;

        _pressed = null;

        KillTween();

        _tween = target.DOScale(1f, ReleaseTime)
                       .SetEase(Ease.OutBack, Overshoot)
                       .SetUpdate(true)
                       .SetLink(target.gameObject, LinkBehaviour.KillOnDisable)
                       .OnKill(() => Restore(target));
    }

    // 끊긴 트윈의 크기 되돌림을 끄고 끊는다 — 다음 트윈이 지금 크기에서 이어 간다
    private void KillTween()
    {
        if (_tween != null && _tween.IsActive())
        {
            _tween.OnKill(null);
            _tween.Kill();
        }

        _tween = null;
    }

    // 트윈이 끝났거나 버튼이 꺼져 끊겼다 (OnKill)
    //   누르는 트윈이 다 들어갔을 뿐이면(아직 누르는 중) 그대로 둔다 — 떼기 전까지 들어간 채다
    private void Restore(Transform target)
    {
        if (target == null)
        {
            return; // 버튼이 파괴됐다
        }

        if (target == _pressed && target.gameObject.activeInHierarchy)
        {
            return;
        }

        target.localScale = Vector3.one;

        if (target == _pressed)
        {
            _pressed = null;
        }
    }
}
