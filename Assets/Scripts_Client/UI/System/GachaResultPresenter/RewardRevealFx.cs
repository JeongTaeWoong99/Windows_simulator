using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using GameData;
using UnityEngine;
using UnityEngine.UI;

// 보상 칸 하나의 공개 연출 — 튀어나오기 · 고등급의 차오르기 · 공개 뒤 강조.
// 'GachaResultPresenter'가 결과 칸을 만들 때 코드로 붙인다 — 칸 프리팹('SlotView')은 인벤토리와 공유라 손대지 않는다.
// 무엇을 언제 부를지(순서·간격·폭발)는 Presenter가 정한다. 여기는 칸 하나를 어떻게 움직일지만 안다.
//
// ■ 칸 안에 만드는 것 (Init에서 한 번)
//   Reveal Glow  — 칸 뒤 번짐 (맨 뒤). 영웅↑은 켜 두고, 전설↑은 숨쉬듯 옅어졌다 진해진다
//   Reveal Frame — 테두리 띠 모양 'Mask' 안에서 원뿔 그라데이션이 돈다 → 테두리를 따라 빛이 흐른다 (영웅↑)
//   Reveal Cover — 차오르는 동안 칸을 덮는 등급색 덮개
//   Reveal Shine — 칸 안을 사선으로 훑는 빛 줄 ('RectMask2D'로 칸 밖은 잘린다)
//   Reveal Flash — 나타나는 순간의 흰 번쩍 (맨 앞)
// 전부 raycast를 끈다 — 칸 클릭(= 스킵)을 가로채지 않는다.
//
// ■ 크기·기울기만 움직인다 — 자리는 건드리지 않는다
// 칸의 자리는 격자('FlexibleGridLayoutGroup')가 정한다. 위치를 흔들면 다음 레이아웃 갱신 때 튄다.
// 크기(localScale)·회전은 레이아웃이 보지 않는다.
//
// ■ 수명
// 움직임은 Sequence 하나('_motion')로 만들어 await한다. 강조의 반복 트윈은 따로 들고 있다가 'Hide'에서 끊는다.
// ⚠️ 끊을 때는 부르는 쪽이 토큰을 먼저 취소한다 — 기다리던 Sequence는 UniTask가 Kill하고('KillAndCancelAwait'),
//    여기서 다시 Kill하지 않는다(dotween 스킬 2장 함정). '_motion'은 살아 있을 때만 Kill한다.
public class RewardRevealFx : MonoBehaviour
{
    // 도는 원뿔이 테두리 띠보다 밖으로 나오는 너비 (px) — 돌 때 모서리가 비지 않게 대각선보다 크게
    private const float ConicOverhang = 30f;

    // 빛 줄 — 굵기와 칸 높이 대비 길이 · 기울기
    private const float ShineWidth       = 26f;
    private const float ShineHeightRatio = 1.8f;
    private const float ShineAngle       = 20f;

    // 차오르기 시작할 때 덮개의 밝기 (등급색 배수)
    private const float CoverStartBrightness = 0.3f;

    // 도는 테두리 빛을 등급색에서 흰색 쪽으로 당기는 정도 — 어두운 등급색(파랑·보라)도 빛으로 보이게
    private const float FrameWhiten = 0.6f;

    private RewardRevealSettings _settings = null!;
    private RectTransform        _rect     = null!;
    private CanvasGroup          _group    = null!;

    private Image         _glow   = null!;
    private GameObject    _frame  = null!;
    private Image         _conic  = null!;
    private Image         _cover  = null!;
    private RectTransform _shine  = null!;
    private Image         _flash  = null!;

    private Sequence? _motion;   // 지금 도는 튀어나오기·차오르기
    private Tween?    _glowLoop; // 전설↑ 번짐 숨쉬기
    private Tween?    _turnLoop; // 전설↑ 테두리 빛 돌기

