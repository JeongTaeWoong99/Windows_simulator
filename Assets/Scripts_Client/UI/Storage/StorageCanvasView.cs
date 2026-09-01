using UnityEngine;

// 창고 열 — 3열 중 재료 쪽. 자원 · 캐릭터 · 장비 · 특성 4탭이 들어간다
// (탭이 무엇이든 'StorageGridPresenter' 하나가 그린다. 지금 데이터가 있는 것은 자원 · 캐릭터 둘).
//
// ■ 왜 작업슬롯 옆에 붙어 있는가
// 창고는 작업슬롯에 끌어다 넣는 재료라 드래그 거리가 곧 조작 비용이다.
// 그래서 'WidgetPositionLayout'이 위젯 위치와 무관하게 창고를 항상 작업슬롯 옆에 둔다.
// → GameDesign/design/ui/README.md 2.1
public class StorageCanvasView : MonoBehaviour
{
    // 이 열을 열고 닫는다 (UIManager가 호출).
    public void Show(bool on)
    {
        gameObject.SetActive(on);
    }
}
