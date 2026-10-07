using DG.Tweening;
using UnityEngine;

// 위젯 칸의 캐릭터 머리 — 일하는 동안 통통 튀고, 수확 순간 찌그러졌다 펄쩍 뛴다.
// 머리 그림('Character Image')에 붙는다. 'WidgetMiniSlotView'가 켜고 끄고 뛰게 한다.
//
// ■ 왜 좌우 흔들기가 아닌가
//   머리 그림은 원본에서 잘라 쓴 **측면** 크롭이다 — 좌우로 흔들면 고개를 돌렸다 되돌리는 것처럼 어색하다.
//   목업 비교(2026-10-08)에서 "통통 바운스 + 수확 펄쩍" 두 가지만 골랐다(땀방울·말풍선 등은 쓰지 않는다).
//
// ■ 픽셀 단위로 끊는다
//   바운스는 0~'bounceHeight'를 정수 px로 반올림해 옮긴다 — 소수 위치는 픽셀 그림을 번지게 한다.
//
// ■ 아래를 붙인 채 찌그러진다
//   피벗이 가운데라 그대로 세로로 줄이면 아래가 뜬다 — 줄어든 만큼 내려 바닥을 맞춘다.
//
// ※ 위상은 칸마다 다르게 준다('SetWorking'의 phase) — 같은 순간에 모두 튀면 스트립 전체가 출렁인다(게임UI 2.3).
[RequireComponent(typeof(RectTransform))]
public class WidgetHeadMotion : MonoBehaviour
{
    [CenterHeader("바운스 (일하는 동안)")]
    [SerializeField, Min(0f), Tooltip("튀는 높이 (px). 정수로 끊어 옮긴다")]
    private float bounceHeight = 2f;

    [SerializeField, Min(0.1f), Tooltip("한 번 튀었다 내려오는 시간 (초)")]
    private float bouncePeriod = 0.9f;

    [CenterHeader("펄쩍 (수확 순간)")]
    [SerializeField, Min(0f), Tooltip("뛰어오르는 높이 (px)")]
    private float hopHeight = 5f;

    [SerializeField, Min(0.05f), Tooltip("찌그러짐 → 펄쩍 → 착지까지 (초)")]
    private float hopDuration = 0.45f;

    [SerializeField, Tooltip("뛰기 전 찌그러진 크기 (가로, 세로)")]
    private Vector2 squash = new Vector2(1.12f, 0.85f);

    [SerializeField, Tooltip("공중에서 늘어난 크기 (가로, 세로)")]
    private Vector2 stretch = new Vector2(0.95f, 1.08f);

    [SerializeField, Tooltip("착지할 때 눌린 크기 (가로, 세로)")]
    private Vector2 land = new Vector2(1.06f, 0.94f);

    private RectTransform _rect = null!;
    private Vector2       _basePosition;

    private Tween?    _bounceTween;
    private Sequence? _hopSeq;

    private bool  _working;
    private float _phase;

    private float   _bounce;               // 0~1 — 바운스 진행
    private float   _hopY;                 // 펄쩍 높이 (px)
    private Vector2 _scale = Vector2.one;  // 찌그러짐·늘어남

    private void Awake()
    {
        _rect         = (RectTransform)transform;
        _basePosition = _rect.anchoredPosition;
    }

    // 다시 켜짐 — 일하던 칸이면 바운스를 잇는다 (Unity 메시지)
    private void OnEnable()
    {
        if (_working)
        {
            StartBounce();
        }
    }

    // 꺼짐 — 트윈을 모두 끊고 제자리로 (Unity 메시지)
    private void OnDisable()
    {
        KillAll();
        ResetPose();
    }

    // 일하는 중인가를 정한다 — 일하면 통통 튀고, 아니면 제자리에 멈춘다 (WidgetMiniSlotView.Bind에서 호출).
    //   phase : 0~1 — 바운스를 어디서부터 시작할지. 칸마다 다르게 줘 동시에 튀지 않게 한다
    public void SetWorking(bool working, float phase)
    {
        _phase = Mathf.Repeat(phase, 1f);

        if (working == _working)
        {
            return;
        }

        _working = working;

        if (!isActiveAndEnabled)
        {
            return;
        }

        if (working)
        {
            StartBounce();
        }
        else
        {
            KillAll();
            ResetPose();
        }
    }

    // 수확 순간 — 찌그러졌다 펄쩍 뛰고 착지한다. 그동안 바운스는 쉰다 (WidgetMiniSlotView.MarkHarvested에서 호출).
    public void Hop()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        _hopSeq?.Kill();
        _bounceTween?.Pause();
        _bounce = 0f;

        float t = hopDuration;

        // 찌그러짐(0) → 공중(35%) → 착지 찌그러짐(70%) → 제 모양(100%)
        _hopSeq = DOTween.Sequence()
            .SetLink(gameObject, LinkBehaviour.KillOnDisable)
            .OnUpdate(Apply)
            .Append(DOTween.To(() => _scale, v => _scale = v, squash, 0f))
            .Append(DOTween.To(() => _scale, v => _scale = v, stretch, t * 0.35f).SetEase(Ease.OutQuad))
            .Join(DOTween.To(() => _hopY, v => _hopY = v, hopHeight, t * 0.35f).SetEase(Ease.OutQuad))
            .Append(DOTween.To(() => _hopY, v => _hopY = v, 0f, t * 0.35f).SetEase(Ease.InQuad))
            .Join(DOTween.To(() => _scale, v => _scale = v, land, t * 0.35f).SetEase(Ease.InQuad))
            .Append(DOTween.To(() => _scale, v => _scale = v, Vector2.one, t * 0.3f).SetEase(Ease.OutQuad))
            .OnComplete(() =>
            {
                _hopSeq = null;
                Apply();

                if (_working)
                {
                    _bounceTween?.Play();
                }
            });
    }

    private void StartBounce()
    {
        _bounceTween?.Kill();

        _bounceTween = DOTween.To(() => _bounce, v => { _bounce = v; Apply(); }, 1f, bouncePeriod * 0.5f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable);

        // 위상 — 한 바퀴(올라갔다 내려옴)는 bouncePeriod
        _bounceTween.Goto(_phase * bouncePeriod, true);
    }

    private void KillAll()
    {
        _bounceTween?.Kill();
        _bounceTween = null;
        _hopSeq?.Kill();
        _hopSeq = null;
    }

    private void ResetPose()
    {
        _bounce = 0f;
        _hopY   = 0f;
        _scale  = Vector2.one;

        if (_rect != null)
        {
            Apply();
        }
    }

    // 지금 값으로 머리를 놓는다 — 바운스는 정수 px, 찌그러짐은 바닥을 맞춘다
    private void Apply()
    {
        float height  = _rect.rect.height;
        float bounceY = Mathf.Round(_bounce * bounceHeight);
        float floorY  = (_scale.y - 1f) * height * _rect.pivot.y; // 세로로 줄면 피벗 아래가 줄어든 만큼 내려 바닥을 붙인다

        _rect.anchoredPosition = _basePosition + new Vector2(0f, bounceY + _hopY + floorY);
        _rect.localScale       = new Vector3(_scale.x, _scale.y, 1f);
    }
}
