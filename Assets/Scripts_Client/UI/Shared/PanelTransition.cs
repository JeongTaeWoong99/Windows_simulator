using DG.Tweening;
using UnityEngine;

// 창 여닫기 연출 — 열 때 살짝 커지며 나타나고, 닫을 때 줄며 사라진다.
// 창마다 스크립트를 붙이지 않는다 — 여닫는 쪽('XxxCanvasView.Show' · 팝업 'SetVisible')이 정적 함수를 부르면 대상에 저절로 붙는다.
//
// ■ 두 가지 여닫기
//   SetActive  : 'SetActive'로 여닫는 캔버스(작업슬롯·상태·인벤토리·거점·경매장). 닫힘 연출이 끝난 뒤에 끈다.
//   SetVisible : 상주라 'CanvasGroup'으로 여닫는 팝업(확인·수량 입력). 오브젝트는 끄지 않는다.
//
// ■ 닫히는 중에도 "열림"으로 읽히면 안 된다
//   끄기를 0.1초 늦추므로 그동안 'activeSelf'가 true다. 토글('UIManager.ToggleInventory' 등)이 그걸 열림으로 읽으면
//   닫는 중에 다시 누른 버튼이 또 닫기가 된다 — 열림 여부는 'IsShown'으로 읽는다.
//
// ■ 수명
//   트윈은 대상에 묶는다(KillOnDisable). 부모가 꺼져 도중에 끊겨도 'OnKill'이 끝 상태(닫힘이면 끔)로 맞춘다.
//   시간은 timeScale을 따르지 않는다(SetUpdate) — 연출 확인용으로 timeScale을 낮춰도 창이 멈추지 않는다.
public class PanelTransition : MonoBehaviour
{
    // 열림 — 나타나는 시간 · 커지는 시간 · 시작 크기
    private const float OpenFade  = 0.14f;
    private const float OpenScale = 0.2f;
    private const float OpenFrom  = 0.95f;

    // 닫힘 — 사라지는 시간 · 끝 크기
    private const float CloseDuration = 0.1f;
    private const float CloseTo       = 0.97f;

    private CanvasGroup   _group = null!;
    private RectTransform _pop   = null!; // 커졌다 줄어드는 것 — 기본은 자기 자신, 팝업은 가운데 판
    private bool          _keepActive;    // true = 'SetVisible'(끄지 않는다)

    private Sequence? _seq;
    private bool      _shown;
    private bool      _closing;

    // 'SetActive'로 여닫는 창 — 켜고 연출하거나, 연출한 뒤 끈다.
    //   pop : 커졌다 줄어드는 것. 없으면 창 전체 (경매장은 차단막을 빼고 판만 키운다)
    public static void SetActive(GameObject target, bool on, RectTransform? pop = null)
    {
        if (!on && !target.activeSelf)
        {
            return; // 이미 꺼진 창을 닫는다 — 붙일 필요도 없다
        }

        Get(target, pop, keepActive: false).Play(on);
    }

    // 'CanvasGroup'으로 여닫는 팝업 — 오브젝트는 끄지 않는다.
    //   instant : 연출 없이 바로 (시작할 때 닫힘으로 맞추기)
    public static void SetVisible(CanvasGroup group, bool on, RectTransform? pop = null, bool instant = false)
    {
        PanelTransition fx = Get(group.gameObject, pop, keepActive: true);

        if (instant)
        {
            fx.Snap(on);
        }
        else
        {
            fx.Play(on);
        }
    }

    // 열려 있는가 — 닫히는 중이면 false ('activeSelf'를 대신한다)
    public static bool IsShown(GameObject target)
    {
        if (!target.activeSelf)
        {
            return false;
        }

        var fx = target.GetComponent<PanelTransition>();

        return fx == null || !fx._closing;
    }

