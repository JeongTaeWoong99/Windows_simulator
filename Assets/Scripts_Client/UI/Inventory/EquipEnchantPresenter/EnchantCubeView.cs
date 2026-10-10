using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 큐브 창의 큐브 고르기 버튼 하나 — 큐브 이름 · 보유 수 · 이번에 쓰면 무엇이 되나. 눌리면 'Clicked'만 쏜다.
//
// 이 칸은 확률도 보유량도 계산하지 않는다 — 완성된 문구만 'Bind'로 받는다.
// (종속 View 규약은 'UI 규칙.md'의 "종속 View 쪽 규약")
//
// ※ 큐브가 0개여도 눌린다 — 고르는 것은 막지 않고, [큐브 사용]이 이유를 알린다(특성 [레벨 올리기]와 같은 판단).
public class EnchantCubeView : MonoBehaviour
{
    // 잠겼을 때 어둡게 누르는 정도
    private const float LockedDarken = 0.35f;

    [CenterHeader("참조")]
    [SerializeField, Tooltip("큐브 이름 (예: '인챈트 큐브')")]
    private TMP_Text nameText = null!;

    [SerializeField, Tooltip("두 번째 줄 — '보유 12개 · 고급 상승 16%'")]
    private TMP_Text detailText = null!;

    [SerializeField, Tooltip("이 큐브를 고르는 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button button = null!;

    [SerializeField, Tooltip("지금 고른 큐브 표시(테두리). 평소 꺼져 있다")]
    private GameObject selectedMark = null!;

    [SerializeField, Tooltip("올리면 큐브 규칙·확률 표를 띄우는 트리거. 내용은 코드가 넘긴다")]
    private TooltipTrigger tooltipTrigger = null!;

    [CenterHeader("색")]
    // ⚠️ 'Image.color'가 아니라 'ColorBlock'을 칠한다 — 'Selectable'이 실행 중 'Image.color'를 덮어쓴다('TraitNodeView'와 같다).
    [SerializeField, Tooltip("가진 큐브")]
    private UIThemeRole ownedRole = UIThemeRole.Button;

    [SerializeField, Tooltip("하나도 없는 큐브 — 눌리기는 한다")]
    private UIThemeRole emptyRole = UIThemeRole.ButtonDisabled;

    // 이 버튼을 눌렀다 ('EquipEnchantPresenter'가 구독).
    public event Action<EnchantCubeView>? Clicked;

    // 자기 버튼만 배선한다 — 서비스를 조회하지 않으므로 Awake로 충분하고,
    // 그래야 패널의 Start가 Bind를 부르기 전에 이미 연결돼 있다 (Unity 메시지)
    private void Awake()
    {
        this.RequireRef(nameText,       nameof(nameText));
        this.RequireRef(detailText,     nameof(detailText));
        this.RequireRef(button,         nameof(button));
        this.RequireRef(selectedMark,   nameof(selectedMark));
        this.RequireRef(tooltipTrigger, nameof(tooltipTrigger));

        button.onClick.AddListener(() => Clicked?.Invoke(this));
    }

    // 이 버튼이 그릴 큐브를 정한다 ('EquipEnchantPresenter'가 호출).
    //   displayName : 큐브 이름
    //   detail      : 두 번째 줄 완성 문구
    //   hasAny      : 하나라도 가졌는지 — 색을 정한다
    //   isSelected  : 지금 고른 큐브인지
    //   tooltip     : 올리면 부를 툴팁 함수
    public void Bind(string displayName, string detail, bool hasAny, bool isSelected, Func<TooltipContent?> tooltip)
    {
        gameObject.SetActive(true);

        nameText.text   = displayName;
        detailText.text = detail;
        selectedMark.SetActive(isSelected);
        tooltipTrigger.SetProvider(tooltip);

        ApplyColor(hasAny);
    }

    // 결과 공개 중엔 누를 수 없다 — 뽑기 결과를 보기 전에 다시 누르지 못하게 ('EquipEnchantPresenter'가 호출).
    public void SetLocked(bool locked)
    {
        button.interactable = !locked;
    }

    // 쓸 큐브가 모자라 남는 버튼을 끈다 ('EquipEnchantPresenter'가 호출).
    public void Hide()
    {
        tooltipTrigger.SetProvider(null);
        selectedMark.SetActive(false);
        gameObject.SetActive(false);
    }

    // 가졌는지에 맞춰 색을 칠한다 (Bind에서 호출).
    // ※ 네 상태를 같은 색으로 덮는다 — 마우스를 올렸다는 이유로 색이 바뀌면 "가졌는가"를 못 읽는다.
    private void ApplyColor(bool hasAny)
    {
        Color      target = UIThemePalette.Of(hasAny ? ownedRole : emptyRole);
        ColorBlock colors = button.colors;

        colors.normalColor      = target;
        colors.highlightedColor = Color.Lerp(target, Color.white, 0.1f);
        colors.pressedColor     = Color.Lerp(target, Color.black, 0.2f);
        colors.selectedColor    = target;
        colors.disabledColor    = Color.Lerp(target, Color.black, LockedDarken); // 공개 중 잠김

        button.colors = colors;
    }
}
