namespace WSGameServer;

public sealed class Item(int itemId, int count, int slot)
{
    public int Id { get; init; } = itemId;
    public int Count { get; set; } = count;

    /// <summary>인벤토리 자원 탭의 칸 번호(0부터).</summary>
    public int Slot { get; set; } = slot;
}
