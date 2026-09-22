using GameData;
using MikaProtocol;

namespace WSGameServer;

/// <summary>
/// 창고 칸 한도(T-063) 검증 — 자원은 보유 <b>종류</b> 수, 캐릭터·장비는 <b>개체</b> 수가 칸이다(각 200).
/// 이번 결과가 새로 차지할 칸이 남은 칸보다 많으면 <c>StorageFull</c>로 거절하고 <b>재화·보유가 그대로</b>여야 한다.
/// 틀리면 200칸을 넘긴 개체가 클라 화면에서 보이지 않는 채로 쌓인다.
/// </summary>
public class StorageLimitTest
{
    private const int CharacterPoolId = 2;
    private const int CharacterTid    = 1002;
    private const int EquipPoolId     = 3;
    private const int SwordTid        = 1101;     // 나무 호미 (Common 무기)
    private const int ItemPoolId      = 1;
    private const int FishTid         = 10001;    // 붕어
    private const int WoodBoxTid      = 100007;   // 나무 상자 → 풀 6
    private const int BoxPoolId       = 6;

    // 창고를 채우는 자리 채움 TID — 표에 없는 값이어도 된다. 칸은 종류 수만 본다.
    private const int FillerTidBase = 900_000;

    public StorageLimitTest() => GameTableFixture.EnsureLoaded();

    private static GachaService ServiceWith(params GachaEntry[] entries)
    {
        var catalog = new GachaPoolCatalog();
        catalog.Load(entries);
        return new GachaService(catalog);
    }

    private static (User User, TestUserBuilder B) RichUserWithCharacters(int count)
    {
        var b    = new TestUserBuilder();
        var user = b.Build();
        user.GainGold(1_000_000);
        user.LoadCharacters(Enumerable.Range(1, count)
            .Select(i => new CharacterRow { character_id = i, character_tid = CharacterTid, level = 1, exp = 0 })
            .ToList());
        b.Channel.Sent.Clear();
        b.DB.Posted.Clear();
        return (user, b);
    }

    // 자원 종류 kinds개를 채운 유저. extra로 준 TID도 함께 보유한다(종류 수에 포함).
    private static (User User, TestUserBuilder B) RichUserWithItemKinds(int kinds, params (int Tid, int Count)[] extra)
    {
        var b    = new TestUserBuilder();
        var user = b.Build();
        user.GainGold(1_000_000);
        foreach (var (tid, count) in extra)
        {
            user.GainItem(tid, count);
        }

        for (var i = 0; i < kinds - extra.Length; i++)
        {
            user.GainItem(FillerTidBase + i, 1);
        }

        b.Channel.Sent.Clear();
        b.DB.Posted.Clear();
        return (user, b);
    }

    private static GachaService CharacterService()
        => ServiceWith(new GachaEntry(CharacterPoolId, GachaRewardType.Character, CharacterTid, 1, 100));

    private static S_GachaDrawResponse LastDraw(TestUserBuilder b)
        => b.Channel.SentOf<S_GachaDrawResponse>().Last();

    [Fact]
    public void 캐릭터_칸이_모자라면_뽑기를_거절하고_골드를_쓰지_않는다()
    {
        // 191 + 10 = 201 > 200
        var (user, b) = RichUserWithCharacters(191);
        var before = user.Gold;

        ServiceWith(new GachaEntry(CharacterPoolId, GachaRewardType.Character, CharacterTid, 1, 100))
            .Draw(user, CharacterPoolId, 10);

        LastDraw(b).Result.ShouldBe(EResultCode.StorageFull);
        user.Gold.ShouldBe(before);
        b.DB.PostedOf<GrantCharacterRepository>().ShouldBeEmpty();
    }

    [Fact]
    public void 캐릭터_칸이_딱_맞으면_뽑기가_통과한다()
    {
        // 190 + 10 = 200
        var (user, b) = RichUserWithCharacters(190);

        CharacterService().Draw(user, CharacterPoolId, 10);

        LastDraw(b).Result.ShouldBe(EResultCode.Ok);
    }

    [Fact]
    public void 지급을_기다리는_캐릭터도_칸을_차지한다()
    {
        // 190 보유 + 10 대기(DB가 PK를 아직 안 줬다) → 1장만 더 뽑아도 201
        var (user, b) = RichUserWithCharacters(190);
        CharacterService().Draw(user, CharacterPoolId, 10);

        CharacterService().Draw(user, CharacterPoolId, 1);

        LastDraw(b).Result.ShouldBe(EResultCode.StorageFull);
        b.DB.PostedOf<GrantCharacterRepository>().ShouldHaveSingleItem();
    }

    [Fact]
    public void 장비_칸이_모자라면_뽑기를_거절한다()
    {
        var b    = new TestUserBuilder();
        var user = b.Build();
        user.GainGold(1_000_000);
        user.LoadEquips(
            Enumerable.Range(0, 200).Select(i => new UserEquipRow { equip_id = i + 1, equip_tid = SwordTid, slot_position = i }).ToList(),
            new List<CharacterEquipRow>());
        b.DB.Posted.Clear();

        ServiceWith(new GachaEntry(EquipPoolId, GachaRewardType.Equip, SwordTid, 1, 100)).Draw(user, EquipPoolId, 1);

        LastDraw(b).Result.ShouldBe(EResultCode.StorageFull);
        b.DB.PostedOf<GrantEquipRepository>().ShouldBeEmpty();
    }

