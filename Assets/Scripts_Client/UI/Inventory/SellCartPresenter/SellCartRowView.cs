using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 판매 목록의 한 줄. 담긴 아이템 하나를 보여 주고 빼기 버튼을 갖는다.
//
// 눌리면 'RemoveClicked'만 쏜다 — 카트도 합계도 이 줄은 모른다.
// 이름·가격·아이콘·툴팁처럼 변환이 필요한 값은 'Bind'로 완성된 값을 받는다.
// (종속 View 규약은 'UI 규칙.md'의 "종속 View 쪽 규약")
public class SellCartRowView : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("왼쪽 아이콘 칸 (ItemIconView 프리팹)")]
    private ItemIconView iconView = null!;

    [SerializeField, Tooltip("줄 루트의 툴팁 트리거. 내용은 코드가 넘긴다")]
    private TooltipTrigger tooltipTrigger = null!;

    [SerializeField, Tooltip("아이템 이름")]
    private TMP_Text nameText = null!;

    [SerializeField, Tooltip("팔 개수")]
    private TMP_Text countText = null!;

    [SerializeField, Tooltip("이 줄이 받을 골드 (즉시 판매가 x 개수)")]
    private TMP_Text priceText = null!;

    [SerializeField, Tooltip("이 줄을 목록에서 빼는 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button removeButton = null!;

    // 이 줄의 빼기를 눌렀다 ('SellCartPresenter'가 구독).
    public event Action<SellCartRowView>? RemoveClicked;

    // 이 줄이 그리고 있는 아이템. 미바인딩이면 0.
    public int ItemId { get; private set; }

    // 자기 버튼만 배선한다 — 서비스를 조회하지 않으므로 Awake로 충분하고,
    // 그래야 패널의 Start가 Bind를 부르기 전에 이미 연결돼 있다 (Unity 메시지)
    private void Awake()
    {
        this.RequireRef(iconView,       nameof(iconView));
        this.RequireRef(tooltipTrigger, nameof(tooltipTrigger));
        this.RequireRef(nameText,     nameof(nameText));
        this.RequireRef(countText,    nameof(countText));
        this.RequireRef(priceText,    nameof(priceText));
        this.RequireRef(removeButton, nameof(removeButton));

        removeButton.onClick.AddListener(() => RemoveClicked?.Invoke(this));
    }

    // 이 줄이 그릴 항목을 정한다 ('SellCartPresenter'가 호출).
    //   itemId      : 빼기 요청에 그대로 실린다
    //   displayName : 이미 완성된 이름 문구
    //   count       : 팔 개수(보유량이 아니다)
    //   price       : 이 줄이 받을 골드
    //   icon        : 왼쪽 아이콘 칸의 완성값
    //   tooltip     : 줄에 마우스를 올리면 부를 툴팁 함수
    public void Bind(int itemId, string displayName, int count, long price, in ItemIconContent icon, Func<TooltipContent?> tooltip)
    {
        ItemId         = itemId;
        iconView.Bind(icon);
        tooltipTrigger.SetProvider(tooltip);
        nameText.text  = displayName;
        countText.text = $"{count:N0}개";
        priceText.text = $"{price:N0} G";
    }

    // 줄을 비운다. 오브젝트는 살려 두고 재사용 풀로 되돌린다 — 판매 목록은 담고 빼기를 반복한다.
    public void Clear()
    {
        ItemId         = 0;
        iconView.Clear();
        tooltipTrigger.SetProvider(null);
        nameText.text  = "";
        countText.text = "";
        priceText.text = "";
    }
}
