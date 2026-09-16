using GameData;

namespace WSGameServer;

/// <summary>종류↔칸 허용표와 TID 인덱스. 장신구가 두 칸 어디든 들어가는지가 핵심이다.</summary>
public class EquipCatalogTest
{
    private static EquipTableRow Row(int tid, EquipKind kind) => new() { EquipTID = tid, Name = $"장비{tid}", EquipKind = kind };

    [Theory]
    [InlineData(EquipKind.Weapon,    EquipSlot.Weapon,     true)]
    [InlineData(EquipKind.Weapon,    EquipSlot.Accessory1, false)]
    [InlineData(EquipKind.Accessory, EquipSlot.Accessory1, true)]
    [InlineData(EquipKind.Accessory, EquipSlot.Accessory2, true)]
    [InlineData(EquipKind.Accessory, EquipSlot.Gem,        false)]
    [InlineData(EquipKind.Gem,       EquipSlot.Gem,        true)]
    [InlineData(EquipKind.Gem,       EquipSlot.Weapon,     false)]
    public void 종류가_칸에_맞는지(EquipKind kind, EquipSlot slot, bool expected)
    {
        EquipCatalog.CanEquip(kind, slot).ShouldBe(expected);
    }

    [Fact]
    public void None과_Max는_유효한_칸이_아니다()
    {
        EquipCatalog.IsValidSlot(EquipSlot.None).ShouldBeFalse();
        EquipCatalog.IsValidSlot(EquipSlot.Max).ShouldBeFalse();
        EquipCatalog.IsValidSlot(EquipSlot.Accessory2).ShouldBeTrue();
    }

    [Fact]
    public void TID로_행을_찾는다()
    {
        var catalog = new EquipCatalog();
        catalog.Load(new[] { Row(1001, EquipKind.Weapon), Row(2001, EquipKind.Accessory) });

        catalog.Count.ShouldBe(2);
        catalog.TryGet(2001, out var row).ShouldBeTrue();
        row.EquipKind.ShouldBe(EquipKind.Accessory);
        catalog.TryGet(9999, out _).ShouldBeFalse();
    }

    [Fact]
    public void TID가_중복되면_예외다()
    {
        var catalog = new EquipCatalog();

        Should.Throw<InvalidOperationException>(() =>
            catalog.Load(new[] { Row(1001, EquipKind.Weapon), Row(1001, EquipKind.Gem) }));
    }

    [Fact]
    public void 실제_엑셀_데이터가_적재된다()
    {
        GameTableFixture.EnsureLoaded();
        var catalog = new EquipCatalog();

        catalog.LoadAll();

        catalog.Count.ShouldBeGreaterThan(0);
    }
}
