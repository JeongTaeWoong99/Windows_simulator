using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// 아이템 획득 연출 — 얻은 아이템 아이콘이 한 자리에서 나타나 위로 떠오르며 사라진다.
// 연출을 띄울 칸의 루트에 붙인다. 아이콘 층·풀은 처음 재생할 때 코드가 만든다(프리팹이 따로 없다).
//
// ■ 누가 쓰나 — 같은 로직, 인스펙터 값만 다르다
//   큰 창 작업슬롯 칸('WorkStationSlotView') — 쓰러지는 대상 자리에서
//   상주 위젯 칸('WidgetMiniSlotView')         — 캐릭터 머리 자리에서, 더 작게
//   어디서 띄울지는 부르는 쪽이 **월드 좌표**로 넘긴다 — 이 컴포넌트는 무대도 캐릭터도 모른다.
//
// ■ 칸의 루트에 붙이는 이유
//   무대 패널은 'RectMask2D'로 잘린다 — 그 안에 띄우면 위로 오르다 잘려 나간다. 칸 루트의 층은 무대 밖까지 그린다.
//   층은 레이아웃에서 빠진다('LayoutElement.ignoreLayout') — 칸에 레이아웃 그룹이 있어도 자리를 밀지 않는다.
//
// ■ 여러 개면 가운데를 중심으로 좌우 대칭
//   간격 = 아이콘 크기 + 'iconSpacing' — 크기를 바꿔도 겹치지 않는다. 모두 한 Sequence라 같은 속도·거리로 함께 오른다.
//
// ■ 수명
//   재생은 켜져 있는 동안만이다. 꺼지거나 파괴되면 토큰이 끊기고 Sequence가 Kill돼('KillAndCancelAwait') 남는 트윈·대기가 없다.
//   아이콘은 'PrefabPool'로 돌려쓴다 — 수확은 칸마다 몇 초에 한 번씩 계속 난다.
public class ItemGainEffectView : MonoBehaviour
{
    [CenterHeader("모양")]
    [SerializeField, Min(1f), Tooltip("아이콘 한 변 (px)")]
    private float iconSize = 24f;

    [SerializeField, Min(0f), Tooltip("아이콘 사이 빈틈 (px). 간격은 크기 + 이 값이라 겹치지 않는다")]
    private float iconSpacing = 4f;

    [SerializeField, Min(1), Tooltip("한 번에 띄울 최대 개수 — 넘치면 앞에서부터 이만큼만")]
    private int maxIcons = 5;

    [SerializeField, Tooltip("넘겨받은 자리에서 더 옮겨 시작할 거리 (px) — 대상 가운데보다 조금 위에서 나오게 하는 등")]
    private Vector2 startOffset = Vector2.zero;

    [CenterHeader("움직임")]
    [SerializeField, Tooltip("위로 오르는 거리 (px)")]
    private float riseDistance = 28f;

    [SerializeField, Min(0.05f), Tooltip("나타나서 사라질 때까지 (초)")]
    private float duration = 1.1f;

    [SerializeField, Tooltip("오르는 곡선")]
    private Ease riseEase = Ease.OutCubic;

    [SerializeField, Min(0f), Tooltip("페이드아웃을 시작하는 때 (초). 이때부터 끝까지 흐려진다")]
    private float fadeDelay = 0.45f;

    [SerializeField, Tooltip("흐려지는 곡선")]
    private Ease fadeEase = Ease.InQuad;

    [SerializeField, Range(0f, 1f), Tooltip("나타날 때 시작 크기 배율 — 1이면 크기 변화 없이 나타난다")]
    private float appearScale = 0.6f;

    [SerializeField, Min(0f), Tooltip("시작 크기에서 제 크기가 되는 시간 (초)")]
    private float appearDuration = 0.18f;

    [SerializeField, Tooltip("나타나는 곡선")]
    private Ease appearEase = Ease.OutBack;

    [CenterHeader("풀")]
    [SerializeField, Min(1), Tooltip("풀 안에 쌓아 둘 최대 아이콘 수 — 동시에 뜨는 최대치의 2배쯤")]
    private int maxPooled = 16;

    private RectTransform?     _layer;
    private PrefabPool<Image>? _pool;
    private CancellationTokenSource? _playCts;

