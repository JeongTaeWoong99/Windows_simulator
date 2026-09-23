using GameData;
using MikaUtils;

namespace WSGameServer;

// 인챈트 3시트 보관소 — 등급별 옵션 풀 · 등급 상승 확률 · 아이템 동작.
// GameTable.LoadAll 다음에 LoadAll 한 번, 이후 조회만(불변) → Server/docs/데이터-카탈로그.md
public sealed class EnchantCatalog : Singleton<EnchantCatalog>
{
    /// <summary>인챈트를 부여하면 생기는 줄 수.</summary>
    public const int BaseLineCount = 2;

    /// <summary>줄 확장의 상한. 넘기면 EnchantLineMax로 거절한다.</summary>
    public const int MaxLineCount = 3;

    private readonly Dictionary<int, EnchantOptionTableRow>                       _optionByTid = new();
    private readonly Dictionary<GlobalRarity, WeightedPicker<EnchantOptionTableRow>> _poolByGrade = new();
    private readonly Dictionary<GlobalRarity, int>                                _upPermille  = new();
    private readonly Dictionary<int, EnchantItemTableRow>                         _itemByTid   = new();

    /// <summary>모든 행을 GameTable에서 읽어 등록한다. GameTable.LoadAll 이후에 부른다.</summary>
    public void LoadAll()
    {
        Load(GameTable.EnchantOptionTable.All, GameTable.EnchantGradeTable.All, GameTable.EnchantItemTable.All);
    }

    /// <summary>세 시트로 인덱스를 만든다. TID 중복 · Value ≤ 0 · 알 수 없는 Action이면 예외(기동 실패).</summary>
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
        _upPermille.Clear();
        _itemByTid.Clear();

        var byGrade = new Dictionary<GlobalRarity, List<EnchantOptionTableRow>>();

        foreach (var row in options)
        {
            if (!_optionByTid.TryAdd(row.EnchantOptionTID, row))
            {
                throw new InvalidOperationException($"EnchantOptionTable에 EnchantOptionTID가 중복됐습니다: {row.EnchantOptionTID}");
            }

            // 음수 줄은 경험치 배율(1000 + 가산)을 0 이하로 끌어내릴 수 있다. 퇴역은 Value가 아니라 Weight 0으로 한다.
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
            _upPermille[row.Grade] = row.UpPermille;
        }

        foreach (var row in items)
        {
            if (!_itemByTid.TryAdd(row.ItemTID, row))
            {
                throw new InvalidOperationException($"EnchantItemTable에 ItemTID가 중복됐습니다: {row.ItemTID}");
            }

            if (row.Action is not (EnchantAction.Grant or EnchantAction.GradeUp or EnchantAction.ExpandLine))
            {
                throw new InvalidOperationException($"EnchantItemTable에 알 수 없는 Action이 있습니다: {row.ItemTID} = {row.Action}");
            }
        }
    }

    public bool TryGetOption(int optionTid, out EnchantOptionTableRow row)
        => _optionByTid.TryGetValue(optionTid, out row!);

    public bool TryGetItem(int itemTid, out EnchantItemTableRow row)
        => _itemByTid.TryGetValue(itemTid, out row!);

    /// <summary>그 등급에 뽑을 옵션이 있는가. 없으면 RollOptions가 예외를 던진다.</summary>
    public bool HasPool(GlobalRarity grade) => _poolByGrade.ContainsKey(grade);

    /// <summary>다음 등급으로 오를 확률(천분율). 표에 없는 등급은 0 — 오르지 않는다.</summary>
    public int UpPermilleOf(GlobalRarity grade)
        => _upPermille.TryGetValue(grade, out var permille) ? permille : 0;

    /// <summary>그 등급의 풀에서 줄 수만큼 뽑는다. 같은 줄이 겹쳐 나오는 것은 허용이다(잭팟).</summary>
    public List<EnchantOptionTableRow> RollOptions(GlobalRarity grade, int lineCount, Random? random = null)
    {
        if (!_poolByGrade.TryGetValue(grade, out var picker))
        {
            throw new InvalidOperationException($"EnchantOptionTable에 {grade} 등급 옵션이 없습니다.");
        }

        return picker.PickMany(lineCount, random);
    }

    /// <summary>인챈트 등급 사다리. Legendary가 최고라 거기서 멈춘다.</summary>
    public static GlobalRarity NextGrade(GlobalRarity grade)
    {
        return grade switch
        {
            GlobalRarity.Rare => GlobalRarity.Epic,
            GlobalRarity.Epic => GlobalRarity.Legendary,
            _                 => grade,
        };
    }
}
