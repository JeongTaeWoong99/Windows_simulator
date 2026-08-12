using TMPro;
using UnityEngine;

/// <summary>
/// 인벤토리 한 칸의 표시. 프리팹에 붙는다.
/// 비어 있는 칸은 파괴하지 않고 'Clear'로 비워 두었다가 재사용한다
/// — 채취가 도는 동안 생성·파괴가 반복되면 GC 부담이 쌓인다.
/// </summary>
public class InventorySlotView : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("아이템 이름 (ItemTable에서 조회)")]
    private TMP_Text nameText = null!;

    [SerializeField, Tooltip("보유 수량")]
    private TMP_Text countText = null!;

    /// <summary>이 칸이 그리고 있는 아이템. 비어 있으면 0.</summary>
    public int ItemId { get; private set; }

    /// <summary>아이템이 없는 빈 칸인가.</summary>
    public bool IsEmpty => ItemId == 0;

    // 필수 참조 검증 — 서비스를 조회하지 않으므로 Awake로 충분하고,
    // 그래야 InventoryPresenter가 Bind를 부르기 전에 이미 검증돼 있다 (Unity 메시지)
    private void Awake()
    {
        this.RequireRef(nameText,  nameof(nameText));
        this.RequireRef(countText, nameof(countText));
    }

    /// <summary>아이템을 표시한다 (InventoryPresenter가 호출).</summary>
    public void Bind(int itemId, int count)
    {
        ItemId         = itemId;
        nameText.text  = GameDataLoader.GetItemName(itemId);
        countText.text = count.ToString();
    }

    /// <summary>칸을 비운다. 오브젝트는 살려 두고 재사용 풀로 되돌린다.</summary>
    public void Clear()
    {
        ItemId         = 0;
        nameText.text  = "";
        countText.text = "";
    }
}
