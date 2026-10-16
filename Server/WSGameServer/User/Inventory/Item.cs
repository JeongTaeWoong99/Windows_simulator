namespace WSGameServer;

/// <summary>자원 칸 하나의 묶음. 칸은 보관함 사전의 키라 바꾸지 않는다 — 옮길 때는 새로 만든다.</summary>
public sealed class Item(int itemId, int count, int slot)
{
    public int Id { get; init; } = itemId;
    public int Count { get; set; } = count;

    /// <summary>보관함 자원 탭의 칸 번호(0부터).</summary>
    public int Slot { get; } = slot;
}
