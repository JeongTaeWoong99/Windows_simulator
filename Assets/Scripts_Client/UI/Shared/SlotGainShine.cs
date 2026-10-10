using DG.Tweening;
using GameData;
using UnityEngine;
using UnityEngine.UI;

// 칸 획득 반짝임 — 인벤토리 칸에 자원이 늘거나 새 개체(자원·캐릭터·장비)가 들어왔을 때 칸 위에서 한 번 번쩍인다.
// 격자('InventoryGridPresenter')가 칸을 처음 반짝일 때 코드로 붙인다 — 칸 프리팹('SlotView')은 가챠 결과와 공유라 손대지 않는다.
//
// ■ 가챠 공개 연출('RewardRevealFx')의 그림을 그대로 쓴다
//   번짐은 획득 연출의 'gain_glow'(등급색), 빛 줄은 공개 연출의 'reveal_shine' — 같은 그림이라 "얻었다"가 같은 말로 읽힌다.
//   공개 연출 컴포넌트를 통째로 붙이지 않는 이유 — 그쪽은 테두리 Mask·덮개·숨쉬기 루프까지 들고 있어
//   칸 수십 개가 동시에 들 무게가 아니다. 여기는 그림 두 장 + Sequence 하나다.
//   그림이 없으면 그 효과만 빠진다(코드로 그리지 않는다 — 'FxSprites').
//
// ■ 칸 안에서만 그린다
//   'RectMask2D'로 칸 밖을 자른다 — 격자 칸은 붙어 있어 번짐이 옆 칸을 덮으면 어느 칸이 들어왔는지 흐려진다.
//   층은 레이아웃에서 빠진다('LayoutElement.ignoreLayout') — 칸 안의 레이아웃 그룹이 자리를 밀지 않는다.
//
// ■ 쉬는 동안은 비용이 없다
//   다 끝나면 그림·마스크를 끈다('Hide') — 켜 둔 RectMask2D는 칸마다 자르기 계산이 돈다.
//
// ■ 수명
//   Sequence 하나를 칸에 묶는다(KillOnDisable — 칸은 풀로 꺼졌다 켜진다). 끊기면 'OnKill'이 그림을 끈다.
//   격자가 칸을 다른 개체로 다시 묶거나 끌 때는 'Stop'을 부른다.
public class SlotGainShine : MonoBehaviour
{
    // 전체 길이 (초) — 수확은 칸마다 몇 초에 한 번 나므로 다음 수확 전에 끝난다
    private const float Duration = 0.6f;

    // 번짐이 다 켜지는 때 (초)
    private const float GlowIn = 0.12f;

    // 번짐 진하기 · 처음 크기 · 끝 크기 (칸 대비)
    private const float GlowAlpha      = 0.85f;
    private const float GlowStartScale = 0.6f;
    private const float GlowEndScale   = 1.25f;

    // 번짐 색을 등급색에서 흰색 쪽으로 당기는 정도 — 어두운 등급색도 빛으로 보이게 ('ItemGainFx'와 같은 값)
    private const float GlowWhiten = 0.2f;

    // 빛 줄 — 시작 때 · 지나가는 시간 (초) · 굵기 · 길이 (칸 대비) · 기울기 ('RewardRevealFx'와 같은 기울기)
    private const float ShineDelay       = 0.05f;
    private const float ShineDuration    = 0.4f;
    private const float ShineWidthRatio  = 0.3f;
    private const float ShineHeightRatio = 1.8f;
    private const float ShineAngle       = 20f;
    private const float ShineAlpha       = 0.9f;

    private RectTransform _rect  = null!;
    private RectMask2D    _clip  = null!;
    private Image         _glow  = null!;
    private Image         _shine = null!;

    private Sequence? _seq;

    // 지금 반짝이는 개체 — 같은 칸이 다른 개체로 다시 묶이면 격자가 끊는다
    public long Key { get; private set; } = -1;

    public bool IsPlaying => _seq != null && _seq.IsActive();

    // 칸에 붙이고 층을 만든다 (격자가 칸마다 한 번)
    public static SlotGainShine Attach(GameObject slot)
    {
        var shine = slot.AddComponent<SlotGainShine>();

        shine.Build();

        return shine;
    }