    // 꺼지는 중에 끝난 재생의 아이콘 — 부모가 꺼지거나 켜지는 동안에는 계층을 바꿀 수 없어 다음 재생 때 반납한다
    private readonly List<Image> _pendingReturns = new List<Image>();

    // 꺼짐 — 도는 연출을 모두 끊는다 (Unity 메시지)
    private void OnDisable()
    {
        CancelAll();
    }

    // 파괴 (Unity 메시지)
    private void OnDestroy()
    {
        CancelAll();
        _pool?.Dispose();
    }

    // 채취 결과의 아이템 아이콘을 담는다 — 같은 아이템은 한 번만, 받은 순서대로 (큰 창·위젯 두 Presenter가 함께 쓴다).
    //
    // ※ 'ItemChanges'의 수량은 델타가 아니라 갱신 후 총량이라 얼마나 얻었는지는 모른다 — 무엇을 얻었는지만 띄운다.
    //   한 아이템이 여러 칸(묶음)에 걸쳐 오면 줄이 여럿이라 ItemId로 거른다.
    public static void ReadGainIcons(IReadOnlyList<MikaProtocol.ItemChangeInfo>? changes, List<Sprite?> into)
    {
        into.Clear();

        if (changes == null)
        {
            return;
        }

        var seen = new HashSet<int>();

        foreach (MikaProtocol.ItemChangeInfo change in changes)
        {
            if (change.Kind != MikaProtocol.EItemChangeKind.Remove && seen.Add(change.ItemId))
            {
                into.Add(VisualCatalog.ItemIconOf(change.ItemId));
            }
        }
    }

    // 'worldCenter'에서 아이콘들을 띄운다. 꺼져 있거나 아이콘이 없으면 아무것도 하지 않는다.
    //   worldCenter : 아이콘 줄의 가운데 — 부르는 쪽이 대상·머리의 월드 좌표를 넘긴다
    //   icons       : 띄울 그림. null인 것(그림 없음)은 건너뛴다
    public void Play(Vector3 worldCenter, IReadOnlyList<Sprite?> icons)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        ReturnPending();

