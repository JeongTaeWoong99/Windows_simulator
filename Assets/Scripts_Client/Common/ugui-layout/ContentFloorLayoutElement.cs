using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 남는 자리를 형제와 나눠 받는(flexible) 칸이, 안 내용이 제 몫보다 크면 **내용만큼 늘어나게** 한다.
// "기본 크기는 나눈 몫, 내용이 더 크면 내용만큼" — 예: 목록 아래 열리는 창이 목록과 반씩 나누다가,
// 창 내용이 반보다 크면 창이 늘고 목록이 그만큼 준다.
//
// ■ 왜 컴포넌트가 필요한가
// 'LayoutElement.minHeight'에 내용 높이를 주면 넘치지는 않지만, UGUI는 남는 자리를 **min 위에 더해** 나눈다
// (크기 = Lerp(min, preferred) + flexible 몫). 그래서 내용이 몫보다 작아도 min만큼 더 커져 목록이 늘 줄어든다.
// "몫과 내용 중 큰 쪽"은 기본 컴포넌트로 표현할 수 없다.
//
// ■ 그래서 몫을 직접 계산해 고른다
// 평소에는 아무것도 주장하지 않는다(-1) — 같은 오브젝트의 'LayoutElement'가 정한 나눔 그대로다.
// 내용(`content`)의 preferred가 몫보다 크면 그 축에서 min = preferred = 내용, flexible = 0을 주장한다.
// 몫은 **부모 크기와 형제의 preferred·flexible**로 계산한다 — 자기 현재 크기는 보지 않는다
// (자기 크기에서 값을 만들면 한 번 커진 크기가 굳는다 — 'ugui-layout 규칙.md' ⚠️ 절).
//
// ■ 붙이는 자리
// 부모가 'HorizontalOrVerticalLayoutGroup'(축이 같은 것)인 칸. 같은 오브젝트에 'LayoutElement'를 두고
// 평소 나눔(preferred 0 · flexible 1 등)을 그쪽에 적는다. 이쪽은 우선순위 2로 늘어날 때만 덮는다.
// `content`는 보통 칸을 꽉 채운 자식(그룹을 가진 정렬 상자)이다 — 비우면 첫 자식.
//
// ⚠️ 내용이 레이아웃 경계(그룹 없는 부모) 안에 있으면 내용 글씨가 바뀌어도 이 칸까지 다시 재지 않는다.
// 이 칸은 켜질 때 · 크기가 바뀔 때 다시 잰다. 열린 채로 내용 높이가 바뀌는 화면이면 'Refresh'를 부른다.
[AddComponentMenu("Layout/Content Floor Layout Element")]
[RequireComponent(typeof(RectTransform))]
public class ContentFloorLayoutElement : UIBehaviour, ILayoutElement
{
    [CenterHeader("※ 나눈 몫보다 내용이 크면 내용만큼 늘어난다")]
    [SerializeField, Tooltip("늘어날 축 — 부모 줄의 방향과 같아야 한다")]
    private RectTransform.Axis axis = RectTransform.Axis.Vertical;

    [SerializeField, Tooltip("크기를 잴 내용. 비우면 첫 자식")]
    private RectTransform? content;

    private float _need;   // 내용이 요구하는 크기
    private bool  _expand; // 몫보다 커서 내용만큼 주장하는 중인가

    // 지금 내용만큼 늘어나 있는가 (확인·디버그용)
    public bool IsExpanded => _expand;

    // ─── ILayoutElement — 늘어날 때 그 축만 주장한다. 나머지는 'LayoutElement'에 맡긴다 ───
    public float minWidth        => Claim(RectTransform.Axis.Horizontal, _need);
    public float preferredWidth  => Claim(RectTransform.Axis.Horizontal, _need);
    public float flexibleWidth   => Claim(RectTransform.Axis.Horizontal, 0f);

    public float minHeight       => Claim(RectTransform.Axis.Vertical, _need);
    public float preferredHeight => Claim(RectTransform.Axis.Vertical, _need);
    public float flexibleHeight  => Claim(RectTransform.Axis.Vertical, 0f);

    // LayoutElement의 기본 우선순위(1)보다 높게 — 늘어날 때만 덮는다
    public int layoutPriority => 2;

    // 자식(내용)이 먼저 계산된 뒤 불린다 — 여기서 내용 크기와 몫을 비교해 둔다 (UGUI 레이아웃 시스템이 호출)
    public void CalculateLayoutInputHorizontal() => Evaluate(RectTransform.Axis.Horizontal);
    public void CalculateLayoutInputVertical()   => Evaluate(RectTransform.Axis.Vertical);

    // 내용 높이가 바뀐 것을 알린다 — 내용이 레이아웃 경계 안에 있어 저절로 전해지지 않을 때 부른다.
    public void Refresh() => MarkDirty();

    private float Claim(RectTransform.Axis target, float value) => target == axis && _expand ? value : -1f;

