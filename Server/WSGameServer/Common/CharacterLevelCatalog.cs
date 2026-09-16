using GameData;
using MikaUtils;

namespace WSGameServer;

/// <summary>
/// <c>CharacterLevelTable</c> 보관소 — 캐릭터 레벨 곡선. <b>마지막 행이 곧 만렙이다</b>(센티널 값 없음).
/// 서버 시작 시 <c>GameTable.LoadAll</c> 다음에 <see cref="LoadAll"/>을 한 번 부르고, 이후에는 읽기만 한다.
/// </summary>
public sealed class CharacterLevelCatalog : Singleton<CharacterLevelCatalog>
{
    private readonly Dictionary<int, int> _requiredExpByLevel = new();

    // 레벨 → 그 레벨까지 번 적성 포인트 누적. 조회마다 합산하지 않으려고 로드 때 한 번 만든다.
    private readonly Dictionary<int, int> _pointsEarnedByLevel = new();

    /// <summary>등록된 최고 레벨. 행이 없으면 1 — 그 레벨에서는 경험치가 쌓이지 않는다.</summary>
    public int MaxLevel { get; private set; } = 1;

    public int Count => _requiredExpByLevel.Count;

    /// <summary>모든 행을 <c>GameTable</c>에서 읽어 등록한다.</summary>
    /// <remarks>반드시 <c>GameTable.LoadAll</c> 이후에 부른다. 테이블 데이터가 없으면 여기서 터진다.</remarks>
    public void LoadAll()
    {
        Load(GameTable.CharacterLevelTable.All);
    }

    /// <summary>행 목록으로 곡선을 만든다. 같은 레벨이 두 번 나오면 예외.</summary>
    public void Load(IEnumerable<CharacterLevelTableRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        _requiredExpByLevel.Clear();
        _pointsEarnedByLevel.Clear();
        MaxLevel = 1;

        var pointByLevel = new Dictionary<int, int>();
        foreach (var row in rows)
        {
            if (!_requiredExpByLevel.TryAdd(row.CharacterLevelTID, row.RequiredExp))
            {
                throw new InvalidOperationException(
                    $"CharacterLevelTable에 레벨이 중복됐습니다: Lv{row.CharacterLevelTID}");
            }

            pointByLevel[row.CharacterLevelTID] = row.AptitudePoint;
            MaxLevel = Math.Max(MaxLevel, row.CharacterLevelTID);
        }

        var earned = 0;
        foreach (var level in pointByLevel.Keys.Order())
        {
            earned += pointByLevel[level];
            _pointsEarnedByLevel[level] = earned;
        }
    }

    /// <summary>이 레벨까지 번 적성 포인트 총합. 테이블 밖 레벨은 마지막 행의 값이다.</summary>
    public int PointsEarnedBy(int level)
    {
        if (_pointsEarnedByLevel.TryGetValue(level, out var earned))
        {
            return earned;
        }

        return level > MaxLevel && _pointsEarnedByLevel.TryGetValue(MaxLevel, out var atMax) ? atMax : 0;
    }

    /// <summary>이 레벨에 <b>도달하는 데</b> 직전 레벨에서 필요한 경험치. 행이 없으면 false.</summary>
    public bool TryGetRequiredExp(int level, out int requiredExp)
        => _requiredExpByLevel.TryGetValue(level, out requiredExp);
}
