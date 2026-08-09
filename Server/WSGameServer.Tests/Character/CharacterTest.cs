using GameData;

namespace WSGameServer;

/// <summary>
/// 캐릭터 적성 검증.
///
/// <b>캐릭터 스탯 = 산업 적성</b>이고 적성이 곧 채취 속도가 되므로,
/// 여기서 어긋나면 재화 생성량이 통째로 틀어진다.
/// </summary>
public class CharacterTest
{
    private static CharacterTableRow Row(
        int tid = 1001,
        int farming = 0, int fishing = 0, int mining = 0, int logging = 0, int hunting = 0)
    {
        return new()
        {
            CharacterTID = tid,
            Name         = "테스트",
            Farming      = farming,
            Fishing      = fishing,
            Mining       = mining,
            Logging      = logging,
            Hunting      = hunting,
        };
    }

    private static Character Make(
        CharacterTableRow row, long id = 1, int level = 1, int exp = 0)
        => new(id, row, level, exp);

    [Fact]
    public void 산업마다_다른_적성을_돌려준다()
    {
        var character = Make(Row(farming: 3, fishing: 7, mining: 0, logging: 1, hunting: 10));

        character.GetAptitude(IndustryType.Farming).ShouldBe(3);
        character.GetAptitude(IndustryType.Fishing).ShouldBe(7);
        character.GetAptitude(IndustryType.Mining).ShouldBe(0);
        character.GetAptitude(IndustryType.Logging).ShouldBe(1);
        character.GetAptitude(IndustryType.Hunting).ShouldBe(10);
    }

    [Fact]
    public void 산업_미지정이면_적성이_0이다()
    {
        // 아이템 분류(Misc·Special)는 IndustryType에 아예 없어 여기 넘길 수조차 없다 —
        // 예전엔 ItemType 하나를 공유해서 런타임에만 걸러졌다(T-023).
        Make(Row(farming: 10, fishing: 10, mining: 10, logging: 10, hunting: 10))
            .GetAptitude(IndustryType.None).ShouldBe(0);
    }

    [Fact]
    public void 적성이_0이면_그_산업을_다루지_못한다()
    {
        var character = Make(Row(fishing: 5, mining: 0));

        character.CanWork(IndustryType.Fishing).ShouldBeTrue();
        character.CanWork(IndustryType.Mining).ShouldBeFalse();
    }

    [Fact]
    public void 산업_목록은_1차_산업_5종이다()
    {
        // 클라에 내려보내는 적성 목록의 기준이다. 아이템 분류(Misc·Special)가 섞이면 배치 UI에 뜬다.
        Character.Industries.ShouldBe(new[]
        {
            IndustryType.Farming, IndustryType.Fishing, IndustryType.Mining, IndustryType.Logging, IndustryType.Hunting,
        });
    }

    [Fact]
    public void 개체_ID와_테이블_TID는_별개다()
    {
        // 같은 캐릭터(TID)를 여러 장 가질 수 있으므로 둘을 섞으면 구분이 안 된다.
        var row = Row(tid: 1001, fishing: 4);

        var first  = Make(row, id: 500);
        var second = Make(row, id: 501);

        first.Tid.ShouldBe(1001);
        second.Tid.ShouldBe(1001);
        first.Id.ShouldNotBe(second.Id);
    }

    [Fact]
    public void 시작_캐릭터_TID는_1001이다()
    {
        // 2~1000번은 예약 대역이고 일반 캐릭터는 1001부터다.
        // 구 TID 1('기본 캐릭터')은 폐기 예정이라 시작 지급이 1001로 옮겨졌다.
        User.DefaultCharacterTid.ShouldBe(1001);
    }
}
