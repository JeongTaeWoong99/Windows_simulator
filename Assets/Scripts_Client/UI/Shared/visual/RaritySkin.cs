using GameData;
using UnityEngine.UI;

// 등급 바탕 한 벌 — 입체 테두리 + 아래 계단빛 + 어두운 속('rarity_bg')에 등급색을 입힌다.
// 속은 검정 반투명이라 등급색을 곱해도 어느 등급이든 같은 어둠이다 — 등급은 테두리와 아래 빛이 말한다(2026-11-12).
//
// ■ 왜 한 곳인가
// 인벤토리 칸 · 목록 아이콘 · 작업 슬롯 · 위젯 칸 · 배치 화면 줄이 모두 등급 바탕을 깐다.
// 화면마다 따로 칠하면 한쪽은 그림, 한쪽은 단색으로 남는다(2026-11-11 — 상점 뽑기권·우편 아이콘이 단색이었다).
//
// ■ 귀퉁이 장식은 두지 않는다 (2026-11-11 사용자)
// 쇠·금 꺾쇠를 자식으로 얹었다가 뺐다 — 보기에 거슬렸고, 'LayoutGroup' 루트(작업슬롯)에서는
// 자식이 한 줄을 차지해 위에 빈 줄이 생겼다. 바탕에 무엇을 더 얹을 때는 레이아웃 자식이 되지 않게 한다.
//
// ■ 쓰는 법
//   Awake에서  RaritySkin.Dress(background);          — 그림을 깐다(한 번)
//   그릴 때    RaritySkin.Set(background, rarity);    — 등급색
// 그림 저장소가 없으면 'Dress'가 아무것도 하지 않고 바탕은 예전처럼 단색이다.
public static class RaritySkin
{
    // 그림 한 칸 = 화면 2칸 — 'UiSkin'과 같은 픽셀 크기
    public const float PixelScale = 0.5f;

    // 바탕에 등급 그림을 깐다. 두 번 불러도 같다(풀 재사용).
    //   background : 등급색을 입힐 Image (칸·줄의 바탕)
    public static void Dress(Image background)
    {
        var plate = FxSprites.RarityBackground;

        if (plate == null)
        {
            return;
        }

        background.sprite                  = plate;
        background.type                    = Image.Type.Sliced;
        background.fillCenter              = true;
        background.pixelsPerUnitMultiplier = PixelScale;
    }

    // 등급색을 입힌다.
    public static void Set(Image background, GlobalRarity rarity)
    {
        background.color = RarityPalette.Get(rarity);
    }
}
