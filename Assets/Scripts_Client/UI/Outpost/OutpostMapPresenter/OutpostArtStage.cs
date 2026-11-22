// 마을 그림 단계 — 레벨이 어느 그림을 쓰는지 정한다 (2026-11-22 사용자 — 건물 5단계 · 마을 바탕 성장).
//
// 보이는 그림만 바꾼다 — 능력치·비용과 상관없다(게임 수치가 아니라 그림 규칙이다).
// 그림은 'Assets/Art/outpost/' — 생성기 '_source/environment/claude-outpost/make_outpost.py'가 같은 번호로 뽑는다.
public static class OutpostArtStage
{
    // 건물 단계 — 0 빈 터(Lv0) · 1 천막 · 2 오두막 · 3 집 · 4 큰 건물 · 5 완성. 값 = 그 단계가 시작하는 레벨
    private static readonly int[] TierFrom = { 1, 20, 50, 90, 130 };

    // 마을 단계 — 건물 레벨 합(0~750). 1 빈터 마을 · 2 말뚝 울타리 · 3 돌길·분수·풍차 · 4 돌담·성문 · 5 망루·꽃밭
    private static readonly int[] StageFrom = { 50, 200, 400, 600 };

    public const int TierCount  = 6;  // 빈 터 포함
    public const int StageCount = 5;

    // 건물 레벨 → 그림 번호 0~5
    public static int BuildingTier(int level) => CountReached(TierFrom, level);

    // 건물 레벨 합 → 바탕 번호 0~4 (그림 파일 이름은 1~5)
    public static int VillageStage(int totalLevel) => CountReached(StageFrom, totalLevel);

    private static int CountReached(int[] from, int value)
    {
        int reached = 0;

        foreach (int start in from)
        {
            if (value >= start)
            {
                reached++;
            }
        }

        return reached;
    }
}
