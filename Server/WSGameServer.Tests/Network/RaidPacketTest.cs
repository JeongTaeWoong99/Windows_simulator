using MemoryPack;
using MikaProtocol;

namespace WSGameServer;

/// <summary>레이드 요청 ID와 전투 재생에 필요한 순서가 패킷 직렬화 뒤에도 유지되는지 검증한다.</summary>
public class RaidPacketTest
{
    private static readonly Guid RequestId = Guid.Parse("a3057ef1-7529-47f4-8501-7c45842ed202");

    [Fact]
    public void 도전_요청은_보스와_재시도_ID를_왕복한다()
    {
        var packet = new C_RaidChallengeRequest { BossTID = 3, RequestId = RequestId };

        var restored = MemoryPackSerializer.Deserialize<C_RaidChallengeRequest>(
            MemoryPackSerializer.Serialize(packet))!;

        (restored.BossTID, restored.RequestId).ShouldBe((3, RequestId));
    }

    [Fact]
    public void 도전_결과는_편성과_시간순_타격_정보를_왕복한다()
    {
        var packet = new S_RaidChallengeResponse
        {
            RequestId = RequestId,
            Party =
            [
                new RaidParticipantInfo { CharacterId = 5, InitialHealth = 100, RemainingHealth = 92 },
                new RaidParticipantInfo { CharacterId = 2, InitialHealth = 100, RemainingHealth = 100 },
            ],
            Hits =
            [
                new RaidHitInfo { ElapsedMs = 1_600, AttackerIndex = 0, TargetIndex = 2, Damage = 10, TargetHealth = 220 },
                new RaidHitInfo { ElapsedMs = 3_200, AttackerIndex = 2, TargetIndex = 0, Damage = 8, TargetHealth = 92 },
            ],
        };

        var restored = MemoryPackSerializer.Deserialize<S_RaidChallengeResponse>(
            MemoryPackSerializer.Serialize(packet))!;

        restored.RequestId.ShouldBe(RequestId);
        restored.Party.Select(member => (member.CharacterId, member.InitialHealth, member.RemainingHealth))
            .ShouldBe(new[] { (5L, 100, 92), (2L, 100, 100) });
        restored.Hits.Select(hit => (hit.ElapsedMs, hit.AttackerIndex, hit.TargetIndex, hit.Damage, hit.TargetHealth))
            .ShouldBe(new[] { (1_600, 0, 2, 10, 220), (3_200, 2, 0, 8, 92) });
    }
}
