using GameData;

namespace WSGameServer;

/// <summary>산업 데이터가 제공하는 번역 키로 실제 한·영 문구를 찾을 수 있는지 검증한다.</summary>
public class IndustryLocalizationTest
{
    [Theory]
    [InlineData(IndustryType.Farming)]
    [InlineData(IndustryType.Excavation)]
    [InlineData(IndustryType.Mining)]
    [InlineData(IndustryType.Logging)]
    [InlineData(IndustryType.Hunting)]
    public void 산업_이름과_툴팁의_번역_키는_enum_이름을_쓴다(IndustryType industry)
    {
        GameTableFixture.EnsureLoaded();

        var nameKey = $"Industry.{industry}.Name";
        var tooltipKey = $"Industry.{industry}.Tooltip";
        var row = GameTable.IndustryTable[industry];
        row.NameLocKey.ShouldBe(nameKey);
        row.TooltipLocKey.ShouldBe(tooltipKey);
        GameTable.LocalizationTable[nameKey].KR.ShouldBe(row.Name);
        GameTable.LocalizationTable[tooltipKey].KR.ShouldBe(row.Tooltip);
        GameTable.LocalizationTable[tooltipKey].EN.ShouldNotBeNullOrWhiteSpace();
    }
}
