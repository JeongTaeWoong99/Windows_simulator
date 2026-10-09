using System;
using UnityEngine;

// 보상 공개 연출이 쓰는 흰 도형 그림 — 번짐 · 광선 · 별 조각 · 빛 줄 · 원뿔 · 테두리 띠.
// 색은 그림에 넣지 않고 'Image.color'로 입힌다 — 등급마다 그림을 따로 두지 않는다.
//
// ■ 그림 파일이 먼저, 없으면 코드로 그린다
// 그림은 그림 저장소 'Assets/Art/fx/reveal/reveal_<부위>.png'에 있고 목록('VisualCatalog.FxOf')으로 찾는다.
// 씬·프리팹이 그림을 직접 가리키면 그림 저장소를 안 받은 사람 쪽에서 참조가 끊기므로('Art 규칙.md') 목록으로만 찾는다.
// 목록이 없거나(그림 저장소 없음) 그 그림이 빠졌으면 **같은 모양을 코드로 그려** 쓴다 — 연출은 어디서나 돈다.
// ※ 지금 'fx/reveal/'의 PNG는 이 코드 모양을 그대로 구운 임시 그림이다(2026-10-09). 다시 그려 같은 이름으로 덮으면 된다.
public static class RevealSprites
{
    // 그림 이름 — 'fx/reveal/<이름>.png'. 파일 이름이 곧 목록의 찾는 이름이다
    public const string GlowKey    = "reveal_glow";
    public const string RaysKey    = "reveal_rays";
    public const string SparkleKey = "reveal_sparkle";
    public const string ShineKey   = "reveal_shine";
    public const string ConicKey   = "reveal_conic";
    public const string FrameKey   = "reveal_frame";

    // 코드로 그릴 때 텍스처 한 변 (px). 연출은 크게 늘려 쓰므로 이 정도면 번짐이 계단지지 않는다
    private const int Size = 128;

    // 테두리 띠(마스크) — 작은 9-slice. 띠 두께와 자르는 경계 (px).
    // ⚠️ 그림 파일로 바꿔도 9-slice 경계(Sprite Editor의 Border)를 띠 바로 안쪽으로 정해야 한다 — 안 그러면 띠가 칸 크기만큼 늘어난다
    private const int FrameSize      = 32;
    private const int FrameThickness = 4;

    private static Sprite? _glow;
    private static Sprite? _rays;
    private static Sprite? _sparkle;
    private static Sprite? _shine;
    private static Sprite? _conic;
    private static Sprite? _frame;

    // 가운데가 밝고 가장자리로 갈수록 사라지는 둥근 번짐 — 칸 뒤 빛 · 폭발 번짐
    public static Sprite SoftGlow => Pick(ref _glow, GlowKey, () => Make(GlowKey, (x, y) =>
    {
        float fade = Mathf.Clamp01(1f - Radius(x, y));

        return fade * fade;
    }));

    // 가운데에서 뻗는 광선 12줄 — 폭발할 때 돌면서 퍼진다
    public static Sprite Rays => Pick(ref _rays, RaysKey, () => Make(RaysKey, (x, y) =>
    {
        const int RayCount = 12;

        float ray  = Mathf.Pow(Mathf.Max(0f, Mathf.Cos(Mathf.Atan2(y, x) * RayCount)), 6f);
        float fade = Mathf.Clamp01(1f - Radius(x, y));

        return ray * fade;
    }));

    // 네 갈래 별 조각 — 폭발 때 흩어지는 '파티클' 한 알
    public static Sprite Sparkle => Pick(ref _sparkle, SparkleKey, () => Make(SparkleKey, (x, y) =>
    {
        // √|x| + √|y| 가 1보다 작은 곳 = 네 끝이 뾰족한 별(astroid). 가운데에 작은 번짐을 더한다
        float star = Mathf.Clamp01(1f - (Mathf.Sqrt(Mathf.Abs(x)) + Mathf.Sqrt(Mathf.Abs(y))));
        float core = Mathf.Pow(Mathf.Clamp01(1f - Radius(x, y)), 4f);

        return Mathf.Clamp01(star * 2f + core * 0.6f);
    }));

