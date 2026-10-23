using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

// 숫자 카운트업 — 값이 바뀌면 한 번에 바꾸지 않고 굴러 올라가며(내려가며) 글자가 한 번 튄다.
// 글자마다 스크립트를 붙이지 않는다 — 'text.text = …' 대신 'NumberRoll.Set(text, 값, 문구)'를 부르면 그 글자에 저절로 붙는다.
//
// ■ 처음 값 · 꺼져 있던 글자는 바로 쓴다
//   로그인 직후 0 → 12,480이 굴러가면 매번 접속할 때마다 돈이 들어온 것처럼 보인다.
//   꺼진 글자는 아무도 못 보니 굴릴 이유가 없다 — 켜졌을 때 이미 맞는 값이 들어 있다.
//
// ■ 같은 값이면 아무것도 안 한다 — 'WidgetPresenter'처럼 초마다 다시 쓰는 곳도 그대로 부를 수 있다.
//
// ■ 수명 — 트윈은 글자에 묶는다(KillOnDisable). 도중에 끊기면 'OnKill'이 목표 값을 쓴다. 시간은 timeScale을 따르지 않는다(SetUpdate).
public class NumberRoll : MonoBehaviour
{
    // 구르는 시간 · 튀는 세기 · 튀는 시간
    private const float Duration   = 0.5f;
    private const float PunchScale = 0.08f; // 더 크면 왼쪽 아이콘(골드 동전) 밑으로 앞자리가 들어간다
    private const float PunchTime  = 0.25f;

    // 정수 · 천 단위 구분 ("12,480") — 골드처럼 그대로 적는 숫자
    public static readonly Func<double, string> Thousands = v => ((long)Math.Round(v)).ToString("N0");

    private TMP_Text              _text   = null!;
    private Func<double, string>  _format = null!;

    private double  _shown;
    private double  _target;
    private bool    _hasValue;
    private Tween?  _roll;
    private Tween?  _punch;

    // 값을 쓴다 — 바뀌었으면 굴린다.
    //   format : 값 → 글자 (천 단위 구분 · 단위 · 꾸밈까지). 구르는 중간 값에도 같은 것을 쓴다
    //   punch  : 바뀔 때 글자를 한 번 튀길지
    public static void Set(TMP_Text text, double value, Func<double, string> format, bool punch = true)
    {
        Get(text).Write(value, format, punch);
    }

    // 굴림을 멈추고 값을 잊는다 — 다음 'Set'은 바로 쓴다 (값이 없는 상태로 돌아갈 때: 빈 칸 · 쉬는 칸)
    public static void Forget(TMP_Text text)
    {
        var roll = text.GetComponent<NumberRoll>();

        if (roll != null)
        {
            roll.Stop();
            roll._hasValue = false;
        }
    }

    private static NumberRoll Get(TMP_Text text)
    {
        var roll = text.GetComponent<NumberRoll>();

        if (roll == null)
        {
            roll       = text.gameObject.AddComponent<NumberRoll>();
            roll._text = text;
        }

        return roll;
    }

    private void Write(double value, Func<double, string> format, bool punch)
    {
        _format = format;

        if (_hasValue && value == _target)
        {
            if (_roll == null)
            {
                _text.text = format(value); // 값은 같아도 문구가 바뀌었을 수 있다(언어 · '최대' 표시) — 같은 글자면 TMP가 건너뛴다
            }

            return;
        }

        bool roll = _hasValue && isActiveAndEnabled;

        Stop();

        _target   = value;
        _hasValue = true;

        if (!roll)
        {
            _shown      = value;
            _text.text  = format(value);

            return;
        }

        _roll = DOTween.To(() => _shown, Show, value, Duration)
                       .SetEase(Ease.OutCubic)
                       .SetUpdate(true)
                       .SetLink(gameObject, LinkBehaviour.KillOnDisable)
                       .OnKill(Finish);

        if (punch)
        {
            _punch = transform.DOPunchScale(Vector3.one * PunchScale, PunchTime, 4, 0.6f)
                              .SetUpdate(true)
                              .SetLink(gameObject, LinkBehaviour.KillOnDisable)
                              .OnKill(ResetScale);
        }
    }

    private void Show(double value)
    {
        _shown     = value;
        _text.text = _format(value);
    }

    // 끝났거나 끊겼다 — 목표 값으로 (OnKill)
    private void Finish()
    {
        _roll = null;

        Show(_target);
    }

    private void ResetScale()
    {
        _punch               = null;
        transform.localScale = Vector3.one;
    }

    // 도는 굴림을 끊는다 — 지금 값('_shown')에서 다음 굴림이 이어 간다
    private void Stop()
    {
        if (_roll != null && _roll.IsActive())
        {
            _roll.OnKill(null);
            _roll.Kill();
        }

        if (_punch != null && _punch.IsActive())
        {
            _punch.Kill(); // OnKill → 크기 되돌림
        }

        _roll  = null;
        _punch = null;
    }
}
