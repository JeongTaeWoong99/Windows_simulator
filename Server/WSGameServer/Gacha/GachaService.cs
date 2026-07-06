using MikaProtocol;
using MikaUtils;

namespace WSGameServer.Gacha;

/// <summary>
/// 가챠 뽑기 흐름을 조립하는 서비스 계층(Singleton).
/// 추첨은 <see cref="GachaTable"/>(순수 데이터)에 위임하고, 인벤토리/DB 부수효과는
/// <see cref="User.User"/>에 위임한다. 여기서는 검증·조립·응답만 담당한다.
/// </summary>
public sealed class GachaService : Singleton<GachaService>
{
    // 허용하는 뽑기 횟수(단차 / 10연차)
    private const int SingleDraw = 1;
    private const int MultiDraw = 10;

    public void Draw(User.User user, int gachaId, int drawCount)
    {
        // 1) 검증: 뽑기 횟수와 풀 존재 여부
        if ((drawCount != SingleDraw && drawCount != MultiDraw)
            || !GachaTable.TryGetPool(gachaId, out _))
        {
            user.Send(new S_GachaDrawResponse { Success = false });
            return;
        }

        // 2) 순수 추첨(뽑힌 순서대로)
        var entries = GachaTable.Draw(gachaId, drawCount);

        // 3) 연출용 결과: 뽑힌 순서/개별 항목 그대로
        var rewards = new List<GachaRewardInfo>(entries.Count);
        foreach (var entry in entries)
            rewards.Add(new GachaRewardInfo
            {
                ItemId = entry.ItemId,
                Count = entry.Count,
                Rarity = entry.Rarity,
            });

        // 4) 인벤토리 반영: itemId별 수량을 합산해 아이템당 한 번만 갱신(UPSERT 최소화)
        var gained = new Dictionary<int, int>();
        foreach (var entry in entries)
            gained[entry.ItemId] = gained.GetValueOrDefault(entry.ItemId) + entry.Count;

        foreach (var (itemId, count) in gained)
            user.GainItem(itemId, count);

        // 5) 뽑기 결과 응답(인벤토리 갱신 패킷은 별도로 보내지 않음)
        user.Send(new S_GachaDrawResponse { Success = true, Rewards = rewards });
    }
}
