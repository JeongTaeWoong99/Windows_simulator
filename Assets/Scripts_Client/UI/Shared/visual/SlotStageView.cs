using System.Collections.Generic;
using MikaProtocol;
using UnityEngine;
using UnityEngine.UI;

// 슬롯 무대 — 패럴랙스 배경 · 왼쪽에서 다가오는 대상 · 오른쪽에서 제자리 달리기·공격하는 캐릭터.
// 무대가 될 RectTransform(패널)에 붙인다. 자식(층·대상·캐릭터)은 처음 그릴 때 코드가 만든다.
//
// ■ 스스로 시간을 세지 않는다
//   주인('WorkStationSlotView')이 이번 주기 안의 초를 넘겨 주면 그 순간을 그린다('SlotStageTimeline').
//   주인의 Update가 멈추면(큰 창을 닫으면) 그리기도 멈춘다.
//
// ■ 배경만 누적이다
//   동작·대상은 주기 안의 순간에서 매번 계산하지만, 땅이 흐른 거리는 달리는 동안 쌓는다 —
//   판정 경계에서 배경이 처음 자리로 되돌아가지 않게(게임UI 2.5 "끊김 없이 이어진다").
//
// ■ 값은 매 프레임 읽는다
//   전체 세팅('SlotStageSettings')·캐릭터 개인 조정(멈추는 거리 · 확대 배율)을 플레이 중에 고쳐도 바로 보인다.
//
// ■ 그림이 없으면 그리지 않는다
//   목록이 없으면(그림 저장소를 받지 않은 PC) 무대 자식을 만들지 않고 패널 바탕만 남는다.
//   프리팹의 'catalog'는 비워 둔다 — 메인 저장소가 그림 저장소의 GUID를 가리키지 않게 Resources로 찾는다.
[RequireComponent(typeof(RectTransform))]
public class SlotStageView : MonoBehaviour
{
    // 다가오는 대상이 처음 나타나는 자리 — 화면 왼쪽 끝에서 이만큼 더 밖 (아트 픽셀)
    private const int SpawnMargin = 40;

    [CenterHeader("참조")]
    [SerializeField, Tooltip("그림 목록. 비우면 'VisualCatalog.Current'(Resources)를 쓴다 — 그것도 없으면 무대를 그리지 않는다")]
    private VisualCatalog? catalog;

    // 무대 자식 — 대상은 둘이다(쓰러지는 것 · 다가오는 것)
    private struct TargetImages
    {
        public Image body;
        public Image flash;
    }

    private RectTransform        _rect     = null!;
    private RectTransform?       _layerRoot;
    private readonly List<RawImage> _layers = new List<RawImage>(); // 만든 층 전부 — 지금 배경이 쓰는 것은 앞의 '_layerCount'개
    private int                  _layerCount;
    private TargetImages         _dying;
    private TargetImages         _coming;
    private Image?               _character;
    private Image?               _effect;     // 공격 이펙트 — 캐릭터 위
    private Image?               _hitEffect;  // 타격 이펙트 — 대상 위

    private CharacterVisual?  _characterVisual;
    private BackgroundVisual? _background;
    private TargetVisual?     _target;
    private int               _level;

    // 땅이 흐른 거리 (화면 단위). 하루 내내 쌓여도 정밀도가 무너지지 않게 double.
    private double _groundDistance;

    // 지난번에 그린 순간 — 시간이 살짝 되감기면 이 자리에서 기다린다(아래 'Rewind')
    private float _lastTime = -1f;

    // 이만큼 이내로 시간이 뒤로 가면 되감기로 보고 버틴다 (초)
    private const float RewindTolerance = 0.5f;

    private void Awake()
    {
        _rect = (RectTransform)transform;
    }

    // 무엇을 그릴지 정한다 (주인의 Bind에서 호출).
    //   characterTid : 종류(TID) — 개체 번호가 아니다. 0이면 빈 슬롯(멈춘 배경만)
    public void Show(int characterTid, EIndustryType industry, int industryLevel)
    {
        VisualCatalog? catalog = this.catalog != null ? this.catalog : VisualCatalog.Current;

        if (catalog == null)
        {
            return;
        }

        bool empty = characterTid == 0;

        Show(empty ? null : catalog.GetCharacter(characterTid),
             empty ? catalog.GetEmptySlotBackground() : catalog.GetBackground(industry),
             empty ? null : catalog.GetTarget(industry),
             industryLevel);
    }

