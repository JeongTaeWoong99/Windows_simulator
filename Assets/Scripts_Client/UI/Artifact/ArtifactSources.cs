using System.Collections.Generic;
using System.Text;
using GameData;
using MikaProtocol;

// 아티팩트를 얻는 곳과 확률 — 도감 상세의 '얻는 곳' (T-150 · 이슈 #135).
//
// ■ 서버와 같은 규칙으로 표에서 계산한다 — 표가 바뀌면 따라간다
//   유물 감정 · 상자: 'OpenGachaId' 풀에서 1회 추첨. 한 풀은 아이템 · 캐릭터 · 장비 · 골드 · 아티팩트 표의
//                    같은 GachaId 행을 모두 섞은 것이다(서버 'GachaPoolCatalog') → 확률 = 이 행 가중치 / 풀 가중치 합
//   레이드 상자:     'RaidBossTable.ArtifactChancePermille' (상자 1개당 ‰)
//   채취:           'GatherArtifactTable.ChancePerMillion' (그 산업 · 레벨의 판정 1회당)
// ■ 'GachaInfoTable'의 골드 뽑기처럼 여는 아이템이 없는 풀은 지금 얻을 길이 없어 뺀다.
public static class ArtifactSources
{
    private static Dictionary<int, long>? _poolWeights;   // GachaId → 풀 가중치 합 (처음 물을 때 한 번 — 표는 적재 후 불변)

    // 얻는 곳 여러 줄 — "흙 묻은 유물 감정 …… 0.84%". 없으면 빈 문자열.
    public static string Build(int artifactTid)
    {
        var text = new StringBuilder();

        foreach (var row in GameTable.GachaArtifactTable.All)
        {
            if (row.ArtifactTID != artifactTid || !PoolWeights().TryGetValue(row.GachaId, out long total) || total <= 0)
            {
                continue;
            }

            foreach (var item in GameTable.ItemTable.All)
            {
                if (item.OpenGachaId != row.GachaId)
                {
                    continue;
                }

                string name = Loc.Get(item.NameLocKey);
                string where = item.ItemType == ItemType.Relic ? Lang.Format("UI.Artifact.SourceAppraise", name) : name;

                AppendLine(text, where, (double)row.Weight / total);
            }
        }

        foreach (var boss in GameTable.RaidBossTable.All)
        {
            if (boss.RewardArtifactTID == artifactTid && boss.ArtifactChancePermille > 0
                && GameTable.ItemTable.TryGet(boss.RewardBoxItemTID, out var box))
            {
                AppendLine(text, Loc.Get(box.NameLocKey), boss.ArtifactChancePermille / 1000.0);
            }
        }

        foreach (var gather in GameTable.GatherArtifactTable.All)
        {
            if (gather.ArtifactTID == artifactTid && gather.ChancePerMillion > 0)
            {
                string industry = IndustryLabel.Get((EIndustryType)(int)gather.IndustryType);

                AppendLine(text, Lang.Format("UI.Artifact.SourceGather", industry, gather.IndustryLevel), gather.ChancePerMillion / 1_000_000.0);
            }
        }

        return text.ToString().TrimEnd('\n');
    }

    // 한 줄 — 왼쪽 얻는 곳 · 오른쪽 확률 (판매 목록 · 툴팁과 같은 'UIRichText.Row')
    private static void AppendLine(StringBuilder text, string where, double chance)
    {
        text.Append(UIRichText.Row(where, UIRichText.Paint(Percent(chance), UIThemeRole.Highlight)));
    }

    // 확률 → 백분율. 작은 값도 0으로 보이지 않게 유효 숫자 두 자리까지 (0.84% · 0.0009% · 0.0001%)
    private static string Percent(double chance)
    {
        double percent = chance * 100.0;

        if (percent >= 1.0)
        {
            return $"{percent:0.#}%";
        }

        int decimals = 2;

        while (decimals < 6 && percent < System.Math.Pow(10, 1 - decimals))
        {
            decimals++;
        }

        return percent.ToString("0." + new string('#', decimals)) + "%";
    }

    private static Dictionary<int, long> PoolWeights()
    {
        if (_poolWeights != null)
        {
            return _poolWeights;
        }

        var weights = new Dictionary<int, long>();

        void Add(int gachaId, int weight) => weights[gachaId] = (weights.TryGetValue(gachaId, out long sum) ? sum : 0) + weight;

        foreach (var row in GameTable.GachaItemTable.All)      { Add(row.GachaId, row.Weight); }
        foreach (var row in GameTable.GachaCharacterTable.All) { Add(row.GachaId, row.Weight); }
        foreach (var row in GameTable.GachaEquipTable.All)     { Add(row.GachaId, row.Weight); }
        foreach (var row in GameTable.GachaGoldTable.All)      { Add(row.GachaId, row.Weight); }
        foreach (var row in GameTable.GachaArtifactTable.All)  { Add(row.GachaId, row.Weight); }

        return _poolWeights = weights;
    }
}
