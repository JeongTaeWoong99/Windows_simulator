using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 창 제어 디버그 패널.
/// - 창 기능 토글 5개(타이틀바·투명·항상위·동적클릭스루·도킹)를 참조로 들고,
///   시작 시 WindowManager 의 start 설정값으로 자동 체크/해제한 뒤,
///   값 변경을 WindowManager 제어 메서드에 코드로 연결(AddListener)한다.
/// - 이 패널은 "표시 동기화 + 배선"만 담당한다. 실제 창 Win32 상태 적용은 WindowManager.InitializeWindow 가 한다.
/// - 실제 창 변화는 빌드(.exe)에서만 일어난다(WindowManager 가 #if !UNITY_EDITOR 가드).
/// </summary>
public class WindowPanelUI : MonoBehaviour
{
    [CenterHeader("Toggle 참조")]
    [SerializeField] private Toggle titleBarToggle;            // OS 타이틀바+테두리 표시 토글(이 바로 창 드래그)
    [SerializeField] private Toggle transparentToggle;         // 투명 배경 토글
    [SerializeField] private Toggle topmostToggle;             // 항상 위 토글
    [SerializeField] private Toggle dynamicClickThroughToggle; // 동적 클릭 통과 토글
    [SerializeField] private Toggle dockToggle;                // 우하단 도킹 토글

    // 토글 초기 상태를 WindowManager start 설정으로 맞추고, 조작을 창 제어에 연결 (Unity 메시지)
    private void Start()
    {
        var window = WindowManager.Instance;

        BindToggle(titleBarToggle,            window.StartTitleBar,            window.SetTitleBar);
        BindToggle(transparentToggle,         window.StartTransparent,         window.SetTransparent);
        BindToggle(topmostToggle,             window.StartTopmost,             window.SetTopmost);
        BindToggle(dynamicClickThroughToggle, window.StartDynamicClickThrough, window.SetDynamicClickThrough);
        BindToggle(dockToggle,                window.StartDock,                window.SetDock);
    }

    // 토글을 시작값으로 세팅(알림 없이)하고, 값 변경 시 창 제어 메서드를 호출하도록 연결
    private void BindToggle(Toggle toggle, bool startValue, UnityAction<bool> onChanged)
    {
        if (toggle == null)
            return;

        toggle.SetIsOnWithoutNotify(startValue);   // 시작 상태에 맞춰 체크(콜백 없이)
        toggle.onValueChanged.AddListener(onChanged);
    }
}
