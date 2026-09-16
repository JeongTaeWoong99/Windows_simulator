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
}
