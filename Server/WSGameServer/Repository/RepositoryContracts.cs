namespace WSGameServer;

// Dapper 조회 전용 Row — DB 컬럼 값을 그대로 나른다. Protocol DTO(ItemInfo 등)와 분리한다.
// 도메인 변환·정책(스킵·시작 시각 등)은 로직 스레드(User.OnLoginDataLoaded)가 한다.

// t_character 조회 전용 Row. CharacterId는 개체 PK(long), CharacterTid는 테이블 정의(int)다.
public sealed record CharacterRow(long CharacterId, int CharacterTid, int Level, int Exp);

// t_user_currency 조회 전용 Row. Amount는 반드시 long이다 —
// 거래 경제가 붙으면 누적 골드가 int 상한(약 21억)을 넘길 수 있다.
public sealed record CurrencyRow(int CurrencyType, long Amount);

// t_user_inventory 조회 전용 Row
public sealed record InventoryRow(int ItemId, int Count);

// t_user_workstation_slot 조회 전용 Row (배치 설정만 — 진행도는 저장하지 않는다)
public sealed record WorkStationSlotRow(int SlotIndex, int Industry, long CharacterId);

/// <summary>
/// 로그인 시 리포지토리가 로직 스레드로 넘기는 조회 결과 묶음.
/// Row가 리포지토리 밖으로 나가는 유일한 통로다 — 순수 코어(Inventory·Wallet·WorkStation)에는 넘기지 않는다.
/// </summary>
public sealed record PlayerLoginData(
    List<InventoryRow> InventoryRows,
    List<CurrencyRow> CurrencyRows,
    List<CharacterRow> CharacterRows,
    List<WorkStationSlotRow> WorkStationSlotRows);
