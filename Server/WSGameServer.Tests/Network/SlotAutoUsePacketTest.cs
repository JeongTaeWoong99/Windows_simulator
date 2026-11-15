using MemoryPack;
using MikaProtocol;

namespace WSGameServer;

/// <summary>자동 사용 요청과 슬롯 스냅샷의 ON/OFF 계약이 직렬화 뒤에도 유지되는지 검증한다.</summary>
public class SlotAutoUsePacketTest
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void 자동_사용_요청은_슬롯과_영약과_ON_OFF를_왕복한다(bool on)
    {
        var bytes = MemoryPackSerializer.Serialize(new C_SlotAutoUseRequest { SlotIndex = 9, ItemTID = 100045, On = on });
        var request = MemoryPackSerializer.Deserialize<C_SlotAutoUseRequest>(bytes)!;
        request.SlotIndex.ShouldBe(9);
        request.ItemTID.ShouldBe(100045);
        request.On.ShouldBe(on);
    }

    [Fact]
    public void 슬롯_스냅샷은_ON인_영약_목록을_왕복한다()
    {
        var slot = new WorkStationSlot(9, GameData.IndustryType.None, 0, TestUserBuilder.Base);
        slot.SetAutoUse(100045, true);
        slot.SetAutoUse(100044, true);
        slot.SetAutoUse(100044, false);
        var packet = new S_WorkStationSlotSyncResponse { Slot = slot.ToInfo() };
        var restored = MemoryPackSerializer.Deserialize<S_WorkStationSlotSyncResponse>(MemoryPackSerializer.Serialize(packet))!;
        var enabled = restored.Slot!.AutoUse!.ShouldHaveSingleItem();
        enabled.ItemTID.ShouldBe(100045);
        enabled.On.ShouldBeTrue();
    }
}