    // 칸 안에 연출 층을 만들고 숨긴다 — 칸을 만든 직후 한 번 ('GachaResultPresenter'가 호출)
    public void Init(RewardRevealSettings settings)
    {
        _settings = settings;
        _rect     = (RectTransform)transform;

        if (!TryGetComponent(out _group))
        {
            _group = gameObject.AddComponent<CanvasGroup>();
        }

        _glow = CreateImage("Reveal Glow", _rect, RevealSprites.SoftGlow, settings.GlowSpread);
        _glow.transform.SetAsFirstSibling(); // 등급 바탕보다 뒤 — 칸 밖으로 번진 부분만 보인다

        Image frame = CreateImage("Reveal Frame", _rect, RevealSprites.Frame, settings.FrameOutset);

        frame.type = Image.Type.Sliced;
        frame.gameObject.AddComponent<Mask>().showMaskGraphic = false;

        _frame = frame.gameObject;
        _conic = CreateImage("Conic", frame.rectTransform, RevealSprites.Conic, ConicOverhang);

        _cover = CreateImage("Reveal Cover", _rect, null, 0f);

        var shineClip = new GameObject("Reveal Shine", typeof(RectTransform), typeof(RectMask2D));

        Stretch((RectTransform)shineClip.transform, _rect, 0f);

        Image band = CreateImage("Band", (RectTransform)shineClip.transform, RevealSprites.ShineBand, 0f);

        _shine           = band.rectTransform;
        _shine.anchorMin = _shine.anchorMax = new Vector2(0.5f, 0.5f);
        _shine.localRotation = Quaternion.Euler(0f, 0f, -ShineAngle);

        _flash = CreateImage("Reveal Flash", _rect, null, 0f);

        Hide();
    }

    // 튀어나오기 대기 상태 — 칸은 자리를 지키되 보이지 않는다 (결과를 그릴 때 · 창을 닫을 때 Presenter가 호출)
    public void Hide()
    {
        StopAll();

        _group.alpha = 0f;
    }

    // 끝 모습으로 바로 놓는다 — 제 크기 · 덮개 없음 · 등급 강조 (스킵 · 튀어나오기가 끝났을 때)
    public void ShowAtRest(GlobalRarity rarity)
    {
        StopAll();

        _group.alpha = 1f;

        SetHighlight(rarity);
    }

    // 튀어나오기 — 크게 나타나 제 크기로 줄며 번쩍 · 빛 줄이 지나가고, 끝나면 등급 강조를 켠다
    // (Presenter의 공개 순서가 호출). 고등급은 더 크게·더 길게 — 차오르기('ChargeAsync') 뒤라 덮개가 함께 걷힌다.
    public async UniTask PopAsync(GlobalRarity rarity, CancellationToken ct)
    {
        _motion = BuildPop(rarity);

        await _motion.ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, ct);

