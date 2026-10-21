using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 작업슬롯 해금 연출 — 칸 가운데에 자물쇠가 나타나 흔들리다 고리가 들리며 열리고, 금빛이 터지며 칸이 한 번 튄다 (T-039).
// 목록('WorkStationListPresenter')이 처음 해금할 때 칸 **프레임**에 코드로 붙인다 — 프레임 프리팹('WorkSlotFrame')을 건드리지 않는다.
//
// ■ 흐름 (약 1.4초)
//   0.00 자물쇠가 튀어나오고 프레임 라벨(잠김 조건 → "비어있음.")은 흐려진다
//   0.25 자물쇠가 좌우로 흔들린다
//   0.55 고리가 들리며 왼쪽 다리를 축으로 돈다 — 열렸다
//   0.70 번짐·별 조각이 터지고 빛 줄이 칸을 훑는다 · 프레임이 한 번 튄다 · 자물쇠는 떠오르며 사라진다
//   1.05 라벨이 다시 나타난다
//
// ■ 그림은 'FxSprites'에서 — 자물쇠 'unlock_body'·'unlock_shackle', 번짐·별은 gain_*, 빛 줄은 reveal_shine
//   그림이 없으면 그 효과만 빠진다(코드로 그리지 않는다). 프레임 튐·라벨 페이드는 그림 없이도 돈다.
//
// ■ 내가 연 해금에서만 재생한다 — 치트·퀘스트로 서버가 연 해금은 조용히 바뀐다(해금 문구와 같은 규칙).
//
// ■ 수명
//   Sequence 하나를 프레임에 묶는다(KillOnDisable — 목록 화면이 꺼지면 끊긴다). 끊겨도 'OnKill'이 그림을 끄고 라벨·크기를 되돌린다.
public class SlotUnlockFx : MonoBehaviour
{
    // 금빛 — 레벨업 연출('LevelUpFx')과 같은 색
    private static readonly Color Gold      = new Color(1f, 0.84f, 0.32f, 1f);
    private static readonly Color GoldLight = new Color(1f, 0.95f, 0.7f, 1f);

    // 자물쇠 크기 (칸 높이 대비)
    private const float LockSize = 0.55f;

    // 고리가 도는 축 — 그림 안 왼쪽 다리 밑동 ('make_fx.py'의 unlock — 반지름 0.36 · 다리 끝 v = -0.05)
    private static readonly Vector2 ShacklePivot = new Vector2(0.32f, 0.475f);

    // 고리가 들리는 높이 (자물쇠 크기 대비) · 도는 각도
    private const float ShackleLift  = 0.14f;
    private const float ShackleAngle = 28f;

    // 터지는 때 (초)
    private const float BurstAt = 0.7f;

    // 번짐 크기 (칸 높이 대비) · 별 조각 개수 · 크기 · 퍼지는 거리 (칸 높이 대비)
    private const float GlowSize      = 1.6f;
    private const int   SparkCount    = 8;
    private const float SparkSize     = 0.18f;
    private const float SparkDistance = 0.7f;

    // 빛 줄 — 굵기 · 길이 (칸 대비) · 기울기 ('SlotGainShine'과 같은 기울기)
    private const float ShineWidthRatio  = 0.25f;
    private const float ShineHeightRatio = 1.8f;
    private const float ShineAngle       = 20f;

    // 프레임 튐 세기
    private const float PunchScale = 0.06f;

    private RectTransform _rect    = null!;
    private RectMask2D    _clip    = null!; // 빛 줄만 칸 안에서 자른다 — 번짐·별은 칸 밖까지 퍼진다
    private Image         _glow    = null!;
    private Image         _shine   = null!;
    private RectTransform _lock    = null!;
    private Image         _body    = null!;
    private Image         _shackle = null!;
    private Image[]       _sparks  = null!;

    private TMP_Text? _label;
    private Sequence? _seq;

    // 프레임에 붙이고 층을 만든다 (프레임마다 처음 해금할 때 한 번).
    //   label : 프레임 라벨(잠김 조건 · "비어있음.") — 자물쇠가 열리는 동안 흐려진다. 없으면 null
    public static SlotUnlockFx Attach(GameObject frame, TMP_Text? label)
    {
        var fx = frame.AddComponent<SlotUnlockFx>();

        fx._label = label;
        fx.Build();

        return fx;
    }

    // 꺼졌던 프레임이 다시 켜졌다 — 도중에 끊긴 연출의 그림이 남지 않게 (Unity 메시지)
    private void OnEnable()
    {
        if (_glow != null)
        {
            Restore();
        }
    }

    // 한 번 재생한다 (목록의 해금 성공에서 호출)
    public void Play()
    {
        Stop();

        Vector2 size = _rect.rect.size;

        if (size.x <= 0f || size.y <= 0f)
        {
            return; // 레이아웃이 아직 안 잡힌 칸
        }

        float h = size.y;

        _seq = DOTween.Sequence().SetLink(gameObject, LinkBehaviour.KillOnDisable);

        AddLabelFade();
        AddLock(h);
        AddGlow(h);
        AddSparks(h);
        AddShine(size);
        AddPunch();

        _seq.OnKill(Restore);
    }

