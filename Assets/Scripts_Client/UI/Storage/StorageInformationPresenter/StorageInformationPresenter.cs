using TMPro;
using UnityEngine;

// 창고 하단의 정보 칸 — 고른 항목의 상세를 보여 주는 자리다.
//
// ⚠️ 아직 고르는 수단이 없다. 인벤토리 칸은 표시만 하고 클릭을 받지 않는다
// ('InventorySlotView'에 이벤트가 없다). 그래서 지금은 'Clear'로 비워 둔 상태가 전부다.
//
// ■ 나중에 붙이는 순서
// [1] InventorySlotView 에 Clicked 이벤트를 단다
// [2] StorageGridPresenter 가 그걸 받아 어떤 칸인지 안다 ('InventorySlotView.Key')
// [3] 그 값을 여기로 넘긴다 → Show(itemId)
//     ⚠️ 이제 칸은 자원만이 아니다 — 캐릭터 탭의 Key는 개체 번호라 'GetItemName'으로 읽으면 안 된다.
//        무엇을 고른 것인지(탭)까지 함께 받아야 한다.
//
// ⚠️ [3]에서 이 클래스를 직접 참조하지 않는다 — 패널끼리 서로를 알면 참조가 그물이 된다
// ('UI 규칙.md'의 "어디에 붙이나").
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

    // 아이템 상세를 표시한다 (선택 수단이 생기면 'StorageGridPresenter' 쪽에서 이어 준다).
    public void Show(int itemId)
    {
        infoText.text = GameDataLoader.GetItemName(itemId);
    }

    // 고른 것이 없을 때. 빈 문자열이 아니라 안내를 남긴다 — 빈 칸은 고장과 구분되지 않는다.
    public void Clear()
    {
        infoText.text = "항목을 고르면 여기에 표시된다";
    }
}
