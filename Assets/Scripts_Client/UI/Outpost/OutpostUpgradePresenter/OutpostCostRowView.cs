using TMPro;
using UnityEngine;

// 업그레이드 비용 한 줄 — 아이콘 · 이름 · '보유 / 필요'. 값은 'OutpostUpgradePresenter'가 완성해 넘긴다.
public class OutpostCostRowView : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("왼쪽 아이콘 칸 (ItemIconView 프리팹)")]
    private ItemIconView icon = null!;

    [SerializeField, Tooltip("골드 · 자원 이름")]
    private TMP_Text nameText = null!;

    [SerializeField, Tooltip("'보유 / 필요' — 충족 색은 부르는 쪽이 칠해 넘긴다")]
    private TMP_Text amountText = null!;

    // 필수 참조 검증 — 서비스를 조회하지 않으므로 Awake로 충분하다 (Unity 메시지)
    private void Awake()
    {
        this.RequireRef(icon,       nameof(icon));
        this.RequireRef(nameText,   nameof(nameText));
        this.RequireRef(amountText, nameof(amountText));
    }

    public void Bind(in ItemIconContent content, string displayName, string amount)
    {
        icon.Bind(content);

        nameText.text   = displayName;
        amountText.text = amount;
    }
}
