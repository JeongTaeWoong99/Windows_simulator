using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 작업슬롯 칸 머리 줄의 버프 칩 하나 — 아이콘 + 남은 판정 횟수 '12' (T-194).
// 'SlotBuffChipsView'가 꺼 둔 견본을 복제해 버프 수만큼 띄운다. 칩은 값만 그리고 무엇을 띄울지는 묶음이 정한다.
public class SlotBuffChipView : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("소모품 아이콘 — 그림이 없으면 끈다")]
    private Image iconImage = null!;

    [SerializeField, Tooltip("남은 판정 횟수")]
    private TMP_Text countText = null!;

    [SerializeField, Tooltip("마우스를 올리면 효과·남은 횟수·자동 사용 칸 개수를 띄운다")]
    private TooltipTrigger tooltip = null!;

    // 필수 참조 검증 (Unity 메시지) — 견본이 꺼진 채라 복제본이 켜질 때 돈다
    private void Awake()
    {
        this.RequireRef(iconImage,  nameof(iconImage));
        this.RequireRef(countText,  nameof(countText));
        this.RequireRef(tooltip,    nameof(tooltip));
    }

    // 칩 하나를 그린다 ('SlotBuffChipsView.Bind'가 호출).
    //   remaining : 남은 판정 횟수
    //   provider  : 툴팁 내용 — 띄우는 순간에 부른다
    public void Bind(int itemTid, int remaining, System.Func<TooltipContent?> provider)
    {
        Sprite? icon = VisualCatalog.ItemIconOf(itemTid);

        iconImage.sprite  = icon!;
        iconImage.enabled = icon != null;
        countText.SetText("{0}", remaining);
        tooltip.SetProvider(provider);
    }
}
