using System.Data;
using System.Globalization;
using Dapper;
using GameData;
using MikaProtocol;
using WSGameServer.User.WorkStation;

namespace WSGameServer.Repository;

/// <summary>
/// 이름(user_name) 기반 로그인. t_user에 없으면 자동 가입 후 로그인한다.
/// ExecuteAsync(DB 스레드)에서 조회/가입하고, Apply(로직 스레드)에서 User 등록·응답 전송.
/// </summary>
public sealed class LoginRepository : IRepository
{

    /// <summary>신규 유저에게 기본으로 열어 주는 작업슬롯 수. 시작 슬롯 수가 미확정이라 1개로 둔다.</summary>
    private const int DefaultSlotCount = 1;

    /// <summary>SQLite <c>datetime('now')</c>가 쓰는 형식. 저장·파싱 양쪽에서 이 값을 쓴다.</summary>
    public const string SqliteDateTimeFormat = "yyyy-MM-dd HH:mm:ss";

    /// <summary>
    /// SQLite TEXT 시각을 UTC <see cref="DateTime"/>으로 읽는다.
    /// 문화권 의존을 피하려고 <see cref="CultureInfo.InvariantCulture"/>로 고정하고,
    /// 파싱 결과에는 Kind가 없으므로 UTC임을 명시한다(전부 UTC로 저장한다).
    /// </summary>
    public static DateTime ParseUtc(string value)
        => DateTime.SpecifyKind(
            DateTime.ParseExact(value, SqliteDateTimeFormat, CultureInfo.InvariantCulture),
            DateTimeKind.Utc);

    // ExecuteAsync에서 채우고 Apply에서 사용하는 조회 결과
    private long _userId;
    private List<ItemInfo> _items = new();
    private List<WorkStationSlot> _slots = new();

    // DBExecutor 파티션 키 — 같은 세션 작업은 직렬 처리
    public long Key => User.SessionId;

    public User.User User { get; init; }

    public LoginRepository(User.User user)
    {
        User = user;
    }

    // === DB 스레드에서 실행 ===
    public async Task ExecuteAsync(IDbConnection connection)
    {
        // 1) 이름으로 조회
        var row = await connection.QueryFirstOrDefaultAsync<UserRow>(
            "SELECT user_id FROM t_user WHERE nickname = @name",
            new { name = User.NickName});

        // 2) 없으면 자동 가입 (RETURNING으로 새 PK 확보)
        if (row is null)
        {
            _userId = await connection.ExecuteScalarAsync<long>(
                "INSERT INTO t_user (provider_id, nickname) VALUES (@pid, @name) RETURNING user_id",
                new { pid = User.Pid, name = User.NickName });
        }
        else
        {
            _userId = row.UserId;
        }

        // 3. Data Fetch — 인벤토리 로드(AddItemRepository와 동일하게 account user_id = User.Uid 기준)
        // DB는 InventoryRow로 받고, User에 넘길 때 네트워크/전달용 ItemInfo로 변환한다.
        var inventoryRows = await connection.QueryAsync<InventoryRow>(
            "SELECT item_id, count FROM t_inventory WHERE user_id = @userId",
            new { userId = User.Uid });

        _items = inventoryRows
            .Select(r => new ItemInfo { ItemId = r.ItemId, Count = r.Count })
            .ToList();

        // 4) 작업슬롯 로드. 신규 유저면 기본 슬롯을 열어 준다.
        //    슬롯이 하나도 없으면 채취가 아예 돌지 않으므로 여기서 보장한다.
        //
        //    ⚠️ user_id 기준은 반드시 User.Uid(t_account)다. 이 메서드의 _userId는 t_user의 PK로,
        //    두 테이블이 각자 PK를 발급하므로 값이 같다는 보장이 없다.
        //    저장 쪽(SaveWorkStationSlotRepository)이 User.Uid를 쓰므로 로드도 맞춰야
        //    재접속 시 슬롯을 찾는다. 인벤토리도 같은 기준이다.
        var slotRows = (await connection.QueryAsync<WorkStationSlotRow>(
            @"SELECT slot_index, industry, character_id, last_tick_at
              FROM t_workstation_slot WHERE user_id = @userId",
            new { userId = User.Uid })).ToList();

        if (slotRows.Count == 0)
        {
            var now = DateTime.UtcNow.ToString(SqliteDateTimeFormat, CultureInfo.InvariantCulture);
            for (var i = 0; i < DefaultSlotCount; i++)
            {
                await connection.ExecuteAsync(
                    @"INSERT INTO t_workstation_slot (user_id, slot_index, last_tick_at)
                      VALUES (@userId, @slotIndex, @now)
                      ON CONFLICT (user_id, slot_index) DO NOTHING;",
                    new { userId = User.Uid, slotIndex = i, now });

                slotRows.Add(new WorkStationSlotRow
                {
                    SlotIndex = i, Industry = 0, CharacterId = 0, LastTickAt = now,
                });
            }
        }

        _slots = slotRows
            .Select(r => new WorkStationSlot(
                r.SlotIndex,
                (ItemType)r.Industry,
                r.CharacterId,
                ParseUtc(r.LastTickAt)))
            .ToList();
    }

    // === 로직 스레드에서 실행 ===
    public void Apply()
    {
        User.LoadDB(_items, _slots);   // DB에서 읽어온 데이터 일괄 적재
        User.Login();                  // S_LoginResponse
    }

    // Dapper 매핑용 DTO (컬럼 snake_case → MatchNamesWithUnderscores로 매핑)
    private sealed record UserRow
    {
        public long UserId { get; set; }
        public long IsBanned { get; set; }
    }

    // t_inventory 조회 전용 Row (Protocol의 ItemInfo와 분리)
    private sealed record InventoryRow
    {
        public int ItemId { get; set; }
        public int Count { get; set; }
    }

    // t_workstation_slot 조회 전용 Row
    private sealed record WorkStationSlotRow
    {
        public int    SlotIndex   { get; set; }
        public int    Industry    { get; set; }
        public long   CharacterId { get; set; }
        public string LastTickAt  { get; set; } = "";
    }
}
