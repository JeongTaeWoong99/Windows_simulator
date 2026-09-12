using GameData;

namespace WSGameServer;

/// <summary>
/// 유저가 소유한 캐릭터 <b>개체</b>. 같은 캐릭터(TID)를 여러 장 가질 수 있으므로
/// <see cref="Id"/>(DB 발급 PK)와 <see cref="Tid"/>(테이블 정의)는 반드시 구분한다.
///
/// <para>
/// <b>스탯을 들고 있지 않다.</b> 캐릭터 스탯 = 산업 적성이고, 적성은 TID별 고정값이라
/// <see cref="CharacterTableRow"/>에서 읽는다. 저장해 두면 엑셀에서 밸런스를 조정해도
/// DB가 낡은 값을 붙들게 된다. 개체가 들고 있는 것은 성장의 <b>입력</b>(레벨·경험치)뿐이다.
/// </para>
/// </summary>
public sealed class Character
{
    /// <summary>테이블 정의를 생성자로 받는다 — 정적 조회에 묶이지 않아 테스트에서 바로 만들 수 있다.</summary>
    public Character(long id, CharacterTableRow row, int level, int exp)
    {
        Id    = id;
        Row   = row;
        Level = level;
        Exp   = exp;
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

    /// <summary>이 산업에 대한 적성(0~10). <b>캐릭터 스탯이 곧 산업 적성이다.</b> 미지정(<c>None</c>)은 0이다.</summary>
    public int GetAptitude(IndustryType industry)
    {
        return industry switch
        {
            IndustryType.Farming => Row.Farming,
            IndustryType.Fishing => Row.Fishing,
            IndustryType.Mining  => Row.Mining,
            IndustryType.Logging => Row.Logging,
            IndustryType.Hunting => Row.Hunting,
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

    /// <summary>
    /// 이 산업에서의 <b>기본 작업속도</b>(천분율). 적성을 <c>WorkSpeedTable</c>로 변환한다.
    ///
    /// <para>
    /// 특성·부스트·장비 보정이 붙기 <b>전</b>의 값이며, 슬롯의 최종 확정값
    /// (<c>WorkStationSlot.CurrentWorkSpeed</c>)과 구분한다.
    /// </para>
    ///
    /// <para>
    /// 변환식을 코드에 두지 않는 이유는 이 값이 <b>재화 생성량에 직접 곱해지기</b> 때문이다.
    /// 테이블에 두면 밸런스 조정이 엑셀 수정만으로 끝난다.
    /// </para>
    /// </summary>
    public int GetBaseWorkSpeed(IndustryType industry)
    {
        var aptitude = GetAptitude(industry);

        // Ref 검사가 CharacterTable의 적성을 WorkSpeedTable.WorkSpeedTID와 대조하므로
        // 정상 데이터에서는 반드시 찾아진다. 못 찾으면 데이터가 깨진 것이니 0으로 막는다.
        return GameTable.WorkSpeedTable.TryGet(aptitude, out var row) ? row.BaseWorkSpeedPermille : 0;
    }
}
