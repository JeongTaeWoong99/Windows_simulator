using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// Special 칸 — 무지개 테두리가 돌고, 빛줄기가 스치고, 반짝임 4개가 엇갈려 깜빡인다 (목업 '유물감정목업.html' · T-151).
// 'SlotView'가 Special 등급을 그릴 때 처음 한 번 코드로 붙인다 — 칸 프리팹은 손대지 않는다(인벤토리·가챠·도감이 함께 쓴다).
//
// ■ 칸 안에 만드는 것 (처음 켤 때 한 번)
//   Special Frame — 테두리 띠 모양 'Mask' 안에서 무지개 원뿔이 돈다 → 테두리를 따라 무지개가 흐른다. 등급 배경 바로 위 · 아이콘 아래
//   Special Shine — 칸 안을 사선으로 훑는 빛 줄 ('RectMask2D'로 칸 밖은 잘린다). 아이콘 위
//   Special Spark — 네 갈래 별 4개. 아이콘 위
// 전부 raycast를 끈다 — 칸 클릭을 가로채지 않는다.
// 그림은 'FxSprites'(special_rainbow · reveal_frame · reveal_shine · reveal_sparkle) — 없는 그림의 효과는 만들지 않는다.
//
// ■ 수명
//   반복 트윈은 이 오브젝트에 묶는다(KillOnDisable). 칸이 꺼졌다 켜지면('OnEnable') 켜 둔 상태대로 다시 돈다.
//   ※ 칸이 풀로 돌아가 다른 등급을 그리면 'SlotView'가 'SetOn(false)'로 끈다.
public class SpecialSlotFx : MonoBehaviour
{
    // 테두리 한 바퀴 · 빛줄기 한 번 · 반짝임 한 번 (초) — 목업 3 · 2.4 · 1.8
    private const float TurnDuration  = 3f;
    private const float ShineDuration = 2.4f;
    private const float SparkDuration = 1.8f;

    // 도는 원뿔이 테두리 띠보다 밖으로 나오는 너비 (칸 대비) — 돌 때 모서리가 비지 않게 대각선보다 크게
    private const float ConicOverhangRatio = 0.3f;

    // 테두리 띠가 칸 밖으로 나오는 너비 (px) — 0이면 등급 배경 띠와 겹친다
    private const float FrameOutset = 0f;

    // 빛 줄 — 굵기(칸 대비) · 칸 높이 대비 길이 · 기울기 · 진하기
    private const float ShineWidthRatio  = 0.26f;
    private const float ShineHeightRatio = 1.8f;
    private const float ShineAngle       = 25f;
    private const float ShineAlpha       = 0.45f;

    // 반짝임 크기 (칸 대비)
    private const float SparkSizeRatio = 0.16f;

    // 반짝임 자리(칸 왼쪽 아래 0 ~ 오른쪽 위 1) · 시작 지연(초) — 목업의 네 모서리 근처 엇갈림
    private static readonly Vector2[] SparkPoints = { new(0.12f, 0.88f), new(0.86f, 0.9f), new(0.88f, 0.16f), new(0.1f, 0.2f) };
    private static readonly float[]   SparkDelays = { 0f, 0.5f, 1f, 1.4f };

    private RectTransform  _rect   = null!;
    private RectTransform? _conic;          // 무지개·테두리 띠 그림이 둘 다 있을 때만 만든다
    private GameObject?    _frame;
    private RectTransform? _shine;          // 빛 줄 그림이 있을 때만
    private GameObject?    _shineClip;
    private Image[]        _sparks = System.Array.Empty<Image>();

    private bool _isBuilt;
    private bool _isOn;

    // 테두리 · 빛줄기 · 반짝임은 주기가 달라 한 Sequence로 묶지 않고 따로 든다
    private Tween?    _turn;
    private Tween?    _sweep;
    private Tween[]   _twinkles = System.Array.Empty<Tween>();

    // 켜고 끈다 — Special 등급이고 흐리게(딤·흑백) 그리지 않을 때만 켠다 ('SlotView.ApplyTint'가 호출).
    // rarityLayer : 등급 배경 — 테두리를 그 바로 위에 끼운다 · iconLayer : 아이콘 — 빛 줄·반짝임을 그 바로 위에 끼운다
    public void SetOn(bool on, Transform rarityLayer, Transform iconLayer)
    {
        if (_isOn == on)
        {
            return;
        }

        _isOn = on;

        if (on && !_isBuilt)
        {
            Build(rarityLayer, iconLayer);
        }

        Apply();
    }

    // 칸이 다시 켜졌다 — 꺼질 때 끊긴 반복을 켜 둔 상태대로 다시 건다 (Unity 메시지)
    private void OnEnable()
    {
        if (_isBuilt)
        {
            Apply();
        }
    }