    // 그림을 직접 정한다 (미리보기 'SlotStagePreview'가 호출).
    public void Show(CharacterVisual? character, BackgroundVisual? background, TargetVisual? target, int industryLevel)
    {
        if (background != _background)
        {
            _background = background;
            RebuildLayers();
        }

        // 캐릭터가 바뀌면 되감기 기준을 버린다 — 정산마다 다시 불려도 같은 캐릭터면 유지한다
        if (character != _characterVisual)
        {
            _lastTime = -1f;
        }

        _characterVisual = character;
        _target          = target;
        _level           = industryLevel;

        EnsureActors();
    }

    // 멈춘 장면 — 빈 슬롯·속도 0인 슬롯 (주인의 Bind에서 호출)
    public void DrawIdle()
    {
        if (_background == null)
        {
            return;
        }

        SlotStageSettings settings = SlotStageSettings.Current;
        float k = Scale(settings);

        DrawLayers(settings, k);
        HideTarget(_dying);
        HideTarget(_coming);
        PlaceCharacter(_characterVisual != null ? _characterVisual.IdleFrame : null, null, settings, k);
        HideHitEffect();
    }

    // 주기 안의 한 순간을 그린다 (주인의 Tick에서 매 프레임 호출).
    //   time      : 이번 주기 안의 초 (0 ~ cycle)
    //   cycle     : 판정 1회의 초
    //   deltaTime : 지난 그리기 뒤로 흐른 초 — 땅 거리를 쌓는 데만 쓴다
    public void Tick(float time, float cycle, float deltaTime)
    {
        if (_background == null || _characterVisual == null || _character == null || cycle <= 0f)
        {
            DrawIdle();

            return;
        }

        time = HoldOnRewind(time, cycle);

        SlotStageSettings settings = SlotStageSettings.Current;
        float k      = Scale(settings);
        float width  = _rect.rect.width;

        SlotStageTimeline.Result now = SlotStageTimeline.Evaluate(time, cycle, _characterVisual, settings);

        float feetX = width - settings.RightPad * k;
        float stopX = feetX - (settings.StopGap + _characterVisual.StopGapOffset) * k; // 대상 오른쪽 끝이 멈추는 자리
        float speed = (stopX + SpawnMargin * k) / settings.ApproachSeconds;            // 화면 단위/초

        // 배경은 달리는 동안만 흐른다
        if (now.Phase == SlotStageTimeline.EPhase.Run)
        {
            _groundDistance += deltaTime * speed;
        }

        DrawLayers(settings, k);
        DrawTargets(now, time, stopX, speed, settings, k);
        PlaceCharacter(PickFrame(now), PickEffect(now), settings, k);
    }

    // 수확 자리 — 판정 경계에서 쓰러지는 대상의 가운데(월드 좌표). 대상 그림이 없으면 false (획득 연출이 호출).
    //
    // ※ 쓰러지는 대상은 여운 동안 멈춤 자리에 서 있다('DrawTargets'의 dyingX가 주기 처음에 stopX - 반폭) —
    //   그래서 시간과 상관없이 같은 자리를 돌려준다.
    public bool TryGetHarvestPoint(out Vector3 worldPoint)
    {
        worldPoint = default;

        Sprite? sprite = _target != null ? _target.Sprite : null;

        if (_background == null || _characterVisual == null || sprite == null)
        {
            return false;
        }

        SlotStageSettings settings = SlotStageSettings.Current;
        float k     = Scale(settings);
        float feetX = _rect.rect.width - settings.RightPad * k;
        float stopX = feetX - (settings.StopGap + _characterVisual.StopGapOffset) * k;
        Vector2 size = sprite.rect.size * k;

        // 무대 좌표는 왼쪽 아래 기준('Place') — 피벗 기준 로컬로 옮긴 뒤 월드로
        var local = new Vector2(stopX - size.x / 2f, GroundY(k) + size.y / 2f) + _rect.rect.min;

        worldPoint = _rect.TransformPoint(local);

        return true;
    }

    // 땅이 지금까지 흐른 거리 (무대 로컬 단위) — 획득 연출이 시작 때 값을 잡아 두고 'FollowGround'로 따라간다
    public double GroundDistance => _groundDistance;

    // 'sinceGround' 때 'worldPoint'에 있던 것이 땅과 함께 흘러 지금 있을 자리 (월드).
    // 쓰러진 대상과 같은 규칙이다 — 땅은 달리는 동안만 흐른다('Tick').
    public Vector3 FollowGround(Vector3 worldPoint, double sinceGround)
        => worldPoint + _rect.TransformVector(new Vector3((float)(_groundDistance - sinceGround), 0f, 0f));

