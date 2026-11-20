namespace WSGameServer;

/// <summary>
/// 슬롯의 <b>배치 설정</b>을 DB에 반영한다. 산업·캐릭터가 바뀌었을 때만 부르면 된다.
///
/// <para>
/// <b>진행도는 저장하지 않는다.</b> 오프라인 진행이 폐지되면서 진행도가 세션 지역 상태가 됐고
/// (접속마다 0에서 시작한다), 그래서 정산 결과로 이 테이블이 바뀔 일이 없어졌다.
/// 정산으로 생긴 아이템은 <c>GainItem</c>이 각자 저장하므로 여기서 신경 쓸 것이 없다.
/// </para>
/// </summary>
public sealed class SaveWorkStationSlotRepository : IRepository
{
    private sealed record SlotAssignment(long UserId, int SlotIndex, int Industry, int IndustryLevel, long CharacterId);

    private readonly IReadOnlyList<WorkStationSlot> _slots;
    private readonly IReadOnlyList<SlotAssignment> _assignments;
    private readonly IReadOnlyList<SlotBuffChange> _buffChanges;

    public SaveWorkStationSlotRepository(User user, IReadOnlyList<WorkStationSlot> slots,
        IReadOnlyList<SlotBuffChange>? buffChanges = null)
    {
        User = user;
        _slots = slots;
        _assignments = slots.Select(s =>
            new SlotAssignment(user.Uid, s.SlotIndex, (int)s.Industry, s.IndustryLevel, s.CharacterId)).ToList();
        _buffChanges = buffChanges ?? Array.Empty<SlotBuffChange>();
    }

    public long Key => User.DbKey;

    public User User { get; }

    public IReadOnlyList<WorkStationSlot> Slots => _slots;

    public Task ExecuteAsync(DbConnection connection)
        => connection.InTransactionAsync(async tx =>
        {
            await tx.ExecuteAsync(
                @"INSERT INTO t_user_workstation_slot (user_id, slot_index, industry, industry_level, character_id)
                  VALUES (@UserId, @SlotIndex, @Industry, @IndustryLevel, @CharacterId)
                  ON CONFLICT (user_id, slot_index) DO UPDATE SET
                      industry       = excluded.industry,
                      industry_level = excluded.industry_level,
                      character_id   = excluded.character_id;",
                _assignments);

            foreach (var change in _buffChanges)
            {
                await SaveSlotBuffRepository.WriteAsync(tx, User.Uid, change);
            }
        });

    public void Apply()
    {
    }
}