    private void Build(Transform rarityLayer, Transform iconLayer)
    {
        _isBuilt = true;
        _rect    = (RectTransform)transform;

        Sprite? rainbow = FxSprites.SpecialRainbow;
        Sprite? frame   = FxSprites.RevealFrame;
        Sprite? shine   = FxSprites.RevealShine;
        Sprite? spark   = FxSprites.RevealSparkle;

        float size = Mathf.Min(_rect.rect.width, _rect.rect.height);

        // 테두리 띠는 마스크 모양이다 — 그림 없이 만들면 칸 전체가 마스크가 돼 무지개가 칸을 덮는다. 둘 다 있어야 만든다
        if (frame != null && rainbow != null)
        {
            Image band = CreateImage("Special Frame", _rect, frame, FrameOutset);

            band.type = Image.Type.Sliced;
            band.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            band.transform.SetSiblingIndex(rarityLayer.GetSiblingIndex() + 1);

            _frame = band.gameObject;
            _conic = CreateImage("Rainbow", band.rectTransform, rainbow, size * ConicOverhangRatio).rectTransform;
        }

        int above = iconLayer.GetSiblingIndex() + 1;

        if (shine != null)
        {
            _shineClip = new GameObject("Special Shine", typeof(RectTransform), typeof(RectMask2D));

            Stretch((RectTransform)_shineClip.transform, _rect, 0f);
            _shineClip.transform.SetSiblingIndex(above++);

            Image band = CreateImage("Band", (RectTransform)_shineClip.transform, shine, 0f);

            band.color = new Color(1f, 1f, 1f, ShineAlpha);

            _shine               = band.rectTransform;
            _shine.anchorMin     = _shine.anchorMax = new Vector2(0.5f, 0.5f);
            _shine.sizeDelta     = new Vector2(size * ShineWidthRatio, _rect.rect.height * ShineHeightRatio);
            _shine.localRotation = Quaternion.Euler(0f, 0f, -ShineAngle);
        }

        if (spark != null)
        {
            _sparks = new Image[SparkPoints.Length];

            for (int i = 0; i < SparkPoints.Length; i++)
            {
                Image image = CreateImage("Special Spark", _rect, spark, 0f);
                RectTransform rt = image.rectTransform;

                rt.anchorMin = rt.anchorMax = SparkPoints[i];
                rt.sizeDelta = Vector2.one * (size * SparkSizeRatio);
                rt.SetSiblingIndex(above++);

                _sparks[i] = image;
            }
        }
    }

    // 지금 상태대로 층을 켜고 반복을 다시 건다 (SetOn · OnEnable에서 호출)
    private void Apply()
    {
        StopLoops();

        _frame?.SetActive(_isOn);
        _shineClip?.SetActive(_isOn);

        foreach (Image spark in _sparks)
        {
            spark.gameObject.SetActive(_isOn);
        }

        if (!_isOn || !isActiveAndEnabled)
        {
            return;
        }

        if (_conic != null)
        {
            _conic.localRotation = Quaternion.identity;
            _turn = _conic.DORotate(new Vector3(0f, 0f, -360f), TurnDuration, RotateMode.FastBeyond360)
                          .SetEase(Ease.Linear).SetLoops(-1, LoopType.Restart)
                          .SetLink(gameObject, LinkBehaviour.KillOnDisable);
        }

        if (_shine != null)
        {
            float width = _rect.rect.width;

            _shine.anchoredPosition = new Vector2(-width, 0f);
            _sweep = _shine.DOAnchorPosX(width, ShineDuration)
                           .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Restart)
                           .SetLink(gameObject, LinkBehaviour.KillOnDisable);
        }

        _twinkles = new Tween[_sparks.Length];

        for (int i = 0; i < _sparks.Length; i++)
        {
            RectTransform rt = _sparks[i].rectTransform;

            rt.localScale = Vector3.zero;

            // 커졌다 작아지며 반 바퀴 돌고 쉰다 — 주기는 넷이 같고 시작만 엇갈린다(SetDelay는 첫 바퀴에만 걸린다)
            _twinkles[i] = DOTween.Sequence()
                                  .Insert(0f, rt.DOScale(1f, SparkDuration * 0.25f).SetEase(Ease.OutQuad))
                                  .Insert(0f, rt.DOLocalRotate(new Vector3(0f, 0f, 90f), SparkDuration * 0.5f, RotateMode.FastBeyond360))
                                  .Insert(SparkDuration * 0.25f, rt.DOScale(0f, SparkDuration * 0.25f).SetEase(Ease.InQuad))
                                  .AppendInterval(SparkDuration * 0.5f)
                                  .SetDelay(SparkDelays[i])
                                  .SetLoops(-1, LoopType.Restart)
                                  .SetLink(gameObject, LinkBehaviour.KillOnDisable);
        }
    }

    private void StopLoops()
    {
        _turn?.Kill();
        _sweep?.Kill();

        foreach (Tween tween in _twinkles)
        {
            tween?.Kill();
        }

        _turn     = null;
        _sweep    = null;
        _twinkles = System.Array.Empty<Tween>();
    }

    // 연출용 Image를 만든다 — 부모를 꽉 채우고 'outset'만큼 밖으로 나온다. raycast는 끈다
    private static Image CreateImage(string name, RectTransform parent, Sprite sprite, float outset)
    {
        var go    = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var image = go.GetComponent<Image>();

        Stretch(image.rectTransform, parent, outset);

        image.raycastTarget = false;
        image.sprite        = sprite;

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
