using GameData;
using UnityEngine;
using UnityEngine.UI;

// 등급 바탕 한 벌 — 어두운 판 + 등급색 테두리('rarity_bg') 위에 쇠·금 귀퉁이 장식('rarity_frame')을 얹는다.
//
// ■ 왜 한 곳인가
// 인벤토리 칸 · 목록 아이콘 · 작업 슬롯 · 위젯 칸 · 배치 화면 줄이 모두 등급 바탕을 깐다.
// 화면마다 따로 칠하면 한쪽은 그림, 한쪽은 단색으로 남는다(2026-11-11 — 상점 뽑기권·우편 아이콘이 단색이었다).
//
// ■ 쓰는 법
//   Awake에서  _frame = RaritySkin.Dress(background);   — 그림을 깔고 장식 자식을 붙인다(한 번)
//   그릴 때    RaritySkin.Set(background, _frame, rarity); — 등급색 + 장식(영웅 이상은 금)
// 그림 저장소가 없으면 'Dress'가 아무것도 하지 않고 바탕은 예전처럼 단색이다.
public static class RaritySkin
{
    // 그림 한 칸 = 화면 2칸 — 'UiSkin'과 같은 픽셀 크기
    public const float PixelScale = 0.5f;

    private const string FrameName = "Rarity Frame";

    // 바탕에 등급 그림을 깔고, 귀퉁이 장식을 첫 자식으로 붙인다 — 장식은 바탕 바로 위 · 아이콘 아래에 그려진다.
    // 이미 붙어 있으면 그것을 돌려준다(풀 재사용 · 두 번 불러도 하나).
    //   background : 등급색을 입힐 Image (칸·줄의 바탕)
    //   반환       : 장식 Image — 그림이 없으면 null
    public static Image? Dress(Image background)
    {
        Sprite? plate = FxSprites.RarityBackground;

        if (plate == null)
        {
            return null;
        }

        background.sprite                  = plate;
        background.type                    = Image.Type.Sliced;
        background.fillCenter              = true;
        background.pixelsPerUnitMultiplier = PixelScale;

        Transform? found = background.transform.Find(FrameName);

        if (found != null)
        {
            return found.GetComponent<Image>();
        }

        Sprite? frame = FxSprites.RarityFrame;

        if (frame == null)
        {
            return null;
        }

        var go   = new GameObject(FrameName, typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)go.transform;

        go.layer = background.gameObject.layer;
        rect.SetParent(background.transform, false);
        rect.SetAsFirstSibling();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var image = go.GetComponent<Image>();
        image.sprite                  = frame;
        image.type                    = Image.Type.Sliced;
        image.fillCenter              = false;
        image.raycastTarget           = false;
        image.pixelsPerUnitMultiplier = PixelScale;

        return image;
    }

    // 등급을 입힌다 — 바탕은 등급색, 장식은 영웅 이상이면 금 · 그 아래는 쇠.
    public static void Set(Image background, Image? frame, GlobalRarity rarity)
    {
        background.color = RarityPalette.Get(rarity);

        SetFrame(frame, rarity);
    }

    // 장식만 갈아 낀다 — 바탕색을 직접 칠하는 칸('SlotView'의 딤·흑백)이 쓴다. 금 그림이 없으면 쇠 그대로.
    public static void SetFrame(Image? frame, GlobalRarity rarity)
    {
        if (frame == null)
        {
            return;
        }

        Sprite? sprite = rarity >= GlobalRarity.Epic ? FxSprites.RarityFrameGold ?? FxSprites.RarityFrame : FxSprites.RarityFrame;

        if (sprite != null)
        {
            frame.sprite = sprite;
        }
    }
}
