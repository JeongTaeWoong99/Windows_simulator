using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// 골드 획득 동전 — 판매 등으로 골드를 받으면 그 자리에서 동전 몇 개가 튀어나와 상단 골드 아이콘으로 날아간다.
// 'ServerWaitManager.GoldGainRaised'를 구독한다. 골드 숫자는 상태바가 따로 굴린다('NumberRoll') — 여기는 그림만 날린다.
//
// ■ 왜 !Overlay Canvas인가
//   동전은 인벤토리 열에서 상태 캔버스까지 열을 건너 난다 — 열마다 캔버스가 따로라 어느 한 열 안에 두면 다른 열 뒤로 숨는다.
//
// ■ 그림이 없으면 아무것도 안 날린다 — 'FxSprites.CoinBody'(그림 저장소 'Art/fx/coin/'). 반짝('CoinSpark')만 없으면 반짝만 빠진다.
//
// ■ 막지 않는다 — 층에 Graphic이 없고 동전·반짝은 raycastTarget을 끈다. 날아가는 동안에도 아래를 그대로 누른다.
//
// ■ 한 번에 한 무리다 — 날아가는 중에 또 오면 앞 무리를 끊고 새로 날린다(끊긴 동전은 바로 숨는다).
//   동전 Image는 처음에 최대 수만큼 만들어 두고 돌려쓴다 — 판매마다 만들고 지우지 않는다.
public class CoinFlyPresenter : MonoBehaviour
{
    // 동전 수 — 기본 + 받은 골드의 자릿수, 상한 (100 G → 6개 · 1만 G → 8개 · 100만 G → 10개)
    private const int MinCoins = 4;
    private const int MaxCoins = 12;

    // 동전 크기 · 반짝 크기 (px)
    private const float CoinSize  = 26f;
    private const float SparkSize = 56f;

    // 튀어나옴 — 시간 · 퍼지는 거리 (px)
    private const float BurstTime      = 0.22f;
    private const float BurstRadiusMin = 28f;
    private const float BurstRadiusMax = 72f;

    // 날아감 — 머뭇거림(튀어나온 뒤) · 날아가는 시간 · 동전 사이 간격 · 길이 휘는 높이와 좌우 흔들림 (px)
    private const float HoldTime  = 0.08f;
    private const float FlyTime   = 0.55f;
    private const float Stagger   = 0.045f;
    private const float ArcHeight = 110f;
    private const float ArcSway   = 90f;

    // 닿을 때 — 동전이 줄어드는 크기 · 아이콘이 튀는 세기와 시간 · 반짝 시간
    private const float ArriveScale = 0.55f;
    private const float PunchScale  = 0.18f;
    private const float PunchTime   = 0.22f;
    private const float SparkTime   = 0.35f;

    // 반짝 색 — 흰 그림에 입힌다 (동전 색은 'FxSprites.CoinColor' — 상단 골드 아이콘과 같다)
    private static readonly Color SparkColor = new Color(1f, 0.95f, 0.7f, 1f);

    [CenterHeader("참조")]
    [SerializeField, Tooltip("동전이 날아갈 곳 — 상태바의 골드 아이콘. 닿으면 이것이 튄다")]
    private RectTransform target = null!;

    private ServerWaitManager _wait = null!;
    private SoundManager      _sound = null!;

    private RectTransform _rect   = null!;
    private Image[]       _coins  = Array.Empty<Image>();
    private Image?        _spark;
    private Vector3       _targetScale;

    private Sequence? _seq;
    private Tween?    _punch;
    private Tween?    _sparkTween;

    private bool _isSubscribed;
    private bool _isReady; // Start 완료 여부 — OnEnable 재구독 가드

    // 참조 확보 → 구독 → 초기화 순서로 진행한다 (클라 공통 규약)
    private void Start()
    {
        this.RequireRef(target, nameof(target));

        _wait        = Services.Get<ServerWaitManager>();
        _sound       = Services.Get<SoundManager>();
        _rect        = (RectTransform)transform;
        _targetScale = target.localScale;

        Build();
        Subscribe();

        _isReady = true;
    }

    // 껐다 켠 경우의 재구독 (Unity 메시지)
    private void OnEnable()
    {
        if (_isReady)
        {
            Subscribe();
        }
    }

    // 구독 해제 · 날던 동전 정리 (Unity 메시지)
    private void OnDisable()
    {
        Unsubscribe();
        Stop();
    }

    private void Subscribe()
    {
        if (_isSubscribed)
        {
            return;
        }

        _isSubscribed         = true;
        _wait.GoldGainRaised += OnGoldGainRaised;
    }

    private void Unsubscribe()
    {
        if (!_isSubscribed)
        {
            return;
        }

        _isSubscribed         = false;
        _wait.GoldGainRaised -= OnGoldGainRaised;
    }

    // 동전·반짝 Image를 만든다 (Start에서 한 번) — 그림은 날릴 때마다 목록에서 다시 찾는다('FxSprites' 주석)
    private void Build()
    {
        _coins = new Image[MaxCoins];

        for (int i = 0; i < MaxCoins; i++)
        {
            _coins[i] = CreateImage($"Coin {i}", CoinSize, FxSprites.CoinColor);
        }

        _spark = CreateImage("Spark", SparkSize, SparkColor);
    }

    private Image CreateImage(string name, float size, Color color)
    {
        var go    = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rect  = (RectTransform)go.transform;
        var image = go.GetComponent<Image>();

        rect.SetParent(transform, false);
        rect.sizeDelta = new Vector2(size, size);

        image.color         = color;
        image.raycastTarget = false;
        image.enabled       = false;

        return image;
    }

