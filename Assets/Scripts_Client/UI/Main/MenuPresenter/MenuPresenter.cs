using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 작업슬롯 화면 하단의 메뉴 줄 — <b>좌우 열(창고·거래)을 여는 버튼들</b>. (기획 2.4)
///
/// <para>
/// ■ 좌우 열은 자리를 뺏지 않는다<br/>
/// 창고·거래는 <c>@Storage Column</c>·<c>@Market Column</c>에 있어서 켜도 작업슬롯이 안 닫힌다.
/// 그래서 <see cref="MainScreen"/> 토글이 아니라 각각의 열 토글(<c>ToggleStorage</c>·<c>ToggleMarket</c>)이다.
/// </para>
///
/// <para>
/// ■ 이 줄은 갈아 끼워지지 않는다<br/>
/// <c>#Main Canvas</c>에서 <c>Title</c>과 함께 <b>늘 켜져 있는 두 칸 중 하나</b>다.
/// 가운데의 세 화면(목록·선택·설정)이 무엇으로 바뀌든 창고·거래 버튼은 그대로 남는다.
/// </para>
///
/// <para>
/// ■ 왜 상위가 아니라 여기가 버튼을 쥐나<br/>
/// 예전엔 상위의 <c>WorkStationPresenter</c>가 이 패널의 버튼을 직접 잡았다. 자기 자식 패널의
/// 위젯을 건너뛰어 잡는 셈이라 <b>"패널의 위젯은 그 패널의 Presenter가 쥔다"</b>는 규칙이 깨진다 —
/// 버튼이 늘어날수록 상위 Presenter가 남의 화면 사정을 알게 된다.
/// (그 상위 Presenter는 2026-08-10에 사라졌다 — <see cref="MainScreen"/>)
/// </para>
///
/// <para>
/// ⚠️ 열지 않고 <b>뒤집는다.</b> 여는 일만 하면 이미 열려 있을 때 눌러도 변화가 없어
/// 버튼이 고장 난 것처럼 보인다 (<c>UIManager.ToggleStorage</c> 주석).
/// </para>
/// </summary>
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
            storageButton.onClick.AddListener(ui.ToggleStorage);

        if (marketButton != null)
            marketButton.onClick.AddListener(ui.ToggleMarket);
    }
}