        _playCts ??= CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);

        PlayAsync(worldCenter, icons, _playCts.Token).Forget();
    }

    private async UniTask PlayAsync(Vector3 worldCenter, IReadOnlyList<Sprite?> icons, CancellationToken ct)
    {
        var sprites = new List<Sprite>(maxIcons);

        foreach (Sprite? sprite in icons)
        {
            if (sprite != null && sprites.Count < maxIcons)
            {
                sprites.Add(sprite);
            }
        }

        if (sprites.Count == 0)
        {
            return;
        }

        EnsureBuilt();

        RectTransform     layer  = _layer!;
        PrefabPool<Image> pool   = _pool!;
        var               rented = new List<Image>(sprites.Count);

        // 층을 맨 앞으로 — 칸이 나중에 만든 자식(무대 배우 등)보다 위에 그린다
        layer.SetAsLastSibling();

        Vector2  center = (Vector2)layer.InverseTransformPoint(worldCenter) + startOffset;
        float    step   = iconSize + iconSpacing;
        Sequence seq    = DOTween.Sequence().SetLink(gameObject);
        bool     handed = false; // Sequence의 수명을 UniTask에 넘겼는가

        try
        {
            for (int i = 0; i < sprites.Count; i++)
            {
                Image         icon = pool.Get(layer);
                RectTransform rect = icon.rectTransform;

                rented.Add(icon);

                // 가운데를 중심으로 좌우 대칭 — i번째의 가운데로부터 거리는 (i - (n-1)/2) 칸
                Vector2 start = center + new Vector2((i - (sprites.Count - 1) * 0.5f) * step, 0f);

                icon.enabled          = true;
                icon.sprite           = sprites[i];
                icon.color            = Color.white;
                rect.sizeDelta        = new Vector2(iconSize, iconSize);
                rect.anchoredPosition = start;
                rect.localScale       = Vector3.one * (appearDuration > 0f ? appearScale : 1f);

                seq.Insert(0f, rect.DOAnchorPosY(start.y + riseDistance, duration).SetEase(riseEase));

                if (appearDuration > 0f)
                {
                    seq.Insert(0f, rect.DOScale(1f, Mathf.Min(appearDuration, duration)).SetEase(appearEase));
                }

                // 중첩 트윈의 SetDelay는 Sequence가 무시한다 — 시작 시각은 Insert로 준다
                float fadeStart = Mathf.Min(fadeDelay, duration);

                seq.Insert(fadeStart, icon.DOFade(0f, duration - fadeStart).SetEase(fadeEase));
            }

            handed = true;

            await seq.ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, ct);
        }
        finally
        {
            // ⚠️ 넘긴 뒤에는 Kill하지 않는다 — 취소되면 UniTask가 Kill하고, 그 Kill 안에서 이 finally가 동기로 돈다.
            //   여기서 한 번 더 Kill하면 DOTween이 같은 트윈을 두 번 치우다 'IndexOutOfRangeException'을 낸다(2026-10-08 확인).
            //   끝까지 돈 Sequence는 스스로 Kill된다. 넘기기 전에 예외가 났을 때만 직접 치운다.
            if (!handed)
            {
                seq.Kill();
            }

            Return(rented);
        }
    }

    // 쓴 아이콘을 풀로 — 꺼져 있으면 미뤘다가 다음 재생 때 (PlayAsync의 finally에서 호출)
    //
    // ⚠️ 끊기는 대부분 'OnDisable' 안에서 난다 — 그 순간에는 부모가 꺼지는 중이라 아이콘의 부모를 옮기면(풀 반납) 예외가 난다.
    //   OnEnable에서 반납해도 같다(켜지는 중). 그래서 그림만 끄고(컴포넌트 끄기는 계층 변경이 아니다) 다음 재생 때 돌려보낸다.
    private void Return(List<Image> rented)
    {
        if (!gameObject.activeInHierarchy)
        {
            foreach (Image icon in rented)
            {
                if (icon != null)
                {
                    icon.enabled = false;
                    _pendingReturns.Add(icon);
                }
            }

            return;
        }

        foreach (Image icon in rented)
        {
            if (icon != null)
            {
                _pool?.Release(icon);
            }
        }
    }

    private void ReturnPending()
    {
        if (_pendingReturns.Count == 0)
        {
            return;
        }

        var pending = new List<Image>(_pendingReturns);

        _pendingReturns.Clear();
        Return(pending);
    }

    // 도는 재생을 모두 끊는다 — 각 재생의 Sequence가 Kill되고 아이콘이 반납된다 (OnDisable · OnDestroy에서 호출)
    private void CancelAll()
    {
        if (_playCts == null)
        {
            return;
        }

        _playCts.Cancel();
        _playCts.Dispose();
        _playCts = null;
    }

    // 아이콘 층 · 쉬는 자리 · 아이콘 원본 · 풀을 한 번만 만든다 (처음 재생할 때)
    private void EnsureBuilt()
    {
        if (_layer != null)
        {
            return;
        }

        _layer = CreateChild("Item Gain Effect", transform);
        _layer.anchorMin = Vector2.zero;
        _layer.anchorMax = Vector2.one;
        _layer.pivot     = new Vector2(0.5f, 0.5f);
        _layer.sizeDelta = Vector2.zero;
        _layer.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

        // 쉬는 아이콘은 꺼 둔 자식 아래에 — 원본도 여기 있어 화면에 나오지 않는다
        RectTransform idle = CreateChild("Pool", _layer);
        idle.gameObject.SetActive(false);

        RectTransform template = CreateChild("Icon", idle);
        template.anchorMin = new Vector2(0.5f, 0.5f);
        template.anchorMax = new Vector2(0.5f, 0.5f);
        template.pivot     = new Vector2(0.5f, 0.5f);

        Image image = template.gameObject.AddComponent<Image>();
        image.raycastTarget  = false;
        image.preserveAspect = true;

        _pool = new PrefabPool<Image>(image, idle, defaultCapacity: maxIcons, maxSize: maxPooled);
    }

    private static RectTransform CreateChild(string name, Transform parent)
    {
        var child = new GameObject(name, typeof(RectTransform));
        child.layer = parent.gameObject.layer;
        child.transform.SetParent(parent, false);

        return (RectTransform)child.transform;
    }
}
