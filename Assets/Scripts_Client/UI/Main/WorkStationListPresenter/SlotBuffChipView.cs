using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 작업슬롯 칸 무대 왼쪽 위의 버프 칩 하나 — 아이콘 + 남은 판정 횟수 '12' · 자동 사용이 켜졌으면 작게 '+5'(보유) (T-194 · 이슈 #123).
// 'SlotBuffChipsView'가 꺼 둔 견본을 복제해 버프 수만큼 띄운다. 칩은 값만 그리고 무엇을 띄울지는 묶음이 정한다.
public class SlotBuffChipView : MonoBehaviour
{
    private const float NoBuffAlpha = 0.4f; // 버프 없이 자동 사용만 켜진 칩은 흐리게

    [CenterHeader("참조")]
    [SerializeField, Tooltip("소모품 아이콘 — 그림이 없으면 끈다")]
    private Image iconImage = null!;

    [SerializeField, Tooltip("남은 판정 횟수 + 자동 사용 보유 개수 '+5'")]
    private TMP_Text countText = null!;

    [SerializeField, Tooltip("마우스를 올리면 효과·남은 횟수·자동 사용 보유 개수를 띄운다")]
    private TooltipTrigger tooltip = null!;

    // 필수 참조 검증 (Unity 메시지) — 견본이 꺼진 채라 복제본이 켜질 때 돈다
    private void Awake()
    {
        this.RequireRef(iconImage,  nameof(iconImage));
        this.RequireRef(countText,  nameof(countText));
        this.RequireRef(tooltip,    nameof(tooltip));
    }

    // 칩 하나를 그린다 ('SlotBuffChipsView.Bind'가 호출).
    //   remaining : 남은 판정 횟수 — 0이면 버프 없이 자동 사용만 켜진 칩(흐리게)
    //   autoOwned : 자동 사용이 켜졌으면 보유 개수, 꺼졌으면 -1
    //   provider  : 툴팁 내용 — 띄우는 순간에 부른다
    public void Bind(int itemTid, int remaining, int autoOwned, System.Func<TooltipContent?> provider)
    {
        Sprite? icon = VisualCatalog.ItemIconOf(itemTid);

        iconImage.sprite  = icon!;
        iconImage.enabled = icon != null;
        iconImage.color   = new Color(1f, 1f, 1f, remaining > 0 ? 1f : NoBuffAlpha);
        countText.text    = autoOwned < 0 ? remaining.ToString() : $"{remaining}{UIRichText.Small($"+{autoOwned}")}";
        tooltip.SetProvider(provider);
    }
}
