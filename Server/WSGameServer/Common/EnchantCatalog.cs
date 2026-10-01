using GameData;
using MikaUtils;

namespace WSGameServer;

// 인챈트 3시트 보관소 — 인챈트 등급별 옵션 풀 · 장비 등급별 칸 수 · 인챈트 등급별 상승 확률 · 큐브 배율.
// GameTable.LoadAll 다음에 LoadAll 한 번, 이후 조회만(불변) → Server/docs/데이터-카탈로그.md
public sealed class EnchantCatalog : Singleton<EnchantCatalog>
{
    /// <summary>장비 하나가 가질 수 있는 칸의 상한. DB 컬럼(enchant_1~3) 수와 같다.</summary>
    public const int MaxSlotCount = 3;

    /// <summary>상승 확률의 단위 — 만분율(10000 = 100%).</summary>
    public const int PermyriadScale = 10000;

    private readonly Dictionary<int, EnchantOptionTableRow>                          _optionByTid = new();
    private readonly Dictionary<GlobalRarity, WeightedPicker<EnchantOptionTableRow>> _poolByGrade = new();
    private readonly Dictionary<GlobalRarity, EnchantGradeTableRow>                  _gradeRows   = new();
    private readonly Dictionary<int, EnchantItemTableRow>                            _itemByTid   = new();

    /// <summary>모든 행을 GameTable에서 읽어 등록한다. GameTable.LoadAll 이후에 부른다.</summary>
    public void LoadAll()
    {
        Load(GameTable.EnchantOptionTable.All, GameTable.EnchantGradeTable.All, GameTable.EnchantItemTable.All);
    }

    /// <summary>세 시트로 인덱스를 만든다. TID 중복 · Value ≤ 0 · 칸 수 상한 초과 · 칸이 있는데 풀이 없는 등급이면 예외(기동 실패).</summary>
    public void Load(
        IEnumerable<EnchantOptionTableRow> options,
        IEnumerable<EnchantGradeTableRow>  grades,
        IEnumerable<EnchantItemTableRow>   items)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(grades);
        ArgumentNullException.ThrowIfNull(items);

        _optionByTid.Clear();
        _poolByGrade.Clear();
        _gradeRows.Clear();
        _itemByTid.Clear();

        var byGrade = new Dictionary<GlobalRarity, List<EnchantOptionTableRow>>();

        foreach (var row in options)
        {
            if (!_optionByTid.TryAdd(row.EnchantOptionTID, row))
            {
                throw new InvalidOperationException($"EnchantOptionTable에 EnchantOptionTID가 중복됐습니다: {row.EnchantOptionTID}");
            }

            // 음수 칸은 경험치 배율(1000 + 가산)을 0 이하로 끌어내릴 수 있다. 퇴역은 Value가 아니라 Weight 0으로 한다.
            if (row.Value <= 0)
            {
                throw new InvalidOperationException($"EnchantOptionTable의 Value는 양수여야 합니다: {row.EnchantOptionTID} = {row.Value}");
            }

            if (!byGrade.TryGetValue(row.Grade, out var list))
            {
                list = new List<EnchantOptionTableRow>();
                byGrade[row.Grade] = list;
            }
            list.Add(row);
        }

        foreach (var (grade, list) in byGrade)
        {
            _poolByGrade[grade] = WeightedPicker<EnchantOptionTableRow>.From(list, r => r.Weight);
        }

        foreach (var row in grades)
        {
            if (row.SlotCount > MaxSlotCount)
            {
                throw new InvalidOperationException($"EnchantGradeTable의 SlotCount가 상한 {MaxSlotCount}을 넘습니다: {row.Grade} = {row.SlotCount}");
            }

            _gradeRows[row.Grade] = row;
        }

        // 인챈트는 일반에서 시작해 한 단계씩 오른다 — 도달할 수 있는 등급에 풀이 없으면 큐브 소모 뒤에 예외가 난다.
        var hasSlots = _gradeRows.Values.Any(r => r.SlotCount > 0);
        for (var grade = GlobalRarity.Common; hasSlots && grade <= GlobalRarity.Mythic; grade++)
        {
            if (!_poolByGrade.ContainsKey(grade))
            {
                throw new InvalidOperationException($"EnchantOptionTable에 {grade} 등급 옵션이 없습니다.");
            }
        }

        foreach (var row in items)
        {
            if (!_itemByTid.TryAdd(row.ItemTID, row))
            {
                throw new InvalidOperationException($"EnchantItemTable에 ItemTID가 중복됐습니다: {row.ItemTID}");
            }
        }
    }

    public bool TryGetOption(int optionTid, out EnchantOptionTableRow row)
        => _optionByTid.TryGetValue(optionTid, out row!);

    public bool TryGetItem(int itemTid, out EnchantItemTableRow row)
        => _itemByTid.TryGetValue(itemTid, out row!);

    /// <summary>그 등급 장비의 칸 수. 표에 없는 등급은 0 — 큐브를 쓸 칸이 없다.</summary>
    public int SlotCountOf(GlobalRarity equipGrade)
    {
        if (!_gradeRows.TryGetValue(equipGrade, out var row))
        {
            return 0;
        }

        return row.SlotCount;
    }

    /// <summary>그 큐브로 인챈트 등급이 한 단계 오를 확률(만분율). 기본 확률 × 큐브 배율(천분율), 최고 등급은 0이다.</summary>
    public int RankUpPermyriadOf(GlobalRarity enchantGrade, EnchantItemTableRow cube)
    {
        if (!_gradeRows.TryGetValue(enchantGrade, out var row) || NextGrade(enchantGrade) == enchantGrade)
        {
            return 0;
        }

        return (int)((long)row.UpPermyriad * cube.UpRatePermille / 1000);
    }

    /// <summary>그 등급에 뽑을 옵션이 있는가. 없으면 RollOptions가 예외를 던진다.</summary>
    public bool HasPool(GlobalRarity grade) => _poolByGrade.ContainsKey(grade);

    /// <summary>그 등급의 풀에서 칸 수만큼 뽑는다. 같은 옵션이 겹쳐 나오는 것은 허용이다(잭팟).</summary>
    public List<EnchantOptionTableRow> RollOptions(GlobalRarity grade, int slotCount, Random? random = null)
    {
        if (!_poolByGrade.TryGetValue(grade, out var picker))
        {
            throw new InvalidOperationException($"EnchantOptionTable에 {grade} 등급 옵션이 없습니다.");
        }

        return picker.PickMany(slotCount, random);
    }

    /// <summary>인챈트 등급 사다리. Mythic이 최고라 거기서 멈춘다.</summary>
    public static GlobalRarity NextGrade(GlobalRarity grade)
    {
        if (grade < GlobalRarity.Common || grade >= GlobalRarity.Mythic)
        {
            return grade;
        }

        return grade + 1;
    }
}