    // 가운데가 밝은 세로 띠 — 칸 안을 사선으로 훑는 빛 줄
    public static Sprite ShineBand => Pick(ref _shine, ShineKey, () => Make(ShineKey, (x, y) =>
    {
        float band = Mathf.Clamp01(1f - Mathf.Abs(x));

        return band * band * 0.85f;
    }));

    // 각도에 따라 밝기가 도는 원뿔 그라데이션 — 밝은 호 두 개(하나는 약하게).
    // 테두리 띠 마스크 안에서 돌리면 테두리를 따라 빛이 흐르는 것처럼 보인다
    public static Sprite Conic => Pick(ref _conic, ConicKey, () => Make(ConicKey, (x, y) =>
    {
        float angle = Mathf.Atan2(y, x);
        float main  = Mathf.Pow(Mathf.Max(0f, Mathf.Cos(angle)), 6f);
        float minor = Mathf.Pow(Mathf.Max(0f, -Mathf.Cos(angle)), 6f) * 0.5f;

        return Mathf.Clamp01(main + minor);
    }));

    // 네모 테두리 띠 — 9-slice라 칸 크기와 상관없이 띠 두께가 같다. 'Mask'의 모양으로만 쓴다
    public static Sprite Frame => Pick(ref _frame, FrameKey, MakeFrame);

    // 목록의 그림을 쓰고, 없으면 코드로 그린다 — 한 번 정하면 다시 찾지 않는다
    private static Sprite Pick(ref Sprite? cache, string key, Func<Sprite> draw)
    {
        if (cache != null)
        {
            return cache;
        }

        Sprite? art = VisualCatalog.FxOf(key);

        cache = art != null ? art : draw();

        return cache;
    }

    // 정사각형 흰 그림을 만든다 — 픽셀마다 'alphaAt(x, y)'(가운데 0, 가장자리 ±1)로 투명도를 정한다
    private static Sprite Make(string name, Func<float, float, float> alphaAt)
    {
        var texture = NewTexture(Size);
        var pixels  = new Color32[Size * Size];

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float u = (x + 0.5f) / Size * 2f - 1f;
                float v = (y + 0.5f) / Size * 2f - 1f;

                pixels[y * Size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alphaAt(u, v)) * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true); // 다시 고칠 일이 없다 — CPU 사본을 버린다

        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);

        sprite.name = name;

        return sprite;
    }

    // 테두리 띠를 그린다 — 가장자리 'FrameThickness'만 불투명, 띠 바로 안쪽에서 9-slice로 자른다
    private static Sprite MakeFrame()
    {
        var texture = NewTexture(FrameSize);
        var pixels  = new Color32[FrameSize * FrameSize];

        for (int y = 0; y < FrameSize; y++)
        {
            for (int x = 0; x < FrameSize; x++)
            {
                bool isEdge = x < FrameThickness || y < FrameThickness
                           || x >= FrameSize - FrameThickness || y >= FrameSize - FrameThickness;

                pixels[y * FrameSize + x] = new Color32(255, 255, 255, isEdge ? (byte)255 : (byte)0);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        float border = FrameThickness + 1;

        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, FrameSize, FrameSize), new Vector2(0.5f, 0.5f), 100f, 0,
                                      SpriteMeshType.FullRect, new Vector4(border, border, border, border));

        sprite.name = FrameKey;

        return sprite;
    }

    // 연출용 텍스처 — 반복하지 않고(가장자리가 반대편으로 번지지 않게) 부드럽게 늘린다
    private static Texture2D NewTexture(int size)
    {
        return new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode   = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags  = HideFlags.DontSave,
        };
    }

    // 가운데로부터의 거리 (가운데 0 · 변의 가운데 1)
    private static float Radius(float x, float y) => Mathf.Sqrt(x * x + y * y);
}
