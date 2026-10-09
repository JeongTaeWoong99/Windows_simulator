using System;
using GameData;
using UnityEngine;

// 아이템 획득 연출에서 아이콘 뒤에 까는 빛의 조정값 — 'ItemGainEffectView' 인스펙터의 'glow' 칸.
// 크기는 모두 **아이콘 한 변에 대한 배율**이다 — 큰 창(27px)과 위젯(18px)이 같은 값으로 같은 모양이 된다.
//
// ■ 등급 3단계 — 흔한 것은 조용히, 좋은 것만 크게
//   채취는 칸마다 몇 초에 한 번씩 계속 나오므로 매번 화려하면 피곤하다.
//   일반·고급 = 번짐 · 희귀·영웅 = 번짐 + 광선 · 전설·신화 = 섬광 + 꼭대기 모양('TopStyle') + 아이콘 튐
[Serializable]
public class ItemGainGlowSettings
{
    // 전설·신화에 얹는 빛 — 희귀·영웅과 **다른 종류의 빛**이라 크기만 키운 것보다 갈라져 보인다
    public enum TopStyle
    {
        Pillar,     // 아래에서 솟는 세로 빛 기둥 + 큰 광선 + 별 조각
        DoubleRays, // 거꾸로 도는 흰 광선 한 겹 더 + 퍼지는 고리 + 둘레에 남는 반짝이
    }

    [CenterHeader("번짐 (모든 등급)")]
    [Tooltip("번짐 지름 — 아이콘 대비")]
    public float GlowScale = 2.6f;

    [Tooltip("희귀·영웅의 번짐 진하기 — 광선과 겹치므로 조금 옅게")]
    [Range(0f, 1f)]
    public float MidGlowAlpha = 0.9f;

    [CenterHeader("광선 (희귀↑)")]
    [Tooltip("희귀·영웅의 광선 지름 — 아이콘 대비")]
    public float RaysScale = 3f;

    [Tooltip("광선이 사는 동안 도는 각도 (도)")]
    public float RaysTurn = 60f;

    [CenterHeader("꼭대기 (전설·신화)")]
    [Tooltip("전설·신화에 얹는 빛 모양")]
    public TopStyle Top = TopStyle.Pillar;

    [Tooltip("전설·신화의 번짐 지름 — 아이콘 대비")]
    public float TopGlowScale = 3.2f;

    [Tooltip("전설·신화의 광선 지름 — 아이콘 대비")]
    public float TopRaysScale = 4.2f;

    [Tooltip("나타날 때 아이콘이 튀는 크기 — 이 배율까지 커졌다가 제 크기로 돌아온다")]
    [Min(1f)]
    public float TopPop = 1.3f;

    [Tooltip("나타나는 순간 흰 섬광 지름 — 아이콘 대비")]
    public float FlashScale = 2.6f;

    [Tooltip("빛 기둥 크기 (가로, 세로) — 아이콘 대비 (Pillar)")]
    public Vector2 PillarScale = new Vector2(1.4f, 3.6f);

    [Tooltip("튀는 별 조각 수 (Pillar)")]
    [Min(0)]
    public int SparkCount = 7;

    [Tooltip("별 조각 한 변 — 아이콘 대비 (Pillar)")]
    public float SparkScale = 0.6f;

    [Tooltip("별 조각이 날아가는 거리 (최소, 최대) — 아이콘 대비 (Pillar)")]
    public Vector2 SparkDistance = new Vector2(1f, 1.6f);

    [Tooltip("겹광선(흰색) 지름 — 아이콘 대비 (DoubleRays)")]
    public float SecondRaysScale = 3.2f;

    [Tooltip("고리가 퍼지는 끝 크기 — 아이콘 대비 (DoubleRays)")]
    public float RingEndScale = 2.8f;

    // 등급 → 단계 (0 일반·고급 · 1 희귀·영웅 · 2 전설·신화). 모르는 등급은 0
    public static int TierOf(GlobalRarity rarity) => rarity switch
    {
        GlobalRarity.Rare or GlobalRarity.Epic           => 1,
        GlobalRarity.Legendary or GlobalRarity.Mythic    => 2,
        _                                                => 0,
    };
}
