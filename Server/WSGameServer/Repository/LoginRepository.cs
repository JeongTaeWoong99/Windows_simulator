namespace WSGameServer;

/// <summary>
/// 로그인한 유저에게 딸린 데이터(인벤토리·재화·캐릭터·작업슬롯)를 한 번에 읽어 온다.
/// 유저 조회·자동가입은 <see cref="AccountRepository"/>가 먼저 끝낸다.
///
/// <para>
/// ExecuteAsync(DB 스레드)는 <b>Row 수집까지만</b> 한다. 도메인 변환·정책·응답 전송은
/// 로직 스레드(<see cref="User.OnLoginDataLoaded"/>)가 맡는다.
/// </para>
/// </summary>
public sealed class LoginRepository : IRepository
{
    /// <summary>신규 유저에게 기본으로 열어 주는 작업슬롯 수. 시작 슬롯 수가 미확정이라 1개로 둔다.</summary>
    private const int DefaultSlotCount = 1;

    // ExecuteAsync에서 채우고 Apply에서 넘기는 조회 결과 (전부 Row — 변환하지 않는다)
    private List<InventoryRow>       _inventoryRows       = new();
    private List<CurrencyRow>        _currencyRows        = new();
    private List<CharacterRow>       _characterRows       = new();
    private List<WorkStationSlotRow> _workStationSlotRows = new();

    // DBExecutor 파티션 키 — 같은 세션 작업은 직렬 처리
    public long Key => User.SessionId;

    public User User { get; init; }

    public LoginRepository(User user)
    {
        User = user;
    }

    // === DB 스레드에서 실행 ===
    //
    // 컬럼을 record 생성자 파라미터 이름으로 별칭(AS)한다 — MatchNamesWithUnderscores는
    // 프로퍼티 매핑에만 적용되고, 위치 기반 record의 생성자 매핑에는 적용되지 않는다.
    public async Task ExecuteAsync(DbConnection connection)
    {
        // 1) 인벤토리
        _inventoryRows = await connection.QueryAsync<InventoryRow>(
            "SELECT item_id AS ItemId, count AS Count FROM t_user_inventory WHERE user_id = @userId",
            new { userId = User.Uid });

        // 2) 재화. 보유하지 않은 재화는 행이 없고, 그건 0으로 본다
        //    (가입 시 0짜리 행을 만들지 않는다 — 재화 종류가 늘 때마다 백필이 필요해진다).
        _currencyRows = await connection.QueryAsync<CurrencyRow>(
            "SELECT currency_type AS CurrencyType, amount AS Amount FROM t_user_currency WHERE user_id = @userId",
            new { userId = User.Uid });

        // 3) 캐릭터. 신규 유저면 기본 캐릭터를 지급한다.
        //    캐릭터가 하나도 없으면 슬롯에 넣을 것이 없어 채취가 시작조차 못 한다.
        _characterRows = await connection.QueryAsync<CharacterRow>(
            @"SELECT character_id AS CharacterId, character_tid AS CharacterTid, level AS Level, exp AS Exp
              FROM t_character WHERE user_id = @userId",
            new { userId = User.Uid });

        if (_characterRows.Count == 0)
        {
            var newId = await connection.ExecuteScalarAsync<long>(
                @"INSERT INTO t_character (user_id, character_tid)
                  VALUES (@userId, @tid) RETURNING character_id;",
                new { userId = User.Uid, tid = User.DefaultCharacterTid });

            _characterRows.Add(new CharacterRow(newId, User.DefaultCharacterTid, Level: 1, Exp: 0));
        }

        // 4) 작업슬롯. 신규 유저면 기본 슬롯을 열어 준다.
        //    슬롯이 하나도 없으면 채취가 아예 돌지 않으므로 여기서 보장한다.
        //
        //    슬롯 행에는 진행도가 없다 — 배치 설정(산업·캐릭터)뿐이다. 오프라인 진행이 폐지돼
        //    비운 동안의 누적이 없으므로 저장할 진행도 자체가 없다.
        _workStationSlotRows = await connection.QueryAsync<WorkStationSlotRow>(
            @"SELECT slot_index AS SlotIndex, industry AS Industry, character_id AS CharacterId
              FROM t_user_workstation_slot WHERE user_id = @userId",
            new { userId = User.Uid });

        if (_workStationSlotRows.Count == 0)
        {
            for (var i = 0; i < DefaultSlotCount; i++)
            {
                await connection.ExecuteAsync(
                    @"INSERT INTO t_user_workstation_slot (user_id, slot_index)
                      VALUES (@userId, @slotIndex)
                      ON CONFLICT (user_id, slot_index) DO NOTHING;",
                    new { userId = User.Uid, slotIndex = i });

                _workStationSlotRows.Add(new WorkStationSlotRow(i, Industry: 0, CharacterId: 0));
            }
        }
    }

    // === 로직 스레드에서 실행 ===
    public void Apply()
    {
        User.OnLoginDataLoaded(new PlayerLoginData(
            _inventoryRows, _currencyRows, _characterRows, _workStationSlotRows));
    }
}
