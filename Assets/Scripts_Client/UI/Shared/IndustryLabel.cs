using MikaProtocol;

// 산업 → 사용자에게 보일 이름.
//
// ⚠️ **여기가 출처가 아니라 사본이다.** 진짜 출처는 'GameDesign/Excel/Enum.xlsx'인데,
// 파이프라인이 그 한글 이름을 'Enum.cs'의 **주석('// 농사')으로만** 내보내 런타임에서 읽을 수 없다.
// 그래서 화면에 낼 이름이 코드에 한 벌 더 필요했다 — 화면마다 적어 두면 한쪽만 고쳐지므로 여기 모았다.
//
// 🔴 **이름이 바뀌면 엑셀과 이 파일을 함께 고쳐야 한다.** 그 이중화를 없애는 것이 일감
// 'T-047'(엑셀의 표시 이름을 런타임 데이터로) — **끝나면 이 파일은 지운다.**
//
// ※ 표기는 기획 단일 진실('게임기획코어.md')을 따른다 — 농사 · 채굴 · 벌목 · 사냥 · 발굴.
//   ⚠️ 엑셀·'Enum.cs' 주석은 아직 "채광"이라 **지금 둘이 어긋나 있다**(T-047이 통일한다).
public static class IndustryLabel
{
    // 화면에 산업을 늘어놓는 순서 — 일반 산업 넷(농사 · 채굴 · 벌목 · 사냥) 다음 특수 산업 발굴을 맨 끝에 둔다(2026-10-19).
    // 'EIndustryType' 값 순서(농사 · 발굴 · …)와 다르다 — enum 값은 서버·DB와 묶여 있어 못 바꾸므로 표시 순서를 따로 둔다.
    // ※ 산업 탭 · 적성 칸 · 필터 · 특성 줄이 모두 이 순서를 탄다 — 화면마다 다르면 같은 칸을 다르게 읽는다.
    //   인스펙터에 버튼·칸을 순서대로 넣는 화면(작업슬롯 선택 · 적성 스트립)도 이 순서로 채운다.
    public static readonly EIndustryType[] DisplayOrder =
    {
        EIndustryType.Farming,
        EIndustryType.Mining,
        EIndustryType.Logging,
        EIndustryType.Hunting,
        EIndustryType.Excavation,
    };

    // 이 산업의 표시 이름. 모르는 값이면 영문 이름을 그대로 돌려준다 —
    // 빈 문자열로 떨어뜨리면 화면에서 사라져 무엇이 빠졌는지 알 수 없다.
    public static string Get(EIndustryType industry)
    {
        switch (industry)
        {
            case EIndustryType.Farming:    return Lang.Get("UI.Industry.Farming");
            case EIndustryType.Excavation: return Lang.Get("UI.Industry.Excavation");
            case EIndustryType.Mining:     return Lang.Get("UI.Industry.Mining");
            case EIndustryType.Logging:    return Lang.Get("UI.Industry.Logging");
            case EIndustryType.Hunting:    return Lang.Get("UI.Industry.Hunting");
            case EIndustryType.None:       return Lang.Get("UI.Industry.None");

            default: return industry.ToString();
        }
    }
}
