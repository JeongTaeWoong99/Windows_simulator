using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 정보바(타이틀바 대용) 토글 + 정보바를 잡아 OS 창 드래그.
/// - 평소엔 정보바가 꺼져 있어 순수 투명 창 상태.
/// - 정보바를 켜면 상단 바가 보이고, 그 바를 누르면 OS 창 자체를 드래그 이동.
/// - 이 컴포넌트는 "정보바" UI 오브젝트(Image 등 RectTransform)에 붙인다.
///
/// ■ 구현 인터페이스 (uGUI EventSystem 콜백)
///   - IPointerDownHandler  : 이 UI를 마우스로 누른 순간 OnPointerDown 호출 → 창 드래그 시작
///   - IPointerEnterHandler : 마우스가 이 UI 위로 들어오면 OnPointerEnter → 클릭을 받도록 강제
///   - IPointerExitHandler  : 마우스가 벗어나면 OnPointerExit → 강제 해제
///   ※ enter/exit 로 forceInteractive 를 켜고 끄는 이유 : 정보바 위에선 클릭스루가 풀려야
///     마우스로 바를 잡을 수 있기 때문. (WindowController.SetForceInteractive 와 연동)
/// </summary>
public class InfoBarToggleUI : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler
{
    [CenterHeader("References")]
    [SerializeField] private WindowController windowController; // 창 제어 본체
    [SerializeField] private GameObject       infoBarRoot;      // 표시/숨김을 토글할 정보바 UI 루트

    [CenterHeader("State")]
    [SerializeField] private bool infoBarVisible = false; // 정보바 표시 여부(기본 꺼짐)

    void Start()
    {
        ApplyVisibility(); // 시작 시 현재 상태대로 정보바를 보이거나 숨긴다
    }
    
    /// <summary>정보바 On/Off 토글 (디버그 패널 버튼 등에서 호출).</summary>
    public void ToggleInfoBar()
    {
        infoBarVisible = !infoBarVisible;
        ApplyVisibility();
    }
    
    /// <summary>현재 infoBarVisible 값에 따라 정보바 루트를 활성/비활성한다.</summary>
    private void ApplyVisibility()
    {
        if (infoBarRoot != null)
            infoBarRoot.SetActive(infoBarVisible);
    }

    // ─── 정보바 위에 마우스가 올라오면 클릭을 받도록 강제 ───
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (windowController != null)
            windowController.SetForceInteractive(true); // 클릭스루 해제 강제 → 바를 잡을 수 있게
    }
    
    // ─── 정보바를 눌러 드래그 시작 ───
    public void OnPointerDown(PointerEventData eventData)
    {
        // 정보바가 켜져 있을 때만 드래그를 허용한다.
        if (!infoBarVisible || windowController == null)
            return;

        windowController.StartWindowDrag(); // OS에 창 이동을 위임
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (windowController != null)
            windowController.SetForceInteractive(false); // 강제 해제 → 다시 동적 클릭스루 판정으로
    }
}
