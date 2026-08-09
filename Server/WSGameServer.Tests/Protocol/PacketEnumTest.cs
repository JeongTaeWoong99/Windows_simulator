using GameData;
using MikaProtocol;

namespace WSGameServer.Tests.ProtocolTests;

/// <summary>
/// 프로토콜 <see cref="EGlobalRarity"/>와 엑셀 생성 <see cref="GlobalRarity"/>의
/// <b>이름·값 1:1</b>을 지킨다. GachaService.RarityOf가 byte 캐스팅으로 값을 그대로
/// 옮기므로, 엑셀에서 등급을 추가·재배열하면 컴파일은 통과한 채
/// 클라이언트 연출 등급만 조용히 어긋난다 — 이 테스트가 그 드리프트를 잡는다.
/// </summary>
public class PacketEnumTest
{
    [Fact]
    public void 프로토콜_등급은_GameData_등급과_이름_값이_1대1이다()
    {
        var protocol = Enum.GetValues<EGlobalRarity>()
            .Select(v => $"{v}={(byte)v}");

        // Max는 개수 셈용 센티널 — 실제 등급이 아니라 와이어로 나가지 않는다.
        var gameData = Enum.GetValues<GlobalRarity>()
            .Where(v => v != GlobalRarity.Max)
            .Select(v => $"{v}={(byte)v}");

        protocol.ShouldBe(gameData);
    }

    [Fact]
    public void 프로토콜_산업타입은_GameData_산업타입과_이름_값이_1대1이다()
    {
        // 배치·적성이 EIndustryType으로 산업을 지목하고 서버가 byte 캐스팅으로 옮긴다.
        var protocol = Enum.GetValues<EIndustryType>()
            .Select(v => $"{v}={(byte)v}");

        var gameData = Enum.GetValues<IndustryType>()
            .Where(v => v != IndustryType.Max)
            .Select(v => $"{v}={(byte)v}");

        protocol.ShouldBe(gameData);
    }

    [Fact]
    public void 산업타입_값은_ItemType의_산업_구간과_같다()
    {
        // DropTID(산업×100000+…)와 DB(t_workstation_slot.industry 등)가 ItemType 숫자로 저장돼 있다.
        // 두 enum이 갈라져 있어도 값이 어긋나면 저장된 데이터가 통째로 다른 산업을 가리킨다.
        foreach (var industry in Enum.GetValues<IndustryType>())
        {
            if (industry == IndustryType.Max)
            {
                continue;
            }

            var name = industry.ToString();

            Enum.IsDefined(typeof(ItemType), name)
                .ShouldBeTrue($"ItemType에 '{name}'이(가) 없습니다 — 산업 구간이 갈라졌습니다.");

            ((byte)Enum.Parse<ItemType>(name)).ShouldBe((byte)industry,
                $"'{name}'의 값이 ItemType과 다릅니다 — DropTID·DB 저장값이 어긋납니다.");
        }
    }

    [Fact]
    public void 산업타입은_아이템_분류를_담지_않는다()
    {
        // Misc·Special은 아이템 분류일 뿐 배치 대상이 아니다. 섞이면 배치 UI·적성 목록에 뜬다.
        Enum.GetValues<IndustryType>()
            .Select(v => v.ToString())
            .ShouldBe(new[] { "None", "Farming", "Fishing", "Mining", "Logging", "Hunting", "Max" });
    }
}
