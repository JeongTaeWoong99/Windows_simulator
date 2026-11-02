using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 캐릭터 레벨업 연출 — 작업슬롯 칸의 캐릭터 자리에서 금빛 기둥이 솟고, 겹화살표가 올라가며, 'LV.12' 글자가 튀어 오른다 (T-052).
// 칸('WorkStationSlotView')이 처음 레벨업할 때 코드로 붙인다 — 칸 프리팹을 건드리지 않는다('SlotGainShine'과 같은 방식).
//
// ■ 토스트를 따로 띄우지 않는다
//   레벨업은 배치된 캐릭터가 일하면서 저절로 오른다 — 칸 수만큼 자주 온다. 알림 창으로 띄우면 주의를 뺏는다(게임기획코어 P1).
//   칸 위 글자('LV.12')가 토스트 몫을 한다. 글자는 레벨 숫자뿐이라 언어 문구가 없다.
//
// ■ 그림은 'FxSprites'에서 — 기둥·번짐·별 조각은 획득 연출(gain_*) 것을 그대로, 화살표만 'levelup_arrow'
//   그림이 없으면 그 효과만 빠진다(코드로 그리지 않는다). 글자는 그림이 없어도 나온다.
//
// ■ 칸 루트에 붙는다 — 층은 레이아웃에서 빠진다.
//   기둥·번짐은 칸 크기의 자르는 층('Clip') 안에 둔다 — 칸 밖(이웃 칸·목록 위)으로 빛이 새지 않는다.
//   기둥 높이도 발에서 칸 위 끝까지로 줄인다. 화살표·별·글자는 작아서 자르지 않는다.
//
// ■ 수명
//   Sequence 하나를 칸에 묶는다(KillOnDisable — 배치가 풀리면 칸이 꺼진다). 끝나도 끊겨도 'OnKill'이 그림을 끈다.
//   연달아 오르면 처음부터 다시 — 겹쳐 쌓이지 않는다.
public class LevelUpFx : MonoBehaviour
{
    // 전체 길이 (초)
    private const float Duration = 1.5f;

    // 금빛 — 등급색과 섞이지 않게 레벨업은 늘 이 색이다
    private static readonly Color Gold      = new Color(1f, 0.84f, 0.32f, 1f);
    private static readonly Color GoldLight = new Color(1f, 0.95f, 0.7f, 1f);

    // 번짐 — 크기(칸 높이 대비) · 진하기
    private const float GlowSize  = 0.7f;
    private const float GlowAlpha = 0.55f;

    // 기둥 — 높이(칸 높이 대비) · 가로(높이 대비 — 그림이 1:2) · 솟는 시간 · 진하기
    private const float PillarHeight = 0.75f;
    private const float PillarWidth  = 0.4f;
    private const float PillarRise   = 0.25f;
    private const float PillarAlpha  = 0.55f;

    // 화살표 — 크기(칸 높이 대비) · 가로 자리(화살표 크기 대비) · 시작 지연 · 오르는 거리(칸 높이 대비) · 오르는 시간
    private const float ArrowSize     = 0.16f;
    private const float ArrowRise     = 0.22f; // 글자 아래에서 멈춘다 — 더 오르면 'LV.12'와 겹쳐 안 읽힌다
    private const float ArrowDuration = 0.7f;
    private static readonly Vector2[] ArrowSpots = { new Vector2(-1.1f, 0.05f), new Vector2(0f, 0f), new Vector2(1.1f, 0.12f) };

    // 별 조각 — 개수 · 크기(칸 높이 대비) · 퍼지는 거리(칸 높이 대비)
    private const int   SparkCount    = 6;
    private const float SparkSize     = 0.1f;
    private const float SparkDistance = 0.3f;

    // 글자 — 크기(칸 높이 대비) · 캐릭터 가운데보다 위로(칸 높이 대비) · 튀어 오르는 시간 · 사라지기 시작하는 때
    private const float TextSize     = 0.18f;
    private const float TextOffset   = 0.12f;
    private const float TextPop      = 0.3f;
    private const float TextFadeFrom = 1.05f;

