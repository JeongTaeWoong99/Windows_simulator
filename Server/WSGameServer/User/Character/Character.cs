using GameData;

namespace WSGameServer;

// 유저가 소유한 캐릭터 개체. 같은 TID를 여러 장 가질 수 있어 Id(DB PK)와 Tid(테이블 정의)를 반드시 구분한다.
// 스탯(적성)은 TID별 고정값이라 들고 있지 않고 CharacterTableRow에서 읽는다 — 저장하면 DB가 낡은 밸런스를 붙든다.
public sealed class Character
{
    /// <summary>테이블 정의를 생성자로 받는다 — 정적 조회에 묶이지 않아 테스트에서 바로 만들 수 있다.</summary>
    public Character(long id, CharacterTableRow row, int level, int exp, AptitudeBonus bonus = default)
    {
        Id    = id;
        Row   = row;
        Level = level;
        Exp   = exp;
        Bonus = bonus;
    }

    /// <summary>캐릭터 개체 PK (<c>t_character.character_id</c>). DB가 발급한다.</summary>
    public long Id { get; }

    /// <summary>캐릭터 종류 (<c>CharacterTable.CharacterTID</c>).</summary>
    public int Tid => Row.CharacterTID;

    public CharacterTableRow Row { get; }

    public string Name => Row.Name;

    public int Level { get; private set; }

    /// <summary>현재 레벨에서 쌓은 경험치(누적이 아니다). 레벨업하면 필요치를 뺀 나머지가 이월된다.</summary>
    public int Exp { get; private set; }

    /// <summary>적성 포인트로 찍은 보너스(산업별). DB에 저장되는 성장 상태는 레벨·경험치와 이것뿐이다.</summary>
    public AptitudeBonus Bonus { get; private set; }

    /// <summary>남은 적성 포인트 = 레벨로 번 총합 − 찍은 합. 저장하지 않고 늘 계산한다 — 둘을 따로 두면 어긋난다.</summary>
    public int RemainingPoints(CharacterLevelCatalog curve) => curve.PointsEarnedBy(Level) - Bonus.Total;

    // 포인트 1개로 산업 하나를 +1. 거절이면 아무것도 바꾸지 않는다. 되돌리기는 없다(캐릭터 기획 5.4).
    public AptitudeRaiseResult TryRaiseAptitude(IndustryType industry, CharacterLevelCatalog curve)
    {
        if (RemainingPoints(curve) <= 0)
        {
            return AptitudeRaiseResult.NoPoint;
        }

        if (GetAptitude(industry) >= GetAptitudeCap(industry))
        {
            return AptitudeRaiseResult.AtCap;
        }

        Bonus = Bonus.Plus(industry);
        return AptitudeRaiseResult.Ok;
    }

    public bool IsMaxLevel(CharacterLevelCatalog curve) => Level >= curve.MaxLevel;

    /// <summary>경험치를 더하고 필요치를 채운 만큼 레벨을 올린다. 만렙에서는 쌓지 않는다. 오른 레벨 수를 돌려준다.</summary>
    public int GainExp(int amount, CharacterLevelCatalog curve)
    {
        if (amount <= 0 || IsMaxLevel(curve))
        {
            return 0;
        }

        Exp += amount;

        var gained = 0;
        while (!IsMaxLevel(curve) && curve.TryGetRequiredExp(Level + 1, out var required) && Exp >= required)
        {
            Exp -= required;
            Level++;
            gained++;
        }

        // 만렙에 닿으면 남은 조각은 버린다 — 상한 위로 쌓이는 값은 아무 의미가 없다.
        if (IsMaxLevel(curve))
        {
            Exp = 0;
        }

        return gained;
    }

    /// <summary>이 산업의 실효 적성(0~10) = 테이블 기본값 + 찍은 보너스. 미지정(<c>None</c>)은 0이다.</summary>
    public int GetAptitude(IndustryType industry)
    {
        var baseAptitude = industry switch
        {
            IndustryType.Farming => Row.Farming,
            IndustryType.Fishing => Row.Fishing,
            IndustryType.Mining  => Row.Mining,
            IndustryType.Logging => Row.Logging,
            IndustryType.Hunting => Row.Hunting,
            _                    => 0,
        };

        return baseAptitude + Bonus.Get(industry);
    }

    /// <summary>이 산업에서 포인트로 오를 수 있는 최댓값. 캐릭터·산업마다 엑셀에 적힌 값이다.</summary>
    public int GetAptitudeCap(IndustryType industry)
    {
        return industry switch
        {
            IndustryType.Farming => Row.FarmingCap,
            IndustryType.Fishing => Row.FishingCap,
            IndustryType.Mining  => Row.MiningCap,
            IndustryType.Logging => Row.LoggingCap,
            IndustryType.Hunting => Row.HuntingCap,
            _                    => 0,
        };
    }

    /// <summary>적성 0 = 그 산업을 다루지 못한다. 배치를 거절하는 기준이다.</summary>
    public bool CanWork(IndustryType industry) => GetAptitude(industry) > 0;

    // 적성이 정의되는 1차 산업 5종. 클라에 내려보내는 적성 목록도 이 순서를 그대로 따른다.
    // 미지정(None)만 빠진다 — 아이템 분류는 IndustryType에 애초에 없다.
    public static readonly IndustryType[] Industries =
    {
        IndustryType.Farming, IndustryType.Fishing,
        IndustryType.Mining,  IndustryType.Logging, IndustryType.Hunting,
    };

    // 이 산업의 기본 작업속도(천분율). 보정이 붙기 전의 값이며 슬롯 확정값(CurrentWorkSpeed)과 구분한다.
    // 변환식을 코드에 두지 않는 이유: 재화 생성량에 직접 곱해지는 값이라 테이블에 둬야 엑셀만으로 밸런스가 끝난다.
    public int GetBaseWorkSpeed(IndustryType industry)
    {
        var aptitude = GetAptitude(industry);

        // Ref 검사가 CharacterTable의 적성을 WorkSpeedTable.WorkSpeedTID와 대조하므로
        // 정상 데이터에서는 반드시 찾아진다. 못 찾으면 데이터가 깨진 것이니 0으로 막는다.
        return GameTable.WorkSpeedTable.TryGet(aptitude, out var row) ? row.BaseWorkSpeedPermille : 0;
    }
}