    // 끊고 그림을 끈다
    public void Stop()
    {
        if (_seq != null && _seq.IsActive())
        {
            _seq.Kill(); // OnKill → Restore
        }

        _seq = null;

        Restore();
    }

    // 라벨 — 처음에 흐려졌다가 자물쇠가 사라진 뒤 다시 나타난다
    private void AddLabelFade()
    {
        if (_label == null)
        {
            return;
        }

        TMP_Text label = _label;

        _seq!.Insert(0f, DOTween.To(() => label.alpha, a => label.alpha = a, 0f, 0.15f));
        _seq.Insert(BurstAt + 0.35f, DOTween.To(() => label.alpha, a => label.alpha = a, 1f, 0.35f));
    }

    // 자물쇠 — 튀어나와 흔들리고, 고리가 들리며 열린 뒤 떠오르며 사라진다
    private void AddLock(float h)
    {
        bool hasBody    = _body.sprite != null;
        bool hasShackle = _shackle.sprite != null;

        if (!hasBody && !hasShackle)
        {
            return;
        }

        float side = h * LockSize;

        _lock.sizeDelta        = Vector2.one * side;
        _lock.anchoredPosition = Vector2.zero;
        _lock.localScale       = Vector3.one * 0.4f;
        _lock.localRotation    = Quaternion.identity;

        _body.enabled    = hasBody;
        _shackle.enabled = hasShackle;
        _body.color      = WithAlpha(GoldLight, 0f);
        _shackle.color   = WithAlpha(GoldLight, 0f);

        RectTransform shackle = _shackle.rectTransform;

        shackle.anchoredPosition = Vector2.zero;
        shackle.localRotation    = Quaternion.identity;

        _seq!.Insert(0f, _lock.DOScale(1f, 0.25f).SetEase(Ease.OutBack, 2f));
        _seq.Insert(0f, _body.DOFade(1f, 0.12f));
        _seq.Insert(0f, _shackle.DOFade(1f, 0.12f));

        // 흔들림 — 덜컥덜컥
        _seq.Insert(0.25f, _lock.DOPunchRotation(new Vector3(0f, 0f, 14f), 0.3f, 8, 0.5f));

        // 열림 — 들리고 돈다
        _seq.Insert(0.55f, shackle.DOAnchorPosY(side * ShackleLift, 0.12f).SetEase(Ease.OutQuad));
        _seq.Insert(0.62f, shackle.DOLocalRotate(new Vector3(0f, 0f, ShackleAngle), 0.15f).SetEase(Ease.OutBack));

        // 사라짐 — 터질 때 금색으로 번쩍이며 떠오른다
        _seq.Insert(BurstAt, _body.DOColor(Gold, 0.1f));
        _seq.Insert(BurstAt, _shackle.DOColor(Gold, 0.1f));
        _seq.Insert(BurstAt + 0.05f, _lock.DOScale(1.25f, 0.35f).SetEase(Ease.OutQuad));
        _seq.Insert(BurstAt + 0.05f, _lock.DOAnchorPosY(h * 0.15f, 0.35f).SetEase(Ease.OutQuad));
        _seq.Insert(BurstAt + 0.1f, _body.DOFade(0f, 0.3f));
        _seq.Insert(BurstAt + 0.1f, _shackle.DOFade(0f, 0.3f));
    }

    // 번짐 — 자물쇠 뒤에서 한 번 크게 부푼다
    private void AddGlow(float h)
    {
        if (_glow.sprite == null)
        {
            return;
        }

        RectTransform rect = _glow.rectTransform;

        _glow.enabled         = true;
        _glow.color           = WithAlpha(Gold, 0f);
        rect.sizeDelta        = Vector2.one * h * GlowSize;
        rect.localScale       = Vector3.one * 0.3f;

        // 자물쇠가 나올 때 살짝 → 터질 때 크게
        _seq!.Insert(0f, _glow.DOFade(0.35f, 0.2f));
        _seq.Insert(0f, rect.DOScale(0.5f, 0.25f));
        _seq.Insert(BurstAt, rect.DOScale(1f, 0.4f).SetEase(Ease.OutCubic));
        _seq.Insert(BurstAt, _glow.DOFade(0.9f, 0.08f));
        _seq.Insert(BurstAt + 0.15f, _glow.DOFade(0f, 0.5f).SetEase(Ease.InQuad));
    }

