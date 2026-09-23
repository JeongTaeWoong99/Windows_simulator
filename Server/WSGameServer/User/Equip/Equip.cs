using GameData;

namespace WSGameServer;

// 유저가 소유한 장비 개체. Id(DB PK)와 Tid(테이블 정의)를 구분한다. 스탯은 Row에서 읽고 개체는 위치(착용·창고 칸)만 든다.
public sealed class Equip
{
    public Equip(long id, EquipTableRow row, int slotPosition)
    {
        Id           = id;
        Row          = row;
        SlotPosition = slotPosition;
    }

    /// <summary>장비 개체 PK (<c>t_user_equip.equip_id</c>). DB가 발급한다.</summary>
    public long Id { get; }

    public EquipTableRow Row { get; }

    public int          Tid              => Row.EquipTID;
    public EquipKind    Kind             => Row.EquipKind;
    public IndustryType Industry         => Row.Industry;
    public int          SpeedAddPermille => Row.SpeedAddPermille;

    /// <summary>인챈트 등급. None이면 인챈트가 없다.</summary>
    public GlobalRarity EnchantGrade { get; private set; }

    private readonly List<EnchantOptionTableRow> _enchantOptions = new();

    /// <summary>옵션 줄. 순서가 곧 DB의 enchant_1~3 순서다.</summary>
    public IReadOnlyList<EnchantOptionTableRow> EnchantOptions => _enchantOptions;

    /// <summary>줄 수. 2 또는 3이며 인챈트가 없으면 0이다.</summary>
    public int EnchantLineCount => _enchantOptions.Count;

    /// <summary>패킷·DB에 싣는 EnchantOptionTID 목록.</summary>
    public IReadOnlyList<int> EnchantOptionTids => _enchantOptions.Select(o => o.EnchantOptionTID).ToList();

    /// <summary>착용 캐릭터 개체. 0이면 창고에 있다.</summary>
    public long EquippedCharacterId { get; private set; }

    /// <summary>착용 칸. 창고면 None.</summary>
    public EquipSlot EquippedSlot { get; private set; }

    /// <summary>창고 장비 탭의 칸 번호(0부터). 착용 중이어도 유지된다 — 해제하면 그 자리로 돌아간다.</summary>
    public int SlotPosition { get; set; }

    public bool IsEquipped => EquippedCharacterId != 0;

    public void Wear(long characterId, EquipSlot slot)
    {
        EquippedCharacterId = characterId;
        EquippedSlot        = slot;
    }

    public void TakeOff()
    {
        EquippedCharacterId = 0;
        EquippedSlot        = EquipSlot.None;
    }

    /// <summary>이 슬롯 산업에 가산이 붙는가. None(전 산업) 장비는 어디든 붙는다.</summary>
    public bool AppliesTo(IndustryType industry)
        => Industry == IndustryType.None || Industry == industry;

    /// <summary>인챈트 상태를 통째로 바꾼다(부여·재롤·확장 모두 이 경로다). 줄은 항상 전부 넘긴다.</summary>
    public void SetEnchant(GlobalRarity grade, IReadOnlyList<EnchantOptionTableRow> options)
    {
        EnchantGrade = grade;
        _enchantOptions.Clear();
        _enchantOptions.AddRange(options);
    }

    /// <summary>이 슬롯 산업에 붙는 속도 가산 — 테이블 기본값 + 인챈트 줄. 산업이 맞지 않는 쪽은 빠진다.</summary>
    public int SpeedAddPermilleFor(IndustryType industry)
    {
        var total = AppliesTo(industry) ? SpeedAddPermille : 0;

        foreach (var option in _enchantOptions)
        {
            if (option.OptionType != EnchantOptionType.Speed)
            {
                continue;
            }
            if (option.Industry != IndustryType.None && option.Industry != industry)
            {
                continue;
            }

            total += option.Value;
        }

        return total;
    }

    /// <summary>캐릭터 경험치 가산(천분율). 경험치에는 산업 구분이 없다.</summary>
    public int ExpAddPermille
    {
        get
        {
            var total = 0;
            foreach (var option in _enchantOptions)
            {
                if (option.OptionType == EnchantOptionType.CharacterExp)
                {
                    total += option.Value;
                }
            }
            return total;
        }
    }
}
