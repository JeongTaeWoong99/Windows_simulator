using System.Data;
using System.Globalization;
using Dapper;
using WSGameServer.User.WorkStation;

namespace WSGameServer.Repository;

/// <summary>
/// 슬롯 상태를 DB에 반영한다. 정산으로 <c>LastTickAt</c>이 전진했거나 배치가 바뀌었을 때 부른다.
///
/// <para>
/// <b>정산 후 저장을 빠뜨리면 재화가 복제된다</b> — 재시작 시 같은 구간을 다시 정산하기 때문이다.
/// 진행도의 단일 원본이 <c>last_tick_at</c>이라 이 컬럼만 정확하면 나머지는 복구 가능하다.
/// </para>
/// </summary>
public sealed class SaveWorkStationSlotRepository : IRepository
{
    private readonly IReadOnlyList<WorkStationSlot> _slots;

    public SaveWorkStationSlotRepository(User.User user, IReadOnlyList<WorkStationSlot> slots)
    {
        User   = user;
        _slots = slots;
    }

    public long Key => User.SessionId;

    public User.User User { get; }

    public async Task ExecuteAsync(IDbConnection connection)
    {
        // 슬롯 여러 개를 한 번의 왕복으로 처리한다. Dapper는 배열을 넘기면 문장을 반복 실행한다.
        var rows = _slots.Select(s => new
        {
            // user_id 기준은 t_account.user_id(User.Uid)다. 로드(LoginRepository)와 반드시 같아야 한다.
            userId      = User.Uid,
            slotIndex   = s.SlotIndex,
            industry    = (int)s.Industry,
            characterId = s.CharacterId,
            // SQLite datetime() 포맷(UTC). 문화권에 흔들리지 않게 InvariantCulture로 고정한다.
            lastTickAt  = s.LastTickAt.ToString(LoginRepository.SqliteDateTimeFormat, CultureInfo.InvariantCulture),
        }).ToList();

        await connection.ExecuteAsync(
            @"INSERT INTO t_workstation_slot (user_id, slot_index, industry, character_id, last_tick_at)
              VALUES (@userId, @slotIndex, @industry, @characterId, @lastTickAt)
              ON CONFLICT (user_id, slot_index) DO UPDATE SET
                  industry     = excluded.industry,
                  character_id = excluded.character_id,
                  last_tick_at = excluded.last_tick_at;",
            rows);
    }

    public void Apply()
    {
    }
}
