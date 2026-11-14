using System.Text.RegularExpressions;

// 한국어 조사 고르기 — '질풍 검호이(가)' → '질풍 검호가' (T-201 · 2026-11-15).
//
// 문구 표에는 이름이 무엇이 올지 몰라 '이(가)'처럼 둘 다 적는다. 채운 뒤 앞 글자의 받침으로 하나를 고른다.
//   이(가) · 은(는) · 을(를) · 과(와) — 받침 있음 → 앞 · 없음 → 뒤
//   (으)로 — 받침 있음(ㄹ 받침 제외) → '으로' · 없음·ㄹ → '로'
// 앞 글자가 한글 음절이 아니면(숫자·영문·괄호) 손대지 않는다 — 읽는 소리를 알 수 없다.
// 사이에 낀 서식 태그('<color=…>고급</color>(으)로')는 건너뛰고 그 앞 글자를 본다.
public static class Josa
{
    private static readonly Regex Pattern = new Regex(@"([가-힣])((?:<[^>]+>)*)(이\(가\)|가\(이\)|은\(는\)|는\(은\)|을\(를\)|를\(을\)|과\(와\)|와\(과\)|\(으\)로)", RegexOptions.Compiled);

    // 문장 안의 '이(가)' 같은 겹 조사를 앞 글자에 맞는 하나로 바꾼다
    public static string Resolve(string text)
    {
        if (text.IndexOf('(') < 0)
        {
            return text;
        }

        return Pattern.Replace(text, m =>
        {
            char last  = m.Groups[1].Value[0];
            int  final = (last - 0xAC00) % 28; // 0 = 받침 없음 · 8 = ㄹ

            bool hasFinal = final != 0;

            string josa = m.Groups[3].Value switch
            {
                "이(가)" or "가(이)" => hasFinal ? "이" : "가",
                "은(는)" or "는(은)" => hasFinal ? "은" : "는",
                "을(를)" or "를(을)" => hasFinal ? "을" : "를",
                "과(와)" or "와(과)" => hasFinal ? "과" : "와",
                _                    => hasFinal && final != 8 ? "으로" : "로",
            };

            return m.Groups[1].Value + m.Groups[2].Value + josa;
        });
    }
}