    // 골드를 받았다 — 'from'에서 동전을 날린다 (ServerWaitManager.GoldGainRaised 구독)
    private void OnGoldGainRaised(Transform from, long amount)
    {
        Sprite? coin = FxSprites.CoinBody;

        if (coin == null || amount <= 0)
        {
            return;
        }

        Stop();

        Sprite? spark = FxSprites.CoinSpark;

        if (_spark != null)
        {
            _spark.sprite = spark!;
        }

        int     count   = Mathf.Clamp(MinCoins + (int)Math.Log10(amount), MinCoins, MaxCoins);
        Vector2 source  = ToLocal(from);
        Vector2 destiny = ToLocal(target);

        _seq = DOTween.Sequence().SetLink(gameObject, LinkBehaviour.KillOnDisable).OnKill(HideCoins);

        for (int i = 0; i < count; i++)
        {
            Image         image = _coins[i];
            RectTransform rect  = image.rectTransform;

            image.sprite  = coin;
            image.enabled = true;
            image.color   = FxSprites.CoinColor;

            rect.anchoredPosition = source;
            rect.localScale       = Vector3.zero;
            rect.localEulerAngles = new Vector3(0f, 0f, UnityEngine.Random.Range(-25f, 25f));

            // 둘레로 고르게 퍼지되 조금씩 어긋나게 — 위쪽으로 조금 더 (아래는 판매 목록 밖이다)
            float   angle = (i / (float)count) * Mathf.PI * 2f + UnityEngine.Random.Range(-0.3f, 0.3f);
            Vector2 burst = source + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.8f + 0.25f)
                                     * UnityEngine.Random.Range(BurstRadiusMin, BurstRadiusMax);

            _seq.Insert(0f, rect.DOAnchorPos(burst, BurstTime).SetEase(Ease.OutCubic));
            _seq.Insert(0f, rect.DOScale(1f, BurstTime).SetEase(Ease.OutBack));

            // 튀어나온 자리에서 골드 아이콘까지 휘어 날아간다 — 2차 베지어(조절점은 가는 길 위쪽으로 · 좌우로 흔든다)
            float   flyAt   = BurstTime + HoldTime + i * Stagger;
            Vector2 control = Vector2.Lerp(burst, destiny, 0.35f)
                              + new Vector2(UnityEngine.Random.Range(-ArcSway, ArcSway), ArcHeight);

            _seq.Insert(flyAt, DOVirtual.Float(0f, 1f, FlyTime, t => rect.anchoredPosition = Bezier(burst, control, destiny, t))
                                        .SetEase(Ease.InQuad));
            _seq.Insert(flyAt + FlyTime * 0.6f, rect.DOScale(ArriveScale, FlyTime * 0.4f).SetEase(Ease.InQuad));
            _seq.InsertCallback(flyAt + FlyTime, () => Arrive(image));

            // 소리는 첫 동전이 닿을 때 한 번 — 동전마다 울리면 12번 겹친다
            if (i == 0)
            {
                _seq.InsertCallback(flyAt + FlyTime, () => _sound.PlaySfx(Sounds.CoinGain));
            }
        }
    }

    // 동전 하나가 닿았다 — 동전을 숨기고 아이콘을 튕기고 반짝인다
    private void Arrive(Image coin)
    {
        coin.enabled = false;

        _punch?.Kill(complete: true);
        target.localScale = _targetScale;
        _punch = target.DOPunchScale(_targetScale * PunchScale, PunchTime, 4, 0.6f)
                       .SetLink(target.gameObject, LinkBehaviour.KillOnDisable)
                       .OnKill(() => target.localScale = _targetScale);

        PlaySpark();
    }

    // 아이콘 위에서 한 번 반짝 — 연달아 닿으면 처음부터 다시
    private void PlaySpark()
    {
        if (_spark == null || _spark.sprite == null)
        {
            return;
        }

        RectTransform rect = _spark.rectTransform;

        _sparkTween?.Kill();

        _spark.enabled        = true;
        _spark.color          = SparkColor;
        rect.anchoredPosition = ToLocal(target);
        rect.localScale       = Vector3.one * 0.4f;
        rect.localEulerAngles = Vector3.zero;

        _sparkTween = DOTween.Sequence()
                             .Join(rect.DOScale(1.2f, SparkTime).SetEase(Ease.OutCubic))
                             .Join(rect.DOLocalRotate(new Vector3(0f, 0f, 45f), SparkTime))
                             .Join(_spark.DOFade(0f, SparkTime).SetEase(Ease.InQuad))
                             .SetLink(gameObject, LinkBehaviour.KillOnDisable)
                             .OnKill(() => _spark.enabled = false);
    }

    // 날던 무리를 끊는다 — 'OnKill'이 동전을 숨긴다
    private void Stop()
    {
        _seq?.Kill();
        _seq = null;

        _sparkTween?.Kill();
        _sparkTween = null;
    }

    private void HideCoins()
    {
        foreach (Image coin in _coins)
        {
            coin.enabled = false;
        }
    }

    // 다른 캔버스의 칸 가운데를 동전의 'anchoredPosition'으로 — 피벗이 어디든 사각형 가운데로 잡는다.
    // 동전의 앵커는 이 층의 가운데라, 층의 피벗 기준 좌표에서 층 가운데만큼 뺀다.
    private Vector2 ToLocal(Transform other)
    {
        Vector3 world = other is RectTransform rect ? rect.TransformPoint(rect.rect.center) : other.position;

        return (Vector2)_rect.InverseTransformPoint(world) - _rect.rect.center;
    }

    private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t)
    {
        float u = 1f - t;

        return u * u * a + 2f * u * t * b + t * t * c;
    }
}
