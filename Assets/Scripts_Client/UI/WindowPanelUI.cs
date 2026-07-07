using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Client.Utils;

/// <summary>
/// 창 제어 디버그 패널.
/// - 창 상태 토글 4개(타이틀바·투명·항상위·동적클릭스루)와 위치·크기 드롭다운 2개를 참조로 들고,
///   시작 시 WindowManager 의 start 설정값으로 자동 세팅한 뒤, 조작을 WindowManager 제어 메서드에
///   코드로 연결(AddListener)한다.
/// - 드롭다운 옵션은 WindowManager 가 제공하는 라벨로 런타임에 채운다(에디터 수동 입력 불필요).
/// - 이 패널은 "표시 동기화 + 배선"만 담당한다. 실제 창 Win32 상태 적용은 WindowManager.InitializeWindow 가 한다.
/// - 실제 창 변화는 빌드(.exe)에서만 일어난다(WindowManager 가 #if !UNITY_EDITOR 가드).
/// </summary>
public class WindowPanelUI : MonoBehaviour
{
    // ※ 아래는 인스펙터에서 반드시 연결해야 하는 "필수" 참조다.
    //   nullable:enable 상태라 ? 없이 두면 "생성자 종료 시 non-null" 검사(CS8618)에 걸리는데,
    //   ? 로 두면 미연결 시 조용히 무시돼 "왜 안 되지?"가 되어버린다. 그래서 = null! 로 non-null
    //   타입을 유지(경고 제거)하고, Start()에서 == null 검사로 미연결을 예외로 즉시 드러낸다(fail-fast).
    [CenterHeader("Toggle 참조")]
    [SerializeField] private Toggle titleBarToggle            = null!; // OS 타이틀바+테두리 표시 토글(이 바로 창 드래그)
    [SerializeField] private Toggle transparentToggle         = null!; // 투명 배경 토글
    [SerializeField] private Toggle topmostToggle             = null!; // 항상 위 토글
    [SerializeField] private Toggle dynamicClickThroughToggle = null!; // 동적 클릭 통과 토글

    [CenterHeader("Dropdown 참조")]
    [SerializeField] private TMP_Dropdown sizeDropdown     = null!; // 창 크기 프리셋 선택 드롭다운
    [SerializeField] private TMP_Dropdown positionDropdown = null!; // 9분할 위치 선택 드롭다운

    // 토글/드롭다운 초기 상태를 WindowManager start 설정으로 맞추고, 조작을 창 제어에 연결 (Unity 메시지)
    private void Start()
    {
        // 필수 참조 검증 — 미연결(null)이면 조용히 넘어가지 않고 즉시 예외로 어떤 참조인지 알린다.
        this.RequireRef(titleBarToggle,            nameof(titleBarToggle));
        this.RequireRef(transparentToggle,         nameof(transparentToggle));
        this.RequireRef(topmostToggle,             nameof(topmostToggle));
        this.RequireRef(dynamicClickThroughToggle, nameof(dynamicClickThroughToggle));
        this.RequireRef(sizeDropdown,              nameof(sizeDropdown));
        this.RequireRef(positionDropdown,          nameof(positionDropdown));

        var window = WindowManager.Instance;

        // 토글 바인딩
        BindToggle(titleBarToggle,            window.StartTitleBar,            window.SetTitleBar);
        BindToggle(transparentToggle,         window.StartTransparent,         window.SetTransparent);
        BindToggle(topmostToggle,             window.StartTopmost,             window.SetTopmost);
        BindToggle(dynamicClickThroughToggle, window.StartDynamicClickThrough, window.SetDynamicClickThrough);

        // 드롭다운 바인딩
        BindDropdown(sizeDropdown,     window.GetSizeLabels(),   window.StartSizeIndex,   window.SetWindowSizeByIndex);
        BindDropdown(positionDropdown, window.GetAnchorLabels(), window.StartAnchorIndex, window.SetAnchorByIndex);
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
