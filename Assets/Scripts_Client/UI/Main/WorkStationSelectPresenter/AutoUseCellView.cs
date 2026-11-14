using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 자동 사용 칸 하나 — 영약 아이콘 + 이름 · 넣어 둔 개수 (이슈 #113 · T-194).
// 좌클릭 = 넣기 · 우클릭 = 꺼내기(2026-11-14 사용자 결정). 무엇을 할지는 'SlotAutoUsePresenter'가 정하고 칸은 클릭만 올린다.
//
// ※ 'Button'은 좌클릭만 받는다 — 우클릭은 'IPointerClickHandler'로 직접 받는다(인벤토리 'SlotView'와 같은 방식).
public class AutoUseCellView : MonoBehaviour, IPointerClickHandler
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("칸 버튼 — 좌클릭(넣기). OnClick은 코드가 연결한다")]
    private Button button = null!;

    [SerializeField, Tooltip("영약 아이콘 — 그림이 없으면 끈다")]
    private Image iconImage = null!;

    [SerializeField, Tooltip("영약 이름 — '신속의 영약'")]
    private TMP_Text nameText = null!;

    [SerializeField, Tooltip("넣어 둔 개수 — '×12'. 비었으면 '×0'")]
    private TMP_Text countText = null!;

    [SerializeField, Tooltip("마우스를 올리면 효과·개수·조작 안내를 띄운다")]
    private TooltipTrigger tooltip = null!;

    public int ItemTid { get; private set; }

    public event Action<AutoUseCellView>? LeftClicked;
    public event Action<AutoUseCellView>? RightClicked;

    // 필수 참조 검증 · 좌클릭 배선 (Unity 메시지) — 견본이 꺼진 채라 복제본이 켜질 때 돈다
    private void Awake()
    {
        this.RequireRef(button,    nameof(button));
        this.RequireRef(iconImage, nameof(iconImage));
        this.RequireRef(nameText,  nameof(nameText));
        this.RequireRef(countText, nameof(countText));
        this.RequireRef(tooltip,   nameof(tooltip));

        button.onClick.AddListener(() => LeftClicked?.Invoke(this));
    }

    // 칸을 그린다 ('SlotAutoUsePresenter.Refresh'가 호출).
    //   count    : 칸에 넣어 둔 개수
    //   provider : 툴팁 내용 — 띄우는 순간에 부른다
    public void Bind(int itemTid, int count, Func<TooltipContent?> provider)
    {
        ItemTid = itemTid;

        Sprite? icon = VisualCatalog.ItemIconOf(itemTid);

        iconImage.sprite  = icon!;
        iconImage.enabled = icon != null;
        nameText.text     = GameDataLoader.GetItemName(itemTid);
        countText.SetText("×{0}", count);
        tooltip.SetProvider(provider);
    }

    // 요청 응답을 기다리는 동안 누르지 못하게 한다 ('SlotAutoUsePresenter'가 호출)
    public void SetInteractable(bool on)
    {
        button.interactable = on;
    }

    // 우클릭 — 꺼내기 (IPointerClickHandler). 좌클릭은 'Button.onClick'이 받는다
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right && button.interactable)
        {
            RightClicked?.Invoke(this);
        }
    }
}