    // ■ 되감기 방어
    //   진행도는 서버 동기화(정산 패킷)를 받을 때마다 기준점이 다시 잡힌다. 클라 시계가 조금 앞서 있었으면
    //   진행도가 살짝 뒤로 간다 — 그대로 그리면 방금 끝난 공격이 다시 나오거나, 판정 경계를 거꾸로 넘어
    //   마무리 공격의 앞 프레임(타격 전이라 번쩍임 없음)이 한 번 더 나온다.
    //   뒤로 간 거리가 'RewindTolerance' 안이면 그 자리에 멈춰 있다가, 진행도가 따라오면 이어 그린다.
    //   그보다 크게 뒤로 가면(캐릭터 교체·속도 변경) 진짜 새 시간으로 보고 따른다.
    private float HoldOnRewind(float time, float cycle)
    {
        if (_lastTime >= 0f && _lastTime <= cycle)
        {
            float back = Mathf.Repeat(_lastTime - time, cycle); // 앞선 순간에서 얼마나 뒤로 왔나 (주기 경계 포함)

            if (back > 0f && back < Mathf.Min(RewindTolerance, cycle * 0.25f))
            {
                return _lastTime;
            }
        }

        _lastTime = time;

        return time;
    }

    #region 그리기

    // 층마다 uv를 밀어 가로로 흐르게 한다. 대상이 왼쪽에서 오므로 그림은 오른쪽으로 흐른다.
    private void DrawLayers(SlotStageSettings settings, float k)
    {
        if (_background == null)
        {
            return;
        }

        float width = _rect.rect.width;
        BackgroundVisual.Layer[] layers = _background.Layers;

        for (int i = 0; i < _layerCount && i < layers.Length; i++)
        {
            Texture2D texture = layers[i].texture;

            if (texture == null)
            {
                continue;
            }

            float tileWidth = Mathf.Round(texture.width * k);
            float ratio     = Mathf.Approximately(layers[i].speedRatio, 1f) ? 1f : layers[i].speedRatio * settings.FarLayerSpeed;
            float shift     = (float)(_groundDistance * ratio % tileWidth);

            RectTransform rect = _layers[i].rectTransform;
            rect.sizeDelta = new Vector2(0f, Mathf.Round(_background.StageHeight * k));

            _layers[i].uvRect = new Rect(-shift / tileWidth, 0f, width / tileWidth, 1f);
        }
    }

    private void DrawTargets(SlotStageTimeline.Result now, float time, float stopX, float speed, SlotStageSettings settings, float k)
    {
        Sprite? sprite = _target != null ? _target.Sprite : null;

        if (_target == null || sprite == null)
        {
            HideTarget(_dying);
            HideTarget(_coming);

            return;
        }

        float halfWidth = sprite.rect.width * k / 2f;
        float dyingX    = stopX - halfWidth + speed * Mathf.Max(0f, Mathf.Min(time, now.RunEnd) - now.RunStart);
        float comingX   = stopX - halfWidth - speed * Mathf.Max(0f, now.RunEnd - time);

        // 쓰러진 대상 — 그 자리에서 사라진다. 여운이 끝나 땅이 흐르기 시작하면 땅과 같이 흘러간다
        if (settings.FadeSeconds > 0f && time < settings.FadeSeconds)
        {
            float alpha = 1f - time / settings.FadeSeconds;

            PlaceTarget(_dying, sprite, dyingX, alpha, time < settings.FlashSeconds, k);
        }
        else
        {
            HideTarget(_dying);
        }

        // 다음 대상 — 달리는 동안 멈춤 자리 왼쪽에서 땅과 같이 흘러온다. 달리기 전에는 화면 밖에 있다
        if (time >= now.RunStart)
        {
            bool flash = now.HitsComing && now.SinceHit < settings.FlashSeconds;

            PlaceTarget(_coming, sprite, comingX, 1f, flash, k);
        }
        else
        {
            HideTarget(_coming);
        }

        // 타격 이펙트 — 맞은 대상(공격 중이면 다가온 대상, 아니면 쓰러진 대상)의 가운데에서 한 번
        DrawHitEffect(now, now.HitsComing ? comingX : dyingX, sprite.rect.height * k / 2f, settings, k);
    }

    private void DrawHitEffect(SlotStageTimeline.Result now, float centerX, float halfHeight, SlotStageSettings settings, float k)
    {
        if (_hitEffect == null || _characterVisual == null || now.SinceHit < 0f || now.SinceHit >= settings.HitEffectSeconds)
        {
            HideHitEffect();

            return;
        }

        Sprite[]? frames = _characterVisual.Attacks[now.HitCombo].hitEffectFrames;

        if (frames == null || frames.Length == 0)
        {
            HideHitEffect();

            return;
        }

        int     index  = Mathf.Min(frames.Length - 1, Mathf.FloorToInt(now.SinceHit / settings.HitEffectSeconds * frames.Length));
        Sprite? effect = frames[index];

        _hitEffect.enabled = effect != null;

        if (effect != null)
        {
            _hitEffect.sprite = effect;
            Place(_hitEffect.rectTransform, new Vector2(Mathf.Round(centerX), GroundY(k) + Mathf.Round(halfHeight)), Snap(effect.rect.size * k));
        }
    }

