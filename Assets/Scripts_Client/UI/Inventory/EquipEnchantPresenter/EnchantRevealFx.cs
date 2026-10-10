using System;
using DG.Tweening;
using GameData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 큐브 비교 상자 하나의 표시 + 연출 — BEFORE · AFTER 상자에 하나씩 붙는다 (이슈 #53 · 목업 '큐브_개편_목업.html').
// 층(번짐 · 테두리 · 도장 · 빛 줄 · 입자)은 코드가 만든다 — 상자 레이아웃에서 빠진다('LayoutElement.ignoreLayout').
//
// ■ 표시 — 제목 · 칸 목록 · 등급색 테두리
//   상자 글씨는 늘 'SetResult'로 쓴다. 테두리는 그 상자 등급색으로 늘 그려 두어 어느 쪽이 무슨 등급인지 한눈에 보이게 한다.
//
// ■ 눈길 배분 — 'emphasized' (사용자 결정 2026-10-11)
//   BEFORE(끔) : 흐린 테두리만 — 이미 아는 값이라 눈이 덜 가게
//   AFTER(켬)  : 진한 테두리 + **테두리를 따라 도는 빛**(결과가 있는 동안. 돌기 중엔 흰빛이 빠르게)
//   도는 빛은 자기 Canvas에 담는다 — 매 프레임 도는 회전이 큰 캔버스의 배치를 다시 짜지 않게.
//
// ■ 공개(Play) — 줄마다 돌다가 차례로 멈춘다 (AFTER만)
//   돌기   : 칸 줄마다 **같은 등급**의 아무 옵션을 흐린 색으로 빠르게 바꿔 끼운다. 제목은 등급을 감춘다('…')
//   멈춤   : 첫 줄부터 차례로 진짜 값으로 멈추고, 멈춘 줄은 잠깐 굵게 튄다
//   상승   : 마지막 줄이 멈추면 테두리가 하얗게 번쩍였다 등급색으로 · 번짐 · '등급 상승!' 도장
//            영웅↑은 입자가 터지고(신화는 더 많이) 신화는 빛 줄이 훑는다
//   목표   : 자동이 목표에 닿으면 초록 테두리 두 번 + 초록 입자 ('FlashGoal')
//   고름   : [선택]으로 고른 값이 남은 상자가 금색으로 번쩍 ('FlashChosen')
//
// ■ 돌기 중에는 결과를 쥐고만 있다
//   응답 뒤 장비 동기화·인벤토리 변경으로 큐브 창이 여러 번 다시 그린다 — 그때 글씨를 바로 쓰면 돌기가 끊긴다.
//   돌기 중 'SetResult'는 쥐었다가 줄이 멈출 때 쓴다.
//
// ■ 빠르기 — 'speed'로 모든 시간을 나눈다 (사람 1 · 자동 2 — 사용자 결정 2026-10-10).
// ■ 그림이 없으면 그 효과만 빠진다 (코드로 그리지 않는다 — 'FxSprites').
// ■ 수명 — Sequence는 상자에 묶는다(KillOnDisable). 끊기면 쥔 결과를 바로 쓰고 층을 끈다. 도장은 다음 공개·'Stop'까지 남는다.
public class EnchantRevealFx : MonoBehaviour
{
    // 돌기 — 글씨 바꾸는 간격 · 첫 줄이 멈추는 때 · 줄 사이 (초, 사람 기준 — 목업 45 · 380 · 140ms)
    private const float SpinTick     = 0.045f;
    private const float FirstLockAt  = 0.38f;
    private const float LockStep     = 0.14f;
    private const float LockBoldTime = 0.12f;

    // 돌고 있는 줄 색 — 흐린 회색
    private const string SpinColor = "#6F7480";

    // 상승 — 테두리 번쩍 · 번짐 · 도장
    private const float FlashSeconds   = 1.0f;
    private const float GlowSeconds    = 1.0f;
    private const float GlowAlpha      = 0.45f;
    private const float GlowStartScale = 0.9f;
    private const float GlowEndScale   = 1.2f;
    private const float GlowWhiten     = 0.2f;
    private const float StampSeconds   = 0.45f;
    private const float StampFromScale = 3f;
    private const float StampAngle     = 8f;

    // 입자 — 영웅·전설 16 · 신화 30 · 목표 40 (목업 값)
    private const int   EpicSparks   = 16;
    private const int   MythicSparks = 30;
    private const int   GoalSparks   = 40;
    private const int   SparkPool    = GoalSparks;
    private const float SparkSize    = 22f;
    private const float SparkMinFly  = 50f;
    private const float SparkMaxFly  = 140f;
    private const float SparkSeconds = 0.9f;

    // 신화 빛 줄
    private const float ShineSeconds = 0.55f;
    private const float ShineWidth   = 0.22f; // 상자 너비 대비
    private const float ShineAngle   = 20f;
    private const float ShineAlpha   = 0.8f;

    // 목표 · 고름
    private const float GoalSeconds   = 0.6f;
    private const float ChosenSeconds = 0.5f;

    // 늘 그리는 등급 테두리 진하기 — 강조 상자(AFTER) · 조용한 상자(BEFORE)
    private const float FrameAlphaLoud  = 0.9f;
    private const float FrameAlphaQuiet = 0.3f;

    // 테두리를 따라 도는 빛 — 한 바퀴 (초) · 돌기 중 한 바퀴 · 진하기 · 돌기 중 색
    private const float  FlowSeconds     = 2.4f;
    private const float  FlowSpinSeconds = 0.6f;
    private const float  FlowAlpha       = 1f;
    private const float  FlowWhiten      = 0.7f;  // 등급색 테두리 위를 지나므로 하얗게 띄운다 — 등급색 그대로면 묻힌다
    private const float  FlowThickness   = 0.6f;  // 마스크 테두리 두께 배율 (작을수록 두껍다 — 'pixelsPerUnitMultiplier')
    private const string FlowSpinColor   = "#C9CDD6";

    [CenterHeader("참조")]
    [SerializeField, Tooltip("칸 목록 글씨 — 돌기 중 바꿔 끼운다")]
    private TMP_Text optionsText = null!;

    [SerializeField, Tooltip("상자 제목 글씨 — 돌기 중엔 등급을 감춘다")]
    private TMP_Text titleText = null!;

    [CenterHeader("눈길")]
    [SerializeField, Tooltip("켜면 진한 테두리 + 테두리를 따라 도는 빛(AFTER). 끄면 흐린 테두리만(BEFORE)")]
    private bool emphasized;

    private RectTransform _rect      = null!;
    private Image         _glow      = null!;
    private Image         _frame     = null!; // 늘 그리는 등급 테두리
    private Image         _flash     = null!; // 번쩍이는 테두리 (상승 · 목표 · 고름)
    private RectMask2D    _shineClip = null!;
    private Image         _shine     = null!;
    private RectTransform _stamp     = null!;
    private CanvasGroup   _stampFade = null!;
    private Image         _stampLine = null!;
    private TMP_Text      _stampText = null!;
    private readonly Image[] _sparks = new Image[SparkPool];

    // 도는 빛 — 강조 상자만 만든다
    private RectTransform? _flowRoot;
    private Image?         _flow;
    private Tween?         _flowTween;
    private Color          _flowColor;
    private float          _flowPeriod;

    private Sequence? _seq;
    private bool      _isBuilt;

    // 돌기 상태
    private Func<string>? _roll;
    private string[]?     _finalLines;
    private string?       _finalTitle;
    private GlobalRarity  _finalFrame;
    private bool          _hasFinal;
    private int           _lineCount;
    private float         _speed = 1f;

    // 지금 보이는 등급 테두리
    private GlobalRarity _frameGrade;

    // 돌기 중인가 — 이때 들어온 결과는 쥐었다가 멈출 때 쓴다
    public bool IsSpinning { get; private set; }

    // 연출이 아직 도는가 — 자동은 이것이 끝나야 다음 큐브를 누른다
    public bool IsPlaying => _seq != null && _seq.IsActive();

    // 공개가 끝났다 — 마지막 줄이 멈춘 순간 (큐브 창이 버튼을 풀고 다시 그린다).
    // ※ 끊겨서(Stop · 닫기) 멈춘 때는 쏘지 않는다 — 끊은 쪽이 이미 다음 일을 하고 있다.
    public event Action? Revealed;

    // 참조 확인 · 층 만들기 (Unity 메시지)
    private void Awake()
    {
        this.RequireRef(optionsText, nameof(optionsText));
        this.RequireRef(titleText,   nameof(titleText));

        Build();
    }

    // 상자 글씨와 등급 테두리를 쓴다 — 돌기 중이면 쥐었다가 멈출 때 쓴다 (큐브 창의 DrawCompare에서 호출).
    //   frame : 테두리 등급 — 'None'이면 테두리를 끈다
    public void SetResult(string title, string options, GlobalRarity frame)
    {
        if (IsSpinning)
        {
            _finalTitle = title;
            _finalLines = options.Split('\n');
            _finalFrame = frame;
            _hasFinal   = true;

            return;
        }

        titleText.text   = title;
        optionsText.text = options;

        ShowFrame(frame);
    }

    // 굴린 결과를 공개한다 (큐브 창이 큐브 응답을 받았을 때 — 다시 그리기보다 먼저).
    //   grade     : 결과 등급 — 테두리 · 번짐 색 · 입자 수 · 신화 빛 줄
    //   rankedUp  : 등급이 올랐는가 — 번쩍 · 도장 · 입자
    //   lineCount : 칸 줄 수 (장비 등급이 정한 칸 수)
    //   roll      : 돌기 중 끼울 가짜 한 줄 — **결과와 같은 등급**의 옵션 글씨(색 없이)
    //   speed     : 빠르기 (사람 1 · 자동 2)
    public void Play(GlobalRarity grade, bool rankedUp, int lineCount, Func<string> roll, float speed)
    {
        Stop();

        _speed      = Mathf.Max(0.01f, speed);
        _roll       = roll;
        _lineCount  = Mathf.Max(1, lineCount);
        _hasFinal   = false;
        _finalLines = null;
        _finalTitle = null;
        IsSpinning  = true;

        titleText.text = $"{SplitTitleHead(titleText.text)}<color={SpinColor}>…</color>";
        ShowFrame(GlobalRarity.None); // 등급은 멈출 때 드러난다
        RunFlow(ParseColor(FlowSpinColor), FlowSpinSeconds / _speed); // 돌기 중엔 흰빛이 빠르게

        _seq = DOTween.Sequence().SetLink(gameObject, LinkBehaviour.KillOnDisable).SetUpdate(true);

        float lastLock = (FirstLockAt + (_lineCount - 1) * LockStep) / _speed;
        float tick     = SpinTick / _speed;

        // 마지막 줄이 멈춘 뒤 굵은 글씨가 풀릴 때까지 간격마다 다시 쓴다
        for (float t = 0f; t <= lastLock + LockBoldTime / _speed + tick; t += tick)
        {
            float at = t;

            _seq.InsertCallback(at, () => ComposeSpin(at));
        }

        _seq.InsertCallback(lastLock, () =>
        {
            Lock();
            Revealed?.Invoke();
        });

        if (rankedUp)
        {
            InsertRankUp(lastLock, grade);
        }
        else if (grade >= GlobalRarity.Mythic)
        {
            InsertShine(lastLock); // 신화에서 다시 뽑기 — 빛 줄만
        }

        // 끝나도 끊겨도 쥔 결과를 쓰고 번쩍임 층을 끈다
        _seq.OnKill(Finish);
    }

    // 목표 달성 — 초록 테두리 두 번 + 초록 입자 (자동이 목표에 닿았을 때). 공개 연출이 끝난 뒤 부른다.
    public void FlashGoal()
    {
        KillSequence();

        Color green = UIThemePalette.Of(UIThemeRole.Positive);
        float half  = GoalSeconds * 0.5f;

        _seq = DOTween.Sequence().SetLink(gameObject, LinkBehaviour.KillOnDisable).SetUpdate(true);

        if (_flash.sprite != null)
        {
            _seq.InsertCallback(0f, () =>
            {
                _flash.enabled = true;
                _flash.color   = WithAlpha(green, 0f);
            });

            for (int i = 0; i < 2; i++)
            {
                _seq.Insert(i * GoalSeconds, _flash.DOFade(1f, half * 0.4f));
                _seq.Insert(i * GoalSeconds + half * 0.4f, _flash.DOFade(0f, GoalSeconds - half * 0.4f).SetEase(Ease.InQuad));
            }
        }

        InsertGlow(0f, green, GoalSeconds * 2f, 0.6f);
        InsertSparks(0f, GoalSparks, green, 1.4f);

        _seq.OnKill(Finish);
    }

    // [선택]으로 고른 값이 남은 상자 — 금색 테두리가 번쩍였다 사라진다 (큐브 창이 고르기 응답을 받았을 때).
    public void FlashChosen()
    {
        KillSequence();

        if (_flash.sprite == null)
        {
            return;
        }

        _flash.enabled = true;
        _flash.color   = UIThemePalette.Of(UIThemeRole.Highlight);

        _seq = DOTween.Sequence().SetLink(gameObject, LinkBehaviour.KillOnDisable).SetUpdate(true);
        _seq.Insert(0f, _flash.DOFade(0f, ChosenSeconds).SetEase(Ease.OutQuad));
        _seq.OnKill(Finish);
    }

    // 끊는다 — 쥐고 있던 결과를 바로 쓰고 층 · 도장을 끈다 (창을 닫을 때 · 고른 뒤 · 다음 공개 직전).
    public void Stop()
    {
        KillSequence();

        if (_isBuilt)
        {
            _stamp.gameObject.SetActive(false);
        }
    }

    private void KillSequence()
    {
        if (_seq != null && _seq.IsActive())
        {
            _seq.Kill(); // OnKill → Finish
        }

        _seq = null;

        Finish();
    }

    // 돌기 한 장면 — 멈춘 줄은 진짜 값(막 멈췄으면 굵게), 나머지는 흐린 가짜 값.
    private void ComposeSpin(float at)
    {
        if (!IsSpinning && _hasFinal == false)
        {
            return;
        }

        var text = new System.Text.StringBuilder();

        for (int i = 0; i < _lineCount; i++)
        {
            if (i > 0)
            {
                text.Append('\n');
            }

            float lockAt = (FirstLockAt + i * LockStep) / _speed;

            if (at >= lockAt && _hasFinal && _finalLines != null && i < _finalLines.Length)
            {
                bool fresh = at - lockAt < LockBoldTime / _speed;

                text.Append(fresh ? $"<b>{_finalLines[i]}</b>" : _finalLines[i]);

                continue;
            }

            text.Append("<color=").Append(SpinColor).Append('>').Append(_roll?.Invoke() ?? "").Append("</color>");
        }

        optionsText.text = text.ToString();
    }

    // 마지막 줄이 멈췄다 — 진짜 제목 · 테두리를 쓴다. 결과가 아직 안 왔으면(상급 큐브는 동기화가 늦다) 들어오는 대로 쓴다.
    private void Lock()
    {
        IsSpinning = false;

        if (!_hasFinal)
        {
            return;
        }

        titleText.text   = _finalTitle ?? titleText.text;
        optionsText.text = string.Join("\n", _finalLines ?? Array.Empty<string>());

        ShowFrame(_finalFrame);

        _hasFinal = false;
    }

    // 상승 — 테두리 번쩍 · 번짐 · 도장 · (영웅↑) 입자 · (신화) 빛 줄.
    private void InsertRankUp(float at, GlobalRarity grade)
    {
        Color color = Color.Lerp(RarityPalette.Get(grade), Color.white, GlowWhiten);

        if (_flash.sprite != null)
        {
            _seq!.InsertCallback(at, () =>
            {
                _flash.enabled = true;
                _flash.color   = Color.white;
            });
            _seq.Insert(at, _flash.DOColor(WithAlpha(color, 0f), FlashSeconds / _speed).SetEase(Ease.InQuad));
        }

        InsertGlow(at, color, GlowSeconds / _speed, GlowAlpha);
        InsertStamp(at, color);

        if (grade >= GlobalRarity.Epic)
        {
            InsertSparks(at, grade >= GlobalRarity.Mythic ? MythicSparks : EpicSparks, color, 1f);
        }

        if (grade >= GlobalRarity.Mythic)
        {
            InsertShine(at);
        }
    }

    // 번짐 — 상자 뒤에서 커지며 사라진다.
    private void InsertGlow(float at, Color color, float duration, float alpha)
    {
        if (_glow.sprite == null)
        {
            return;
        }

        RectTransform glow = _glow.rectTransform;

        _seq!.InsertCallback(at, () =>
        {
            _glow.enabled   = true;
            _glow.color     = WithAlpha(color, 0f);
            glow.localScale = Vector3.one * GlowStartScale;
        });
        _seq.Insert(at, glow.DOScale(GlowEndScale, duration).SetEase(Ease.OutCubic));
        _seq.Insert(at, _glow.DOFade(alpha, duration * 0.25f));
        _seq.Insert(at + duration * 0.25f, _glow.DOFade(0f, duration * 0.75f).SetEase(Ease.InQuad));
    }

    // '등급 상승!' 도장 — 크게 떨어져 찍힌다. 다음 공개 · Stop까지 남는다.
    private void InsertStamp(float at, Color color)
    {
        _seq!.InsertCallback(at, () =>
        {
            _stamp.gameObject.SetActive(true);
            _stamp.localScale    = Vector3.one * StampFromScale;
            _stampFade.alpha     = 0f;
            _stampText.color     = color;
            _stampLine.color     = color;
        });
        _seq.Insert(at, _stamp.DOScale(1f, StampSeconds / _speed).SetEase(Ease.OutBack, 2.5f));
        _seq.Insert(at, _stampFade.DOFade(1f, StampSeconds * 0.4f / _speed));
    }

    // 입자 — 상자 가운데서 사방으로 튀며 작아지고 사라진다.
    private void InsertSparks(float at, int count, Color color, float flyScale)
    {
        if (_sparks[0].sprite == null)
        {
            return;
        }

        float seconds = SparkSeconds / _speed;

        for (int i = 0; i < count && i < _sparks.Length; i++)
        {
            Image         spark = _sparks[i];
            RectTransform rect  = spark.rectTransform;

            float   angle  = UnityEngine.Random.value * Mathf.PI * 2f;
            float   fly    = UnityEngine.Random.Range(SparkMinFly, SparkMaxFly) * flyScale;
            Vector2 target = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * fly;

            _seq!.InsertCallback(at, () =>
            {
                spark.enabled         = true;
                spark.color           = color;
                rect.anchoredPosition = Vector2.zero;
                rect.localScale       = Vector3.one;
                rect.localRotation    = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 90f));
            });
            _seq.Insert(at, rect.DOAnchorPos(target, seconds).SetEase(Ease.OutCubic));
            _seq.Insert(at, rect.DOScale(0.2f, seconds).SetEase(Ease.InQuad));
            _seq.Insert(at + seconds * 0.4f, spark.DOFade(0f, seconds * 0.6f));
        }
    }

    // 신화 빛 줄 — 상자를 왼쪽에서 오른쪽으로 한 번 훑는다.
    private void InsertShine(float at)
    {
        if (_shine.sprite == null)
        {
            return;
        }

        RectTransform band = _shine.rectTransform;

        _seq!.InsertCallback(at, () =>
        {
            Vector2 size = _rect.rect.size;

            _shineClip.enabled    = true;
            _shine.enabled        = true;
            _shine.color          = new Color(1f, 1f, 1f, ShineAlpha);
            band.sizeDelta        = new Vector2(size.x * ShineWidth, size.y * 1.8f);
            band.anchoredPosition = new Vector2(-size.x * 0.7f, 0f);
        });
        _seq.Insert(at, DOTween.To(() => band.anchoredPosition.x,
                                   x => band.anchoredPosition = new Vector2(x, 0f),
                                   _rect.rect.width * 0.7f,
                                   ShineSeconds / _speed).SetEase(Ease.InOutSine));
    }

    // 등급 테두리 — 'None'이면 끈다.
    private void ShowFrame(GlobalRarity grade)
    {
        _frameGrade = grade;

        if (!_isBuilt)
        {
            return;
        }

        bool show = grade != GlobalRarity.None && _frame.sprite != null;

        _frame.enabled = show;

        if (show)
        {
            _frame.color = WithAlpha(RarityPalette.Get(grade), emphasized ? FrameAlphaLoud : FrameAlphaQuiet);
        }

        // 돌기 중엔 'Play'가 흰빛을 돌린다 — 등급이 드러날 때 등급색으로 바뀐다
        if (IsSpinning)
        {
            return;
        }

        if (grade == GlobalRarity.None)
        {
            StopFlow();
        }
        else
        {
            RunFlow(Color.Lerp(RarityPalette.Get(grade), Color.white, FlowWhiten), FlowSeconds);
        }
    }

    // 테두리를 따라 빛을 돌린다 — 강조 상자만. 같은 색 · 빠르기로 이미 돌고 있으면 그대로 둔다(다시 그리기마다 끊기지 않게).
    private void RunFlow(Color color, float period)
    {
        if (_flowRoot == null || _flow == null)
        {
            return;
        }

        color = WithAlpha(color, FlowAlpha);

        if (_flowTween != null && _flowTween.IsActive() && _flowColor == color && Mathf.Approximately(_flowPeriod, period))
        {
            return;
        }

        _flowTween?.Kill();

        _flowColor  = color;
        _flowPeriod = period;

        // 빛 그림은 상자 대각선보다 커야 모서리까지 훑는다
        Vector2 size = _rect.rect.size;
        float   side = Mathf.Sqrt(size.x * size.x + size.y * size.y) * 1.05f;

        _flowRoot.gameObject.SetActive(true);
        _flow.color                   = color;
        _flow.rectTransform.sizeDelta = new Vector2(side, side);

        _flowTween = _flow.rectTransform
                          .DOLocalRotate(new Vector3(0f, 0f, -360f), period, RotateMode.FastBeyond360)
                          .SetRelative(true)
                          .SetEase(Ease.Linear)
                          .SetLoops(-1)
                          .SetLink(gameObject, LinkBehaviour.KillOnDisable)
                          .SetUpdate(true);
    }

    private void StopFlow()
    {
        _flowTween?.Kill();
        _flowTween = null;

        if (_flowRoot != null)
        {
            _flowRoot.gameObject.SetActive(false);
        }
    }

    // 끝 — 쥔 결과를 쓰고 번쩍임 층을 끈다 (OnKill · Stop). 도장 · 등급 테두리는 남긴다.
    private void Finish()
    {
        if (IsSpinning)
        {
            Lock();
        }

        if (!_isBuilt)
        {
            return;
        }

        _glow.enabled      = false;
        _flash.enabled     = false;
        _shine.enabled     = false;
        _shineClip.enabled = false;

        foreach (Image spark in _sparks)
        {
            spark.enabled = false;
        }

        if (_stamp.gameObject.activeSelf)
        {
            _stamp.localScale = Vector3.one;
            _stampFade.alpha  = 1f;
        }
    }

    // 제목 머리 — 'AFTER · …'의 'AFTER · ' 까지 (돌기 중 등급을 감춘다)
    private static string SplitTitleHead(string title)
    {
        int dot = title.IndexOf(UIRichText.Dot, StringComparison.Ordinal);

        return dot < 0 ? $"{title}{UIRichText.Dot}" : title.Substring(0, dot + UIRichText.Dot.Length);
    }

    // 층 — 번짐(글씨 뒤) · 테두리 · 빛 줄 · 도장 · 입자(맨 위) (Awake에서 한 번)
    private void Build()
    {
        _rect = (RectTransform)transform;

        // 번짐은 상자 배경 바로 위 · 글씨 아래
        RectTransform back = CreateLayer("Reveal Fx Back");

        back.SetAsFirstSibling();

        _glow = CreateImage("Glow", back, FxSprites.GainGlow);
        Stretch(_glow.rectTransform);

        RectTransform front = CreateLayer("Reveal Fx Front");

        front.SetAsLastSibling();

        _frame = CreateImage("Grade Frame", front, FxSprites.RevealFrame);
        _frame.type = Image.Type.Sliced;
        Stretch(_frame.rectTransform);

        _flash = CreateImage("Flash Frame", front, FxSprites.RevealFrame);
        _flash.type = Image.Type.Sliced;
        Stretch(_flash.rectTransform);

        if (emphasized)
        {
            BuildFlow(front);
        }

        // 빛 줄은 상자 안에서만 — 입자는 상자 밖으로 튀어도 된다
        RectTransform clip = CreateRect("Shine Clip", front);

        Stretch(clip);

        _shineClip = clip.gameObject.AddComponent<RectMask2D>();
        _shine     = CreateImage("Shine", clip, FxSprites.RevealShine);
        _shine.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -ShineAngle);

        BuildStamp(front);

        for (int i = 0; i < SparkPool; i++)
        {
            _sparks[i] = CreateImage($"Spark ({i})", front, FxSprites.RevealSparkle);
            _sparks[i].rectTransform.sizeDelta = new Vector2(SparkSize, SparkSize);
        }

        _isBuilt = true;

        ShowFrame(_frameGrade);
        Finish();
    }

    // 도장 — 오른쪽 아래에 기울어진 테두리 + 글씨. 글씨는 제목 글씨를 복제해 같은 글꼴을 쓴다.
    // ※ 위쪽에 두면 제목('등급 상승! 고급 ▶ 희귀')을 가린다 — 칸 줄은 왼쪽 정렬이라 오른쪽 아래가 빈다.
    private void BuildStamp(RectTransform parent)
    {
        _stamp = CreateRect("Stamp", parent);
        _stamp.anchorMin = _stamp.anchorMax = new Vector2(1f, 0f);
        _stamp.pivot            = new Vector2(1f, 0f);
        _stamp.anchoredPosition = new Vector2(-10f, 10f);
        _stamp.sizeDelta        = new Vector2(104f, 28f);
        _stamp.localRotation    = Quaternion.Euler(0f, 0f, StampAngle);

        _stampFade = _stamp.gameObject.AddComponent<CanvasGroup>();
        _stampFade.blocksRaycasts = false;
        _stampFade.interactable   = false;

        Image back = _stamp.gameObject.AddComponent<Image>();

        back.color         = WithAlpha(UIThemePalette.Of(UIThemeRole.Overlay), 0.6f);
        back.raycastTarget = false;

        _stampLine = CreateImage("Line", _stamp, FxSprites.RevealFrame);
        _stampLine.type    = Image.Type.Sliced;
        _stampLine.enabled = _stampLine.sprite != null;
        Stretch(_stampLine.rectTransform);

        GameObject copy = Instantiate(titleText.gameObject, _stamp, false);

        copy.name = "Stamp Text";

        foreach (var element in copy.GetComponents<LayoutElement>())
        {
            Destroy(element);
        }

        _stampText = copy.GetComponent<TMP_Text>();
        _stampText.text               = "등급 상승!";
        _stampText.fontStyle          = FontStyles.Bold;
        _stampText.fontSize           = 17f;
        _stampText.alignment          = TextAlignmentOptions.Center;
        _stampText.textWrappingMode   = TextWrappingModes.NoWrap;
        _stampText.raycastTarget      = false;
        Stretch(_stampText.rectTransform);

        _stamp.gameObject.SetActive(false);
    }

    // 도는 빛 — 테두리 그림을 마스크로, 그 안에서 원뿔 빛이 돈다. 그림이 하나라도 없으면 만들지 않는다.
    // ※ 자기 Canvas — 도는 회전이 상자를 담은 큰 캔버스의 배치를 매 프레임 다시 짜지 않게 가둔다.
    private void BuildFlow(RectTransform parent)
    {
        if (FxSprites.RevealFrame == null || FxSprites.RevealConic == null)
        {
            return;
        }

        _flowRoot = CreateRect("Flow Border", parent);
        Stretch(_flowRoot);

        _flowRoot.gameObject.AddComponent<Canvas>();

        Image mask = _flowRoot.gameObject.AddComponent<Image>();

        mask.sprite                  = FxSprites.RevealFrame;
        mask.type                    = Image.Type.Sliced;
        mask.pixelsPerUnitMultiplier = FlowThickness;
        mask.raycastTarget           = false;

        _flowRoot.gameObject.AddComponent<Mask>().showMaskGraphic = false;

        _flow         = CreateImage("Flow", _flowRoot, FxSprites.RevealConic);
        _flow.enabled = true;

        _flowRoot.gameObject.SetActive(false);
    }

    private static Color ParseColor(string html) => ColorUtility.TryParseHtmlString(html, out Color color) ? color : Color.white;

    // 상자를 꽉 채우고 레이아웃에서 빠진 층.
    private RectTransform CreateLayer(string name)
    {
        RectTransform layer = CreateRect(name, transform);

        Stretch(layer);

        layer.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

        return layer;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin        = Vector2.zero;
        rect.anchorMax        = Vector2.one;
        rect.pivot            = new Vector2(0.5f, 0.5f);
        rect.sizeDelta        = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
    }

    // 가운데 기준의 Image — raycast는 끈다 ([선택] 버튼을 가로채지 않는다)
    private static Image CreateImage(string name, RectTransform parent, Sprite? sprite)
    {
        RectTransform rect  = CreateRect(name, parent);
        Image         image = rect.gameObject.AddComponent<Image>();

        image.sprite        = sprite;
        image.raycastTarget = false;
        image.enabled       = false;

        return image;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));

        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);

        var rect = (RectTransform)go.transform;

        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = Vector2.zero;

        return rect;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;

        return color;
    }
}
