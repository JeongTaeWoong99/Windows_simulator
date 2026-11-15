using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// 축하 터짐 — 대상 가운데서 빛살이 돌며 펴지고, 고리가 퍼져 나가고, 별 조각이 사방으로 튄다 (2026-11-15 · T-201).
// 응축 성공(작게) · ★ 상승(크게) · 큐브 등급 상승(등급만큼)이 같은 모양을 세기만 바꿔 쓴다.
//
// ■ 세기 'power' 0~1 — 빛살 크기 · 고리 크기 · 별 조각 수가 같이 커진다
//   0   : 작은 반짝 — 별 조각 몇 개와 작은 고리만 (빛살 없음)
//   0.5 : 빛살 + 고리 + 별 조각 14
//   1   : 큰 빛살 두 겹 + 고리 두 번 + 별 조각 28
//
// ■ 그림은 'FxSprites'에서 — 빛살 'reveal_rays' · 고리 'gain_ring' · 번짐 'gain_glow' · 별 'reveal_sparkle'.
//   그림이 없으면 그 효과만 빠진다(코드로 그리지 않는다).
//
// ■ 대상에 붙는다 — 층은 맨 위 형제 · 레이아웃에서 빠진다. 대상 밖으로 튀어도 된다(자르지 않는다).
// ■ 수명 — Sequence 하나를 대상에 묶는다(KillOnDisable). 연달아 오면 처음부터 다시 — 겹쳐 쌓이지 않는다.
public class CelebrationFx : MonoBehaviour
{
    // 전체 길이 (초 · speed 1 기준)
    private const float Duration = 1.1f;

    // 빛살 — 대상 짧은 변 대비 크기(세기 1일 때) · 도는 각 · 진하기
    private const float RaySize   = 1.7f; // 더 크면 64px 그림이 너무 뭉툭하게 커진다
    private const float RayTurn   = 70f;
    private const float RayAlpha  = 0.75f;

    // 고리 — 대상 짧은 변 대비 끝 크기(세기 1일 때) · 진하기
    private const float RingSize  = 2.6f;
    private const float RingAlpha = 0.9f;

    // 번짐 — 짧게 번쩍이고 빠진다(오래 남으면 탁해 보인다 — 사용자 지적 2026-11-15)
    private const float GlowSize  = 1.3f;
    private const float GlowAlpha = 0.6f;

    // 별 조각 — 최소 · 최대 개수 · 크기 · 퍼지는 거리(대상 짧은 변 대비)
    private const int   SparkMin  = 6;
    private const int   SparkMax  = 28;
    private const float SparkPx   = 26f;
    private const float SparkFlyMin = 0.6f;
    private const float SparkFlyMax = 1.5f;

    private RectTransform _rect   = null!;
    private RectTransform _layer  = null!;
    private Image         _glow   = null!;
    private Image[]       _rays   = null!;
    private Image[]       _rings  = null!;
    private Image[]       _sparks = null!;

    private Sequence? _seq;

    // 대상에 붙은 것을 찾거나 새로 붙인다.
    public static CelebrationFx On(RectTransform target)
    {
        var fx = target.GetComponent<CelebrationFx>();

        if (fx == null)
        {
            fx = target.gameObject.AddComponent<CelebrationFx>();
            fx.Build();
        }

        return fx;
    }

