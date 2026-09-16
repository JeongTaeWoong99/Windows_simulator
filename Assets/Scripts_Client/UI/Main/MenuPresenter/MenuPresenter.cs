using UnityEngine;
using UnityEngine.UI;

// 작업슬롯 화면 하단의 메뉴 줄 — 좌우 열(창고·거래)을 여는 버튼들. (기획 2.4)
// 'Title'과 함께 늘 켜져 있어, 가운데 세 화면이 무엇으로 바뀌든 그대로 남는다.
//
// ■ 이 줄이 특별 이벤트 자리다 (기획 2.4 · 2026-09-16 확정)
//   보스 · 대형 작업물처럼 채취 루프 밖의 콘텐츠가 들어오면 이 줄에 버튼이 는다. 높이가 고정이라 슬롯 영역은 밀리지 않는다.
//   ⚠️ 아직 없는 콘텐츠의 빈 칸·잠긴 버튼을 미리 두지 않는다 — "지금 못 하는 것"이 상시로 눈에 남는다.
//   ⚠️ 버튼에 배지·타이머·"오늘까지" 표시를 붙이지 않는다 — 상주 창이 재촉하는 물건이 된다(P1).
//
// ⚠️ 열지 않고 뒤집는다 — 여는 일만 하면 이미 열려 있을 때 눌러도 변화가 없어
// 버튼이 고장 난 것처럼 보인다. 창고·거래는 자리를 뺏지 않으므로 'MainScreen' 전환이 아니라
// 각각의 열 토글('ToggleStorage'·'ToggleMarket')을 쓴다.
public class MenuPresenter : MonoBehaviour
{
    // ※ 선택 참조다. 비워 두면 그 버튼이 아직 없는 것으로 보고 넘어간다.
    [CenterHeader("참조")]
    [SerializeField, Tooltip("창고 열기 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button? storageButton;

    [SerializeField, Tooltip("거래 열기 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button? marketButton;

    // 참조 확보 → 배선 (클라 공통 규약. 구독할 이벤트가 없다)
    // ※ 서비스 조회는 반드시 Start — Awake·OnEnable은 등록 순서가 보장되지 않는다(MonoService 주석).
    private void Start()
    {
        var ui = Services.Get<UIManager>();

        if (storageButton != null)
        {
            storageButton.onClick.AddListener(ui.ToggleStorage);
        }

        if (marketButton != null)
        {
            marketButton.onClick.AddListener(ui.ToggleMarket);
        }
    }
}
