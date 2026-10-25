using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using GameData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 유물 감정 연출 — 결과 칸을 공개하기 전에 한 번 도는 무대 (T-151 · 목업 'GameDesign/design/ui/유물감정목업.html' 타임라인).
// ① 흔들림 → ②③④ 금 세 번 → ⑤ 번쩍 → ⑥ 조각 → ⑦ 대표 보상 등장 → ⑧ (아티팩트 · Special 유물이면) 띠 + 한 번 더 빛남.
// 결과 팝업 위에 덮은 층 하나에서 그린다 — 'GachaResultPresenter'가 처음에 코드로 만든다('Create'). 'RewardBurstFx'와 같은 방식.
//
// ■ 여러 개를 한꺼번에 감정해도 무대는 한 번이다
//   대표 보상 하나만 등장시킨다(고르는 것은 Presenter — 아티팩트 → Special 유물 → 가장 높은 등급). 나머지는 이어지는 결과 칸 공개가 보여 준다.
//   감정할 때마다 무대를 한 번씩 돌리면 50개 감정이 수십 초가 된다.
//
// ■ 클릭을 막지 않는다 — 층의 'CanvasGroup'이 레이캐스트를 통과시켜, 팝업 바탕 클릭(스킵)을 Presenter가 그대로 받는다.
//
// ■ 그림은 'FxSprites' — 없는 부위(금 · 조각 · 번짐 · 광선)는 켜지 않는다(빈 Image는 흰 네모로 그려진다). 움직임은 그대로 돈다.
//   유물·보상 그림은 결과 칸 프리팹('SlotView')을 키워 쓴다 — 아이콘이 없으면 칸이 이름 첫 글자로 그린다.
public class RelicAppraiseFx : MonoBehaviour
{
    // ── 타임라인 (초 — 목업 표)
    private const float ShakeTime  = 0.6f;
    private const float CrackTime  = 0.22f;
    private const float CrackGap   = 0.08f;
    private const float FlashIn    = 0.25f;
    private const float FlashOut   = 0.45f;
    private const float ShardTime  = 0.7f;
    private const float PopTime    = 0.45f;
    private const float BannerTime = 0.35f;
    private const float AfterFlash = 0.6f;
    private const float HoldTime   = 0.8f;  // 다 나온 뒤 결과 칸으로 넘어가기 전에 머문다 — 무엇이 나왔는지 읽을 틈
    private const float RaysTurn   = 14f;   // 광선이 한 바퀴 도는 시간
    private const float RainbowLap = 3f;    // Special 무지개가 한 바퀴 도는 시간

    // 흔들림 각도 (도) — 점점 세게 (목업 그대로)
    private static readonly float[] ShakeAngles = { -3f, 4f, -5f, 6f, -6f, 5f, 0f };

    // 금이 퍼질 때마다 유물이 부풀었다 오그라드는 정도 — 갈수록 크게
    private const float CrackPunch     = 0.03f;
    private const float CrackPunchStep = 0.02f;

    // ── 크기 (칸 프리팹 배율 · px)
    private const float StageScale  = 2f;     // 결과 칸을 키워 무대 아이콘으로 쓴다
    private const float FlashSize   = 900f;
    private const float RaysSize    = 560f;
    private const float ShardSize   = 34f;
    private const int   ShardCount  = 12;
    private const float ShardFall   = 60f;    // 조각이 바깥으로 가며 떨어지는 만큼
    private const float ShardSpin   = 270f;
    private const float PopFrom     = 0.3f;
    private const float PopOver     = 1.15f;
    private const float PopOverAt   = 0.7f;   // 넘쳤다 돌아오는 때 (등장 시간 대비)
    private const float BannerRise  = 24f;
    private const float BannerScale = 1.3f;   // 제목 글자 대비
    private const float AfterGlow   = 0.6f;   // ⑧ 두 번째 빛의 세기

    private static readonly Vector2 StageCenter   = new Vector2(0f, 20f);    // 화면 가운데보다 살짝 위 (목업 46%)
    private static readonly Vector2 BannerPos     = new Vector2(0f, 250f);
    private static readonly Vector2 ShardDistance = new Vector2(150f, 240f);

