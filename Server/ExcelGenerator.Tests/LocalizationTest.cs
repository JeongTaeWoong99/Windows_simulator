using static ExcelGenerator.ExcelGenerator;

namespace ExcelGenerator.Tests;

/// <summary>
/// lstring 컬럼이 번역 파일(Localization/Loc_Item.xlsx 등)과 맞물리는지 지킨다.
/// 틀리면 번역이 사라지거나, 원문이 바뀐 줄을 놓치거나, 영어 화면에 한국어 이름이 섞여 나간다.
/// </summary>
public class LocalizationTest
{
    private static ColumnInfo Col(string name, string rawType, string? def = null)
    {
        var type = rawType switch
        {
            "int"               => ColumnInfo.RecordType.Int,
            "string" or "lstring" => ColumnInfo.RecordType.String,
            _                   => ColumnInfo.RecordType.Ignore,
        };
        return new ColumnInfo(name, type, rawType, ColumnInfo.Platform.ServerClient, null, null, def, null);
    }

    private static TableData ItemTable(params string[][] rows)
    {
        var columns = new[] { Col("ItemTID", "int"), Col("Name", "lstring"), Col("Gold", "int"), Col("Tooltip", "lstring", "\"\""), Col("Description", "string", "\"\"") };
        return new TableData("ItemTable", columns, rows);
    }

    private static readonly ColumnInfo[] SheetColumns =
    {
        Col("Key", "string"), Col("KR", "string"), Col("EN", "string", "\"\""), Col("Status", ""),
    };

    private static TableData Sheet(params string[][] rows) => new("LocalizationTable.Item", SheetColumns, rows);

    private static TableData Ui(params string[][] rows) => new("LocalizationTable.UI", SheetColumns, rows);

    private static string[] Item(string tid, string name, string gold, string tooltip) => new[] { tid, name, gold, tooltip, "메모" };

    [Fact]
    public void lstring_컬럼만_테이블_TID_컬럼으로_키를_만든다()
    {
        var sources = Localization.Collect(new[] { ItemTable(Item("100061", "흙 묻은 유물", "200", "골드 {Gold}")) });

        sources.Select(s => (s.Key, s.Template)).ShouldBe(new[]
        {
            ("Item.100061.Name", "흙 묻은 유물"),
            ("Item.100061.Tooltip", "골드 {Gold}"),
        });
    }

    [Fact]
    public void 새_키는_번역이_비어_있다고_표시한다()
    {
        var sources = Localization.Collect(new[] { ItemTable(Item("100061", "흙 묻은 유물", "200", "")) });

        var rows = Localization.Sync(sources, Sheet());

        rows[0].ShouldBe(new[] { "Item.100061.Name", "흙 묻은 유물", "", "⚠️ EN 없음" });
    }

    [Fact]
    public void 원문이_그대로면_번역을_지키고_상태를_비운다()
    {
        var sources = Localization.Collect(new[] { ItemTable(Item("100061", "흙 묻은 유물", "200", "")) });
        var sheet = Sheet(new[] { "Item.100061.Name", "흙 묻은 유물", "Muddy Relic", "" });

        var rows = Localization.Sync(sources, sheet);

        rows[0].ShouldBe(new[] { "Item.100061.Name", "흙 묻은 유물", "Muddy Relic", "" });
    }

    [Fact]
    public void 원문이_바뀌면_번역은_두고_재번역을_표시한다()
    {
        var sources = Localization.Collect(new[] { ItemTable(Item("100061", "진흙 묻은 유물", "200", "")) });
        var sheet = Sheet(new[] { "Item.100061.Name", "흙 묻은 유물", "Muddy Relic", "" });

        var rows = Localization.Sync(sources, sheet);

        rows[0].ShouldBe(new[] { "Item.100061.Name", "진흙 묻은 유물", "Muddy Relic", "🔄 EN" });
    }

    [Fact]
    public void 재번역_표시는_사람이_지울_때까지_남는다()
    {
        var sources = Localization.Collect(new[] { ItemTable(Item("100061", "진흙 묻은 유물", "200", "")) });

        var kept    = Localization.Sync(sources, Sheet(new[] { "Item.100061.Name", "진흙 묻은 유물", "Muddy Relic", "🔄 EN" }));
        var cleared = Localization.Sync(sources, Sheet(new[] { "Item.100061.Name", "진흙 묻은 유물", "Mud Relic", "" }));

        kept[0][3].ShouldBe("🔄 EN");
        cleared[0][3].ShouldBe("");
    }