    private void HideHitEffect()
    {
        if (_hitEffect != null)
        {
            _hitEffect.enabled = false;
        }
    }

    // 대상 하나를 놓는다 — 발이 땅선, x는 가운데
    private void PlaceTarget(TargetImages images, Sprite sprite, float centerX, float alpha, bool flash, float k)
    {
        Vector2 position = new Vector2(Mathf.Round(centerX), GroundY(k));
        Vector2 size     = Snap(sprite.rect.size * k);
        Color   tint     = _target != null ? _target.GetTint(_level) : Color.white;

        images.body.enabled = true;
        images.body.sprite  = sprite;
        images.body.color   = new Color(tint.r, tint.g, tint.b, tint.a * alpha);
        Place(images.body.rectTransform, position, size);

        Sprite? flashSprite = _target != null ? _target.Flash : null;

        // 번쩍임은 몸통의 자식으로 늘여 두어 자리를 따로 잡지 않는다
        if (flash && flashSprite != null)
        {
            images.flash.enabled = true;
            images.flash.sprite  = flashSprite;
            images.flash.color   = new Color(1f, 1f, 1f, alpha);
        }
        else
        {
            images.flash.enabled = false;
        }
    }

    private static void HideTarget(TargetImages images)
    {
        if (images.body == null)
        {
            return;
        }

        images.body.enabled  = false;
        images.flash.enabled = false;
    }

    // 캐릭터 — 칸의 발 자리(아래 가운데)를 발 위치에 맞춘다
    private void PlaceCharacter(Sprite? frame, Sprite? effect, SlotStageSettings settings, float k)
    {
        if (_character == null)
        {
            return;
        }

        _character.enabled = frame != null;

        if (_effect != null)
        {
            _effect.enabled = frame != null && effect != null;
        }

        if (frame == null)
        {
            return;
        }

        Vector2 feet = new Vector2(Mathf.Round(_rect.rect.width - settings.RightPad * k), GroundY(k));

        _character.sprite = frame;
        // 캐릭터 크기 배수 — 발 자리는 그대로 두고 그림만 키운다(피벗이 발이라 땅에서 뜨지 않는다)
        float size = k * (_characterVisual != null ? _characterVisual.CharacterScale : 1f);

        Place(_character.rectTransform, feet, Snap(frame.rect.size * size));

        // 공격 이펙트는 캐릭터와 같은 칸 규격이라 같은 자리에 겹친다
        if (_effect != null && effect != null)
        {
            _effect.sprite = effect;
            Place(_effect.rectTransform, feet, Snap(effect.rect.size * size));
        }
    }

    private Sprite? PickFrame(SlotStageTimeline.Result now)
    {
        if (_characterVisual == null)
        {
            return null;
        }

        if (now.Phase == SlotStageTimeline.EPhase.Idle)
        {
            return _characterVisual.IdleFrame;
        }

        Sprite[] frames = now.Phase == SlotStageTimeline.EPhase.Run
            ? _characterVisual.RunFrames
            : _characterVisual.Attacks[now.Combo].frames;

        return frames.Length > 0 ? frames[Mathf.Clamp(now.Frame, 0, frames.Length - 1)] : null;
    }

    // 공격 이펙트 — 공격·여운일 때 그 공격의 이펙트를 같은 진행도로 (프레임 수가 달라도 비율로 맞춘다)
    private Sprite? PickEffect(SlotStageTimeline.Result now)
    {
        if (_characterVisual == null || (now.Phase != SlotStageTimeline.EPhase.Attack && now.Phase != SlotStageTimeline.EPhase.Recover))
        {
            return null;
        }

        CharacterVisual.AttackMotion motion  = _characterVisual.Attacks[now.Combo];
        Sprite[]?                    effects = motion.effectFrames;

        if (effects == null || effects.Length == 0 || motion.frames.Length == 0)
        {
            return null;
        }

        return effects[Mathf.Clamp(now.Frame * effects.Length / motion.frames.Length, 0, effects.Length - 1)];
    }

