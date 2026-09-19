using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 채취 공통 보상(T-030) 검증 — 산업·레벨과 무관하게 <b>판정 1회마다 행마다 따로</b> 굴린다.
/// 보상은 그 슬롯의 채취 결과(<see cref="S_GatherResultResponse"/>)에 함께 실린다.
/// 확률 자체는 난수라 경계값(항상 · 없음)만 본다.
/// </summary>
public class CommonRewardTest
{
    private static readonly DateTime Base = TestUserBuilder.Base;

    private const int  AllRounderTid = 1001;
    private const long CharacterId   = 500;
    private const int  BoxTid        = 100007;

    public CommonRewardTest() => GameTableFixture.EnsureLoaded();

    private static (User User, TestUserBuilder B) UserWith(params CommonRewardTableRow[] rewards)
    {
        var b = new TestUserBuilder().WithFishingDrops();
        b.CommonRewards.Load(rewards);

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
        var (user, b) = UserWith(new CommonRewardTableRow { CommonRewardTID = 1, ItemTID = BoxTid, Count = 1, ChancePerMillion = 1_000_000 });

        user.SettleWorkStation(Base.AddMinutes(5));

        user.GetItemCount(BoxTid).ShouldBe(10);
        b.Channel.SentOf<S_GatherResultResponse>().ShouldHaveSingleItem()
            .ItemChanges!.ShouldContain(c => c.ItemId == BoxTid && c.Count == 10);
    }

    [Fact]
    public void 행마다_따로_굴린다()
    {
        // 두 행 모두 확정 — 한 판정에서 둘 다 나올 수 있어야 한다(택1이 아니다).
        var (user, _) = UserWith(
            new CommonRewardTableRow { CommonRewardTID = 1, ItemTID = BoxTid,     Count = 1, ChancePerMillion = 1_000_000 },
            new CommonRewardTableRow { CommonRewardTID = 2, ItemTID = BoxTid + 1, Count = 2, ChancePerMillion = 1_000_000 });

        user.SettleWorkStation(Base.AddSeconds(30));

        user.GetItemCount(BoxTid).ShouldBe(1);
        user.GetItemCount(BoxTid + 1).ShouldBe(2);
    }

    [Fact]
    public void 공통_보상이_없으면_드롭만_나온다()
    {
        var (user, _) = UserWith();

        user.SettleWorkStation(Base.AddMinutes(5));

        user.GetItemCount(BoxTid).ShouldBe(0);
        user.GetItemCount(TestUserBuilder.FishItemTid).ShouldBe(10);
    }
}
