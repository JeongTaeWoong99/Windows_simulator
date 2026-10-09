using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// 고등급 공개 순간의 빛 폭발 — 도는 광선 · 퍼지는 번짐 · 흩어지는 별 조각.
// 결과 창(Panel) 위에 덮은 층 하나에서 그린다 — 'GachaResultPresenter'가 처음에 코드로 만든다('Create').
//
// ■ 왜 칸 안이 아니라 창 위 층인가
// 칸 안에 그리면 나중에 그려지는 옆 칸(형제)에 가려지고 칸 크기 근처에 갇힌다.
// 층은 레이아웃에서 빠진다('LayoutElement.ignoreLayout') — 창의 'VerticalLayoutGroup'이 자리를 밀지 않는다.
//
// ■ 파티클 시스템을 쓰지 않는다
// Screen Space Overlay 캔버스에서는 'ParticleSystem'이 UI 위에 그려지지 않는다(UI 파티클 패키지가 없다).
// 별 조각 Image를 트윈으로 흩뿌려 파티클처럼 보이게 한다.
//
// ■ 그림은 'FxSprites' — 없는 부위(광선·번짐·별)는 켜지 않는다(빈 Image는 흰 네모로 그려진다)
//
// ■ 폭발이 겹칠 수 있다 — 한 벌씩 돌려쓴다
// 고등급 연출이 뒤 칸 공개를 막지 않으므로(2026-10-09) 영웅·전설이 연달아 나오면 폭발이 동시에 돈다.
// 폭발 한 벌(광선 · 번짐 · 별 조각)을 끝난 것부터 다시 쓰고, 모자라면 한 벌 더 만든다 — 10연차라도 몇 벌이면 된다.
public class RewardBurstFx : MonoBehaviour
{
    // 전설↑ 폭발을 영웅보다 키우는 배율 — 광선 크기 · 별 조각 거리
    private const float TopBoost = 1.4f;

    // 번짐 — 광선보다 조금 작게 시작해 크게 퍼진다
    private const float HaloSizeRatio = 0.9f;
    private const float HaloEndScale  = 2f;

    // 광선이 퍼지는 동안 도는 각도 (도)
    private const float RaysTurn = 60f;

    // 별 조각이 흐려지기 시작하는 때 (폭발 시간 대비)
    private const float SparkFadeStart = 0.4f;

    // 별 조각 색을 흰색 쪽으로 당기는 정도 — 반짝이로 보이게
    private const float SparkWhiten = 0.4f;

    // 폭발 한 벌 — 그림 오브젝트와 지금 도는 Sequence
    private sealed class BurstSet
    {
        public Image       Rays   = null!;
        public Image       Halo   = null!;
        public List<Image> Sparks = new List<Image>();
        public Sequence?   Motion;

        public bool IsPlaying => Motion != null && Motion.IsActive();
    }

    private RewardRevealSettings _settings = null!;
    private RectTransform        _layer    = null!;

    private readonly List<BurstSet> _sets = new List<BurstSet>();

    // 'panel' 위에 폭발 층을 만든다 — 창을 꽉 채우고 맨 앞에 그린다 ('GachaResultPresenter.Start'가 한 번 호출)
    public static RewardBurstFx Create(RectTransform panel, RewardRevealSettings settings)
    {
        var go = new GameObject("Reveal Burst Layer", typeof(RectTransform), typeof(LayoutElement));

        var layer = (RectTransform)go.transform;

        layer.SetParent(panel, false);
        layer.anchorMin = Vector2.zero;
        layer.anchorMax = Vector2.one;
        layer.offsetMin = Vector2.zero;
        layer.offsetMax = Vector2.zero;
        layer.SetAsLastSibling();

        go.GetComponent<LayoutElement>().ignoreLayout = true;

        var burst = go.AddComponent<RewardBurstFx>();

        burst._settings = settings;
        burst._layer    = layer;

        return burst;
    }

    // 'worldCenter'에서 폭발한다 — 도는 폭발은 두고 쉬는 한 벌로 (고등급 칸이 공개되는 순간 Presenter가 호출)
    public void Play(Vector3 worldCenter, Color color, bool isTop)
    {
        BurstSet set    = GetIdleSet();
        float    boost  = isTop ? TopBoost : 1f;
        float    time   = _settings.BurstDuration;
        Vector2  center = _layer.InverseTransformPoint(worldCenter);

        Sequence seq = DOTween.Sequence().SetLink(gameObject, LinkBehaviour.KillOnDisable);

        PlaceAndFade(seq, set.Rays, center, color, _settings.RaysSize, time);
        seq.Insert(0f, set.Rays.rectTransform.DOScale(_settings.RaysScale * boost, time).From(Vector3.one * 0.3f).SetEase(Ease.OutCubic));
        seq.Insert(0f, set.Rays.rectTransform.DORotate(new Vector3(0f, 0f, RaysTurn), time, RotateMode.FastBeyond360));

        PlaceAndFade(seq, set.Halo, center, color, _settings.RaysSize * HaloSizeRatio, time);
        seq.Insert(0f, set.Halo.rectTransform.DOScale(HaloEndScale * boost, time).From(Vector3.one * 0.4f).SetEase(Ease.OutCubic));

        int   count      = isTop ? _settings.TopSparkCount : _settings.SparkCount;
        Color sparkColor = Color.Lerp(color, Color.white, SparkWhiten);

        for (int i = 0; i < count; i++)
        {
            AddSpark(seq, GetSpark(set, i), center, sparkColor, (float)i / count, boost);
        }

        // 다 퍼지면 그림을 끈다 — 마지막 값(알파 0)으로 남아도 보이지 않지만 그릴 거리에서 빼 둔다
        seq.AppendCallback(() => HideSet(set));

        set.Motion = seq;
    }