    // 조각 색 — 흙빛 (목업 clay)
    private static readonly Color ShardTint = new Color32(0x9E, 0x81, 0x50, 0xFF);

    // 광선 색 — 아티팩트가 아니면 옅은 흰빛
    private static readonly Color PlainRays = new Color(1f, 1f, 1f, 0.14f);

    // 금 세 장이 자라는 방향 — 1 위에서 아래 · 2 오른쪽에서 왼쪽 아래로 · 3 왼쪽에서 오른쪽으로 (그림의 갈래 방향)
    private static readonly (Image.FillMethod Method, int Origin)[] CrackFills =
    {
        (Image.FillMethod.Vertical,   (int)Image.OriginVertical.Top),
        (Image.FillMethod.Horizontal, (int)Image.OriginHorizontal.Right),
        (Image.FillMethod.Horizontal, (int)Image.OriginHorizontal.Left),
    };

    private CanvasGroup _group  = null!;
    private RectTransform _layer = null!;
    private Image       _rays   = null!;
    private SlotView    _relic  = null!;
    private SlotView    _reward = null!;
    private Image       _flash  = null!;
    private TMP_Text    _banner = null!;

    private readonly List<Image> _cracks = new List<Image>();
    private readonly List<Image> _shards = new List<Image>();

    // 'root'(팝업 몸통)을 꽉 채우는 층을 맨 앞에 만든다 ('GachaResultPresenter.Start'가 한 번 호출)
    //   slotPrefab     : 결과 칸 프리팹 — 유물·보상 그림을 그린다
    //   bannerTemplate : 띠 글자를 복제할 원본(결과 창 제목) — 서체·외곽선을 맞춘다
    public static RelicAppraiseFx Create(RectTransform root, SlotView slotPrefab, TMP_Text bannerTemplate)
    {
        var go    = new GameObject("Relic Appraise Layer", typeof(RectTransform), typeof(CanvasGroup), typeof(LayoutElement));
        var layer = (RectTransform)go.transform;

        layer.SetParent(root, false);
        layer.anchorMin = Vector2.zero;
        layer.anchorMax = Vector2.one;
        layer.offsetMin = Vector2.zero;
        layer.offsetMax = Vector2.zero;
        layer.SetAsLastSibling();

        go.GetComponent<LayoutElement>().ignoreLayout = true;

        var fx = go.AddComponent<RelicAppraiseFx>();

        fx._layer = layer;
        fx._group = go.GetComponent<CanvasGroup>();
        fx._group.blocksRaycasts = false; // 클릭은 팝업 바탕(스킵)이 받는다
        fx._group.interactable   = false;
        fx._group.alpha          = 0f;

        // 그리는 순서 = 만드는 순서 — 광선 · 유물 · 보상 · 조각 · 번짐 · 띠
        fx._rays   = fx.CreateImage("Rays", FxSprites.RevealRays, RaysSize);
        fx._relic  = fx.CreateSlot("Relic", slotPrefab);
        fx._reward = fx.CreateSlot("Reward", slotPrefab);

        for (int i = 0; i < CrackFills.Length; i++)
        {
            fx._cracks.Add(fx.CreateCrack(i));
        }

        for (int i = 0; i < ShardCount; i++)
        {
            fx._shards.Add(fx.CreateImage("Shard", FxSprites.AppraiseShard, ShardSize));
        }

        fx._flash  = fx.CreateImage("Flash", FxSprites.RevealGlow, FlashSize);
        fx._banner = fx.CreateBanner(bannerTemplate);

        return fx;
    }

