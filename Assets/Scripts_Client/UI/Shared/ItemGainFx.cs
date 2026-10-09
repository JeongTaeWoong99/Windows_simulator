using System.Collections.Generic;
using DG.Tweening;
using GameData;
using UnityEngine;
using UnityEngine.UI;

// 획득 연출의 아이콘 한 개 묶음 — 아이콘 + 그 뒤·앞에 까는 등급 빛.
// 'ItemGainEffectView'가 처음 재생할 때 원본을 코드로 만들고('CreateTemplate') 풀로 돌려쓴다.
// 묶음 전체가 떠오르고 가로로 따라가므로, 빛은 묶음 안에서 제자리 움직임만 한다.
//
// ■ 그리는 순서 = 자식 순서
//   빛 기둥 · 번짐 · 광선 · 겹광선 · 고리 → 아이콘 → 섬광 · 별 조각 · 반짝이
//   뒤에 깔 것은 아이콘보다 앞 자식, 덮을 것(섬광·별)은 뒤 자식이다.
//
// ■ 단계별 빛은 'ItemGainGlowSettings' 머리말 참고. 그림은 'FxSprites'의 gain_* (그림 저장소 'Art/fx/gain/') — 없는 그림의 효과는 켜지 않는다.
public class ItemGainFx : MonoBehaviour
{
    // 번짐이 다 커지는 때 · 광선이 다 나오는 때 (재생 시간 대비)
    private const float GlowInRatio = 0.2f;
    private const float RaysInRatio = 0.25f;

    // 번짐이 흐려지기 시작하는 때 (재생 시간 대비)
    private const float GlowFadeStart = 0.6f;

    // 광선 진하기
    private const float RaysAlpha = 1f;

    // 섬광 · 고리 · 별 조각이 사는 시간 (재생 시간 대비) — 나타나는 순간만 반짝인다
    private const float FlashRatio = 0.3f;
    private const float RingRatio  = 0.5f;
    private const float SparkRatio = 0.55f;

    // 빛 색을 등급색에서 흰색 쪽으로 당기는 정도 — 어두운 등급색(희귀 파랑·영웅 보라)도 어두운 바탕에서 빛으로 보이게
    private const float GlowWhiten = 0.2f;

    // 별 조각 · 반짝이 색을 흰색 쪽으로 더 당기는 정도 — 반짝이로 보이게
    private const float SparkWhiten = 0.4f;

    // 반짝이 자리 (아이콘 가운데 기준, 아이콘 대비) 와 크기 — z가 크기
    private static readonly Vector3[] TwinkleSpots =
    {
        new Vector3(-0.75f,  0.5f, 0.5f),
        new Vector3( 0.8f,   0.1f, 0.38f),
        new Vector3(-0.3f,  -0.7f, 0.3f),
    };

    [SerializeField] private Image       pillar      = null!;
    [SerializeField] private Image       glow        = null!;
    [SerializeField] private Image       rays        = null!;
    [SerializeField] private Image       secondRays  = null!;
    [SerializeField] private Image       ring        = null!;
    [SerializeField] private Image       icon        = null!;
    [SerializeField] private Image       flash       = null!;
    [SerializeField] private List<Image> sparks      = new List<Image>();
    [SerializeField] private List<Image> twinkles    = new List<Image>();

    // 아이콘 — 크기·페이드는 'ItemGainEffectView'가 움직인다
    public Image Icon => icon;

    // 묶음 원본을 'parent' 아래에 만든다 — 빛 그림은 모두 꺼 둔다 (ItemGainEffectView.EnsureBuilt에서 한 번)
    public static ItemGainFx CreateTemplate(Transform parent, ItemGainGlowSettings settings)
    {
        RectTransform root = CreateRect("Gain", parent);
        var           fx   = root.gameObject.AddComponent<ItemGainFx>();

        fx.pillar     = CreateImage("Pillar", root, FxSprites.GainPillar);
        fx.glow       = CreateImage("Glow", root, FxSprites.GainGlow);
        fx.rays       = CreateImage("Rays", root, FxSprites.GainRays);
        fx.secondRays = CreateImage("Second Rays", root, FxSprites.GainRays);
        fx.ring       = CreateImage("Ring", root, FxSprites.GainRing);
        fx.icon       = CreateImage("Icon", root, null);
        fx.flash      = CreateImage("Flash", root, FxSprites.GainGlow);

        fx.icon.preserveAspect = true;

        for (int i = 0; i < settings.SparkCount; i++)
        {
            fx.sparks.Add(CreateImage("Spark", root, FxSprites.GainSparkle));
        }

        for (int i = 0; i < TwinkleSpots.Length; i++)
        {
            fx.twinkles.Add(CreateImage("Twinkle", root, FxSprites.GainSparkle));
        }

        return fx;
    }