    // 터뜨린다.
    //   color : 빛 색 — 보통 등급색이나 금빛
    //   power : 세기 0~1 (위 표)
    //   speed : 빠르기 — 모든 시간을 나눈다(자동 큐브 2)
    public void Play(Color color, float power, float speed = 1f)
    {
        power = Mathf.Clamp01(power);
        speed = Mathf.Max(0.1f, speed);

        _seq?.Kill();
        _layer.SetAsLastSibling();

        float   unit  = Mathf.Min(_rect.rect.width, _rect.rect.height);
        float   time  = Duration / speed;
        Color   light = Color.Lerp(color, Color.white, 0.35f);

        _seq = DOTween.Sequence().SetLink(gameObject, LinkBehaviour.KillOnDisable).OnKill(HideAll);

        // 번짐 — 번쩍 커지고 빠진다
        if (_glow.sprite != null)
        {
            Show(_glow, light, unit * GlowSize * (0.6f + 0.4f * power));
            _glow.rectTransform.localScale = Vector3.one * 0.5f;
            _seq.Insert(0f, _glow.rectTransform.DOScale(1f, time * 0.3f).SetEase(Ease.OutCubic));
            _seq.Insert(0f, _glow.DOFade(GlowAlpha, time * 0.08f));
            _seq.Insert(time * 0.12f, _glow.DOFade(0f, time * 0.4f).SetEase(Ease.InQuad));
        }

        // 빛살 — 세기 0.3부터. 두 겹은 0.8부터(반대로 돈다)
        int rayCount = power >= 0.8f ? 2 : power >= 0.3f ? 1 : 0;

        for (int i = 0; i < _rays.Length; i++)
        {
            Image ray = _rays[i];

            if (i >= rayCount || ray.sprite == null)
            {
                continue;
            }

            float size = unit * RaySize * (0.55f + 0.45f * power) * (i == 0 ? 1f : 0.75f);
            float turn = i == 0 ? RayTurn : -RayTurn * 1.4f;

            Show(ray, i == 0 ? light : color, size);
            ray.rectTransform.localScale    = Vector3.one * 0.3f;
            ray.rectTransform.localRotation = Quaternion.Euler(0f, 0f, i * 22.5f);
            _seq.Insert(0f, ray.rectTransform.DOScale(1f, time * 0.45f).SetEase(Ease.OutBack, 1.4f));
            _seq.Insert(0f, ray.rectTransform.DOLocalRotate(new Vector3(0f, 0f, i * 22.5f + turn), time, RotateMode.FastBeyond360).SetEase(Ease.OutQuad));
            _seq.Insert(0f, ray.DOFade(RayAlpha, time * 0.1f));
            _seq.Insert(time * 0.45f, ray.DOFade(0f, time * 0.55f).SetEase(Ease.InQuad));
        }

        // 고리 — 하나는 늘, 둘째는 세기 0.6부터 조금 늦게
        int ringCount = power >= 0.6f ? 2 : 1;

        for (int i = 0; i < _rings.Length; i++)
        {
            Image ring = _rings[i];

            if (i >= ringCount || ring.sprite == null)
            {
                continue;
            }

            float at   = i * time * 0.14f;
            float size = unit * RingSize * (0.45f + 0.55f * power) * (i == 0 ? 1f : 0.8f);

            Show(ring, i == 0 ? light : color, size);
            ring.rectTransform.localScale = Vector3.one * 0.15f;
            _seq.Insert(at, ring.rectTransform.DOScale(1f, time * 0.55f).SetEase(Ease.OutCubic));
            _seq.Insert(at, ring.DOFade(RingAlpha, time * 0.05f));
            _seq.Insert(at + time * 0.2f, ring.DOFade(0f, time * 0.35f));
        }

        // 별 조각 — 가운데서 사방으로
        int sparkCount = Mathf.RoundToInt(Mathf.Lerp(SparkMin, SparkMax, power));

        for (int i = 0; i < _sparks.Length; i++)
        {
            Image spark = _sparks[i];

            if (i >= sparkCount || spark.sprite == null)
            {
                continue;
            }

            float   angle  = (i / (float)sparkCount + Random.Range(-0.04f, 0.04f)) * Mathf.PI * 2f;
            float   fly    = unit * Random.Range(SparkFlyMin, SparkFlyMax) * (0.6f + 0.4f * power);
            Vector2 target = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * fly;
            float   at     = Random.Range(0f, time * 0.08f);
            float   life   = time * Random.Range(0.55f, 0.85f);

            Show(spark, Random.value < 0.5f ? light : color, SparkPx * Random.Range(0.7f, 1.2f), 1f);
            spark.rectTransform.anchoredPosition = Vector2.zero;
            spark.rectTransform.localScale       = Vector3.one; // 지난번 터짐이 0.2로 줄여 둔 것을 되돌린다
            spark.rectTransform.localRotation    = Quaternion.Euler(0f, 0f, Random.Range(0f, 90f));
            _seq.Insert(at, spark.rectTransform.DOAnchorPos(target, life).SetEase(Ease.OutCubic));
            _seq.Insert(at, spark.rectTransform.DOScale(0.2f, life).SetEase(Ease.InQuad));
            _seq.Insert(at + life * 0.5f, spark.DOFade(0f, life * 0.5f));
        }
    }

    // 층 — 번짐 · 빛살 2 · 고리 2 · 별 조각 (On에서 한 번)
    private void Build()
    {
        _rect = (RectTransform)transform;

        var go = new GameObject("Celebration Fx", typeof(RectTransform), typeof(LayoutElement));

        go.layer = gameObject.layer;
        go.GetComponent<LayoutElement>().ignoreLayout = true;
        _layer = (RectTransform)go.transform;
        _layer.SetParent(_rect, false);
        _layer.anchorMin = _layer.anchorMax = new Vector2(0.5f, 0.5f);
        _layer.sizeDelta = Vector2.zero;

        _glow   = CreateImage("Glow", FxSprites.GainGlow);
        _rays   = new[] { CreateImage("Rays (0)", FxSprites.RevealRays), CreateImage("Rays (1)", FxSprites.RevealRays) };
        _rings  = new[] { CreateImage("Ring (0)", FxSprites.GainRing),   CreateImage("Ring (1)", FxSprites.GainRing) };
        _sparks = new Image[SparkMax];

        for (int i = 0; i < SparkMax; i++)
        {
            _sparks[i] = CreateImage($"Spark ({i})", FxSprites.RevealSparkle);
        }

        HideAll();
    }

    private Image CreateImage(string name, Sprite? sprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

        go.layer = gameObject.layer;

        var image = go.GetComponent<Image>();

        image.rectTransform.SetParent(_layer, false);
        image.sprite        = sprite;
        image.raycastTarget = false;
        image.enabled       = false;

        return image;
    }

    // 층 하나를 켠다 — 대부분 투명에서 시작해 트윈이 올린다. 별 조각은 처음부터 보인다(alpha 1)
    private static void Show(Image image, Color color, float size, float alpha = 0f)
    {
        image.enabled                  = true;
        image.color                    = new Color(color.r, color.g, color.b, alpha);
        image.rectTransform.sizeDelta  = new Vector2(size, size);
        image.rectTransform.anchoredPosition = Vector2.zero;
    }

    // 끝나거나 끊기면 모든 층을 끈다.
    private void HideAll()
    {
        if (_glow == null)
        {
            return;
        }

        _glow.enabled = false;

        foreach (Image image in _rays)
        {
            image.enabled = false;
        }

        foreach (Image image in _rings)
        {
            image.enabled = false;
        }

        foreach (Image image in _sparks)
        {
            image.enabled = false;
        }
    }
}
