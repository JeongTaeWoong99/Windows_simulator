using UnityEngine;

// 한 줄 안에서 **라벨과 값을 색으로 가르는** TMP 리치 텍스트 조각 — 색은 테마 역할에서 읽는다.
//
// ■ 왜 필요한가
// 안내문·힌트가 "총 1,000 G · 등록비 10 G · 즉시 판매하면 400 G"처럼 한 색으로 이어지면 눈이 값을 못 찾는다.
// 라벨은 흐린 색(TextSub), 가격은 강조색(Highlight)으로 칠하면 같은 문장이 표처럼 읽힌다.
//
// ※ 색 값을 코드에 적지 않는다 — 팔레트('UIThemePalette')가 바뀌면 문구도 따라와야 한다.
//   문자열을 만드는 순간의 팔레트를 쓰므로, 팔레트를 바꾼 뒤에는 화면이 다시 그려질 때 반영된다.
public static class UIRichText
{
    // 'text'를 역할의 색으로 칠한다.
    public static string Paint(string text, UIThemeRole role)
        => $"<color=#{ColorUtility.ToHtmlStringRGB(UIThemePalette.Of(role))}>{text}</color>";

    // 'text'를 임의의 색으로 칠한다 — 등급색처럼 테마 밖의 의미 색.
    public static string Paint(string text, Color color)
        => $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>";

    // 라벨 — 흐린 글씨. "라벨 값"에서 앞쪽.
    public static string Label(string text) => Paint(text, UIThemeRole.TextSub);

    // 골드 금액 — 강조색 '1,234 G'.
    public static string Gold(long amount) => Paint($"{amount:N0} G", UIThemeRole.Highlight);

    // 조금 작은 글씨 — 부연 설명 줄.
    public static string Small(string text) => $"<size=85%>{text}</size>";

    // 라벨 · 값 한 쌍 — '<흐린>총</흐린> 1,000 G'.
    public static string Pair(string label, string value) => $"{Label(label)} {value}";

    // 줄 사이 가르는 점 — 흐린 색이라 값 사이에서 튀지 않는다.
    public static string Dot => Paint(" · ", UIThemeRole.TextDisabled);
}
