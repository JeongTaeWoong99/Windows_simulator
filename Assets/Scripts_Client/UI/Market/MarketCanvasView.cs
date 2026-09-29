using UnityEngine;

// 거래 열 — 뽑기 · 경매장 · 창고 3탭이 들어간다('MarketTabPresenter'). 탭마다 화면이 따로다.
// 지금 동작하는 것은 뽑기뿐이고, 경매장(T-096)·창고는 '(기능 없음)' 자리만 있다.
//
// ⚠️ 여기 '창고'는 인벤토리가 아니다 — 인벤토리(옛 이름 창고/Storage)는 왼쪽 열이다('InventoryCanvasView').
//   거래·상점 수치는 기획이 정한다 → GameDesign/design/trade/README.md
public class MarketCanvasView : MonoBehaviour
{
    // 이 열을 열고 닫는다 (UIManager가 호출).
    public void Show(bool on)
    {
        gameObject.SetActive(on);
    }
}