    [Fact]
    public void 원본에서_사라진_키는_시트에서도_빠진다()
    {
        var sources = Localization.Collect(new[] { ItemTable(Item("100061", "흙 묻은 유물", "200", "")) });
        var sheet = Sheet(new[] { "Item.999.Name", "없어진 것", "Gone", "" });

        var rows = Localization.Sync(sources, sheet);

        rows.Select(r => r[0]).ShouldNotContain("Item.999.Name");
    }

    [Fact]
    public void 원문이_빈_셀은_시트에_올리지_않고_테이블에는_빈_값으로_둔다()
    {
        var sources = Localization.Collect(new[] { ItemTable(Item("100061", "흙 묻은 유물", "200", "")) });

        var rows = Localization.Sync(sources, Sheet());
        var result = Localization.Build(sources, Sheet(rows.ToArray()), Ui(), strict: false);

        rows.Select(r => r[0]).ShouldBe(new[] { "Item.100061.Name" });
        result.Rows[1].ShouldBe(new[] { "Item.100061.Tooltip", "", "" });
    }

    [Fact]
    public void Tooltip은_언어마다_보간하고_이름은_그_언어로_넣는다()
    {
        var table = ItemTable(Item("100061", "흙 묻은 유물", "200", "{Name}: 골드 {Gold}"));
        var sources = Localization.Collect(new[] { table });
        var data = Sheet(
            new[] { "Item.100061.Name", "흙 묻은 유물", "Muddy Relic", "" },
            new[] { "Item.100061.Tooltip", "{Name}: 골드 {Gold}", "{Name}: {Gold} gold", "" });

        var result = Localization.Build(sources, data, Ui(), strict: false);

        result.Name.ShouldBe("LocalizationTable");
        result.columnInfos.Select(c => c.Name).ShouldBe(new[] { "Key", "KR", "EN" });
        result.Rows[1].ShouldBe(new[] { "Item.100061.Tooltip", "흙 묻은 유물: 골드 200", "Muddy Relic: 200 gold" });
        table.Rows[0][3].ShouldBe("{Name}: 골드 {Gold}");   // 원본 행은 건드리지 않는다
    }

    [Fact]
    public void 번역이_비면_한국어로_대신한다()
    {
        var sources = Localization.Collect(new[] { ItemTable(Item("100061", "흙 묻은 유물", "200", "골드 {Gold}")) });
        var data = Sheet(
            new[] { "Item.100061.Name", "흙 묻은 유물", "", "" },
            new[] { "Item.100061.Tooltip", "골드 {Gold}", "", "" });

        var result = Localization.Build(sources, data, Ui(new[] { "UI.Inventory.Sell", "판매", "", "" }), strict: false);

        result.Rows.Select(r => r[2]).ShouldBe(new[] { "흙 묻은 유물", "골드 200", "판매" });
    }

    [Fact]
    public void strict면_빈_번역에서_멈춘다()
    {
        var sources = Localization.Collect(new[] { ItemTable(Item("100061", "흙 묻은 유물", "200", "")) });
        var data = Sheet(new[] { "Item.100061.Name", "흙 묻은 유물", "", "" });

        var ex = Should.Throw<InvalidDataException>(() => Localization.Build(sources, data, Ui(), strict: true));
        ex.Message.ShouldContain("Item.100061.Name");
    }

    [Fact]
    public void 키_앞부분으로_번역_파일을_고른다()
    {
        Localization.FileOf("Item.100061.Name").ShouldBe("Loc_Item.xlsx");
        Localization.FileOf("IndustryLevel.101.Name").ShouldBe("Loc_IndustryLevel.xlsx");
    }

    [Fact]
    public void UI_파일과_겹치는_테이블은_멈춘다()
    {
        Should.Throw<InvalidDataException>(() => Localization.FileOf("UI.1.Name"));
    }

    [Fact]
    public void Tooltip_토큰이_원문과_다르면_멈춘다()
    {
        var sources = Localization.Collect(new[] { ItemTable(Item("100061", "흙 묻은 유물", "200", "골드 {Gold}")) });
        var data = Sheet(
            new[] { "Item.100061.Name", "흙 묻은 유물", "Muddy Relic", "" },
            new[] { "Item.100061.Tooltip", "골드 {Gold}", "{Gld} gold", "" });

        var ex = Should.Throw<InvalidDataException>(() => Localization.Build(sources, data, Ui(), strict: false));
        ex.Message.ShouldContain("Item.100061.Tooltip");
        ex.Message.ShouldContain("EN");
    }
}
