using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 상주 위젯의 내용 — 지금은 열기/닫기 버튼 하나뿐이다.
///
/// ★ 위젯 말고는 바탕화면에 아무것도 안 떠 있으므로, 이 버튼이 3열을 여는 유일한 입구다.
/// 열려 있으면 위젯만 남기고 전부 접는다 → 'UIManager.ToggleAll'
///
/// ■ 앞으로 들어올 것
/// 상단 = 골드 · 가동 슬롯 · 시간당 산출 · 누적 수확
/// 스트립 = 미니 슬롯(캐릭터 + 수확 표시 + 게이지만. 텍스트 라벨은 넣지 않는다)
/// 그것들을 그리려면 'PlayerDataModel' 구독이 여기 붙는다 — 캔버스가 아니라 이 패널에.
///
/// ⚠️ 연출을 여기서 돌리지 않는다. 배경·캐릭터 모션은 큰 창 전용이다.
/// 상시 실행 앱에서 리소스는 기능이 아니라 생존 조건이고, 급격한 애니메이션은
/// P1(주의를 뺏지 않는다)을 정면으로 어긴다.
/// → GameDesign/design/ui/README.md 2.1
/// </summary>
public class WidgetPresenter : MonoBehaviour
{
    [CenterHeader("참조")]
    [SerializeField, Tooltip("열기/닫기 버튼. OnClick은 코드가 연결하므로 인스펙터에서 비워 둔다")]
    private Button toggleButton = null!;

    // 참조 확보 → 배선 (클라 공통 규약. 아직 구독할 이벤트가 없다)
    // ※ 서비스 조회는 반드시 Start — Awake·OnEnable은 등록 순서가 보장되지 않는다(MonoService 주석).
    private void Start()
    {
        this.RequireRef(toggleButton, nameof(toggleButton));

        var ui = Services.Get<UIManager>();
        toggleButton.onClick.AddListener(ui.ToggleAll);
    }
}