    // 튀어 오를 때 가장 커지는 배율 (OutBack 2.5) — 칸 옆 여백 계산에 쓴다
    private const float TextOvershoot = 1.3f;

    private RectTransform _rect   = null!;
    private Image         _glow   = null!;
    private Image         _pillar = null!;
    private Image[]       _arrows = null!;
    private Image[]       _sparks = null!;
    private TextMeshProUGUI _text = null!;

    private Sequence? _seq;

    // 칸 루트에 붙이고 층을 만든다 (칸이 처음 레벨업할 때 한 번).
    //   style : 글자 모양을 빌릴 칸의 글자 — 폰트·재질을 그대로 쓴다(TMP 폰트를 코드가 찾지 않는다 ·
    //           외곽선을 코드로 바꾸면 재질 사본이 생기므로 재질은 빌린 것 그대로다)
    public static LevelUpFx Attach(GameObject slot, TMP_Text style)
    {
        var fx = slot.AddComponent<LevelUpFx>();

        fx.Build(style);

        return fx;
    }

    // 꺼졌던 칸이 다시 켜졌다 — 도중에 끊긴 연출의 그림이 남지 않게 (Unity 메시지)
    private void OnEnable()
    {
        if (_glow != null)
        {
            Hide();
        }
    }

    // 한 번 재생한다.
    //   feet · center : 캐릭터 발 자리 · 가운데 (월드). 무대 그림이 없으면 칸 왼쪽 가운데를 넘긴다
    //   level         : 새 레벨 — 글자 'LV.12'
    public void Play(Vector3 feet, Vector3 center, int level)
    {
        Stop();

        float height = _rect.rect.height;

        if (height <= 0f)
        {
            return; // 레이아웃이 아직 안 잡힌 칸
        }

        Vector2 feetLocal   = _rect.InverseTransformPoint(feet);
        Vector2 centerLocal = _rect.InverseTransformPoint(center);

        _seq = DOTween.Sequence().SetLink(gameObject, LinkBehaviour.KillOnDisable);

        AddGlow(centerLocal, height);
        AddPillar(feetLocal, height);
        AddArrows(feetLocal, height);
        AddSparks(centerLocal, height);
        AddText(centerLocal, height, level);

        _seq.OnKill(Hide);

        Services.Get<SoundManager>().PlaySfx(Sounds.RewardChime); // 런타임에 붙는 부품이라 Start가 없다 — 쓸 때 꺼낸다
    }

    // 끊고 그림을 끈다
    public void Stop()
    {
        if (_seq != null && _seq.IsActive())
        {
            _seq.Kill(); // OnKill → Hide
        }

        _seq = null;

        Hide();
    }

    // 번짐 — 캐릭터 뒤에서 한 번 부풀었다 꺼진다
    private void AddGlow(Vector2 at, float height)
    {
        if (_glow.sprite == null)
        {
            return;
        }

        RectTransform rect = _glow.rectTransform;

        _glow.enabled         = true;
        _glow.color           = WithAlpha(Gold, 0f);
        rect.anchoredPosition = at;
        rect.sizeDelta        = Vector2.one * height * GlowSize;
        rect.localScale       = Vector3.one * 0.4f;

        _seq!.Insert(0f, rect.DOScale(1f, 0.35f).SetEase(Ease.OutBack));
        _seq.Insert(0f, _glow.DOFade(GlowAlpha, 0.15f));
        _seq.Insert(0.5f, _glow.DOFade(0f, 0.6f).SetEase(Ease.InQuad));
    }