    // 별 조각 — 터질 때 둥글게 흩어진다
    private void AddSparks(float h)
    {
        for (int i = 0; i < _sparks.Length; i++)
        {
            Image spark = _sparks[i];

            if (spark.sprite == null)
            {
                continue;
            }

            RectTransform rect  = spark.rectTransform;
            float         angle = (i + 0.25f) / _sparks.Length * Mathf.PI * 2f;
            float         reach = h * SparkDistance * (i % 2 == 0 ? 1f : 0.65f);
            Vector2       to    = new Vector2(Mathf.Cos(angle) * 1.6f, Mathf.Sin(angle)) * reach; // 칸이 가로로 길어 옆으로 더 퍼진다

            spark.enabled         = true;
            spark.color           = GoldLight;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta        = Vector2.one * h * SparkSize;
            rect.localScale       = Vector3.zero;

            _seq!.Insert(BurstAt, rect.DOAnchorPos(to, 0.6f).SetEase(Ease.OutCubic));
            _seq.Insert(BurstAt, rect.DOScale(1f, 0.12f).SetEase(Ease.OutBack));
            _seq.Insert(BurstAt + 0.2f, rect.DOScale(0f, 0.4f).SetEase(Ease.InQuad));
            _seq.Insert(BurstAt, rect.DOLocalRotate(new Vector3(0f, 0f, 120f), 0.6f, RotateMode.FastBeyond360));
        }
    }

    // 빛 줄 — 터질 때 칸 왼쪽 밖에서 오른쪽 밖으로 훑는다
    //   기울어진 줄은 가로 폭이 넓어 칸 가장자리에 걸치면 모서리가 미리 보인다 — 기울어진 폭만큼 밖에서 출발하고, 터지기 전까지는 투명하다
    private void AddShine(Vector2 size)
    {
        if (_shine.sprite == null)
        {
            return;
        }

        RectTransform band   = _shine.rectTransform;
        Vector2       extent = new Vector2(size.y * ShineWidthRatio * 2f, size.y * ShineHeightRatio);
        float         rad    = ShineAngle * Mathf.Deg2Rad;
        float         halfW  = (extent.x * Mathf.Cos(rad) + extent.y * Mathf.Sin(rad)) * 0.5f; // 기울어진 줄의 가로 반폭
        float         edge   = size.x * 0.5f + halfW;

        _clip.enabled         = true;
        _shine.enabled        = true;
        _shine.color          = WithAlpha(GoldLight, 0f);
        band.sizeDelta        = extent;
        band.anchoredPosition = new Vector2(-edge, 0f);

        _seq!.Insert(BurstAt, _shine.DOFade(0.85f, 0.05f));
        _seq.Insert(BurstAt, band.DOAnchorPosX(edge, 0.45f).SetEase(Ease.InOutSine));
    }

    // 프레임 튐 — 열린 순간 칸 전체가 한 번 부푼다
    private void AddPunch()
    {
        _seq!.Insert(BurstAt, transform.DOPunchScale(Vector3.one * PunchScale, 0.35f, 5, 0.6f));
    }

    // 그림을 끄고 라벨·크기를 되돌린다 — 끝나도 끊겨도
    private void Restore()
    {
        _glow.enabled    = false;
        _shine.enabled   = false;
        _body.enabled    = false;
        _shackle.enabled = false;
        _clip.enabled    = false;

        foreach (Image spark in _sparks)
        {
            spark.enabled = false;
        }

        transform.localScale = Vector3.one;

        if (_label != null)
        {
            _label.alpha = 1f;
        }
    }

    // 프레임 위를 덮는 층 — 번짐 · 빛 줄(자르는 층 안) · 자물쇠 · 별 순서 (Attach에서 한 번)
    private void Build()
    {
        RectTransform root = CreateRect("Unlock Fx", transform);

        Stretch(root);
        root.SetAsLastSibling(); // 프레임의 모든 표시 위

        root.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

        _rect = root;

        _glow = CreateImage("Glow", root, FxSprites.GainGlow);

        RectTransform clip = CreateRect("Shine Clip", root);

        Stretch(clip);

        _clip  = clip.gameObject.AddComponent<RectMask2D>();
        _shine = CreateImage("Shine", clip, FxSprites.RevealShine);
        _shine.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -ShineAngle);

        _lock = CreateRect("Lock", root);

        // 고리 → 몸통 순서 — 다리 끝이 몸통 뒤로 들어간다
        _shackle = CreateImage("Shackle", _lock, FxSprites.UnlockShackle);
        Stretch(_shackle.rectTransform);
        _shackle.rectTransform.pivot = ShacklePivot;

        _body = CreateImage("Body", _lock, FxSprites.UnlockBody);
        Stretch(_body.rectTransform);

        _sparks = new Image[SparkCount];

        for (int i = 0; i < _sparks.Length; i++)
        {
            _sparks[i] = CreateImage("Spark", root, FxSprites.GainSparkle);
        }

        Restore();
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;

        return color;
    }

    // 부모를 꽉 채운다 — 피벗을 바꿔도 자리가 같게 오프셋으로 맞춘다
    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // 가운데 기준의 Image — raycast는 끈다 (프레임 클릭을 가로채지 않는다)
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