        ShowAtRest(rarity);
    }

    // 차오르기 — 등급색 덮개가 어둡게 나타나 밝아지며 칸이 커지고 떤다 (고등급 공개 직전, Presenter가 호출)
    public async UniTask ChargeAsync(GlobalRarity rarity, CancellationToken ct)
    {
        _motion = BuildCharge(rarity);

        await _motion.ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, ct);

        _rect.localRotation = Quaternion.identity;
    }

    // 튀어나오기의 시작 모습을 놓고 Sequence를 만든다 (PopAsync에서 호출).
    // ※ async 밖에서 만든다 — 트윈은 await할 수 있는 값이라 async 안에서 'Insert'의 반환을 버리면 CS4014가 난다.
    private Sequence BuildPop(GlobalRarity rarity)
    {
        bool  isBig    = RewardRevealSettings.IsBig(rarity);
        float scale    = isBig ? _settings.BigPopScale : _settings.PopScale;
        float duration = isBig ? _settings.BigPopDuration : _settings.PopDuration;
        float width    = _rect.rect.width;

        KillMotion();

        _rect.localScale    = Vector3.one * scale;
        _rect.localRotation = Quaternion.identity;
        _flash.enabled      = true;
        _flash.color        = new Color(1f, 1f, 1f, _settings.FlashAlpha);

        _shine.gameObject.SetActive(true);
        _shine.sizeDelta        = new Vector2(ShineWidth, _rect.rect.height * ShineHeightRatio);
        _shine.anchoredPosition = new Vector2(-width, 0f);

        Sequence seq = DOTween.Sequence().SetLink(gameObject, LinkBehaviour.KillOnDisable);

        seq.Insert(0f, _rect.DOScale(1f, duration).SetEase(_settings.PopEase));
        seq.Insert(0f, _flash.DOFade(0f, _settings.FlashDuration).SetEase(Ease.OutQuad));
        seq.Insert(_settings.ShineDelay, _shine.DOAnchorPosX(width, _settings.ShineDuration).SetEase(Ease.InOutSine));

        // 덮개가 있으면(고등급) 이미 보이는 칸이다 — 덮개만 걷는다. 없으면 칸째 빠르게 나타난다
        if (_cover.enabled)
        {
            seq.Insert(0f, _cover.DOFade(0f, _settings.FlashDuration));
        }
        else
        {
            seq.Insert(0f, _group.DOFade(1f, duration * 0.3f));
        }

        return seq;
    }

    // 차오르기의 시작 모습을 놓고 Sequence를 만든다 (ChargeAsync에서 호출 — async 밖인 이유는 BuildPop과 같다)
    private Sequence BuildCharge(GlobalRarity rarity)
    {
        Color color    = RarityPalette.Get(rarity);
        float duration = RewardRevealSettings.IsTop(rarity) ? _settings.TopChargeDuration : _settings.EpicChargeDuration;

        KillMotion();

        _group.alpha   = 1f;
        _cover.enabled = true;
        _cover.color   = new Color(color.r * CoverStartBrightness, color.g * CoverStartBrightness, color.b * CoverStartBrightness, 1f);
        _glow.enabled  = true;
        _glow.color    = new Color(color.r, color.g, color.b, 0f);

        Sequence seq = DOTween.Sequence().SetLink(gameObject, LinkBehaviour.KillOnDisable);

        seq.Insert(0f, _cover.DOColor(color, duration).SetEase(Ease.InQuad));
        seq.Insert(0f, _glow.DOFade(_settings.GlowAlpha, duration).SetEase(Ease.InQuad));
        seq.Insert(0f, _rect.DOScale(_settings.ChargeScale, duration).SetEase(Ease.InQuad));
        seq.Insert(0f, _rect.DOShakeRotation(duration, new Vector3(0f, 0f, _settings.ChargeShakeAngle), 25, 90f, false));

        return seq;
    }

    // 공개 뒤 강조 — 영웅↑은 번짐 + 도는 테두리 빛, 전설↑은 번짐이 숨쉰다 (ShowAtRest에서 호출).
    //
    // ※ 테두리 빛을 영웅에도 준다 — 칸 사이가 5px라 칸 뒤 번짐은 옆 칸에 가려 거의 안 보인다(2026-10-09 실측).
    //   영웅과 전설↑은 숨쉬는 번짐(창 가장자리 칸에서 보인다) · 등급색으로 가른다.
    private void SetHighlight(GlobalRarity rarity)
    {
        bool isBig = RewardRevealSettings.IsBig(rarity);
        bool isTop = RewardRevealSettings.IsTop(rarity);

        Color color = RarityPalette.Get(rarity);

        _glow.enabled = isBig;
        _glow.color   = new Color(color.r, color.g, color.b, _settings.GlowAlpha);

        _frame.SetActive(isBig);

        if (!isBig)
        {
            return;
        }

        _conic.color = Color.Lerp(color, Color.white, FrameWhiten);
        _conic.rectTransform.localRotation = Quaternion.identity;

        _turnLoop = _conic.rectTransform.DORotate(new Vector3(0f, 0f, -360f), _settings.FrameTurnDuration, RotateMode.FastBeyond360)
                          .SetEase(Ease.Linear).SetLoops(-1, LoopType.Restart)
                          .SetLink(gameObject, LinkBehaviour.KillOnDisable);

        if (isTop)
        {
            _glowLoop = _glow.DOFade(_settings.GlowPulseMin, _settings.GlowPulseDuration)
                             .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo)
                             .SetLink(gameObject, LinkBehaviour.KillOnDisable);
        }
    }

    // 도는 트윈을 모두 끊고 연출 층을 걷는다 — 칸은 제 크기·제 기울기로 (Hide · ShowAtRest에서 호출)
    private void StopAll()
    {
        KillMotion();

        _glowLoop?.Kill();
        _turnLoop?.Kill();
        _glowLoop = null;
        _turnLoop = null;

        _rect.localScale    = Vector3.one;
        _rect.localRotation = Quaternion.identity;
        _glow.enabled       = false;
        _cover.enabled      = false;
        _flash.enabled      = false;

        _frame.SetActive(false);
        _shine.gameObject.SetActive(false);
    }

    // 도는 움직임을 끊는다 — 이미 끝났거나 UniTask가 Kill했으면 건드리지 않는다
    private void KillMotion()
    {
        if (_motion != null && _motion.IsActive())
        {
            _motion.Kill();
        }

        _motion = null;
    }

    // 연출용 Image를 만든다 — 부모를 꽉 채우고 'outset'만큼 밖으로 나온다. raycast는 끈다
    private static Image CreateImage(string name, RectTransform parent, Sprite? sprite, float outset)
    {
        var go    = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var image = go.GetComponent<Image>();

        Stretch(image.rectTransform, parent, outset);

        image.raycastTarget = false;

        // 그림이 없으면 Image가 흰 네모로 그린다 — 번쩍·덮개는 그것으로 충분하다
        if (sprite != null)
        {
            image.sprite = sprite;
        }

        return image;
    }

    // 부모를 꽉 채우게 놓는다 — 'outset'이 양수면 그만큼 밖으로 나온다
    private static void Stretch(RectTransform rect, RectTransform parent, float outset)
    {
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(-outset, -outset);
        rect.offsetMax = new Vector2(outset, outset);
    }
}
