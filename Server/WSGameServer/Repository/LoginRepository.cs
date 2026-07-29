using System.Data;
using Dapper;
using GameData;
using MikaProtocol;
using WSGameServer.Common;
using WSGameServer.User.WorkStation;

namespace WSGameServer.Repository;

/// <summary>
/// 로그인한 유저에게 딸린 데이터(인벤토리·재화·작업슬롯)를 한 번에 읽어 적재한다.
/// 유저 조회·자동가입은 <see cref="AccountRepository"/>가 먼저 끝낸다.
/// ExecuteAsync(DB 스레드)에서 조회하고, Apply(로직 스레드)에서 User에 적재·응답 전송.
/// </summary>
public sealed class LoginRepository : IRepository
{
    /// <summary>신규 유저에게 기본으로 열어 주는 작업슬롯 수. 시작 슬롯 수가 미확정이라 1개로 둔다.</summary>
    private const int DefaultSlotCount = 1;

    // ExecuteAsync에서 채우고 Apply에서 사용하는 조회 결과
    private List<ItemInfo> _items = new();
    private List<WorkStationSlot> _slots = new();
    private Dictionary<CurrencyType, long> _currencies = new();
    private List<User.Character.Character> _characters = new();

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
        // 유저 조회·자동가입은 AccountRepository가 이미 끝냈다(User.Uid가 그 결과다).
        // 여기서는 그 유저에게 딸린 데이터만 읽는다.

        // 1) 인벤토리 로드. DB는 InventoryRow로 받고, User에 넘길 때 ItemInfo로 변환한다.
        var inventoryRows = await connection.QueryAsync<InventoryRow>(
            "SELECT item_id, count FROM t_user_inventory WHERE user_id = @userId",
            new { userId = User.Uid });

        _items = inventoryRows
            .Select(r => new ItemInfo { ItemId = r.ItemId, Count = r.Count })
            .ToList();

        // 2) 재화 로드. 보유하지 않은 재화는 행이 없고, 그건 0으로 본다
        //    (가입 시 0짜리 행을 만들지 않는다 — 재화 종류가 늘 때마다 백필이 필요해진다).
        var currencyRows = await connection.QueryAsync<CurrencyRow>(
            "SELECT currency_type, amount FROM t_user_currency WHERE user_id = @userId",
            new { userId = User.Uid });

        _currencies = currencyRows.ToDictionary(r => (CurrencyType)r.CurrencyType, r => r.Amount);

        // 3) 캐릭터 로드. 신규 유저면 기본 캐릭터를 지급한다.
        //    캐릭터가 하나도 없으면 슬롯에 넣을 것이 없어 채취가 시작조차 못 한다.
        var characterRows = (await connection.QueryAsync<CharacterRow>(
            @"SELECT character_id, character_tid, level, exp
              FROM t_character WHERE user_id = @userId",
            new { userId = User.Uid })).ToList();

        if (characterRows.Count == 0)
        {
            var newId = await connection.ExecuteScalarAsync<long>(
                @"INSERT INTO t_character (user_id, character_tid)
                  VALUES (@userId, @tid) RETURNING character_id;",
                new { userId = User.Uid, tid = global::WSGameServer.User.User.DefaultCharacterTid });

            characterRows.Add(new CharacterRow
            {
                CharacterId = newId, CharacterTid = global::WSGameServer.User.User.DefaultCharacterTid, Level = 1, Exp = 0,
            });
        }

        // 테이블에 없는 TID는 건너뛴다. 여기서 예외를 던지면 데이터 한 줄 때문에 로그인이 막힌다.
        _characters = new List<User.Character.Character>(characterRows.Count);
        foreach (var r in characterRows)
        {
            if (!GameTable.CharacterTable.TryGet(r.CharacterTid, out var row))
            {
                ServerLog.Warn("로그인", $"CharacterTable에 없는 TID, 건너뜀: {r.CharacterTid} (개체 {r.CharacterId})");
                continue;
            }

            _characters.Add(new User.Character.Character(r.CharacterId, row, r.Level, r.Exp));
        }

        // 4) 작업슬롯 로드. 신규 유저면 기본 슬롯을 열어 준다.
        //    슬롯이 하나도 없으면 채취가 아예 돌지 않으므로 여기서 보장한다.
        //
        //    슬롯 행에는 진행도가 없다 — 배치 설정(산업·캐릭터)뿐이다. 오프라인 진행이 폐지돼
        //    비운 동안의 누적이 없으므로 저장할 진행도 자체가 없다.
        var slotRows = (await connection.QueryAsync<WorkStationSlotRow>(
            @"SELECT slot_index, industry, character_id
              FROM t_user_workstation_slot WHERE user_id = @userId",
            new { userId = User.Uid })).ToList();

        if (slotRows.Count == 0)
        {
            for (var i = 0; i < DefaultSlotCount; i++)
            {
                await connection.ExecuteAsync(
                    @"INSERT INTO t_user_workstation_slot (user_id, slot_index)
                      VALUES (@userId, @slotIndex)
                      ON CONFLICT (user_id, slot_index) DO NOTHING;",
                    new { userId = User.Uid, slotIndex = i });

                slotRows.Add(new WorkStationSlotRow { SlotIndex = i, Industry = 0, CharacterId = 0 });
            }
        }

        // ⚠️ 채취는 "지금부터" 시작한다. 로그아웃 시각을 읽어 그 구간을 정산하면
        //    오프라인 진행이 되살아난다(게임기획코어 P2 재정의 — 접속 중에만 자란다).
        //    진행 조각도 이월하지 않으므로 매 접속이 0에서 출발한다.
        var startedAt = DateTime.UtcNow;

        _slots = slotRows
            .Select(r => new WorkStationSlot(
                r.SlotIndex,
                (ItemType)r.Industry,
                r.CharacterId,
                startedAt))
            .ToList();
    }

    // === 로직 스레드에서 실행 ===
    public void Apply()
    {
        User.LoadDB(_items, _slots, _currencies, _characters);   // DB에서 읽어온 데이터 일괄 적재
        User.Login();                                            // S_LoginResponse
    }

    // Dapper 매핑용 DTO (컬럼 snake_case → MatchNamesWithUnderscores로 매핑)

    // t_character 조회 전용 Row. character_id는 개체 PK, character_tid는 테이블 정의다.
    private sealed record CharacterRow
    {
        public long CharacterId  { get; set; }
        public int  CharacterTid { get; set; }
        public int  Level        { get; set; }
        public int  Exp          { get; set; }
    }

    // t_user_currency 조회 전용 Row. amount는 반드시 long이다 —
    // 거래 경제가 붙으면 누적 골드가 int 상한(약 21억)을 넘길 수 있다.
    private sealed record CurrencyRow
    {
        public int  CurrencyType { get; set; }
        public long Amount       { get; set; }
    }

    // t_user_inventory 조회 전용 Row (Protocol의 ItemInfo와 분리)
    private sealed record InventoryRow
    {
        public int ItemId { get; set; }
        public int Count { get; set; }
    }

    // t_user_workstation_slot 조회 전용 Row (배치 설정만 — 진행도는 저장하지 않는다)
    private sealed record WorkStationSlotRow
    {
        public int  SlotIndex   { get; set; }
        public int  Industry    { get; set; }
        public long CharacterId { get; set; }
    }
}
