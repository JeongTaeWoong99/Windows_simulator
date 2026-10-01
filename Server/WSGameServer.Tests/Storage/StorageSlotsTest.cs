namespace WSGameServer;

/// <summary>
/// 칸 계산(T-058)을 지킨다 — 새 항목은 앞에서부터 첫 빈 칸, 자리 이동은 빈 칸이면 옮기고 차 있으면 교환.
/// 틀리면 재접속할 때마다 칸이 흔들리거나 두 항목이 한 칸에 겹쳐 하나가 화면에서 사라진다.
/// </summary>
public class StorageSlotsTest
{
    [Fact]
    public void 첫_빈_칸은_앞에서부터_센_구멍이다()
    {
        // 0·1·3이 찼다 → 2
        StorageSlots.FirstFree(new[] { 0, 1, 3 }).ShouldBe(2);
    }

    [Fact]
    public void 빈_격자의_첫_빈_칸은_0이다()
    {
        StorageSlots.FirstFree(Array.Empty<int>()).ShouldBe(0);
    }

    [Fact]
    public void 빈_칸으로_옮기면_그_항목만_바뀐다()
    {
        var occupied = new Dictionary<int, long> { [0] = 100, [1] = 200 };

        StorageSlots.Move(occupied, from: 1, to: 5)
            .ShouldBe(new[] { new SlotChange(200, 5) });
    }

    [Fact]
    public void 찬_칸으로_옮기면_두_항목이_자리를_바꾼다()
    {
        var occupied = new Dictionary<int, long> { [0] = 100, [1] = 200 };

        StorageSlots.Move(occupied, from: 0, to: 1)
            .ShouldBe(new[] { new SlotChange(100, 1), new SlotChange(200, 0) });
    }

    [Fact]
    public void 빈_칸에서_옮기면_아무것도_바뀌지_않는다()
    {
        var occupied = new Dictionary<int, long> { [0] = 100 };

        StorageSlots.Move(occupied, from: 3, to: 0).ShouldBeEmpty();
    }

    [Fact]
    public void 재번호는_순서대로_0부터_촘촘히_매긴다()
    {
        StorageSlots.Renumber(new long[] { 30, 10, 20 })
            .ShouldBe(new[] { new SlotChange(30, 0), new SlotChange(10, 1), new SlotChange(20, 2) });
    }
}
