namespace WSGameServer;

// Dapper 조회 전용 Row — DB 컬럼 값을 그대로 나른다. Protocol DTO(ItemInfo 등)와 분리한다.
// 도메인 변환·정책(스킵·시작 시각 등)은 로직 스레드(User.OnLoginDataLoaded)가 한다.
//
// ⚠️ 프로퍼티 이름 = DB 컬럼 이름 그대로(snake_case). 매핑 옵션 없이 SQL과 1:1로 대응된다 —
//    Row 타입에 한해 C# 명명 관례(PascalCase)의 예외로 둔다. 컬럼과 이름이 어긋나면 매핑이 조용히 빈다.
//
// ⚠️ 전부 "프로퍼티 record"로 둔다 — 위치 기반 record로 바꾸지 않는다.
//    SQLite는 INTEGER를 전부 long으로 돌려주는데, long → int 변환은 프로퍼티 매핑에서만 동작한다.
//    위치 기반이면 생성자 매핑으로 떨어져 타입 불일치로 깨진다.

// t_character 조회 전용 Row. character_id는 개체 PK(long), character_tid는 테이블 정의(int)다.
public sealed record CharacterRow
{
    public long character_id  { get; init; }
    public int  character_tid { get; init; }
    public int  level         { get; init; }
    public int  exp           { get; init; }
    public int  farming_bonus { get; init; }
    public int  fishing_bonus { get; init; }
    public int  mining_bonus  { get; init; }
    public int  logging_bonus { get; init; }
    public int  hunting_bonus { get; init; }
}

// t_user_currency 조회 전용 Row. 재화가 늘면 행이 아니라 컬럼이 는다.
// 전부 long이다 — 거래 경제가 붙으면 누적 골드가 int 상한(약 21억)을 넘길 수 있다.
public sealed record CurrencyRow
{
    public long gold { get; init; }
    public long dia  { get; init; }
}

// t_user_inventory 조회 전용 Row
public sealed record InventoryRow
{
    public int item_id { get; init; }
    public int count   { get; init; }
}

// t_user_workstation_slot 조회 전용 Row (배치 설정만 — 진행도는 저장하지 않는다)
public sealed record WorkStationSlotRow
{
    public int  slot_index     { get; init; }
    public int  industry       { get; init; }
    public int  industry_level { get; init; }
    public long character_id   { get; init; }
}

// t_user_account 조회 전용 Row. 행이 없으면 레벨 1로 본다(재화와 같은 규약).
public sealed record AccountRow
{
    public int  level       { get; init; }
    public long exp         { get; init; }
    public int  trait_point { get; init; }
}

// t_user_unlock 조회 전용 Row (열린 해금만 행이 있다 — 해금은 영구다)
public sealed record UserUnlockRow
{
    public int unlock_tid { get; init; }
}

// t_user_equip 조회 전용 Row. equip_id는 개체 PK(long), equip_tid는 테이블 정의(int).
public sealed record UserEquipRow
{
    public long equip_id      { get; init; }
    public int  equip_tid     { get; init; }
    public int  slot_position { get; init; }
}

// t_character_equip 조회 전용 Row. slot은 GameData.EquipSlot 정수값.
public sealed record CharacterEquipRow
{
    public long character_id { get; init; }
    public int  slot         { get; init; }
    public long equip_id     { get; init; }
}

/// <summary>
/// 로그인 시 리포지토리가 로직 스레드로 넘기는 조회 결과 묶음.
/// Row가 리포지토리 밖으로 나가는 유일한 통로다 — 순수 코어(Inventory·WorkStation)에는 넘기지 않는다.
///
/// <para>
/// <paramref name="Currency"/>만 목록이 아니라 단건이고 <b>null이 될 수 있다</b> —
/// 한 번도 재화를 번 적이 없으면 행 자체가 없다. 0으로 보는 판단은 로직 스레드가 한다.
/// </para>
/// </summary>
public sealed record PlayerLoginData(
    List<InventoryRow> InventoryRows,
    CurrencyRow? Currency,
    List<CharacterRow> CharacterRows,
    List<WorkStationSlotRow> WorkStationSlotRows,
    List<UserUnlockRow> UnlockRows,
    List<UserEquipRow> EquipRows,
    List<CharacterEquipRow> CharacterEquipRows,
    AccountRow? Account = null);
