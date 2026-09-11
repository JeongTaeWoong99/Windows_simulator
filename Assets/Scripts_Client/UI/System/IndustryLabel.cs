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
// ※ 표기는 기획 단일 진실('게임기획코어.md')을 따른다 — 농사 · 낚시 · 채굴 · 벌목 · 사냥.
//   ⚠️ 엑셀·'Enum.cs' 주석은 아직 "채광"이라 **지금 둘이 어긋나 있다**(T-047이 통일한다).
public static class IndustryLabel
{
    // 이 산업의 표시 이름. 모르는 값이면 영문 이름을 그대로 돌려준다 —
    // 빈 문자열로 떨어뜨리면 화면에서 사라져 무엇이 빠졌는지 알 수 없다.
    public static string Get(EIndustryType industry)
    {
        switch (industry)
        {
            case EIndustryType.Farming: return "농사";
            case EIndustryType.Fishing: return "낚시";
            case EIndustryType.Mining:  return "채굴";
            case EIndustryType.Logging: return "벌목";
            case EIndustryType.Hunting: return "사냥";
            case EIndustryType.None:    return "미지정";

            default: return industry.ToString();
        }
    }
}
