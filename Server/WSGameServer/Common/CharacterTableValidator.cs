using GameData;

namespace WSGameServer;

/// <summary>
/// <c>CharacterTable</c>의 산업별 <b>기본 적성 ≤ 상한</b>을 기동 시 검사한다 (캐릭터 기획 5.4).
/// 엑셀 파이프라인은 컬럼 하나의 범위만 보므로 두 컬럼의 관계는 여기서 잡는다 — 어긋난 채 돌면 그 캐릭터는 영영 못 찍는다.
/// </summary>
public static class CharacterTableValidator
{
    /// <summary>위반을 전부 모아 한 번에 예외로 던진다. 정상이면 아무 일도 없다.</summary>
    public static void Validate(IEnumerable<CharacterTableRow> rows)
    {
        var errors = new List<string>();

        foreach (var row in rows)
        {
            var character = new Character(0, row, level: 1, exp: 0);
            foreach (var industry in Character.Industries)
            {
                var aptitude = character.GetAptitude(industry);
                var cap      = character.GetAptitudeCap(industry);
                if (aptitude > cap)
                {
                    errors.Add($"CharacterTID {row.CharacterTID} {industry}: 기본 적성 {aptitude} > 상한 {cap}");
                }
            }
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException("CharacterTable 상한 오류 " + errors.Count + "건\n  " + string.Join("\n  ", errors));
        }
    }
}
