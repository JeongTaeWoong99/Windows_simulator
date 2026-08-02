namespace MikaProtocol
{
    public enum EItemChangeKind : byte
    {
        None = 0,
        Add = 1,
        Update = 2,
        Remove = 3,
    }

    // 아이템 등급(전역 공통). GameData.GlobalRarity(Enum.xlsx)와 값이 1:1이어야 한다 —
    // 서버가 테이블 값을 byte 캐스팅으로 그대로 실어 보내기 때문이다.
    public enum EGlobalRarity : byte
    {
        None = 0,
        Common = 1,
        Uncommon = 2,
        Rare = 3,
        Epic = 4,
        Legendary = 5,
        Mythic = 6,
    }
}