    // 기둥 — 발에서 위로 솟았다가 가늘어지며 꺼진다
    private void AddPillar(Vector2 feet, float height)
    {
        if (_pillar.sprite == null)
        {
            return;
        }

        RectTransform rect = _pillar.rectTransform;
        float         top  = _rect.rect.height * 0.5f - feet.y;          // 발에서 칸 위 끝까지
        float         h    = Mathf.Min(height * PillarHeight, top);

        if (h <= 0f)
        {
            return;
        }

        _pillar.enabled       = true;
        _pillar.color         = WithAlpha(GoldLight, PillarAlpha);
        rect.anchoredPosition = feet;
        rect.sizeDelta        = new Vector2(h * PillarWidth, h);
        rect.localScale       = new Vector3(1f, 0f, 1f);

        _seq!.Insert(0f, rect.DOScaleY(1f, PillarRise).SetEase(Ease.OutCubic));
        _seq.Insert(0.55f, rect.DOScaleX(0.2f, 0.5f).SetEase(Ease.InQuad));
        _seq.Insert(0.55f, _pillar.DOFade(0f, 0.5f).SetEase(Ease.InQuad));
    }

    // 겹화살표 — 셋이 엇갈려 발에서 솟아오르며 흐려진다
    private void AddArrows(Vector2 feet, float height)
    {
        float size = height * ArrowSize;

        for (int i = 0; i < _arrows.Length; i++)
        {
            Image arrow = _arrows[i];

            if (arrow.sprite == null)
            {
                continue;
            }

            RectTransform rect  = arrow.rectTransform;
            Vector2       start = feet + new Vector2(ArrowSpots[i].x * size, ArrowSpots[i].y * height + size * 0.5f);
            float         delay = i == 1 ? 0.08f : 0.2f; // 가운데가 먼저, 양옆이 뒤따른다

            arrow.enabled         = true;
            arrow.color           = WithAlpha(i == 1 ? GoldLight : Gold, 0f);
            rect.anchoredPosition = start;
            rect.sizeDelta        = Vector2.one * size;

            _seq!.Insert(delay, rect.DOAnchorPosY(start.y + height * ArrowRise, ArrowDuration).SetEase(Ease.OutCubic));
            _seq.Insert(delay, arrow.DOFade(1f, 0.12f));
            _seq.Insert(delay + ArrowDuration * 0.5f, arrow.DOFade(0f, ArrowDuration * 0.5f));
        }
    }

    // 별 조각 — 번짐이 다 부푼 때 가운데에서 둥글게 흩어진다
    private void AddSparks(Vector2 center, float height)
    {
        for (int i = 0; i < _sparks.Length; i++)
        {
            Image spark = _sparks[i];

            if (spark.sprite == null)
            {
                continue;
            }

            RectTransform rect  = spark.rectTransform;
            float         angle = (i + 0.5f) / _sparks.Length * Mathf.PI * 2f;
            float         reach = height * SparkDistance * (i % 2 == 0 ? 1f : 0.7f);
            Vector2       to    = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * reach;

            spark.enabled         = true;
            spark.color           = GoldLight;
            rect.anchoredPosition = center;
            rect.sizeDelta        = Vector2.one * height * SparkSize;
            rect.localScale       = Vector3.zero;

            const float at = 0.2f;

            _seq!.Insert(at, rect.DOAnchorPos(to, 0.55f).SetEase(Ease.OutCubic));
            _seq.Insert(at, rect.DOScale(1f, 0.15f).SetEase(Ease.OutBack));
            _seq.Insert(at + 0.15f, rect.DOScale(0f, 0.4f).SetEase(Ease.InQuad));
            _seq.Insert(at, rect.DOLocalRotate(new Vector3(0f, 0f, 90f), 0.55f, RotateMode.FastBeyond360));
        }
    }

