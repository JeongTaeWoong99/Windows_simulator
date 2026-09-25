using System.Collections.Generic;
using UnityEngine;

// 툴팁 한 장에 그릴 내용 — 제목 한 줄과 그 아래 줄 목록.
//
// ■ 모양이 둘인데 타입은 하나다
// 이름만 알리는 간단형("세팅")은 제목만, 세부 정보(산업 레벨)는 제목 + 줄이다.
// 줄이 없으면 'TooltipPresenter'가 줄 영역을 접어 제목 폭에 맞게 줄어든다 — 부르는 쪽이 모양을 고르지 않는다.
//
// 값은 **완성된 문구**로 받는다. 툴팁은 테이블·세션을 모른다('UI 규칙.md'의 종속 View 규약과 같은 축).
public sealed class TooltipContent
{
    // 줄 하나. 값 칸이 둘인 이유는 'TooltipRowView' 머리 주석.
    public readonly struct Line
    {
        public readonly string Label;
        public readonly string Value;
        public readonly string SubValue;
        public readonly Color? Background; // null이면 줄 프리팹의 기본 바탕색

        public Line(string label, string value, string subValue, Color? background)
        {
            Label      = label;
            Value      = value;
            SubValue   = subValue;
            Background = background;
        }
    }

    private readonly List<Line> _lines = new List<Line>();

    public string             Title { get; }
    public IReadOnlyList<Line> Lines => _lines;

    public TooltipContent(string title)
    {
        Title = title;
    }

    // 묶음 제목 줄 — 줄이 여러 묶음일 때 사이에 끼워 가른다.
    public TooltipContent Header(string text) => Row($"■ {text}", "", "", null);

    // 라벨 · 값 한 줄.
    public TooltipContent Row(string label, string value) => Row(label, value, "", null);

    // 라벨 · 값 · 보조 값 한 줄. 'background'로 줄 바탕을 칠한다(등급색 등).
    public TooltipContent Row(string label, string value, string subValue, Color? background)
    {
        _lines.Add(new Line(label, value, subValue, background));

        return this;
    }
}
