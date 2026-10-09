using System;
using DG.Tweening;
using GameData;
using UnityEngine;

// 보상 공개 연출의 조정값 — 'GachaResultPresenter'의 인스펙터에서 고친다.
// 칸 연출('RewardRevealFx')과 폭발('RewardBurstFx')은 코드가 만들어 붙이므로 인스펙터가 없다 — 값은 전부 여기서 받는다.
//
// ■ 연출의 두 갈래 (2026-10-09 목업 A + D)
//   보통(영웅 미만) — 크게 튀어나왔다 제 크기로 줄며 번쩍 · 빛 줄. 짧은 간격으로 차례차례 (A)
//   고등급(영웅↑)  — 잠깐 멈춤 → 등급색 덮개가 떨며 차오름 → 빛 폭발 · 별 조각 → 더 크게 튀어나옴 (D)
//                    공개 뒤 테두리를 따라 빛이 돈다. 전설↑은 차오름이 길고 창이 흔들리며, 칸 뒤 번짐이 숨쉰다
//   ※ 고등급 연출은 뒤 칸을 막지 않는다 — 차오르는 동안에도 다음 칸이 'Gap' 간격으로 계속 나온다
[Serializable]
public class RewardRevealSettings
{
    [CenterHeader("순서")]
    [Min(0f), Tooltip("창이 뜬 뒤 첫 칸이 나오기까지 (초)")]
    public float StartDelay = 0.1f;

    [Min(0f), Tooltip("보통 칸 사이 간격 (초) — 10연차면 이것 × 10이 대략의 길이다")]
    public float Gap = 0.09f;

    [CenterHeader("튀어나오기 — 보통")]
    [Min(1f), Tooltip("나타나는 순간의 크기 배율 — 여기서 1로 줄어든다")]
    public float PopScale = 1.5f;

    [Min(0.01f), Tooltip("제 크기가 되기까지 (초)")]
    public float PopDuration = 0.25f;

    [Tooltip("줄어드는 곡선 — OutBack이면 1보다 살짝 작아졌다 돌아온다")]
    public Ease PopEase = Ease.OutBack;

    [Range(0f, 1f), Tooltip("나타나는 순간 흰 번쩍의 진하기")]
    public float FlashAlpha = 0.8f;

    [Min(0.01f), Tooltip("번쩍이 사라지기까지 (초)")]
    public float FlashDuration = 0.2f;

    [Min(0f), Tooltip("빛 줄이 지나가기 시작하는 때 (초, 나타난 뒤)")]
    public float ShineDelay = 0.06f;

    [Min(0.01f), Tooltip("빛 줄이 칸을 가로지르는 시간 (초)")]
    public float ShineDuration = 0.35f;

    [CenterHeader("고등급 — 영웅↑")]
    [Min(0f), Tooltip("고등급 칸이 차오르기 시작하기까지 (초) — 그동안에도 뒤 칸은 계속 나온다")]
    public float BigPause = 0.15f;

    [Min(0.01f), Tooltip("영웅 — 덮개가 차오르는 시간 (초)")]
    public float EpicChargeDuration = 0.45f;

    [Min(0.01f), Tooltip("전설↑ — 덮개가 차오르는 시간 (초)")]
    public float TopChargeDuration = 0.7f;

    [Min(1f), Tooltip("차오르는 동안 커지는 배율")]
    public float ChargeScale = 1.1f;

    [Min(0f), Tooltip("차오르는 동안 떠는 각도 (도)")]
    public float ChargeShakeAngle = 6f;

    [Min(1f), Tooltip("고등급이 나타나는 순간의 크기 배율")]
    public float BigPopScale = 2f;

    [Min(0.01f), Tooltip("고등급이 제 크기가 되기까지 (초)")]
    public float BigPopDuration = 0.38f;

    [Min(0f), Tooltip("전설↑ — 창이 흔들리는 시간 (초)")]
    public float PanelShakeDuration = 0.3f;

    [Min(0f), Tooltip("전설↑ — 창이 흔들리는 세기 (px)")]
    public float PanelShakeStrength = 8f;

    [CenterHeader("폭발 — 광선 · 별 조각")]
    [Min(0.01f), Tooltip("폭발이 퍼져 사라지기까지 (초)")]
    public float BurstDuration = 0.7f;

    [Min(1f), Tooltip("광선 그림 한 변 (px) — 퍼지기 전 크기")]
    public float RaysSize = 180f;

    [Min(0.1f), Tooltip("광선이 퍼지는 최종 배율 (전설↑은 1.4배 더)")]
    public float RaysScale = 1.4f;

    [Min(0), Tooltip("영웅 — 흩어지는 별 조각 수")]
    public int SparkCount = 10;

    [Min(0), Tooltip("전설↑ — 흩어지는 별 조각 수")]
    public int TopSparkCount = 16;

    [Min(1f), Tooltip("별 조각 한 변 (px)")]
    public float SparkSize = 22f;

    [Tooltip("별 조각이 날아가는 거리 범위 (px) — x 최소 · y 최대")]
    public Vector2 SparkDistance = new Vector2(55f, 115f);

    [CenterHeader("공개 뒤 강조")]
    [Min(0f), Tooltip("칸 뒤 번짐이 칸 밖으로 번지는 너비 (px)")]
    public float GlowSpread = 16f;

    [Range(0f, 1f), Tooltip("번짐의 진하기 — 영웅은 이 값으로 고정, 전설↑은 여기서 아래 값까지 숨쉰다")]
    public float GlowAlpha = 0.9f;

    [Range(0f, 1f), Tooltip("전설↑ — 숨쉴 때 가장 옅은 진하기")]
    public float GlowPulseMin = 0.4f;

    [Min(0.05f), Tooltip("전설↑ — 한 번 옅어지는 데 걸리는 시간 (초)")]
    public float GlowPulseDuration = 0.8f;

    [Min(0f), Tooltip("영웅↑ — 도는 테두리가 칸 밖으로 나오는 너비 (px)")]
    public float FrameOutset = 2f;

    [Min(0.1f), Tooltip("영웅↑ — 테두리 빛이 한 바퀴 도는 시간 (초)")]
    public float FrameTurnDuration = 1.6f;

    // 고등급(차오름 · 폭발)으로 공개하는 등급인가 — 영웅↑
    public static bool IsBig(GlobalRarity rarity) => rarity >= GlobalRarity.Epic;

    // 최고 등급(창 흔들림 · 도는 테두리)인가 — 전설↑
    public static bool IsTop(GlobalRarity rarity) => rarity >= GlobalRarity.Legendary;
}
