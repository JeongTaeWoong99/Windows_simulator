using TMPro;
using UnityEngine;

/// <summary>
/// 창고 하단의 정보 칸 — <b>고른 항목의 상세를 보여 주는 자리</b>다.
///
/// <para>
/// ⚠️ <b>아직 고르는 수단이 없다.</b> 인벤토리 칸은 표시만 하고 클릭을 받지 않는다
/// (<c>InventorySlotView</c>에 이벤트가 없다). 그래서 지금은 <see cref="Clear"/>로 비워 둔 상태가 전부다.
/// </para>
///
/// <para>
/// ■ 붙는 순서<br/>
/// <code>
/// [1] InventorySlotView 에 Clicked 이벤트를 단다
/// [2] InventoryPresenter 가 그걸 받아 어떤 ItemId 인지 안다
/// [3] 그 ItemId 를 여기로 넘긴다 → Show(itemId)
/// </code>
/// <b>[3]에서 이 클래스를 직접 참조하지 않는다.</b> 창고 캔버스가 둘을 잇거나
/// 인벤토리가 이벤트를 쏘고 이쪽이 구독한다 — 패널끼리 서로를 알면 참조가 그물이 된다
/// (<c>UI 스크립트 규칙.md</c> §3).
/// </para>
///
/// <para>
/// ■ 왜 미리 만들어 두나<br/>
/// 오브젝트는 씬에 있는데 스크립트가 없으면 <b>"여긴 아직 아무도 담당하지 않는다"가 안 보인다.</b>
/// 껍데기라도 있으면 다음 사람이 여기에 붙이면 된다는 걸 폴더만 열어 봐도 안다.
/// </para>
/// </summary>
public class StorageInformationPresenter : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("고른 항목의 상세 문구")]
    private TMP_Text infoText = null!;

    // 참조 확보 → 초기화 (클라 공통 규약. 아직 구독할 이벤트가 없다)
    private void Start()
    {
        this.RequireRef(infoText, nameof(infoText));
        Clear();
    }

    /// <summary>아이템 상세를 표시한다 (선택 수단이 생기면 <c>InventoryPresenter</c> 쪽에서 이어 준다).</summary>
    public void Show(int itemId)
    {
        infoText.text = GameDataLoader.GetItemName(itemId);
    }

    /// <summary>고른 것이 없을 때. 빈 문자열이 아니라 안내를 남긴다 — 빈 칸은 고장과 구분되지 않는다.</summary>
    public void Clear()
    {
        infoText.text = "항목을 고르면 여기에 표시된다";
    }
}
