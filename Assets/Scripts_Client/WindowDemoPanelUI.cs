using UnityEngine;

/// <summary>
/// 데모 디버그 패널.
/// - 각 창 기능(투명·항상위·클릭스루·정보바·도킹·동적클릭스루)을 런타임에 On/Off 하며 효과를 검증한다.
/// - 사용법 : uGUI Button 의 OnClick, Toggle 의 OnValueChanged 이벤트에 아래 public 메서드를 연결한다.
///   · Toggle(bool 인자) → OnToggleTransparent / OnToggleTopmost / OnToggleClickThrough / OnToggleDynamicClickThrough
///   · Button(인자 없음) → OnToggleInfoBar / OnDockToTaskbar
/// - 실제 창 변화는 빌드(.exe)에서만 일어난다(WindowController 가 #if !UNITY_EDITOR 가드).
///   에디터에선 토글/버튼 상태만 바뀌고 창은 반응하지 않는다.
/// </summary>
public class WindowDemoPanelUI : MonoBehaviour
{
    [CenterHeader("References")]
    [SerializeField] private WindowController windowController; // 창 제어 본체
    [SerializeField] private InfoBarToggleUI  infoBar;          // 정보바 토글 담당

    [CenterHeader("Dock Settings - 작업표시줄 도킹 시 창 크기")]
    [SerializeField] private int dockWidth  = 600; // 도킹 시 가로(px)
    [SerializeField] private int dockHeight = 200; // 도킹 시 세로(px)

    // ─── UI 이벤트 연결용 public 메서드 ───
    //   인스펙터의 OnClick/OnValueChanged 목록에서 이 컴포넌트를 대상으로 선택해 연결한다.

    /// 1 <summary>투명 배경 On/Off (Toggle 연결).</summary>
    public void OnToggleTransparent(bool enable)
    {
        if (windowController != null)
            windowController.SetTransparent(enable);
    }

    /// 2 <summary>항상 위 On/Off (Toggle 연결).</summary>
    public void OnToggleTopmost(bool enable)
    {
        if (windowController != null)
            windowController.SetTopmost(enable);
    }
    
    /// 3 <summary>동적 클릭 스루(마우스 위치 자동 판정) On/Off (Toggle 연결).</summary>
    public void OnToggleDynamicClickThrough(bool enable)
    {
        if (windowController != null)
            windowController.SetDynamicClickThrough(enable);
    }

    /// 4 <summary>정보바 표시 토글 (Button 연결).</summary>
    public void OnToggleInfoBar()
    {
        if (infoBar != null)
            infoBar.ToggleInfoBar();
    }

    /// 5 <summary>창을 작업표시줄 위로 도킹 (Button 연결).</summary>
    public void OnDockToTaskbar()
    {
        if (windowController != null)
            windowController.DockToTaskbar(dockWidth, dockHeight);
    }

}