    // 글자 'LV.12' — 캐릭터 위에서 튀어 나와 조금 오르다 사라진다
    private void AddText(Vector2 center, float height, int level)
    {
        RectTransform rect  = _text.rectTransform;
        float         halfH = _rect.rect.height * 0.5f;
        float         y     = Mathf.Min(center.y + height * TextOffset, halfH - height * (TextSize * 0.5f * TextOvershoot + 0.08f)); // 칸 위로 넘치지 않게 (오르는 몫 0.08까지)

        _text.enabled         = true;
        _text.text            = $"LV.{level}";
        _text.fontSize        = height * TextSize;
        _text.alpha           = 1f;

        // 칸 옆으로 넘치지 않게 — 캐릭터는 칸 왼쪽에 서 있어 가운데 정렬 글자의 앞이 잘린다
        float halfW = _rect.rect.width * 0.5f;
        float textW = _text.GetPreferredValues().x * 0.5f * TextOvershoot; // 튀어 오를 때 커지는 만큼 더 비운다
        float x     = Mathf.Clamp(center.x, -halfW + textW, Mathf.Max(-halfW + textW, halfW - textW));

        rect.anchoredPosition = new Vector2(x, y);
        rect.localScale       = Vector3.zero;

        _seq!.Insert(0.1f, rect.DOScale(1f, TextPop).SetEase(Ease.OutBack, 2.5f));
        _seq.Insert(0.1f, rect.DOAnchorPosY(y + height * 0.08f, Duration - 0.1f).SetEase(Ease.OutSine));
        _seq.Insert(TextFadeFrom, DOTween.To(() => _text.alpha, a => _text.alpha = a, 0f, Duration - TextFadeFrom));
    }

    private void Hide()
    {
        _glow.enabled   = false;
        _pillar.enabled = false;
        _text.enabled   = false;

        foreach (Image arrow in _arrows)
        {
            arrow.enabled = false;
        }

        foreach (Image spark in _sparks)
        {
            spark.enabled = false;
        }
    }

    // 칸 위를 덮는 층 하나 — 기둥·번짐(자르는 층 안) · 화살표 · 별 · 글자 순서 (Attach에서 한 번)
    private void Build(TMP_Text style)
    {
        RectTransform root = CreateRect("Level Up Fx", transform);

        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.sizeDelta = Vector2.zero;
        root.SetAsLastSibling(); // 칸의 모든 표시 위

        root.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

        _rect = root;

        // 기둥·번짐을 칸 안에서 자르는 층 — 칸과 같은 좌표라 자리 계산은 그대로다
        RectTransform clip = CreateRect("Clip", root);

        clip.anchorMin = Vector2.zero;
        clip.anchorMax = Vector2.one;
        clip.sizeDelta = Vector2.zero;
        clip.gameObject.AddComponent<RectMask2D>();

        _pillar = CreateImage("Pillar", clip, FxSprites.GainPillar);
        _pillar.rectTransform.pivot = new Vector2(0.5f, 0f); // 발에서 위로 자란다

        _glow = CreateImage("Glow", clip, FxSprites.GainGlow);

        _arrows = new Image[ArrowSpots.Length];

        for (int i = 0; i < _arrows.Length; i++)
        {
            _arrows[i] = CreateImage("Arrow", root, FxSprites.LevelUpArrow);
        }

        _sparks = new Image[SparkCount];

        for (int i = 0; i < _sparks.Length; i++)
        {
            _sparks[i] = CreateImage("Spark", root, FxSprites.GainSparkle);
        }

        RectTransform textRect = CreateRect("Text", root);

        textRect.sizeDelta = new Vector2(400f, 100f); // 넉넉히 — 글자는 가운데 정렬이라 상자 크기에 따라 밀리지 않는다

        _text = textRect.gameObject.AddComponent<TextMeshProUGUI>();

        _text.font               = style.font;
        _text.fontSharedMaterial = style.fontSharedMaterial;
        _text.color              = Gold;
        _text.fontStyle          = FontStyles.Bold;
        _text.alignment          = TextAlignmentOptions.Center;
        _text.textWrappingMode   = TextWrappingModes.NoWrap;
        _text.raycastTarget      = false;
        _text.enabled            = false;

        Hide();
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;

        return color;
    }

    // 가운데 기준의 Image — raycast는 끈다 (칸 클릭·툴팁을 가로채지 않는다)
    private static Image CreateImage(string name, RectTransform parent, Sprite? sprite)
    {
        RectTransform rect  = CreateRect(name, parent);
        Image         image = rect.gameObject.AddComponent<Image>();

        image.sprite        = sprite!;
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
