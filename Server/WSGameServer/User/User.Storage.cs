namespace WSGameServer;

public partial class User
{
    /// <summary>창고 탭마다의 칸 수(자원·캐릭터·장비 각각). 클라 칸 프레임 수와 같다 — 공용 상수 시트(T-077)가 서면 옮긴다.</summary>
    public const int StorageCapacity = 200;

    // DB가 PK를 발급하기 전의 캐릭터 수. 세지 않으면 응답이 오기 전 연속 뽑기가 한도를 지나친다.
    private int _pendingCharacterCount;

    /// <summary>자원 칸 — 보유 수량이 0보다 큰 아이템 종류 수</summary>
    public int ItemSlotsUsed => Inventory.KindCount;

    /// <summary>캐릭터 칸 — 보유 개체 + 지급 대기 중인 개체</summary>
    public int CharacterSlotsUsed => _characters.Count + _pendingCharacterCount;

    /// <summary>장비 칸 — 보유 개체 + 지급 대기 중인 개체</summary>
    public int EquipSlotsUsed => _equips.Count + _pendingEquipPositions.Count;

    // 이번 지급이 새로 차지할 칸이 남은 칸 안에 드는가. 이미 가진 자원 TID는 칸을 더 쓰지 않는다.
    // freedItemTid — 이번에 전량 소모돼 비는 자원 칸(상자를 전부 여는 경우). 없으면 0.
    public bool HasStorageFor(IEnumerable<int> itemTids, int characterCount, int equipCount, int freedItemTid = 0)
    {
        var newKinds = itemTids.Distinct().Count(tid => tid != freedItemTid && Inventory.GetCount(tid) == 0);
        var freed    = freedItemTid != 0 ? 1 : 0;

        return ItemSlotsUsed - freed + newKinds <= StorageCapacity &&
               CharacterSlotsUsed + characterCount <= StorageCapacity &&
               EquipSlotsUsed + equipCount <= StorageCapacity;
    }
}
