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
    //   글씨 역할(TextMain·TextSub·TextDisabled)은 색을 박지 않고 **바탕 글씨색을 이어받아 투명도만** 준다 (양피지 · 2026-11-14).
    //   한 화면에 밝은 양피지 바탕과 어두운 칸이 섞여, 색을 박으면 어느 한쪽에서 글씨가 묻힌다 — 바탕 글씨색은 태그('UIThemeColor')가 이미 바탕에 맞췄다.
    public static string Paint(string text, UIThemeRole role) => role switch
    {
        UIThemeRole.TextMain     => text,
        UIThemeRole.TextSub      => $"<alpha=#D0>{text}<alpha=#FF>", // B8(72%)은 양피지 위에서 너무 흐렸다 — 82%로(2026-11-15 사용자: 회색 글씨가 안 보인다)
        UIThemeRole.TextDisabled => $"<alpha=#A0>{text}<alpha=#FF>",
        _                        => $"<color=#{ColorUtility.ToHtmlStringRGB(UIThemePalette.Of(role))}>{text}</color>",
    };

    // 'text'를 임의의 색으로 칠한다 — 등급색처럼 테마 밖의 의미 색.
    public static string Paint(string text, Color color)
        => $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>";

    // 라벨 — 흐린 글씨. "라벨 값"에서 앞쪽.
    public static string Label(string text) => Paint(text, UIThemeRole.TextSub);

    // 골드 금액 — 강조색 '1,234 G'. 기본은 밝은 양피지 바탕용 진한 금(HighlightDark)
    public static string Gold(long amount) => Paint($"{amount:N0} G", UIThemeRole.HighlightDark);

    // 골드 금액 — 어두운 바탕(진홍 제목 띠·어두운 칸) 위 밝은 금
    public static string GoldOnDark(long amount) => Paint($"{amount:N0} G", UIThemeRole.Highlight);

    // 표 한 줄 — 왼쪽 라벨 · 오른쪽 끝에 붙인 값. 상세 상자(확인 팝업)처럼 값을 세로로 맞춰 읽게 한다.
    //   줄 높이 1.6em — 값을 키운 줄(총액 120%)이 위 줄과 겹치지 않게
    public static string Row(string label, string value) => $"{Label(label)}<line-height=0>\n<align=right>{value}</align><line-height=1.6em>\n";

    // 상자 안 묶음 사이 빈 줄 — 반 줄 높이
    public static string Gap => "<size=45%>\n</size>";

    // 조금 작은 글씨 — 부연 설명 줄.
    public static string Small(string text) => $"<size=85%>{text}</size>";

    // 라벨 · 값 한 쌍 — '<흐린>총</흐린> 1,000 G'.
    public static string Pair(string label, string value) => $"{Label(label)} {value}";

    // 줄 사이 가르는 점 — 흐린 색이라 값 사이에서 튀지 않는다.
    public static string Dot => Paint(" · ", UIThemeRole.TextDisabled);
}