    // 소수 배율에서 크기를 화면 정수 칸에 맞춘다 — 반 칸 걸친 그림이 흐려지지 않게
    private static Vector2 Snap(Vector2 size) => new Vector2(Mathf.Round(size.x), Mathf.Round(size.y));

    // 확대 배율 — 캐릭터 개인 값이 있으면 그것 (키 큰 캐릭터는 무대째 작게 본다)
    private float Scale(SlotStageSettings settings)
        => _characterVisual != null ? _characterVisual.GetPixelScale(settings) : settings.PixelScale;

    // 땅선 높이 — 무대 그림의 아래를 패널 아래에 붙인다
    private float GroundY(float k) => _background != null ? Mathf.Round(_background.GroundHeight * k) : 0f;

    // 왼쪽 아래 기준 · 피벗은 아래 가운데
    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchoredPosition = position;
        rect.sizeDelta        = size;
    }

    #endregion

    #region 자식 만들기

    // 배경이 바뀌면 층을 다시 짠다 — 층 수가 배경마다 다르다.
    // 층은 파괴하지 않는다: 모자라면 만들고, 남으면 꺼 둔다(배치·해제·산업 변경마다 생성·파괴가 돌지 않게).
    private void RebuildLayers()
    {
        _layerCount = 0;

        if (_background == null)
        {
            HideLayersFrom(0);

            return;
        }

        if (_layerRoot == null)
        {
            _layerRoot = CreateChild("Layers", transform);
            _layerRoot.anchorMin = Vector2.zero;
            _layerRoot.anchorMax = Vector2.one;
            _layerRoot.sizeDelta = Vector2.zero;
            _layerRoot.SetAsFirstSibling();
        }

        // 뒤 → 앞 순서 그대로 자식이 된다 — i번째 층이 늘 i번째 자식이라 다시 써도 순서가 같다
        foreach (BackgroundVisual.Layer layer in _background.Layers)
        {
            if (_layerCount == _layers.Count)
            {
                RectTransform rect = CreateChild($"Layer {_layers.Count}", _layerRoot);
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot     = new Vector2(0.5f, 0f);

                RawImage created = rect.gameObject.AddComponent<RawImage>();
                created.raycastTarget = false;

                _layers.Add(created);
            }

            RawImage image = _layers[_layerCount++];
            image.texture = layer.texture;
            image.uvRect  = new Rect(0f, 0f, 1f, 1f); // 이전 배경이 밀어 둔 uv를 남기지 않는다 — 다음 'DrawLayers'가 다시 정한다
            image.gameObject.SetActive(true);
        }

        HideLayersFrom(_layerCount);
    }

    // 지금 배경이 쓰지 않는 층을 끈다 ('RebuildLayers'에서 호출)
    private void HideLayersFrom(int startIndex)
    {
        for (int i = startIndex; i < _layers.Count; i++)
        {
            _layers[i].gameObject.SetActive(false);
        }
    }

    // 대상 둘 · 캐릭터 · 이펙트 둘을 한 번만 만든다 — 층보다 앞, 이펙트가 맨 앞
    private void EnsureActors()
    {
        if (_character != null)
        {
            return;
        }

        _dying     = CreateTarget("Dying Target");
        _coming    = CreateTarget("Coming Target");
        _character = CreateImage("Character", transform);
        _effect    = CreateImage("Attack Effect", transform);
        _hitEffect = CreateImage("Hit Effect", transform);
        _hitEffect.rectTransform.pivot = new Vector2(0.5f, 0.5f);
    }

    private TargetImages CreateTarget(string name)
    {
        Image body  = CreateImage(name, transform);
        Image flash = CreateImage("Flash", body.transform);

        // 번쩍임은 몸통과 같은 자리에 겹친다 — 몸통 크기를 따라가게 늘인다
        RectTransform flashRect = flash.rectTransform;
        flashRect.anchorMin        = Vector2.zero;
        flashRect.anchorMax        = Vector2.one;
        flashRect.anchoredPosition = Vector2.zero;
        flashRect.sizeDelta        = Vector2.zero;

        return new TargetImages { body = body, flash = flash };
    }

    private static Image CreateImage(string name, Transform parent)
    {
        RectTransform rect = CreateChild(name, parent);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot     = new Vector2(0.5f, 0f);

        Image image = rect.gameObject.AddComponent<Image>();
        image.raycastTarget  = false;
        image.preserveAspect = false;
        image.enabled        = false;

        return image;
    }

    private static RectTransform CreateChild(string name, Transform parent)
    {
        var child = new GameObject(name, typeof(RectTransform));
        child.layer = parent.gameObject.layer;
        child.transform.SetParent(parent, false);

        return (RectTransform)child.transform;
    }

    #endregion
}
