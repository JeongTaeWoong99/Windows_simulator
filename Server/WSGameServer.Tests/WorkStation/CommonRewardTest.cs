using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 채취 공통 보상(상자 · T-030) 검증 — 자원 롤과 따로, <b>그 슬롯 레벨의 공통 행</b>을 판정 1회마다 행마다 따로 굴린다.
/// 그 (산업, 레벨)에 덮어쓰기 행이 있으면 공통 행 대신 덮어쓰기 행만 굴린다.
/// 보상은 그 슬롯의 채취 결과(<see cref="S_GatherResultResponse"/>)에 함께 실린다.
/// 확률 자체는 난수라 경계값(항상 · 없음)만 본다.
/// </summary>
public class CommonRewardTest
{
    private static readonly DateTime Base = TestUserBuilder.Base;

    private const int  AllRounderTid = 1001;
    private const long CharacterId   = 500;
    private const int  BoxTid        = 100019;   // 나무 상자 Lv1

    public CommonRewardTest() => GameTableFixture.EnsureLoaded();

    private static CommonRewardTableRow Common(int tid, int level, int itemTid, int count = 1, int chance = 1_000_000)
        => new() { CommonRewardTID = tid, IndustryLevel = level, ItemTID = itemTid, Count = count, ChancePerMillion = chance };

    private static CommonRewardOverrideTableRow Override(int tid, IndustryType industry, int level, int itemTid)
        => new() { CommonRewardOverrideTID = tid, IndustryType = industry, IndustryLevel = level, ItemTID = itemTid, Count = 1, ChancePerMillion = 1_000_000 };

    /// <summary>낚시 Lv1 슬롯 하나를 굴리는 유저.</summary>
    private static (User User, TestUserBuilder B) UserWith(
        CommonRewardTableRow[]          commons,
        CommonRewardOverrideTableRow[]? overrides = null)
    {
        var b = new TestUserBuilder().WithFishingDrops();
        b.CommonRewards.Load(commons, overrides ?? Array.Empty<CommonRewardOverrideTableRow>());

        var user = b.Build();
        user.LoadCharacters(new[]
        {
            new CharacterRow { character_id = CharacterId, character_tid = AllRounderTid, level = 1, exp = 0 },
        });
        user.WorkStation.Load(new[] { new WorkStationSlot(0, IndustryType.Fishing, CharacterId, Base) });
        return (user, b);
    }

    [Fact]
    public void 확률이_백만분의_백만이면_판정마다_나온다()
    {
        // 기본 속도 30초에 1판정 → 5분 = 10판정 → 상자 10개
        var (user, b) = UserWith(new[] { Common(101, 1, BoxTid) });

        user.SettleWorkStation(Base.AddMinutes(5));

        user.GetItemCount(BoxTid).ShouldBe(10);
        b.Channel.SentOf<S_GatherResultResponse>().ShouldHaveSingleItem()
            .ItemChanges!.ShouldContain(c => c.ItemId == BoxTid && c.Count == 10);
    }

    [Fact]
    public void 행마다_따로_굴린다()
    {
        // 두 행 모두 확정 — 한 판정에서 둘 다 나올 수 있어야 한다(택1이 아니다).
        var (user, _) = UserWith(new[] { Common(101, 1, BoxTid), Common(102, 1, BoxTid + 1, count: 2) });

        user.SettleWorkStation(Base.AddSeconds(30));

        user.GetItemCount(BoxTid).ShouldBe(1);
        user.GetItemCount(BoxTid + 1).ShouldBe(2);
    }

    [Fact]
    public void 상자가_나와도_자원은_그대로_나온다()
    {
        // 자원 롤과 따로 굴린다 — 상자가 그 판정의 자원을 대신하지 않는다.
        var (user, _) = UserWith(new[] { Common(101, 1, BoxTid) });

        user.SettleWorkStation(Base.AddMinutes(5));

        user.GetItemCount(BoxTid).ShouldBe(10);
        user.GetItemCount(TestUserBuilder.FishItemTid).ShouldBe(10);
    }

    [Fact]
    public void 슬롯_레벨과_다른_레벨의_행은_굴리지_않는다()
    {
        // 슬롯은 Lv1 — Lv2 행은 확정이어도 나오지 않는다.
        var (user, _) = UserWith(new[] { Common(201, 2, BoxTid + 3) });

        user.SettleWorkStation(Base.AddMinutes(5));

        user.GetItemCount(BoxTid + 3).ShouldBe(0);
    }

    [Fact]
    public void 덮어쓰기_행이_있으면_그_레벨의_공통_행은_통째로_무시한다()
    {
        // 낚시 Lv1에 덮어쓰기가 하나라도 있으면 공통 행(확정)은 굴리지 않는다 — 레벨 단위 덮어쓰기.
        var (user, _) = UserWith(
            new[] { Common(101, 1, BoxTid) },
            new[] { Override(2011, IndustryType.Fishing, 1, BoxTid + 1) });

        user.SettleWorkStation(Base.AddMinutes(5));

        user.GetItemCount(BoxTid).ShouldBe(0);
        user.GetItemCount(BoxTid + 1).ShouldBe(10);
    }

    [Fact]
    public void 다른_산업의_덮어쓰기는_공통_행을_막지_않는다()
    {
        // 덮어쓰기는 채굴 Lv1 — 낚시 슬롯은 공통 행을 그대로 굴린다.
        var (user, _) = UserWith(
            new[] { Common(101, 1, BoxTid) },
            new[] { Override(3011, IndustryType.Mining, 1, BoxTid + 1) });

        user.SettleWorkStation(Base.AddMinutes(5));

        user.GetItemCount(BoxTid).ShouldBe(10);
        user.GetItemCount(BoxTid + 1).ShouldBe(0);
    }

    [Fact]
    public void 공통_보상이_없으면_드롭만_나온다()
    {
        var (user, _) = UserWith(Array.Empty<CommonRewardTableRow>());

        user.SettleWorkStation(Base.AddMinutes(5));

        user.GetItemCount(BoxTid).ShouldBe(0);
        user.GetItemCount(TestUserBuilder.FishItemTid).ShouldBe(10);
    }
}
