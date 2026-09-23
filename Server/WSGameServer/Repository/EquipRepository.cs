using GameData;

namespace WSGameServer;

/// <summary>착용 매핑 변경 하나. EquipId가 0이면 그 (캐릭터, 칸)을 비운다.</summary>
public readonly record struct EquipMappingChange(long CharacterId, EquipSlot Slot, long EquipId);

/// <summary>장비 개체 1개를 지급(INSERT)하고 발급된 PK를 로직 스레드로 돌려준다. 창고 칸은 호출자가 정해 넘긴다.</summary>
public sealed class GrantEquipRepository : IRepository
{
    private readonly int _equipTid;
    private readonly int _slotPosition;

    public GrantEquipRepository(User user, int equipTid, int slotPosition)
    {
        User          = user;
        _equipTid     = equipTid;
        _slotPosition = slotPosition;
    }

    public long Key => User.DbKey;

    public User User { get; }

    /// <summary>발급된 개체 PK. ExecuteAsync 뒤에만 유효하다.</summary>
    public long EquipId { get; private set; }

    // === DB 스레드에서 실행 ===
    public async Task ExecuteAsync(DbConnection connection)
    {
        EquipId = await connection.ExecuteScalarAsync<long>(
            @"INSERT INTO t_user_equip (user_id, equip_tid, slot_position)
              VALUES (@userId, @tid, @position) RETURNING equip_id;",
            new { userId = User.Uid, tid = _equipTid, position = _slotPosition });
    }

    // === 로직 스레드에서 실행 ===
    public void Apply()
    {
        User.OnEquipGranted(EquipId, _equipTid, _slotPosition);
    }
}

/// <summary>
/// 착용 매핑 변경을 한 트랜잭션으로 쓴다. 자동 이동은 행 최대 3개(이전 칸 비움·밀려난 장비·새 매핑)를 건드리므로
/// 나누면 중간 실패 시 장비가 두 곳에 있거나 어디에도 없게 된다. 비우기를 전부 먼저 하고 넣는다 — UNIQUE(equip_id) 순서 문제.
/// </summary>
public sealed class SaveCharacterEquipRepository : IRepository
{
    private readonly IReadOnlyList<EquipMappingChange> _changes;

    public SaveCharacterEquipRepository(User user, IReadOnlyList<EquipMappingChange> changes)
    {
        User     = user;
        _changes = changes;
    }

    public long Key => User.DbKey;

    public User User { get; }

    public IReadOnlyList<EquipMappingChange> Changes => _changes;

    public Task ExecuteAsync(DbConnection connection)
    {
        return connection.InTransactionAsync(async tx =>
        {
            foreach (var c in _changes)
            {
                await tx.ExecuteAsync(
                    "DELETE FROM t_character_equip WHERE character_id = @characterId AND slot = @slot",
                    new { characterId = c.CharacterId, slot = (int)c.Slot });
            }

            foreach (var c in _changes)
            {
                if (c.EquipId == 0)
                {
                    continue;
                }

                await tx.ExecuteAsync(
                    "INSERT INTO t_character_equip (character_id, slot, equip_id) VALUES (@characterId, @slot, @equipId)",
                    new { characterId = c.CharacterId, slot = (int)c.Slot, equipId = c.EquipId });
            }
        });
    }

    public void Apply()
    {
    }
}

/// <summary>
/// 장비 개체의 인챈트를 저장한다. 줄을 전부 덮어쓰므로 UPDATE 1행으로 끝난다 —
/// 재롤이 "줄 전부 교체"라서 부분 갱신이 없다.
/// </summary>
public sealed class SaveEquipEnchantRepository : IRepository
{
    private readonly long _equipId;
    private readonly int  _grade;
    private readonly int  _option1;
    private readonly int  _option2;
    private readonly int  _option3;

    public SaveEquipEnchantRepository(User user, long equipId, int grade, int option1, int option2, int option3)
    {
        User     = user;
        _equipId = equipId;
        _grade   = grade;
        _option1 = option1;
        _option2 = option2;
        _option3 = option3;
    }

    public long Key => User.DbKey;

    public User User { get; }

    public Task ExecuteAsync(DbConnection connection)
    {
        return connection.ExecuteAsync(
            @"UPDATE t_user_equip
                 SET enchant_grade = @grade, enchant_1 = @o1, enchant_2 = @o2, enchant_3 = @o3
               WHERE equip_id = @equipId",
            new { equipId = _equipId, grade = _grade, o1 = _option1, o2 = _option2, o3 = _option3 });
    }

    public void Apply()
    {
    }
}