    private static PanelTransition Get(GameObject target, RectTransform? pop, bool keepActive)
    {
        var fx = target.GetComponent<PanelTransition>();

        if (fx == null)
        {
            fx             = target.AddComponent<PanelTransition>();
            fx._keepActive = keepActive;
            fx._shown      = keepActive ? false : target.activeSelf;

            fx._group = target.GetComponent<CanvasGroup>();

            if (fx._group == null)
            {
                fx._group = target.AddComponent<CanvasGroup>();
            }
        }

        fx._pop = pop != null ? pop : (RectTransform)target.transform;

        return fx;
    }

    private void Play(bool on)
    {
        // 누가 연출을 거치지 않고 직접 껐다 켰을 수 있다 — 도는 연출이 없으면 실제 상태를 믿는다
        if (!_keepActive && !_closing)
        {
            _shown = gameObject.activeSelf;
        }

        if (on == _shown)
        {
            return; // 이미 그 상태 — 열린 창을 또 열어도 다시 튀지 않는다 · 닫히는 중엔 '_shown'이 이미 false다
        }

        if (on)
        {
            Open();
        }
        else
        {
            Close();
        }
    }

    private void Open()
    {
        bool wasHidden = !_shown || _closing;

        Stop();

        _shown = true;

        if (!_keepActive && !gameObject.activeSelf)
        {
            _group.alpha = 0f;
            gameObject.SetActive(true);
        }
        else if (wasHidden && _group.alpha >= 0.999f)
        {
            _group.alpha = 0f; // 닫힌 채 alpha만 1로 남은 팝업 — 처음부터 나타난다
        }

        SetInput(true);

        // 닫히던 중이면 지금 크기에서 이어 간다 — 처음부터 다시 줄었다 커지지 않는다
        if (_pop.localScale.x >= 0.999f)
        {
            _pop.localScale = Vector3.one * OpenFrom;
        }

        _seq = DOTween.Sequence()
                      .SetLink(gameObject, LinkBehaviour.KillOnDisable)
                      .SetUpdate(true);

        _seq.Insert(0f, _group.DOFade(1f, OpenFade).SetEase(Ease.OutQuad));
        _seq.Insert(0f, _pop.DOScale(1f, OpenScale).SetEase(Ease.OutBack, 1.6f));
        _seq.OnKill(Settle);
    }

    private void Close()
    {
        Stop();

        _shown   = false;
        _closing = true;

        SetInput(false); // 사라지는 동안 눌리지 않게

        _seq = DOTween.Sequence()
                      .SetLink(gameObject, LinkBehaviour.KillOnDisable)
                      .SetUpdate(true);

        _seq.Insert(0f, _group.DOFade(0f, CloseDuration).SetEase(Ease.InQuad));
        _seq.Insert(0f, _pop.DOScale(CloseTo, CloseDuration).SetEase(Ease.InQuad));
        _seq.OnKill(Settle);
    }

    // 연출 없이 끝 상태로
    private void Snap(bool on)
    {
        Stop();

        _shown   = on;
        _closing = !on;

        Settle();
    }

    // 도는 연출을 끊는다 — 끝 상태로 맞추지 않는다(다음 연출이 지금 값에서 이어 간다)
    private void Stop()
    {
        Sequence? seq = _seq;

        _seq = null;

        if (seq != null && seq.IsActive())
        {
            _closing = false; // Settle이 끄지 않게
            seq.OnKill(null);
            seq.Kill();
        }
    }

    // 끝 상태 — 다 돌았을 때도, 부모가 꺼져 끊겼을 때도 (OnKill)
    private void Settle()
    {
        _seq            = null;
        _pop.localScale = Vector3.one;

        if (_shown)
        {
            _group.alpha = 1f;
            SetInput(true);

            return;
        }

        _closing = false;

        if (_keepActive)
        {
            _group.alpha = 0f;
            SetInput(false);
        }
        else
        {
            // 다음에 켤 때를 위해 되돌려 두고 끈다
            _group.alpha = 1f;
            SetInput(true);
            gameObject.SetActive(false);
        }
    }

    private void SetInput(bool on)
    {
        _group.blocksRaycasts = on;
        _group.interactable   = on;
    }
}
