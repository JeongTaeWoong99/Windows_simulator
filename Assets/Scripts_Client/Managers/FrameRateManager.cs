using System;
using System.Collections.Generic;
using UnityEngine;

// 프레임 제한 항목. 숫자 항목은 'targetFrameRate'로, 모니터 동기화는 'vSyncCount'로 건다.
public enum FrameRateOption
{
    Fps30,
    Fps60,
    Fps90,
    Fps144,
    VSync,
}

// FPS 텍스트를 창의 어느 구석에 띄우는가. 'Hidden'이면 띄우지 않는다.
public enum FpsTextPosition
{
    Hidden,
    UpperLeft,
    UpperRight,
    LowerLeft,
    LowerRight,
}

// 프레임 제한과 FPS 텍스트 위치를 정하고 적용한다 — 설정 화면이 고르고, 저장은 'WindowSettings'가 한다.
//
// ■ 권위 소스 — 에디터·빌드 모두 저장값
// 창 설정('WindowManager')과 달리 에디터에서도 저장값을 읽는다. 프레임은 에디터에서도 실제로
// 먹는 값이라 마지막 선택이 유지되는 편이 테스트에 맞다. 인스펙터 'setStart*'는 첫 실행 기본값뿐이다.
//
// ⚠️ 모니터 동기화는 에디터에서 Game 뷰의 VSync 토글이 덮을 수 있다('Settings 규칙.md' 1장).
public class FrameRateManager : MonoService<FrameRateManager>
{
    // 숫자 항목의 목표 프레임. 'FrameRateOption' 순서와 같다(VSync는 숫자가 없다).
    private static readonly int[] TargetFrameRates = { 30, 60, 90, 144 };

    [CenterHeader("첫 실행 기본값 (저장값이 있으면 그걸 따른다)")]
    [SerializeField] private FrameRateOption setStartFrameRate       = FrameRateOption.Fps60;
    [SerializeField] private FpsTextPosition setStartFpsTextPosition = FpsTextPosition.Hidden;

    private FrameRateOption _frameRate;
    private FpsTextPosition _fpsTextPosition;

    // FPS 텍스트 위치가 바뀌었다 — 'FpsTextPresenter'가 구독한다.
    public event Action<FpsTextPosition>? FpsTextPositionChanged;

    public int             FrameRateIndex       => (int)_frameRate;
    public int             FpsTextPositionIndex => (int)_fpsTextPosition;
    public FpsTextPosition FpsTextPosition      => _fpsTextPosition;

    // 저장값을 읽어 곧바로 프레임을 건다. 다른 서비스를 건드리지 않는 순수 값 로드라 Awake에서 안전하다.
    protected override void Awake()
    {
        base.Awake();

        // 저장값이 열거형 범위를 벗어나면(버전이 바뀌어 항목이 줄었다면) 안쪽으로 당긴다.
        int frameRate = WindowSettings.LoadInt(WindowSettings.FrameRateKey,       (int)setStartFrameRate);
        int position  = WindowSettings.LoadInt(WindowSettings.FpsTextPositionKey, (int)setStartFpsTextPosition);

        _frameRate       = (FrameRateOption)Mathf.Clamp(frameRate, 0, (int)FrameRateOption.VSync);
        _fpsTextPosition = (FpsTextPosition)Mathf.Clamp(position,  0, (int)FpsTextPosition.LowerRight);

        ApplyFrameRate();
    }

    // 프레임 드롭다운 옵션 라벨을 'FrameRateOption' 순서대로 만든다.
    public List<string> GetFrameRateLabels() => new List<string>
    {
        "30", "60", "90", "144", "모니터 동기화",
    };

    // FPS 텍스트 위치 드롭다운 옵션 라벨을 'FpsTextPosition' 순서대로 만든다.
    public List<string> GetFpsTextPositionLabels() => new List<string>
    {
        "숨김", "Upper Left", "Upper Right", "Lower Left", "Lower Right",
    };

    // 프레임 항목을 인덱스로 고른다 (설정 드롭다운 onValueChanged). 저장하고 즉시 건다.
    public void SetFrameRateByIndex(int index)
    {
        _frameRate = (FrameRateOption)Mathf.Clamp(index, 0, (int)FrameRateOption.VSync);
        WindowSettings.SaveInt(WindowSettings.FrameRateKey, (int)_frameRate);
        ApplyFrameRate();
    }

    // FPS 텍스트 위치를 인덱스로 고른다 (설정 드롭다운 onValueChanged). 저장하고 알린다.
    public void SetFpsTextPositionByIndex(int index)
    {
        _fpsTextPosition = (FpsTextPosition)Mathf.Clamp(index, 0, (int)FpsTextPosition.LowerRight);
        WindowSettings.SaveInt(WindowSettings.FpsTextPositionKey, (int)_fpsTextPosition);
        FpsTextPositionChanged?.Invoke(_fpsTextPosition);
    }

    // 현재 항목을 엔진에 건다. 'vSyncCount'가 0이 아니면 'targetFrameRate'는 무시되므로 숫자 항목은 VSync를 끈다.
    private void ApplyFrameRate()
    {
        if (_frameRate == FrameRateOption.VSync)
        {
            QualitySettings.vSyncCount = 1;
            return;
        }

        QualitySettings.vSyncCount  = 0;
        Application.targetFrameRate = TargetFrameRates[(int)_frameRate];
    }
}
