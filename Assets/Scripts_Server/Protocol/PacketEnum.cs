namespace MikaProtocol
{
    public enum EItemChangeKind : byte
    {
        None = 0,
        Add = 1,
        Update = 2,
        Remove = 3,
    }

    // 가챠 결과 아이템의 등급(뽑기 연출용)
    public enum EItemRarity : byte
    {
        None = 0,
        Common = 1,
        Rare = 2,
        Epic = 3,
        Legendary = 4,
    }
}