    // 무대를 한 번 돌린다 — 끝까지 가면 대표 보상이 놓인 채로 'HoldTime'만큼 머물고 돌아온다.
    //   relic  : 감정한 유물 칸 값(수량은 감정한 개수)
    //   reward : 대표 보상 칸 값
    //   banner : ⑧ 띠 글자 — 아티팩트 · Special 유물이 아니면 null(띠 없이 끝난다)
    //   accent : ⑧ 띠 · 두 번째 빛 · 광선 색. null이면 옅은 흰빛
    // ※ 취소되면 그 자리에서 멈춘다 — 숨기는 것은 부르는 쪽('Stop') 몫이다.
    public async UniTask PlayAsync(SlotData relic, SlotData reward, string? banner, Color? accent, CancellationToken ct)
    {
        Color relicColor = RarityPalette.Get(relic.Rarity);
        bool  isSpecial  = relic.Rarity == GlobalRarity.Special || reward.Rarity == GlobalRarity.Special;

        Prepare(relic, reward, relicColor);

        // ① 흔들림
        await ShakeAsync(ct);

        // ②③④ 금 세 번 — 틈새 빛이 점점 세진다
        for (int i = 0; i < _cracks.Count; i++)
        {
            await CrackAsync(i, ct);
            await UniTask.Delay(System.TimeSpan.FromSeconds(CrackGap), cancellationToken: ct);
        }

        // ⑤ 번쩍 — 등급 색 (Special = 흰빛 · 광선이 무지개로 돈다)
        _flash.color = Transparent(isSpecial ? Color.white : relicColor);
        await Track(_flash.DOFade(1f, FlashIn)).ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, ct);

        // ⑥ 조각 — 유물을 숨기고 흩뿌린다 · 번짐은 걷힌다 (기다리지 않는다 — ⑦과 겹친다)
        _relic.gameObject.SetActive(false);
        ScatterShards();
        _ = Track(_flash.DOFade(0f, FlashOut));

        // ⑦ 등장 — 뒤에서 광선이 돈다
        SpinRays(accent ?? PlainRays, isSpecial);
        await PopRewardAsync(ct);

        // ⑧ 아티팩트 · Special 유물 — 띠가 내려오고 한 번 더 빛난다
        if (banner != null)
        {
            await BannerAsync(banner, accent ?? Color.white, ct);
        }

