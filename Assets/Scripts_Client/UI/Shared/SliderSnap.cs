using UnityEngine;
using UnityEngine.UI;

// 진행 막대 값을 막대 폭 1칸 단위로 끊어 넣는다 (2026-11-21 UI 점검).
//
// ■ 왜 — 작업 칸 · 위젯의 진행 막대 40개가 매 프레임 값을 받아, 채움이 1px도 안 늘었는데 매 프레임 다시 그려졌다
//   (300프레임 집계에서 다시 그린 그림의 대부분). 값을 칸 단위로 끊으면 칸이 바뀌는 프레임에만 다시 그린다.
//   'Slider.value'는 같은 값이 들어오면 아무것도 하지 않는다 — 끊은 값이 같으면 그대로 넘어간다.
public static class SliderSnap
{
    // 막대 폭(채움 영역의 칸 좌표 폭)만큼 잘게 끊은 값을 넣는다. 폭을 모르면 그대로 넣는다.
    public static void SetSnapped(this Slider slider, float value)
    {
        RectTransform? area = slider.fillRect != null ? slider.fillRect.parent as RectTransform : null;
        float          step = area != null ? Mathf.Max(1f, Mathf.Round(area.rect.width)) : 0f;

        slider.value = step > 0f ? Mathf.Round(Mathf.Clamp01(value) * step) / step : value;
    }
}
