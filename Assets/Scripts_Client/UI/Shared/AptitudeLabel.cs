using UnityEngine;

// 적성 값(0~10) → 칸에 적을 문구와 색.
//
// ■ 왜 Presenter 밖에 있나
// 창고 칸('SlotView'의 적성 스트립)과 작업슬롯 선택 화면('CharacterStateRowView')이 같은 5칸을 그린다.
// 표기 규칙("0은 X · 흐리게")이 한쪽에만 고쳐지면 두 화면이 같은 캐릭터를 다르게 말한다.
// 'IndustryLabel'·'RarityPalette'와 같은 부류 — 캔버스를 가로지르는 표시용 변환이라 여기 둔다.
public static class AptitudeLabel
{
    // 적성 칸 수 = 1차 산업 5종. 'EIndustryType'의 None 제외 개수와 같아야 한다.
    public const int Count = 5;

    // 적성은 0~10이라 미리 만들어 둔다 — 창고는 200칸 × 5개를 매번 그리므로
    // 'ToString()'을 그때그때 부르면 그릴 때마다 문자열 1000개가 버려진다(상주 앱이라 쌓인다).
    // ※ 0번은 'X'다 — **빈 칸으로 두면 배선이 빠진 칸과 구분되지 않는다.**
    //   "못 다루는 산업"은 알려 줄 값이 없는 게 아니라 알려 줄 것이 있는 상태다.
    private static readonly string[] Texts = { "X", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10" };

    // 적성 0은 흐리게 — 'X'가 숫자와 같은 세기로 보이면 눈이 먼저 X를 읽는다.
    public static readonly Color ValueColor = Color.white;
    public static readonly Color ZeroColor  = new Color(0.62f, 0.62f, 0.62f, 1f);

    // 칸에 적을 문구 (적성 칸을 그릴 때 호출)
    public static string GetText(byte value)
        => value < Texts.Length ? Texts[value] : value.ToString();

    // 칸 글자색 (적성 칸을 그릴 때 호출)
    public static Color GetColor(byte value)
        => value == 0 ? ZeroColor : ValueColor;
}