    // 내용 크기 · 몫을 재고 늘어날지 정한다 (CalculateLayoutInput에서 호출)
    private void Evaluate(RectTransform.Axis pass)
    {
        if (pass != axis)
        {
            return;
        }

        RectTransform? target = content != null ? content
                              : transform.childCount > 0 ? transform.GetChild(0) as RectTransform
                              : null;

        if (target == null)
        {
            _expand = false;

            return;
        }

        _need   = LayoutUtility.GetPreferredSize(target, (int)axis);
        _expand = _need > BaseShare() + 0.5f;
    }

    // 늘어나지 않았을 때 받을 몫 — 부모 줄이 이 칸에 줄 크기를 같은 식으로 계산한다.
    //   몫 = 내 preferred + (부모 크기 − 패딩 − 간격 − 모든 칸의 preferred) × 내 flexible / flexible 합
    // ※ 부모 줄이 없거나 축이 다르면 몫을 모른다 — 무한대로 두어 늘어나지 않는다.
    private float BaseShare()
    {
        var parent = transform.parent as RectTransform;
        var group  = parent != null ? parent.GetComponent<HorizontalOrVerticalLayoutGroup>() : null;

        if (parent == null || group == null || (group is VerticalLayoutGroup) != (axis == RectTransform.Axis.Vertical))
        {
            return float.PositiveInfinity;
        }

        bool  vertical    = axis == RectTransform.Axis.Vertical;
        bool  forceExpand = vertical ? group.childForceExpandHeight : group.childForceExpandWidth;
        float available   = parent.rect.size[(int)axis] - (vertical ? group.padding.vertical : group.padding.horizontal);

        float otherPreferred = 0f;
        float totalFlexible  = 0f;
        float myPreferred    = 0f;
        float myFlexible     = 0f;
        int   count          = 0;

        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i) as RectTransform;

            if (child == null || !child.gameObject.activeInHierarchy || IsIgnored(child))
            {
                continue;
            }

            count++;

            if (child == transform)
            {
                // 내 평소 값 — 내 주장(이 컴포넌트)을 빼고 'LayoutElement'만 본다
                var element = GetComponent<LayoutElement>();

                myPreferred = element != null ? Mathf.Max(0f, vertical ? element.preferredHeight : element.preferredWidth) : 0f;
                myFlexible  = element != null ? Mathf.Max(0f, vertical ? element.flexibleHeight : element.flexibleWidth) : 0f;

                if (forceExpand)
                {
                    myFlexible = Mathf.Max(myFlexible, 1f);
                }

                totalFlexible += myFlexible;

                continue;
            }

            float flexible = LayoutUtility.GetFlexibleSize(child, (int)axis);

            otherPreferred += LayoutUtility.GetPreferredSize(child, (int)axis);
            totalFlexible  += forceExpand ? Mathf.Max(flexible, 1f) : flexible;
        }

        available -= group.spacing * Mathf.Max(0, count - 1);

        float leftover = available - otherPreferred - myPreferred;

        return leftover <= 0f || totalFlexible <= 0f
            ? myPreferred
            : myPreferred + leftover * myFlexible / totalFlexible;
    }

    // 레이아웃에서 빠진 칸인가 ('LayoutElement.ignoreLayout' 등)
    private static bool IsIgnored(RectTransform child)
    {
        foreach (var ignorer in child.GetComponents<ILayoutIgnorer>())
        {
            if (ignorer is Behaviour { isActiveAndEnabled: true } && ignorer.ignoreLayout)
            {
                return true;
            }
        }

        return false;
    }

    #region 다시 계산해야 할 때

    // 켜질 때 · 꺼질 때 · 부모가 바뀔 때 부모 줄을 다시 재게 한다 (Unity 메시지)
    protected override void OnEnable()                 { base.OnEnable();                 MarkDirty(); }
    protected override void OnDisable()                { MarkDirty();                     base.OnDisable(); }
    protected override void OnTransformParentChanged() { base.OnTransformParentChanged(); MarkDirty(); }

    // 크기가 바뀌었다 — 레이아웃이 바꾼 것이면 이미 반영된 값이라 다시 재지 않는다 (Unity 메시지).
    // 그 밖(앵커·부모 크기 변화)이면 다시 잰다. 몫은 부모·형제로 계산하므로 같은 값으로 수렴한다 — 순환하지 않는다.
    protected override void OnRectTransformDimensionsChange()
    {
        if (!CanvasUpdateRegistry.IsRebuildingLayout())
        {
            MarkDirty();
        }
    }

#if UNITY_EDITOR
    // 인스펙터에서 값을 바꿔 보며 확인할 수 있게 (Unity 에디터가 호출)
    protected override void OnValidate() { base.OnValidate(); MarkDirty(); }
#endif

    private void MarkDirty()
    {
        if (!IsActive())
        {
            return;
        }

        // 내가 아니라 부모 줄이 다시 계산돼야 내 크기가 반영된다
        var parent = transform.parent as RectTransform;
        LayoutRebuilder.MarkLayoutForRebuild(parent != null ? parent : (RectTransform)transform);
    }

    #endregion
}