    // 꺼졌던 칸이 다시 켜졌다 — 도중에 끊긴 연출의 그림이 남지 않게 (Unity 메시지)
    private void OnEnable()
    {
        if (_glow != null)
        {
            Hide();
        }
    }

    // 한 번 반짝인다. 이미 반짝이는 중이면 처음부터 다시 — 연달아 들어와도 겹쳐 쌓이지 않는다 (격자의 DrawCell에서 호출).
    //   delay : 한꺼번에 여러 칸이 들어왔을 때 칸 순서대로 물결지게 하는 지연 (초)
    public void Play(long key, GlobalRarity rarity, float delay)
    {
        Stop();

        Key = key;

        Vector2 size = _rect.rect.size;

        if (size.x <= 0f || size.y <= 0f)
        {
            return; // 레이아웃이 아직 안 잡힌 칸 — 그릴 자리가 없다
        }

        bool hasGlow  = _glow.sprite != null;
        bool hasShine = _shine.sprite != null;

        if (!hasGlow && !hasShine)
        {
            return;
        }

        _clip.enabled = true;
        _seq          = DOTween.Sequence().SetLink(gameObject, LinkBehaviour.KillOnDisable).SetDelay(delay);

        if (hasGlow)
        {
            Color color = Color.Lerp(RarityPalette.Get(rarity), Color.white, GlowWhiten);

            color.a = 0f;

            RectTransform glow = _glow.rectTransform;

            _glow.enabled    = true;
            _glow.color      = color;
            glow.localScale  = Vector3.one * GlowStartScale;

            _seq.Insert(0f, glow.DOScale(GlowEndScale, Duration).SetEase(Ease.OutCubic));
            _seq.Insert(0f, _glow.DOFade(GlowAlpha, GlowIn));
            _seq.Insert(GlowIn, _glow.DOFade(0f, Duration - GlowIn).SetEase(Ease.InQuad));
        }

        if (hasShine)
        {
            RectTransform band = _shine.rectTransform;

            _shine.enabled        = true;
            _shine.color          = new Color(1f, 1f, 1f, ShineAlpha);
            band.sizeDelta        = new Vector2(size.x * ShineWidthRatio, size.y * ShineHeightRatio);
            band.anchoredPosition = new Vector2(-size.x, 0f);

            _seq.Insert(ShineDelay, band.DOAnchorPosX(size.x, ShineDuration).SetEase(Ease.InOutSine));
        }

        // 끝나도 끊겨도 그림을 끈다 — 완료 뒤 자동 Kill에서도 불린다
        _seq.OnKill(Hide);
    }

    // 끊고 그림을 끈다 (격자 — 칸을 끌 때 · 다른 개체로 다시 묶을 때)
    public void Stop()
    {
        if (_seq != null && _seq.IsActive())
        {
            _seq.Kill(); // OnKill → Hide
        }

        _seq = null;
        Key  = -1;

        Hide();
    }

    private void Hide()
    {
        _glow.enabled  = false;
        _shine.enabled = false;
        _clip.enabled  = false;
    }

    // 칸 위를 덮는 층 하나 — 번짐 · 빛 줄 순서 (Attach에서 한 번)
    private void Build()
    {
        RectTransform root = CreateRect("Gain Shine", transform);

        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.sizeDelta = Vector2.zero;
        root.SetAsLastSibling(); // 칸의 모든 표시 위

        root.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

        _rect = root;
        _clip = root.gameObject.AddComponent<RectMask2D>();

        _glow = CreateImage("Glow", root, FxSprites.GainGlow);

        // 번짐은 칸을 꽉 채운 크기에서 커졌다 작아진다
        RectTransform glow = _glow.rectTransform;

        glow.anchorMin = Vector2.zero;
        glow.anchorMax = Vector2.one;
        glow.sizeDelta = Vector2.zero;

        _shine = CreateImage("Shine", root, FxSprites.RevealShine);
        _shine.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -ShineAngle);

        Hide();
    }

    // 가운데 기준의 Image — raycast는 끈다 (칸 클릭·우클릭·끌기를 가로채지 않는다)
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
