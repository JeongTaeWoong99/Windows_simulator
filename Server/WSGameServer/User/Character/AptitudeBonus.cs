using GameData;

namespace WSGameServer;

/// <summary>
/// 캐릭터 개체가 적성 포인트로 <b>찍은 보너스</b>(산업별). 실효 적성 = 기본값(<c>CharacterTable</c>) + 이 값.
/// DB(<c>t_character.*_bonus</c>)와 1:1이고, 남은 포인트는 여기 두지 않는다 — 레벨로 번 총합에서 뺀다.
/// </summary>
public readonly record struct AptitudeBonus(
    int Farming = 0,
    int Fishing = 0,
    int Mining  = 0,
    int Logging = 0,
    int Hunting = 0)
{
    public int Get(IndustryType industry)
    {
        return industry switch
        {
            IndustryType.Farming => Farming,
            IndustryType.Fishing => Fishing,
            IndustryType.Mining  => Mining,
            IndustryType.Logging => Logging,
            IndustryType.Hunting => Hunting,
            _                    => 0,
        };
    }

    /// <summary>찍은 포인트 합. 남은 포인트 계산의 피감수다.</summary>
    public int Total => Farming + Fishing + Mining + Logging + Hunting;

    /// <summary>해당 산업에 1을 더한 새 값. 미지정 산업이면 그대로다.</summary>
    public AptitudeBonus Plus(IndustryType industry)
    {
        return industry switch
        {
            IndustryType.Farming => this with { Farming = Farming + 1 },
            IndustryType.Fishing => this with { Fishing = Fishing + 1 },
            IndustryType.Mining  => this with { Mining  = Mining  + 1 },
            IndustryType.Logging => this with { Logging = Logging + 1 },
            IndustryType.Hunting => this with { Hunting = Hunting + 1 },
            _                    => this,
        };
    }
}

/// <summary>적성 포인트 찍기의 결과. 거절 사유를 프로토콜 코드로 옮기는 건 <c>User</c>가 한다.</summary>
public enum AptitudeRaiseResult
{
    Ok      = 0,
    NoPoint = 1,   // 남은 포인트 0
    AtCap   = 2,   // 그 산업이 이미 상한
}