    // 도는 폭발을 모두 끊고 숨긴다 (스킵 · 창 닫기 · 새 결과)
    public void Stop()
    {
        foreach (BurstSet set in _sets)
        {
            if (set.IsPlaying)
            {
                set.Motion!.Kill();
            }

            HideSet(set);
        }
    }

    // 쉬는 한 벌 — 없으면 하나 더 만든다 (Play에서 호출)
    private BurstSet GetIdleSet()
    {
        foreach (BurstSet set in _sets)
        {
            if (!set.IsPlaying)
            {
                return set;
            }
        }

        var created = new BurstSet
        {
            Halo = CreateImage("Halo", FxSprites.RevealGlow),
            Rays = CreateImage("Rays", FxSprites.RevealRays),
        };

        _sets.Add(created);

        return created;
    }

    // 한 벌의 그림을 모두 끈다
    private static void HideSet(BurstSet set)
    {
        set.Motion        = null;
        set.Rays.enabled  = false;
        set.Halo.enabled  = false;

        foreach (Image spark in set.Sparks)
        {
            spark.enabled = false;
        }
    }

    // 광선·번짐을 가운데에 놓고 끝으로 갈수록 사라지게 한다 (Play에서 호출)
    private static void PlaceAndFade(Sequence seq, Image image, Vector2 center, Color color, float size, float time)
    {
        RectTransform rect = image.rectTransform;

        image.enabled         = image.sprite != null;
        image.color           = color;
        rect.sizeDelta        = new Vector2(size, size);
        rect.anchoredPosition = center;
        rect.localRotation    = Quaternion.identity;

        seq.Insert(0f, image.DOFade(0f, time).SetEase(Ease.InQuad));
    }

    // 별 조각 하나를 가운데에서 바깥으로 날린다 — 방향은 고르게 나누되 조금씩 흔들고, 거리·크기는 제각각
    //   turn : 몇 번째 조각인가 (0~1 — 한 바퀴 중 자리)
    private void AddSpark(Sequence seq, Image spark, Vector2 center, Color color, float turn, float boost)
    {
        RectTransform rect = spark.rectTransform;

        float angle    = (turn + Random.Range(-0.03f, 0.03f)) * Mathf.PI * 2f;
        float distance = Random.Range(_settings.SparkDistance.x, _settings.SparkDistance.y) * boost;
        float time     = _settings.BurstDuration * Random.Range(0.75f, 1f);

        Vector2 target = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;

        spark.enabled         = spark.sprite != null;
        spark.color           = color;
        rect.sizeDelta        = new Vector2(_settings.SparkSize, _settings.SparkSize);
        rect.anchoredPosition = center;
        rect.localScale       = Vector3.one * Random.Range(0.6f, 1.2f);
        rect.localRotation    = Quaternion.identity;

        seq.Insert(0f, rect.DOAnchorPos(target, time).SetEase(Ease.OutCubic));
        seq.Insert(0f, rect.DOScale(0f, time).SetEase(Ease.InQuad));
        seq.Insert(0f, rect.DORotate(new Vector3(0f, 0f, Random.Range(-180f, 180f)), time, RotateMode.FastBeyond360));
        seq.Insert(time * SparkFadeStart, spark.DOFade(0f, time * (1f - SparkFadeStart)));
    }

    // 이 벌의 'index'번째 별 조각 — 없으면 그때 만든다 (Play에서 호출)
    private Image GetSpark(BurstSet set, int index)
    {
        while (set.Sparks.Count <= index)
        {
            set.Sparks.Add(CreateImage("Spark", FxSprites.RevealSparkle));
        }

        return set.Sparks[index];
    }

    // 층 가운데 기준의 연출용 Image를 만든다 — raycast는 끈다
    private Image CreateImage(string name, Sprite? sprite)
    {
        var go    = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var image = go.GetComponent<Image>();
        var rect  = image.rectTransform;

        rect.SetParent(_layer, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);

        image.sprite        = sprite;
        image.raycastTarget = false;
        image.enabled       = false;

        return image;
    }
}
