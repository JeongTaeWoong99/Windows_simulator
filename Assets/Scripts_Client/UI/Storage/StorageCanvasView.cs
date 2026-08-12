using UnityEngine;

/// <summary>
/// 창고 열 — 3열 중 재료 쪽. 캐릭터 · 장비 · 자원 · 특성 4탭이 들어간다
/// (지금은 자원 탭의 'InventoryPresenter' 하나뿐이다).
///
/// ■ 왜 작업슬롯 옆에 붙어 있는가
/// 창고는 작업슬롯에 끌어다 넣는 재료라 드래그 거리가 곧 조작 비용이다.
/// 그래서 'WidgetPositionLayout'이 위젯 위치와 무관하게 창고를 항상 작업슬롯 옆에 둔다.
/// → GameDesign/design/ui/README.md 2.1
/// </summary>
public class StorageCanvasView : MonoBehaviour
{
    /// <summary>이 열을 열고 닫는다 (UIManager가 호출).</summary>
    public void Show(bool on)
    {
        gameObject.SetActive(on);
    }
}
