using MikaProtocol;

namespace WSGameServer;

/// <summary>로그인 때 판매 중 건수를 읽는다 — 등록 건수 상한 판정. 읽기 전에는 등록을 받지 않는다.</summary>
public sealed class LoadAuctionStateRepository(User user) : IRepository
{
    private long _activeListings;

    public long Key => User.DbKey;

    public User User { get; } = user;

    public async Task ExecuteAsync(DbConnection connection)
        => _activeListings = await AuctionDb.CountActiveAsync(connection, User.Uid);

    public void Apply() => User.OnAuctionStateLoaded((int)_activeListings);
}

/// <summary>완료 탭 조회 → <see cref="AuctionHistoryDb.QueryAsync"/> (T-156).</summary>
public sealed class AuctionHistoryRepository(
    User user, EAuctionHistoryType type, int days, long cursor, int pageSize, bool markSeen, DateTime now) : IRepository
{
    private AuctionHistoryPage? _page;

    public long Key => User.DbKey;

    public User User { get; } = user;

    public EAuctionHistoryType Type     => type;
    public int                 Days     => days;
    public long                Cursor   => cursor;
    public int                 PageSize => pageSize;
    public bool                MarkSeen => markSeen;

    public async Task ExecuteAsync(DbConnection connection)
        => _page = await AuctionHistoryDb.QueryAsync(connection, User.Uid, type, days, cursor, pageSize, markSeen, now);

    public void Apply() => User.OnAuctionHistoryLoaded(_page!);
}

/// <summary>등록 한 트랜잭션 → <see cref="AuctionDb.RegisterAsync"/>. 끝나면 매물 ID를 알린다.</summary>
public sealed class RegisterAuctionRepository(
    User user, AuctionItemSnapshot item, long unitPrice, long listingFee,
    List<ItemChangeInfo> inventoryChanges, long gold, DateTime expiresAt, DateTime now, List<ItemChangeInfo>? artifactChanges = null) : IRepository
{
    private long _tradeId;

    public long Key => User.DbKey;

    public User User { get; } = user;

    public AuctionItemSnapshot Item => item;

    public async Task ExecuteAsync(DbConnection connection)
        => _tradeId = await AuctionDb.RegisterAsync(connection, User.Uid, item, unitPrice, listingFee, inventoryChanges, gold, expiresAt, now, artifactChanges);

    public void Apply() => User.OnAuctionRegistered(_tradeId, item, listingFee, inventoryChanges);
}

/// <summary>
/// 버린 예약을 놓는다(Confirm 실패 outbox). 저장이 실패해도 세션을 끊지 않는다 — 예약은 타임아웃으로도 풀린다.
/// 유저가 이미 나갔어도 DB 큐는 돈다.
/// </summary>
public sealed class ReleasePurchaseRepository(User user, long purchaseId, DateTime now) : IRepository
{
    public long Key => User.DbKey;

    public User User { get; } = user;

    public long PurchaseId => purchaseId;

    public Task ExecuteAsync(DbConnection connection) => AuctionDb.ReleasePurchaseAsync(connection, purchaseId, now);

    public void Apply() => User.OnPurchaseReleased();

    public void OnFailed(Exception e) => ServerLog.Warn("경매", $"예약 해제 저장 실패 — 타임아웃으로 풀린다. 구매 {purchaseId}: {e.Message}");
}

/// <summary>정산 한 트랜잭션 → <see cref="AuctionDb.SettleAsync"/>. 작업 파티션은 구매자다.</summary>
public sealed class SettleAuctionRepository(
    User buyer, long purchaseId, long listingId, long totalPrice, long gold, DateTime now) : IRepository
{
    private AuctionSettleResult _result = new(false, 0, null, null);

    public long Key => User.DbKey;

    public User User { get; } = buyer;

    public async Task ExecuteAsync(DbConnection connection)
        => _result = await AuctionDb.SettleAsync(connection, listingId, User.Uid, purchaseId, totalPrice, gold, now);

    public void Apply() => User.OnAuctionSettled(listingId, totalPrice, _result);

    // 예외(디스크·락)로 끝나면 트랜잭션은 롤백됐고 메모리만 대금을 뺀 채다 — 돌려준 뒤 기본 정책(세션 끊기)으로 간다.
    public void OnFailed(Exception e) => User.OnSettleFailed(purchaseId, totalPrice, GetType().Name, e);
}

/// <summary>우편으로 온 잠긴 장비의 잠금을 풀고 창고 칸을 준다.</summary>
/// <summary>우편으로 온 잠긴 캐릭터의 잠금을 푼다. 끝나면 메모리에 올린다.</summary>
public sealed class UnlockMailCharacterRepository(User user, MailCharacter character, int slot) : IRepository
{
    private bool _unlocked;

    public long Key => User.DbKey;

    public User User { get; } = user;

    public async Task ExecuteAsync(DbConnection connection)
        => _unlocked = await AuctionDb.UnlockCharacterAsync(connection, character.CharacterId, User.Uid, slot);

    public void Apply() => User.OnMailCharacterUnlocked(character, slot, _unlocked);
}

public sealed class UnlockMailEquipRepository(User user, MailEquip equip, int slotPosition) : IRepository
{
    private bool _unlocked;

    public long Key => User.DbKey;

    public User User { get; } = user;

    public async Task ExecuteAsync(DbConnection connection)
        => _unlocked = await AuctionDb.UnlockEquipAsync(connection, equip.EquipId, User.Uid, slotPosition);

    public void Apply() => User.OnMailEquipUnlocked(equip, slotPosition, _unlocked);
}

/// <summary>더미 매물 원장 쓰기(치트 T-168) → <see cref="AuctionDb.SeedDummyAsync"/>. 작업 파티션은 치트를 쓴 유저다.</summary>
public sealed class SeedDummyAuctionRepository(User user, List<DummyListing> listings, DateTime now) : IRepository
{
    private int _count;

    public long Key => User.DbKey;

    public User User { get; } = user;

    public IReadOnlyList<DummyListing> Listings => listings;

    public async Task ExecuteAsync(DbConnection connection) => _count = await AuctionDb.SeedDummyAsync(connection, listings, now);

    public void Apply() => User.OnDummyAuctionSeeded(_count);

    // 치트 실패로 세션을 끊지 않는다 — 치트를 쓴 유저의 메모리는 바뀐 것이 없다.
    public void OnFailed(Exception e) => User.OnDummyAuctionFailed(e);
}

/// <summary>더미 흔적 지우기(치트 T-168) → <see cref="AuctionDb.PurgeDummyAsync"/>. 끝나면 경매장 쪽 매물을 취소한다.</summary>
public sealed class PurgeDummyAuctionRepository(User user) : IRepository
{
    private int _deleted;

    public long Key => User.DbKey;

    public User User { get; } = user;

    public async Task ExecuteAsync(DbConnection connection) => _deleted = await AuctionDb.PurgeDummyAsync(connection);

    public void Apply() => User.OnDummyAuctionPurged(_deleted);

    public void OnFailed(Exception e) => User.OnDummyAuctionFailed(e);
}