    // 등급 빛을 'seq'에 넣는다 — 모든 빛은 0초에 함께 시작한다. 아이콘 그림·크기도 여기서 맞춘다.
    // 아이콘이 나타날 때 튈 크기 배율을 돌려준다 (전설·신화만 1보다 크다).
    //   size     : 아이콘 한 변 (px)
    //   duration : 묶음이 떠올라 사라질 때까지 (초)
    public float Build(Sequence seq, Sprite sprite, GlobalRarity rarity, float size, float duration, ItemGainGlowSettings settings)
    {
        HideAll();

        icon.enabled                 = true;
        icon.sprite                  = sprite;
        icon.color                   = Color.white;
        icon.rectTransform.sizeDelta = new Vector2(size, size);

        int   tier  = ItemGainGlowSettings.TierOf(rarity);
        Color color = Color.Lerp(RarityPalette.Get(rarity), Color.white, GlowWhiten);

        AddGlow(seq, color, size * (tier == 2 ? settings.TopGlowScale : settings.GlowScale), tier == 1 ? settings.MidGlowAlpha : 1f, duration);

        if (tier == 0)
        {
            return 1f;
        }

        AddRays(seq, rays, color, size * (tier == 2 ? settings.TopRaysScale : settings.RaysScale), settings.RaysTurn, duration);

        if (tier == 1)
        {
            return 1f;
        }

        AddFlash(seq, size * settings.FlashScale, duration);

        if (settings.Top == ItemGainGlowSettings.TopStyle.Pillar)
        {
            AddPillar(seq, color, size * settings.PillarScale, duration);
            AddSparks(seq, color, size, duration, settings);
        }
        else
        {
            AddRays(seq, secondRays, Color.white, size * settings.SecondRaysScale, -settings.RaysTurn, duration);
            AddRing(seq, color, size, settings.RingEndScale, duration);
            AddTwinkles(seq, color, size, duration);
        }

        return settings.TopPop;
    }

    // 빛 그림을 모두 끈다 — 풀에서 다시 꺼낼 때 지난 등급의 빛이 남지 않게 (Build 첫머리 · 꺼진 채 반납을 미룰 때)
    public void HideAll()
    {
        pillar.enabled     = false;
        glow.enabled       = false;
        rays.enabled       = false;
        secondRays.enabled = false;
        ring.enabled       = false;
        flash.enabled      = false;

        foreach (Image spark in sparks)
        {
            spark.enabled = false;
        }

        foreach (Image twinkle in twinkles)
        {
            twinkle.enabled = false;
        }
    }

    // 번짐 — 작게 나와 커지며 진해지고, 절반 뒤부터 흐려진다
    private void AddGlow(Sequence seq, Color color, float diameter, float alpha, float duration)
    {
        Image         image = glow;
        RectTransform rect  = Place(image, WithAlpha(color, 0f), diameter);

        rect.localScale = Vector3.one * 0.4f;

        seq.Insert(0f, rect.DOScale(1f, duration * GlowInRatio).SetEase(Ease.OutQuad));
        seq.Insert(duration * GlowInRatio, rect.DOScale(1.15f, duration * (1f - GlowInRatio)));
        seq.Insert(0f, image.DOFade(alpha, duration * GlowInRatio));
        seq.Insert(duration * GlowFadeStart, image.DOFade(0f, duration * (1f - GlowFadeStart)).SetEase(Ease.InQuad));
    }

    // 광선 — 작게 나와 퍼지며 돈다. 'turn'이 음수면 반대로 돈다
    private void AddRays(Sequence seq, Image image, Color color, float diameter, float turn, float duration)
    {
        RectTransform rect = Place(image, WithAlpha(color, 0f), diameter);

        rect.localScale = Vector3.one * 0.3f;

        seq.Insert(0f, rect.DOScale(1f, duration * RaysInRatio).SetEase(Ease.OutCubic));
        seq.Insert(duration * RaysInRatio, rect.DOScale(1.1f, duration * (1f - RaysInRatio)));
        seq.Insert(0f, rect.DOLocalRotate(new Vector3(0f, 0f, turn), duration, RotateMode.FastBeyond360).SetEase(Ease.Linear));
        seq.Insert(0f, image.DOFade(RaysAlpha, duration * RaysInRatio));
        seq.Insert(duration * RaysInRatio, image.DOFade(0f, duration * (1f - RaysInRatio)).SetEase(Ease.InQuad));
    }

    // 흰 섬광 — 나타나는 순간 아이콘 위를 덮었다가 퍼지며 사라진다
    private void AddFlash(Sequence seq, float diameter, float duration)
    {
        RectTransform rect = Place(flash, Color.white, diameter);
        float         time = duration * FlashRatio;

        rect.localScale = Vector3.one * 0.5f;

        seq.Insert(0f, rect.DOScale(1.4f, time).SetEase(Ease.OutQuad));
        seq.Insert(0f, flash.DOFade(0f, time).SetEase(Ease.InQuad));
    }

