using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 설정 패널 — 사용자가 바꿀 수 있는 값을 입력받아 넘기는 곳.
///
/// ■ 위치 드롭다운은 하나로 창·위젯을 함께 정한다
/// 창의 데스크톱 위치(앵커)와 위젯의 창 안 위치는 같은 6칸이라, 드롭다운 하나가
/// 'WindowManager'와 'WidgetPositionLayout' 양쪽을 같은 인덱스로 몰이한다.
///
/// ■ 드롭다운 옵션은 코드가 채운다
/// 라벨을 인스펙터에 손으로 넣으면 enum이 바뀔 때 조용히 어긋난다.
///
/// ⚠️ 창 관련 변화는 빌드(.exe)에서만 실제로 일어난다 — 에디터에서는 값만 바뀌고 창은 그대로다
/// ('DesktopWindow 규칙.md' 5-1).
/// </summary>
public class SettingPresenter : MonoBehaviour
{
    // ※ 아래는 인스펙터에서 반드시 연결해야 하는 "필수" 참조다.
    //   nullable:enable 상태라 ? 없이 두면 "생성자 종료 시 non-null" 검사(CS8618)에 걸리는데,
    //   ? 로 두면 미연결 시 조용히 무시돼 "왜 안 되지?"가 되어버린다. 그래서 = null! 로 non-null
    //   타입을 유지(경고 제거)하고, Start()에서 == null 검사로 미연결을 예외로 즉시 드러낸다(fail-fast).
    // ※ 이 화면엔 제목을 두지 않는다 — #Main Canvas 의 Title 이 이미 "Setting"을 띄운다.
    //   헤더 줄에는 뒤로가기 버튼만 있고, WorkStationSelectPresenter 의 Header Panel 과 같은 규격이다.
    [CenterHeader("공통 Header Panel (항상 보인다)")]
    [SerializeField, Tooltip("작업슬롯 목록으로 나간다. OnClick은 코드가 연결한다")]
    private Button backButton = null!;

    [CenterHeader("창 제어 (Win32 네이티브)")]
    [SerializeField] private Toggle topmostToggle             = null!; // 항상 위 토글
    [SerializeField] private Toggle titleBarToggle            = null!; // OS 타이틀바+테두리 표시 토글(이 바로 창 드래그)
    [SerializeField] private Toggle transparentToggle         = null!; // 투명 배경 토글
    [SerializeField] private Toggle dynamicClickThroughToggle = null!; // 동적 클릭 통과 토글

    [SerializeField] private TMP_Dropdown sizeDropdown           = null!; // 창 크기 배율 프리셋
    // 위치 드롭다운 하나가 창의 데스크톱 위치(앵커)와 위젯의 창 안 위치(6칸)를 함께 정한다.
    // 둘은 같은 6칸 나열 순서라 인덱스가 1:1이다 — 짝이 맞는 모서리 조합만 유효.
    [SerializeField] private TMP_Dropdown windowPositionDropdown = null!; // 창+위젯 위치 (6칸)

    // ※ WidgetPositionLayout은 Services에 등록되지 않는다([ExecuteAlways] 레이아웃 컴포넌트라
    //   에디터에서도 돌아야 해서 서비스 로케이터에 묶지 않았다). 그래서 인스펙터로 직접 받는다.
    //
    // ★ 그 컴포넌트는 이 패널이 아니라 !Horizental Columns 에 붙어 있다 — 같은 오브젝트에 두면
    //   설정 화면이 꺼져 있는 동안 OnEnable 이 안 돌아 3열 순서와 위젯 위치가 아예 반영되지 않는다.
    [SerializeField, Tooltip("위젯 위치를 실제로 반영할 레이아웃 컴포넌트 — !Horizental Columns 에 있다")]
    private WidgetPositionLayout widgetLayout = null!;

