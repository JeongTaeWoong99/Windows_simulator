using static ExcelGenerator.ExcelGenerator;

namespace ExcelGenerator.Tests;

/// <summary>
/// Tooltip 컬럼의 {컬럼} 토큰이 같은 행 값으로 치환되는지 지킨다.
/// 틀리면 설명문의 숫자가 실제 컬럼과 어긋나거나, 잘못 쓴 토큰이 그대로 플레이어 화면에 나간다.
/// </summary>
public class TooltipInterpolatorTest
{
    private static ColumnInfo Col(string name, string rawType = "int", string? def = null)
    {
        return new ColumnInfo(name, ColumnInfo.RecordType.Int, rawType, ColumnInfo.Platform.ServerClient, null, null, def, null);
    }

    private static TableData Table(params string[][] rows)
    {
        var columns = new[] { Col("EquipTID"), Col("SpeedAddPermille"), Col("Name", "string"), Col("Tooltip", "string", "\"\"") };
        return new TableData("EquipTable", columns, rows);
    }

    private static string Apply(string tooltip, string speed = "40")
    {
        var row = new[] { "1001", speed, "낚싯대", tooltip };
        TooltipInterpolator.Apply(new[] { Table(row) });
        return row[3];
    }

    [Fact]
    public void 컬럼_토큰은_같은_행의_값으로_바뀐다()
    {
        Apply("{Name} 장착").ShouldBe("낚싯대 장착");
    }

    [Fact]
    public void permille_지정자는_천분율을_퍼센트로_적는다()
    {
        Apply("속도 +{SpeedAddPermille:permille}").ShouldBe("속도 +4%");
        Apply("{SpeedAddPermille:permille}", speed: "500").ShouldBe("50%");
        Apply("{SpeedAddPermille:permille}", speed: "15").ShouldBe("1.5%");
    }

    [Fact]
    public void 중괄호_두_개는_글자_그대로_남는다()
    {
        Apply("{{고정}} {Name}").ShouldBe("{고정} 낚싯대");
    }

    [Fact]
    public void 토큰이_없으면_그대로_둔다()
    {
        Apply("").ShouldBe("");
        Apply("평범한 설명").ShouldBe("평범한 설명");
    }

    [Theory]
    [InlineData("{Nmae}")]            // 없는 컬럼
    [InlineData("{Name:percent}")]    // 모르는 지정자
    [InlineData("{Name")]             // 닫히지 않음
    [InlineData("값 }")]              // 짝 없는 닫는 괄호
    [InlineData("{Name:permille}")]   // 숫자가 아닌 값에 permille
    [InlineData("{Tooltip}")]         // 자기 자신
    public void 잘못된_토큰은_조용히_넘어가지_않고_멈춘다(string tooltip)
    {
        var ex = Should.Throw<InvalidDataException>(() => Apply(tooltip));
        ex.Message.ShouldContain("EquipTable");
        ex.Message.ShouldContain("1001");
    }

    [Fact]
    public void Tooltip이_아닌_컬럼은_건드리지_않는다()
    {
        var row = new[] { "1001", "40", "{Name}", "" };
        TooltipInterpolator.Apply(new[] { Table(row) });
        row[2].ShouldBe("{Name}");
    }
}
