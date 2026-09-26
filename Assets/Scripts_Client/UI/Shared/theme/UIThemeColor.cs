using UnityEngine;
using UnityEngine.UI;

// Image·TMP에 붙여 "이 칸은 어떤 역할의 색인가"만 적어 두는 태그.
//
// ■ 실행 중에는 아무것도 안 한다
// 색은 에디터에서 씬·프리팹에 **구워 둔다**('Apply'). 실행 중에 칠하면 탭 선택·등급색처럼
// Presenter가 바꾼 색을 캔버스를 껐다 켤 때마다 덮어쓴다.
// 상태로 색이 바뀌는 곳은 이 태그가 아니라 Presenter가 'UIThemePalette.Of'로 칠한다.
//
// ■ 버튼 바탕이면 색이 'ColorBlock'으로 간다
// 'Selectable'(ColorTint)은 실행 중 'ColorBlock'을 'Image.color'에 곱한다. 바탕을 남색으로 두고
// 틴트를 노랑으로 주면 곱해져 탁해진다 — 그래서 바탕은 흰색으로 두고 역할 색을 'ColorBlock'에 넣는다.
//
// 🎨 아트가 들어오면 이 태그를 걷어낸다.
[DisallowMultipleComponent]
[RequireComponent(typeof(Graphic))]
public class UIThemeColor : MonoBehaviour
{
    [SerializeField, Tooltip("팔레트의 어느 색을 쓸지")]
    private UIThemeRole role = UIThemeRole.PanelSub;

    [SerializeField, Range(0f, 1f), Tooltip("팔레트 색의 투명도에 곱한다 (딤·흐린 글씨)")]
    private float alpha = 1f;

    public UIThemeRole Role => role;

    // 자동 태그가 역할을 정한다 (에디터 'UIThemeBaker'에서 호출)
    public void Setup(UIThemeRole newRole, float newAlpha)
    {
        role  = newRole;
        alpha = newAlpha;
    }

    // 팔레트 색을 이 칸에 칠한다. **실제로 바뀐** 대상만 돌려준다 — 에디터가 거기에만 저장 표시를 붙인다
    // (씬을 열 때마다 OnValidate가 불려도 값이 같으면 씬이 더러워지지 않게).
    public Object[] Apply(UIThemePalette palette)
    {
        var graphic = GetComponent<Graphic>();
        var color   = palette.Get(role);
        color.a *= alpha;

        var selectable = GetComponent<Selectable>();

        if (selectable != null
            && selectable.transition == Selectable.Transition.ColorTint
            && selectable.targetGraphic == graphic)
        {
            var colors = selectable.colors;
            colors.normalColor      = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.15f);
            colors.pressedColor     = Color.Lerp(color, Color.black, 0.2f);
            colors.selectedColor    = color;
            colors.disabledColor    = palette.Get(UIThemeRole.ButtonDisabled);
            colors.colorMultiplier  = 1f;

            if (graphic.color == Color.white && selectable.colors == colors)
            {
                return new Object[0];
            }

            graphic.color     = Color.white;
            selectable.colors = colors;
            return new Object[] { graphic, selectable };
        }

        if (graphic.color == color)
        {
            return new Object[0];
        }

        graphic.color = color;
        return new Object[] { graphic };
    }

#if UNITY_EDITOR
    // 인스펙터에서 역할을 바꾸면 바로 칠한다. OnValidate 안에서 Graphic을 건드리면 경고가 나서 한 프레임 미룬다 (Unity 메시지)
    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            return;
        }

        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null)
            {
                return;
            }

            foreach (var target in Apply(UIThemePalette.Current))
            {
                UnityEditor.EditorUtility.SetDirty(target);
            }
        };
    }
#endif
}