    // 토글/드롭다운을 현재 값으로 맞추고, 조작을 각 담당자에게 연결한다 (Unity 메시지)
    private void Start()
    {
        // 필수 참조 검증 — 미연결(null)이면 조용히 넘어가지 않고 즉시 예외로 어떤 참조인지 알린다.
        this.RequireRef(backButton,                nameof(backButton));
        this.RequireRef(titleBarToggle,            nameof(titleBarToggle));
        this.RequireRef(transparentToggle,         nameof(transparentToggle));
        this.RequireRef(topmostToggle,             nameof(topmostToggle));
        this.RequireRef(dynamicClickThroughToggle, nameof(dynamicClickThroughToggle));
        this.RequireRef(sizeDropdown,              nameof(sizeDropdown));
        this.RequireRef(windowPositionDropdown,    nameof(windowPositionDropdown));
        this.RequireRef(widgetLayout,              nameof(widgetLayout));

        var window = Services.Get<WindowManager>();
        var ui     = Services.Get<UIManager>();

        // ─── 헤더 ───
        // 이 화면을 직접 끄지 않는다 — UIManager 가 목록을 켜면서 같은 자리의 이 화면을 끈다.
        // 스스로 끄면 목록이 켜지기 전 빈 칸이 남는다 (UI 규칙 §3).
        backButton.onClick.AddListener(() => ui.ShowMainScreen(MainScreen.WorkStationList));

        // ─── 창 제어 ───
        // 초기값은 WindowManager가 저장에서 복원해 둔 "현재" 값이다.
        BindToggle(titleBarToggle,            window.TitleBar,            window.SetTitleBar);
        BindToggle(transparentToggle,         window.Transparent,         window.SetTransparent);
        BindToggle(topmostToggle,             window.Topmost,             window.SetTopmost);
        BindToggle(dynamicClickThroughToggle, window.DynamicClickThrough, window.SetDynamicClickThrough);

        BindDropdown(sizeDropdown, window.GetSizeLabels(), window.SizeIndex, window.SetWindowSizeByIndex);

        // 위치 드롭다운 하나가 창 앵커와 위젯 위치를 함께 몰이한다. 시작 인덱스는 창 앵커를 권위 소스로 삼는다.
        // ※ 이 Start는 부팅이 아니라 "설정 패널을 처음 열 때" 돈다(UIManager가 시작 시 Setting 패널을 꺼 둠).
        //   그래서 아래 SetPosition은 부팅 정렬이 아니라, 옛 저장값이 서로 어긋나 있으면 설정을 여는 이 순간
        //   위젯을 창 앵커에 맞춰 흡수하는 역할이다. 부팅 직후 일치는 두 공장 기본값을 같은 모서리로 맞춰 보장한다.
        int startPosition = window.AnchorIndex;
        widgetLayout.SetPosition((WidgetPosition)startPosition);
        BindDropdown(windowPositionDropdown, window.GetAnchorLabels(), startPosition, index =>
        {
            window.SetAnchorByIndex(index);
            widgetLayout.SetPosition((WidgetPosition)index);
        });
    }

    // 토글을 시작값으로 세팅(알림 없이)하고, 값 변경 시 창 제어 메서드를 호출하도록 연결
    private void BindToggle(Toggle toggle, bool startValue, UnityAction<bool> onChanged)
    {
        toggle.SetIsOnWithoutNotify(startValue);   // 시작 상태에 맞춰 체크(콜백 없이)
        toggle.onValueChanged.AddListener(onChanged);
    }

    // 드롭다운 옵션을 채우고 시작 인덱스로 세팅(알림 없이)한 뒤, 선택 변경 시 창 제어 메서드를 호출하도록 연결
    private void BindDropdown(TMP_Dropdown dropdown, List<string> options, int startIndex, UnityAction<int> onChanged)
    {
        dropdown.ClearOptions();
        dropdown.AddOptions(options);
        dropdown.SetValueWithoutNotify(startIndex); // 시작 인덱스에 맞춤(콜백 없이)
        dropdown.onValueChanged.AddListener(onChanged);
    }
}
