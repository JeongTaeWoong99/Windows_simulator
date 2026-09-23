namespace WSGameServer;

/// <summary>
/// 로그인한 유저에게 딸린 데이터(인벤토리·재화·캐릭터·작업슬롯)를 한 번에 읽어 온다.
/// 유저 조회·자동가입은 <see cref="AccountRepository"/>가 먼저 끝낸다.
///
/// <para>
/// ExecuteAsync(DB 스레드)는 <b>읽기 전용 — Row 수집까지만</b> 한다. 도메인 변환·신규 지급 판단·
/// 응답 전송은 로직 스레드(<see cref="User.OnLoginDataLoaded"/>)가 맡는다.
/// </para>
/// </summary>
public sealed class LoginRepository : IRepository
{
    // ExecuteAsync에서 채우고 Apply에서 넘기는 조회 결과 (전부 Row — 변환하지 않는다)
    private List<InventoryRow>       _inventoryRows       = new();
    private CurrencyRow?             _currency;
    private List<CharacterRow>       _characterRows       = new();
    private List<WorkStationSlotRow> _workStationSlotRows = new();
    private AccountRow?                _account;
    private List<UserUnlockRow>        _unlockRows        = new();
    private List<UserEquipRow>         _equipRows          = new();
    private List<CharacterEquipRow>    _characterEquipRows = new();

    // DBExecutor 파티션 키 — 같은 계정의 작업은 직렬 처리
    public long Key => User.DbKey;

    public User User { get; init; }

    public LoginRepository(User user)
    {
        User = user;
    }

    // === DB 스레드에서 실행 ===
    //
    // Row 프로퍼티 = DB 컬럼명 그대로(snake_case)라 매핑 옵션 없이 1:1로 대응된다.
    // RepositoryContracts.cs 상단 주석 참조.
    public async Task ExecuteAsync(DbConnection connection)
    {
        // 1) 인벤토리
        _inventoryRows = await connection.QueryAsync<InventoryRow>(
            "SELECT item_id, count FROM t_user_inventory WHERE user_id = @userId",
            new { userId = User.Uid });

        // 2) 재화. 한 번도 벌지 않았으면 행 자체가 없고, 그건 0으로 본다
        //    (가입 시 0짜리 행을 만들지 않는다 — 재화 컬럼이 늘 때마다 백필이 필요해진다).
        _currency = await connection.QueryFirstOrDefaultAsync<CurrencyRow>(
            "SELECT gold, dia FROM t_user_currency WHERE user_id = @userId",
            new { userId = User.Uid });

        // 3) 캐릭터. 하나도 없으면(신규 유저) 지급 판단은 로직 스레드가 한다.
        _characterRows = await connection.QueryAsync<CharacterRow>(
            @"SELECT character_id, character_tid, level, exp,
                     farming_bonus, fishing_bonus, mining_bonus, logging_bonus, hunting_bonus
              FROM t_character WHERE user_id = @userId",
            new { userId = User.Uid });

        // 4) 작업슬롯. 슬롯 행에는 진행도가 없다 — 배치 설정(산업·캐릭터)뿐이다.
        //    오프라인 진행이 폐지돼 비운 동안의 누적이 없으므로 저장할 진행도 자체가 없다.
        //    기본 슬롯 개설도 로직 스레드가 한다.
        _workStationSlotRows = await connection.QueryAsync<WorkStationSlotRow>(
            @"SELECT slot_index, industry, industry_level, character_id
              FROM t_user_workstation_slot WHERE user_id = @userId",
            new { userId = User.Uid });

        // 5) 계정 레벨·특성 포인트. 한 번도 경험치를 번 적 없으면 행이 없고, 레벨 1로 본다(재화와 같은 규약).
        _account = await connection.QueryFirstOrDefaultAsync<AccountRow>(
            "SELECT level, exp, trait_point FROM t_user_account WHERE user_id = @userId",
            new { userId = User.Uid });

        // 6) 열린 해금. 열린 것만 행이 있다 — 작업슬롯의 열린 칸·산업 레벨·찍은 특성이 전부 이 목록으로 정해진다.
        _unlockRows = await connection.QueryAsync<UserUnlockRow>(
            "SELECT unlock_tid FROM t_user_unlock WHERE user_id = @userId",
            new { userId = User.Uid });

        // 7) 장비 개체와 착용 매핑. 매핑은 유저 컬럼이 없어 개체 테이블과 JOIN으로 유저를 가른다.
        // 경매에 잠긴 개체(등록 중·우편 대기)는 싣지 않는다 — 메모리에 있으면 저장이 잠긴 행을 덮는다.
        _equipRows = await connection.QueryAsync<UserEquipRow>(
            @"SELECT equip_id, equip_tid, slot_position, enchant_grade, enchant_1, enchant_2, enchant_3
              FROM t_user_equip WHERE user_id = @userId AND auction_trade_id = 0",
            new { userId = User.Uid });

        _characterEquipRows = await connection.QueryAsync<CharacterEquipRow>(
            @"SELECT ce.character_id, ce.slot, ce.equip_id
              FROM t_character_equip ce
              JOIN t_user_equip ue ON ue.equip_id = ce.equip_id
              WHERE ue.user_id = @userId",
            new { userId = User.Uid });
    }

    // === 로직 스레드에서 실행 ===
    //
    // 시각은 여기서 만든다. Apply()는 인자를 받을 수 없는 인프라 경계이고,
    // User 쪽은 시각을 전부 인자로 받도록 되어 있어 "만드는 곳"이 진입점으로 밀려난 결과다.
    public void Apply()
    {
        User.OnLoginDataLoaded(
            new PlayerLoginData(_inventoryRows, _currency, _characterRows,
                                _workStationSlotRows, _unlockRows,
                                _equipRows, _characterEquipRows, _account),
            DateTime.UtcNow);
    }
}
