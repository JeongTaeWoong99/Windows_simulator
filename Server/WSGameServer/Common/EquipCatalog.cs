using GameData;
using MikaUtils;

namespace WSGameServer;

// EquipTable 보관소. TID → 행과 "이 종류가 이 칸에 들어가는가"를 답한다 — 장신구만 두 칸(Accessory1·2) 어디든.
// GameTable.LoadAll 다음에 LoadAll 한 번, 이후 조회만(불변) → Server/docs/데이터-카탈로그.md 5장
public sealed class EquipCatalog : Singleton<EquipCatalog>
{
    private readonly Dictionary<int, EquipTableRow> _byTid = new();

    /// <summary>등록된 장비 종류 수.</summary>
    public int Count => _byTid.Count;

    /// <summary>모든 행을 GameTable에서 읽어 등록한다. GameTable.LoadAll 이후에 부른다.</summary>
    public void LoadAll()
    {
        Load(GameTable.EquipTable.All);
    }

    /// <summary>행 목록으로 인덱스를 만든다. 같은 TID가 두 번 나오면 예외.</summary>
    public void Load(IEnumerable<EquipTableRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        _byTid.Clear();

        foreach (var row in rows)
        {
            if (!_byTid.TryAdd(row.EquipTID, row))
            {
                throw new InvalidOperationException($"EquipTable에 EquipTID가 중복됐습니다: {row.EquipTID}");
            }
        }
    }

    public bool TryGet(int equipTid, out EquipTableRow row)
        => _byTid.TryGetValue(equipTid, out row!);

    /// <summary>None·Max를 뺀 실제 칸인가. 클라가 보낸 값이라 범위부터 거른다.</summary>
    public static bool IsValidSlot(EquipSlot slot)
        => slot > EquipSlot.None && slot < EquipSlot.Max;

    /// <summary>종류가 칸에 맞는가. 장신구는 두 칸 어디든 — 칸을 나눈 이유는 매핑 유일키 때문이지 종류가 달라서가 아니다.</summary>
    public static bool CanEquip(EquipKind kind, EquipSlot slot)
    {
        return kind switch
        {
            EquipKind.Weapon    => slot == EquipSlot.Weapon,
            EquipKind.Accessory => slot == EquipSlot.Accessory1 || slot == EquipSlot.Accessory2,
            EquipKind.Gem       => slot == EquipSlot.Gem,
            _                   => false,
        };
    }
}
