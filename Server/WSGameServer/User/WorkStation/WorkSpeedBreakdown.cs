namespace WSGameServer;

/// <summary>
/// 슬롯 작업속도를 이루는 항목들(천분율). 서버는 이것으로 속도를 확정하고, 같은 값을 슬롯 정보에 실어 클라가 그대로 그린다(T-055).
/// 가산 항목이 늘면 여기와 <see cref="Resolve"/> · <c>WorkStationSlot.ToInfo</c>에 함께 붙인다.
/// </summary>
public readonly record struct WorkSpeedBreakdown(
    int BaseWorkSpeed,
    int LevelAddPermille,
    int TraitAddPermille,
    int EquipAddPermille,
    int StarAddPermille,
    int GatherSpeedPermille)
{
    /// <summary>보정 없이 기본값만 있는 내역. 빈 슬롯·옛 경로가 쓴다.</summary>
    public static WorkSpeedBreakdown Of(int baseWorkSpeed)
    {
        return new WorkSpeedBreakdown(baseWorkSpeed, 0, 0, 0, 0, GatherSpeed.DefaultPermille);
    }

    public int Resolve()
    {
        return WorkSpeed.From(BaseWorkSpeed)
            .Add(LevelAddPermille)
            .Add(TraitAddPermille)
            .Add(EquipAddPermille)
            .Add(StarAddPermille)
            .Multiply(GatherSpeedPermille)
            .Resolve();
    }
}
