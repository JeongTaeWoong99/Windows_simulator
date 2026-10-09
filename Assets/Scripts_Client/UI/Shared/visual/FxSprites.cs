using UnityEngine;

// 연출이 쓰는 이펙트 그림의 이름표 — 어떤 연출이 어떤 그림 파일을 쓰는지 한 곳에 모은다.
// 그림은 그림 저장소 'Assets/Art/fx/<연출 키>/<이름>.png'에 있고 목록('VisualCatalog.FxOf')으로 찾는다.
// 흰색 + 투명도만 담은 그림이다 — 색은 'Image.color'로 입힌다(등급색 등). 등급마다 그림을 따로 두지 않는다.
//
// ■ 그림을 바꾸려면
//   같은 파일 이름으로 덮는다 — 코드는 고치지 않는다. 새 부위를 더하면 여기에 이름을 하나 늘린다.
//   지금 그림은 임시다 — 'Art/_source/fx-placeholder/make_fx.py'가 구웠다(모양을 다듬으려면 그 스크립트를 고쳐 다시 굽는다).
//
// ■ 그림이 없으면 그 효과만 빠진다
//   씬·프리팹이 그림을 직접 가리키면 그림 저장소를 안 받은 사람 쪽에서 참조가 끊기므로('Art 규칙.md') 목록으로만 찾는다.
//   목록이 없거나(그림 저장소 없음) 그 그림이 빠졌으면 null — 쓰는 쪽은 그 Image를 켜지 않는다(빈 Image는 흰 네모로 그려진다).
//   ※ 찾을 때마다 목록을 본다(캐시하지 않는다) — 목록의 사전 조회라 가볍고, 플레이 중 그림을 바꿔도 다음 연출부터 따라온다.
public static class FxSprites
{
    // ── reveal — 가챠·상자·우편 결과 하나씩 공개 ('RewardRevealFx' · 'RewardBurstFx') ──

    // 가운데가 밝고 가장자리로 사라지는 둥근 번짐 — 칸 뒤 빛 · 폭발 번짐
    public static Sprite? RevealGlow    => VisualCatalog.FxOf("reveal_glow");
    // 가운데에서 뻗는 광선 12줄 — 폭발할 때 돌면서 퍼진다
    public static Sprite? RevealRays    => VisualCatalog.FxOf("reveal_rays");
    // 네 갈래 별 조각 — 폭발 때 흩어지는 '파티클' 한 알
    public static Sprite? RevealSparkle => VisualCatalog.FxOf("reveal_sparkle");
    // 가운데가 밝은 세로 띠 — 칸 안을 사선으로 훑는 빛 줄
    public static Sprite? RevealShine   => VisualCatalog.FxOf("reveal_shine");
    // 각도에 따라 밝기가 도는 원뿔 — 테두리 띠 마스크 안에서 돌려 테두리를 따라 빛이 흐르게 한다
    public static Sprite? RevealConic   => VisualCatalog.FxOf("reveal_conic");
    // 네모 테두리 띠 — 'Mask' 모양으로만 쓴다. ⚠️ 9-slice 경계(Border)를 띠 바로 안쪽으로 둬야 띠가 늘어나지 않는다
    public static Sprite? RevealFrame   => VisualCatalog.FxOf("reveal_frame");

    // ── gain — 아이템 획득 아이콘 뒤 등급 빛 ('ItemGainFx') ──

    // 번짐 — 공개 연출 것보다 속이 진하다(작은 칸에서도 보이게). 섬광도 이것을 흰색으로 쓴다
    public static Sprite? GainGlow    => VisualCatalog.FxOf("gain_glow");
    // 광선 — 희귀↑
    public static Sprite? GainRays    => VisualCatalog.FxOf("gain_rays");
    // 별 조각 · 반짝이 — 전설·신화
    public static Sprite? GainSparkle => VisualCatalog.FxOf("gain_sparkle");
    // 세로 빛 기둥 (1:2 그림) — 전설·신화 'Pillar'
    public static Sprite? GainPillar  => VisualCatalog.FxOf("gain_pillar");
    // 얇은 고리 — 전설·신화 'DoubleRays'
    public static Sprite? GainRing    => VisualCatalog.FxOf("gain_ring");
}