        await UniTask.Delay(System.TimeSpan.FromSeconds(HoldTime), cancellationToken: ct);
    }

    // 모든 움직임을 끊고 층을 숨긴다 (끝 · 스킵 · 창 닫기 · 새 결과 — Presenter가 호출)
    // ※ 'DOTween.Kill(this)'는 살아 있는 것만 죽인다 — 'ToUniTask'가 이미 Kill한 트윈을 다시 Kill하지 않는다(dotween 스킬 함정 표).
    public void Stop()
    {
        DOTween.Kill(this);

        _group.alpha = 0f;
        _relic.gameObject.SetActive(false);
        _reward.gameObject.SetActive(false);

        foreach (Image shard in _shards)
        {
            shard.enabled = false;
        }
    }

    #region 단계

    // 시작 모습으로 놓는다 — 유물 · 금(0) 보이고, 나머지는 투명·숨김 (PlayAsync 처음)
    private void Prepare(SlotData relic, SlotData reward, Color relicColor)
    {
        DOTween.Kill(this);

        _group.alpha = 1f;

        _relic.gameObject.SetActive(true);
        _relic.Bind(relic);
        _relic.SetSubVisible(true);
        ResetTransform(_relic.transform, StageScale);

        _reward.gameObject.SetActive(true);
        _reward.Bind(reward);
        _reward.SetSubVisible(true);
        ResetTransform(_reward.transform, 0f);

        foreach (Image crack in _cracks)
        {
            crack.enabled    = crack.sprite != null;
            crack.color      = relicColor;
            crack.fillAmount = 0f;
        }

        foreach (Image shard in _shards)
        {
            shard.enabled = false;
        }

        _rays.color   = Transparent(PlainRays);
        _flash.color  = Transparent(Color.white);
        _banner.alpha = 0f;
        ResetTransform(_rays.transform, 1f);
    }

    // ① 좌우로 흔든다 — 점점 세게 (목업 ±6°)
    private UniTask ShakeAsync(CancellationToken ct)
    {
        Sequence seq  = Track(DOTween.Sequence());
        float    step = ShakeTime / ShakeAngles.Length;

        foreach (float angle in ShakeAngles)
        {
            seq.Append(_relic.transform.DOLocalRotate(new Vector3(0f, 0f, angle), step).SetEase(Ease.Linear));
        }

        return seq.ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, ct);
    }

    // ②③④ 금 한 갈래 — 그림이 자라는 동안 유물이 살짝 부풀었다 돌아온다
    private UniTask CrackAsync(int index, CancellationToken ct)
    {
        float punch = CrackPunch + CrackPunchStep * index;
        float tilt  = index % 2 == 0 ? -3f : 3f;

        Sequence seq = Track(DOTween.Sequence());

        seq.Insert(0f, _cracks[index].DOFillAmount(1f, CrackTime).SetEase(Ease.OutQuad));
        seq.Insert(0f, _relic.transform.DOScale(StageScale * (1f + punch), CrackTime * 0.5f).SetLoops(2, LoopType.Yoyo));
        seq.Insert(0f, _relic.transform.DOLocalRotate(new Vector3(0f, 0f, tilt), CrackTime * 0.5f).SetLoops(2, LoopType.Yoyo));

        return seq.ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, ct);
    }

    // ⑥ 조각 12개를 가운데에서 바깥으로 — 돌며 떨어지고 흐려진다 (기다리지 않는다)
    private void ScatterShards()
    {
        for (int i = 0; i < _shards.Count; i++)
        {
            Image         shard = _shards[i];
            RectTransform rect  = shard.rectTransform;

            float   angle    = ((float)i / _shards.Count + Random.Range(0f, 0.06f)) * Mathf.PI * 2f;
            float   distance = Random.Range(ShardDistance.x, ShardDistance.y);
            Vector2 target   = StageCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance + Vector2.down * ShardFall;

            shard.enabled         = shard.sprite != null;
            shard.color           = ShardTint;
            rect.anchoredPosition = StageCenter;
            rect.localRotation    = Quaternion.identity;
            rect.localScale       = Vector3.one * Random.Range(0.7f, 1.3f);

            Track(rect.DOAnchorPos(target, ShardTime).SetEase(Ease.OutCubic));
            Track(rect.DORotate(new Vector3(0f, 0f, Random.Range(-ShardSpin, ShardSpin)), ShardTime, RotateMode.FastBeyond360));
            Track(shard.DOFade(0f, ShardTime).SetEase(Ease.InQuad));
        }
    }

    // ⑦ 광선 — 나타나며 돌기 시작해 무대가 끝날 때까지 돈다. Special이면 색이 무지개로 돈다
    private void SpinRays(Color color, bool rainbow)
    {
        _rays.color = Transparent(color);

        Track(_rays.DOFade(color.a, PopTime));
        Track(_rays.rectTransform.DOLocalRotate(new Vector3(0f, 0f, -360f), RaysTurn, RotateMode.FastBeyond360)
                                 .SetEase(Ease.Linear).SetLoops(-1));

        if (rainbow)
        {
            Track(DOVirtual.Float(0f, 1f, RainbowLap, hue => _rays.color = WithAlpha(Color.HSVToRGB(hue, 0.5f, 1f), _rays.color.a))
                           .SetEase(Ease.Linear).SetLoops(-1));
        }
    }

    // ⑦ 대표 보상이 튀어나온다 — 0.3 → 1.15 → 1
    private UniTask PopRewardAsync(CancellationToken ct)
    {
        Transform reward = _reward.transform;
        Sequence  seq    = Track(DOTween.Sequence());

        reward.localScale = Vector3.one * (StageScale * PopFrom);

        seq.Append(reward.DOScale(StageScale * PopOver, PopTime * PopOverAt).SetEase(Ease.OutCubic));
        seq.Append(reward.DOScale(StageScale, PopTime * (1f - PopOverAt)).SetEase(Ease.InOutQuad));

        return seq.ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, ct);
    }

    // ⑧ 띠가 위에서 내려오고, 대표 보상 자리가 한 번 더 빛난다
    private UniTask BannerAsync(string text, Color color, CancellationToken ct)
    {
        RectTransform rect = _banner.rectTransform;

        _banner.text          = text;
        _banner.color         = WithAlpha(color, 0f);
        rect.anchoredPosition = BannerPos + Vector2.up * BannerRise;
        _flash.color          = Transparent(color);

        Sequence seq = Track(DOTween.Sequence());

        seq.Insert(0f, _banner.DOFade(1f, BannerTime));
        seq.Insert(0f, rect.DOAnchorPos(BannerPos, BannerTime).SetEase(Ease.OutCubic));
        seq.Insert(0f, _flash.DOFade(AfterGlow, AfterFlash * 0.3f));
        seq.Insert(AfterFlash * 0.3f, _flash.DOFade(0f, AfterFlash * 0.7f));

        return seq.ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, ct);
    }

    #endregion

    #region 만들기

    // 무대 가운데에 놓는 연출용 Image — raycast는 끈다. 그림이 없으면 켜지 않는다 (Create에서 호출)
    private Image CreateImage(string name, Sprite? sprite, float size)
    {
        var go    = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var image = go.GetComponent<Image>();
        var rect  = image.rectTransform;

        rect.SetParent(_layer, false);
        rect.anchorMin        = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta        = new Vector2(size, size);
        rect.anchoredPosition = StageCenter;

        image.sprite        = sprite!;
        image.raycastTarget = false;
        image.enabled       = sprite != null;
        image.color         = Transparent(Color.white);

        return image;
    }

    // 유물·보상 칸 — 결과 칸 프리팹을 무대 가운데에 (Create에서 호출)
    private SlotView CreateSlot(string name, SlotView prefab)
    {
        SlotView      slot = Instantiate(prefab, _layer, false);
        RectTransform rect = (RectTransform)slot.transform;

        slot.name             = name;
        rect.anchorMin        = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot            = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = StageCenter;
        slot.gameObject.SetActive(false);

        return slot;
    }

    // 금 한 장 — 유물 칸의 자식이라 흔들림·부풂을 함께 탄다. 'fillAmount'로 자란다 (Create에서 호출)
    private Image CreateCrack(int index)
    {
        Sprite? sprite = index switch
        {
            0 => FxSprites.AppraiseCrack1,
            1 => FxSprites.AppraiseCrack2,
            _ => FxSprites.AppraiseCrack3,
        };

        var go    = new GameObject($"Crack {index + 1}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var image = go.GetComponent<Image>();
        var rect  = image.rectTransform;

        rect.SetParent(_relic.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        image.sprite        = sprite!;
        image.raycastTarget = false;
        image.enabled       = sprite != null;
        image.type          = Image.Type.Filled;
        image.fillMethod    = CrackFills[index].Method;
        image.fillOrigin    = CrackFills[index].Origin;
        image.fillAmount    = 0f;

        return image;
    }

    // 띠 글자 — 결과 창 제목을 복제해 서체를 맞춘다. 테마 색 부품은 떼어 낸다(색은 이 연출이 정한다) (Create에서 호출)
    private TMP_Text CreateBanner(TMP_Text template)
    {
        TMP_Text      banner = Instantiate(template, _layer, false);
        RectTransform rect   = banner.rectTransform;

        banner.name = "Banner";

        foreach (MonoBehaviour extra in banner.GetComponents<MonoBehaviour>())
        {
            if (extra is not TMP_Text)
            {
                Destroy(extra);
            }
        }

        rect.anchorMin          = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot              = new Vector2(0.5f, 0.5f);
        rect.sizeDelta          = new Vector2(900f, 80f);
        banner.alignment        = TextAlignmentOptions.Center;
        banner.fontSize         = template.fontSize * BannerScale;
        banner.raycastTarget    = false;
        banner.alpha            = 0f;

        return banner;
    }

    #endregion

    // 이 연출의 트윈으로 묶는다 — 'Stop'이 한꺼번에 끊는다 · 층이 꺼지면 함께 죽는다
    private T Track<T>(T tween) where T : Tween
    {
        tween.SetId(this).SetLink(gameObject, LinkBehaviour.KillOnDisable);

        return tween;
    }

    private static void ResetTransform(Transform target, float scale)
    {
        target.localRotation = Quaternion.identity;
        target.localScale    = Vector3.one * scale;
    }

    private static Color Transparent(Color color) => WithAlpha(color, 0f);

    private static Color WithAlpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);
}