    // 빛 기둥 — 아래에서 납작하게 솟아올라 서고, 끝으로 가며 가늘어지며 사라진다
    private void AddPillar(Sequence seq, Color color, Vector2 size, float duration)
    {
        RectTransform rect = Place(pillar, WithAlpha(color, 0f), 0f);

        rect.sizeDelta  = size;
        rect.localScale = new Vector3(1.4f, 0.2f, 1f);

        seq.Insert(0f, rect.DOScale(Vector3.one, duration * RaysInRatio).SetEase(Ease.OutCubic));
        seq.Insert(duration * RaysInRatio, rect.DOScaleX(0.5f, duration * (1f - RaysInRatio)).SetEase(Ease.InQuad));
        seq.Insert(0f, pillar.DOFade(0.9f, duration * RaysInRatio));
        seq.Insert(duration * GlowFadeStart, pillar.DOFade(0f, duration * (1f - GlowFadeStart)).SetEase(Ease.InQuad));
    }

    // 별 조각 — 고르게 나눈 방향으로 튀어 나가며 작아진다
    private void AddSparks(Sequence seq, Color color, float size, float duration, ItemGainGlowSettings settings)
    {
        Color sparkColor = Color.Lerp(color, Color.white, SparkWhiten);
        float time       = duration * SparkRatio;

        for (int i = 0; i < sparks.Count; i++)
        {
            Image         spark = sparks[i];
            RectTransform rect  = Place(spark, sparkColor, size * settings.SparkScale);

            float   angle    = ((float)i / sparks.Count + Random.Range(-0.03f, 0.03f)) * Mathf.PI * 2f;
            float   distance = size * Random.Range(settings.SparkDistance.x, settings.SparkDistance.y);
            Vector2 target   = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;

            rect.localScale = Vector3.one * 1.1f;

            seq.Insert(0f, rect.DOAnchorPos(target, time).SetEase(Ease.OutCubic));
            seq.Insert(0f, rect.DOScale(0f, time).SetEase(Ease.InQuad));
            seq.Insert(0f, rect.DOLocalRotate(new Vector3(0f, 0f, 90f), time, RotateMode.FastBeyond360));
        }
    }

    // 고리 — 아이콘 크기에서 퍼져 나가며 흐려진다
    private void AddRing(Sequence seq, Color color, float size, float endScale, float duration)
    {
        RectTransform rect = Place(ring, color, size);
        float         time = duration * RingRatio;

        rect.localScale = Vector3.one * 0.6f;

        seq.Insert(0f, rect.DOScale(endScale, time).SetEase(Ease.OutCubic));
        seq.Insert(0f, ring.DOFade(0f, time).SetEase(Ease.InQuad));
    }

    // 반짝이 — 아이콘 둘레 고정 자리에서 하나씩 커졌다 작아진다
    private void AddTwinkles(Sequence seq, Color color, float size, float duration)
    {
        Color sparkColor = Color.Lerp(color, Color.white, SparkWhiten);

        for (int i = 0; i < twinkles.Count; i++)
        {
            Image         twinkle = twinkles[i];
            Vector3       spot    = TwinkleSpots[i];
            RectTransform rect    = Place(twinkle, sparkColor, size * spot.z);

            // 차례로 — 앞의 것이 커지는 동안 다음 것이 따라 나온다
            float peak = duration * (0.25f + i * 0.1f);
            float end  = duration * (0.6f + i * 0.1f);

            rect.anchoredPosition = new Vector2(spot.x, spot.y) * size;
            rect.localScale       = Vector3.zero;

            seq.Insert(0f, rect.DOScale(1.2f, peak).SetEase(Ease.OutQuad));
            seq.Insert(peak, rect.DOScale(0f, end - peak).SetEase(Ease.InQuad));
            seq.Insert(0f, rect.DOLocalRotate(new Vector3(0f, 0f, 90f), end, RotateMode.FastBeyond360));
        }
    }

    // 빛 그림 하나를 가운데에 켠다 — 지름 0이면 크기는 부르는 쪽이 정한다
    private static RectTransform Place(Image image, Color color, float diameter)
    {
        RectTransform rect = image.rectTransform;

        image.enabled         = image.sprite != null; // 그림이 없으면 그 효과만 빠진다 — 빈 Image는 흰 네모로 그려진다
        image.color           = color;
        rect.anchoredPosition = Vector2.zero;
        rect.localRotation    = Quaternion.identity;
        rect.localScale       = Vector3.one;

        if (diameter > 0f)
        {
            rect.sizeDelta = new Vector2(diameter, diameter);
        }

        return rect;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;

        return color;
    }

    // 묶음 가운데 기준의 Image를 만든다 — raycast는 끄고 그림은 꺼 둔다
    private static Image CreateImage(string name, RectTransform parent, Sprite? sprite)
    {
        RectTransform rect  = CreateRect(name, parent);
        Image         image = rect.gameObject.AddComponent<Image>();

        image.sprite        = sprite;
        image.raycastTarget = false;
        image.enabled       = false;

        return image;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));

        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);

        var rect = (RectTransform)go.transform;

        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = Vector2.zero;

        return rect;
    }
}