    [Fact]
    public void 자원_칸이_가득해도_이미_가진_종류만_나오면_통과한다()
    {
        var (user, b) = RichUserWithItemKinds(200, (FishTid, 1));

        ServiceWith(new GachaEntry(ItemPoolId, GachaRewardType.Item, FishTid, 1, 100)).Draw(user, ItemPoolId, 10);

        LastDraw(b).Result.ShouldBe(EResultCode.Ok);
        user.GetItemCount(FishTid).ShouldBe(11);
    }

    [Fact]
    public void 자원_칸이_가득하면_새_종류는_거절한다()
    {
        var (user, b) = RichUserWithItemKinds(200);
        var before = user.Gold;

        ServiceWith(new GachaEntry(ItemPoolId, GachaRewardType.Item, FishTid, 1, 100)).Draw(user, ItemPoolId, 1);

        LastDraw(b).Result.ShouldBe(EResultCode.StorageFull);
        user.Gold.ShouldBe(before);
        user.GetItemCount(FishTid).ShouldBe(0);
    }

    [Fact]
    public void 상자를_전부_열면_상자_칸이_비어_새_종류가_들어간다()
    {
        // 200종(상자 포함) — 상자 3개를 다 열면 199종 + 붕어 = 200
        var (user, b) = RichUserWithItemKinds(200, (WoodBoxTid, 3));

        ServiceWith(new GachaEntry(BoxPoolId, GachaRewardType.Item, FishTid, 1, 100)).OpenBox(user, WoodBoxTid, 3, TestUserBuilder.Base);

        b.Channel.SentOf<S_ItemUseResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.Ok);
        user.GetItemCount(FishTid).ShouldBe(3);
    }

    [Fact]
    public void 상자_보상이_창고에_안_들어가면_우편으로_보관한다()
    {
        // 상자가 남으면 칸이 비지 않는다 — 200 + 붕어 = 201. 상자는 소모하고 보상 전체를 넘침 우편 1통으로(T-082)
        var (user, b) = RichUserWithItemKinds(200, (WoodBoxTid, 3));

        ServiceWith(new GachaEntry(BoxPoolId, GachaRewardType.Item, FishTid, 1, 100)).OpenBox(user, WoodBoxTid, 2, TestUserBuilder.Base);

        var res = b.Channel.SentOf<S_ItemUseResponse>().ShouldHaveSingleItem();
        res.Result.ShouldBe(EResultCode.Ok);
        res.StoredInMail.ShouldBeTrue();
        user.GetItemCount(WoodBoxTid).ShouldBe(1);
        user.GetItemCount(FishTid).ShouldBe(0);

        var mail = b.DB.PostedOf<StoreOverflowMailRepository>().ShouldHaveSingleItem();
        mail.Attachment.Items.ShouldBe(new[] { (FishTid, 2) });
    }

    [Fact]
    public void 우편함이_가득하면_상자_개봉을_거절하고_상자가_그대로다()
    {
        // 안 받은 우편 100통 — 넘침 보관이 멈추고 원래대로 거절한다. 우편함이 두 번째 창고가 되지 않게 한다.
        var (user, b) = RichUserWithItemKinds(200, (WoodBoxTid, 3));
        user.OnMailboxLoaded(Enumerable.Range(1, 100).Select(i => new UserMailRow
        {
            mail_id = i, template_tid = 1, received_at = MailDb.ToDb(TestUserBuilder.Base),
        }).ToList());
        b.Channel.Sent.Clear();

        ServiceWith(new GachaEntry(BoxPoolId, GachaRewardType.Item, FishTid, 1, 100)).OpenBox(user, WoodBoxTid, 2, TestUserBuilder.Base);

        b.Channel.SentOf<S_ItemUseResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.StorageFull);
        user.GetItemCount(WoodBoxTid).ShouldBe(3);
        b.DB.PostedOf<StoreOverflowMailRepository>().ShouldBeEmpty();
    }

    [Fact]
    public void 저장을_기다리는_넘침_우편도_우편함_칸을_차지한다()
    {
        // 안 받은 99통 + 넘침 1통 대기(DB가 mail_id를 아직 안 줬다) = 100 → 다음 개봉은 거절
        var (user, b) = RichUserWithItemKinds(200, (WoodBoxTid, 3));
        user.OnMailboxLoaded(Enumerable.Range(1, 99).Select(i => new UserMailRow
        {
            mail_id = i, template_tid = 1, received_at = MailDb.ToDb(TestUserBuilder.Base),
        }).ToList());
        var service = ServiceWith(new GachaEntry(BoxPoolId, GachaRewardType.Item, FishTid, 1, 100));
        service.OpenBox(user, WoodBoxTid, 1, TestUserBuilder.Base);
        b.Channel.Sent.Clear();

        service.OpenBox(user, WoodBoxTid, 1, TestUserBuilder.Base);

        b.Channel.SentOf<S_ItemUseResponse>().ShouldHaveSingleItem().Result.ShouldBe(EResultCode.StorageFull);
        b.DB.PostedOf<StoreOverflowMailRepository>().ShouldHaveSingleItem();
    }
}
