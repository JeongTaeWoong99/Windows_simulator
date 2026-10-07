using GameData;
using MikaUtils;

namespace WSGameServer;

/// <summary>
/// <c>CharacterStarTable</c> 보관소 — 응축 ★(캐릭터 기획 5.5). 개체는 넣은 재료의 <b>누적 수</b>만 기억하고, ★은 여기서 읽는다.
/// 서버 시작 시 <c>GameTable.LoadAll</c> 다음에 <see cref="LoadAll"/>을 한 번 부르고, 이후에는 읽기만 한다.
/// </summary>
public sealed class CharacterStarCatalog : Singleton<CharacterStarCatalog>
{
    // ★ → 그 ★에 닿는 누적 재료 수 · 그 ★의 속도 가산(천분율, 총량). 인덱스 0 = ★1.
    private readonly List<int> _thresholds = new();
    private readonly List<int> _speedAdds  = new();

    public int Count => _thresholds.Count;

    /// <summary>최고 ★. 행이 없으면 0 — 응축할 수 없다.</summary>
    public int MaxStar => _thresholds.Count;

    /// <summary>최고 ★에 닿는 누적 재료 수. 개체의 누적은 여기서 멈춘다.</summary>
    public int MaxCount => _thresholds.Count == 0 ? 0 : _thresholds[^1];

    public void LoadAll()
    {
        Load(GameTable.CharacterStarTable.All);
    }

    /// <summary>★1부터 빠짐없이 이어져야 한다. 빠지거나 겹치면 예외(기동 실패).</summary>
    public void Load(IEnumerable<CharacterStarTableRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        _thresholds.Clear();
        _speedAdds.Clear();

        var total = 0;
        foreach (var row in rows.OrderBy(r => r.CharacterStarTID))
        {
            if (row.CharacterStarTID != _thresholds.Count + 1)
            {
                throw new InvalidOperationException($"CharacterStarTable은 ★1부터 빠짐없이 이어져야 합니다: ★{row.CharacterStarTID}");
            }

            total = checked(total + row.RequiredCount);
            _thresholds.Add(total);
            _speedAdds.Add(row.SpeedAddPermille);
        }
    }

    /// <summary>누적 재료 수로 닿은 ★(0~최고).</summary>
    public int StarAt(int condenseCount)
    {
        var star = 0;
        while (star < _thresholds.Count && condenseCount >= _thresholds[star])
        {
            star++;
        }

        return star;
    }

    /// <summary>이 ★의 작업속도 가산(천분율). ★0·행 없음은 0이다.</summary>
    public int SpeedAddAt(int star)
    {
        if (star <= 0 || _speedAdds.Count == 0)
        {
            return 0;
        }

        return _speedAdds[Math.Min(star, _speedAdds.Count) - 1];
    }
}
