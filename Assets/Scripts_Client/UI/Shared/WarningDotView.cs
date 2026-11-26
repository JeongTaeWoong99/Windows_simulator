using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// 경고 점 — 레드닷과 다른 용도의 점. **"놔두면 손해"** 를 그 자리에서 알리고, 올리면 무슨 일인지 툴팁으로 보인다 (T-145 · 2026-11-27).
//
// ■ 레드닷과 무엇이 다른가
//   레드닷('notice_dot')은 "열면 볼 것이 있다" — 작고 조용하다. 이 점은 위젯 알림 줄과 같은 사유(가득 참 · 빈 작업슬롯)를
//   3열이 열려 있을 때 대신 알린다 — 그래서 더 크고(느낌표) 천천히 숨 쉰다. 같은 자리에 둘이 겹치면 이 점이 이긴다
//   (부르는 쪽이 레드닷을 끈다).
//
// ■ 툴팁을 받으려고 레이캐스트를 켠다
//   'TooltipTrigger'는 커서 밑 레이캐스트로 찾는다. 점이 버튼 모서리를 조금 가리지만, 버튼 본체는 그대로 눌린다.
[RequireComponent(typeof(Image), typeof(TooltipTrigger))]
public class WarningDotView : MonoBehaviour
{
    // 숨 쉬기 — 크기 · 한 번 커지는 시간(초)
    private const float PulseScale    = 1.18f;
    private const float PulseDuration = 0.55f;

    private TooltipTrigger? _tooltip;
    private Tween?          _pulse;

    // 툴팁 트리거 — 꺼진 채 시작하면 Awake가 아직 안 돌았으므로 처음 쓸 때 찾는다
    private TooltipTrigger Tooltip
    {
        get
        {
            if (_tooltip == null)
            {
                _tooltip = GetComponent<TooltipTrigger>();
                GetComponent<Image>().raycastTarget = true;
            }

            return _tooltip;
        }
    }

    // 켜고 끈다 — 켤 때 툴팁 내용을 만드는 함수를 넘긴다 (MenuPresenter · WorkStationListPresenter)
    public void Show(bool on, Func<TooltipContent?>? tooltip = null)
    {
        Tooltip.SetProvider(on ? tooltip : null);

        if (gameObject.activeSelf != on)
        {
            gameObject.SetActive(on); // 켜지면 OnEnable이 숨 쉬기를 건다
        }
    }

    // 보이게 될 때마다 숨 쉬기를 건다 (Unity 메시지) — 열(캔버스)이 접혔다 펴져도 다시 돈다.
    // 꺼지면 SetLink가 트윈을 죽인다.
    private void OnEnable()
    {
        if (_pulse != null)
        {
            return;
        }

        transform.localScale = Vector3.one;
        _pulse = transform.DOScale(PulseScale, PulseDuration)
                          .SetEase(Ease.InOutSine)
                          .SetLoops(-1, LoopType.Yoyo)
                          .SetLink(gameObject, LinkBehaviour.KillOnDisable)
                          .OnKill(() => _pulse = null);
    }

    // 다음에 켤 때 원래 크기에서 시작한다 (Unity 메시지)
    private void OnDisable()
    {
        transform.localScale = Vector3.one;
    }

    // 툴팁 한 벌 — 제목 '경고' + 사유 한 줄 (부르는 쪽이 provider로 쓴다)
    public static TooltipContent Content(string message) => new TooltipContent(Lang.Get("UI.Widget.WarningTitle")).Row(message, "");
}
